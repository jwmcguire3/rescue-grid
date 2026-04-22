using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rescue.Core.Pipeline;
using Rescue.Core.State;

namespace Rescue.Telemetry
{
    public sealed class TelemetrySessionState
    {
        public bool FirstActionFired { get; set; }
        public long? PreviousActionEndMs { get; set; }
        public long LevelStartMs { get; set; }
    }

    public static class TelemetryHooks
    {
        public static void OnLevelStart(
            string levelId,
            ulong seed,
            GameState initialState,
            long timestampMs,
            TelemetryLogger logger)
        {
            if (levelId is null) throw new ArgumentNullException(nameof(levelId));
            if (initialState is null) throw new ArgumentNullException(nameof(initialState));
            if (logger is null) throw new ArgumentNullException(nameof(logger));

            logger.Append(new LevelStartEvent(
                LevelId: levelId,
                TimestampMs: timestampMs,
                Seed: seed,
                AssistanceChance: initialState.LevelConfig.AssistanceChance,
                RiseInterval: initialState.Water.RiseInterval,
                InitialFloodedRows: initialState.Water.FloodedRows,
                VineGrowthThreshold: initialState.Vine.GrowthThreshold,
                TargetCount: initialState.Targets.Length));
        }

        public static void OnAction(
            string levelId,
            GameState stateBefore,
            ActionInput input,
            ActionResult result,
            ulong levelSeed,
            long actionStartMs,
            long actionEndMs,
            TelemetrySessionState session,
            TelemetryLogger logger)
        {
            if (levelId is null) throw new ArgumentNullException(nameof(levelId));
            if (stateBefore is null) throw new ArgumentNullException(nameof(stateBefore));
            if (result is null) throw new ArgumentNullException(nameof(result));
            if (session is null) throw new ArgumentNullException(nameof(session));
            if (logger is null) throw new ArgumentNullException(nameof(logger));

            long timestampMs = actionEndMs;

            // Invalid input — emit only invalid_tap and return; no hazard advance, no other events.
            foreach (ActionEvent ev in result.Events)
            {
                if (ev is InvalidInput ii)
                {
                    logger.Append(new InvalidTapEvent(
                        LevelId: levelId,
                        TimestampMs: timestampMs,
                        TappedCoord: ii.TappedCoord,
                        Reason: MapInvalidReason(ii.Reason)));
                    return;
                }
            }

            int actionIndex = result.State.ActionCount;

            // time_to_first_action / idle_time
            if (!session.FirstActionFired)
            {
                long firstActionMs = actionStartMs - session.LevelStartMs;
                logger.Append(new TimeToFirstActionEvent(
                    LevelId: levelId,
                    TimestampMs: timestampMs,
                    FirstActionMs: firstActionMs));
                session.FirstActionFired = true;
            }
            else if (session.PreviousActionEndMs.HasValue)
            {
                long idleMs = actionStartMs - session.PreviousActionEndMs.Value;
                logger.Append(new IdleTimeEvent(
                    LevelId: levelId,
                    TimestampMs: timestampMs,
                    ActionIndex: actionIndex,
                    IdleMs: idleMs));
            }

            session.PreviousActionEndMs = actionEndMs;

            // action_taken
            logger.Append(new ActionTakenEvent(
                LevelId: levelId,
                TimestampMs: timestampMs,
                ActionIndex: actionIndex,
                Input: input.TappedCoord,
                Seed: levelSeed,
                RngStateBefore: stateBefore.RngState,
                RngStateAfter: result.State.RngState,
                UndoAvailable: stateBefore.UndoAvailable,
                DebugSpawnOverride: stateBefore.DebugSpawnOverride));

            // dock_occupancy
            int occupancy = CountOccupancy(result.State.Dock);
            logger.Append(new DockOccupancyEvent(
                LevelId: levelId,
                TimestampMs: timestampMs,
                ActionIndex: actionIndex,
                Occupancy: occupancy,
                WarningLevel: DockHelpers.GetWarningLevel(result.State.Dock)));

            // Pipeline event scan
            bool dockJamWasActive = stateBefore.DockJamActive;

            foreach (ActionEvent ev in result.Events)
            {
                switch (ev)
                {
                    case WaterRose:
                        logger.Append(new WaterRiseEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            NewFloodedRows: result.State.Water.FloodedRows));
                        break;

                    case VineGrown vg:
                        logger.Append(new VineGrowthEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            GrownTile: vg.Coord));
                        break;

                    case TargetExtracted te:
                        logger.Append(new TargetExtractedEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            TargetId: te.TargetId));
                        break;

                    case DockJamTriggered _:
                        logger.Append(new DockJamTriggeredEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex));
                        break;
                }
            }

            // dock_jam_resolved — fires when this action was a DockJam recovery action
            if (dockJamWasActive)
            {
                string resolvedBy = result.Outcome == ActionOutcome.Ok
                    ? DockJamResolutions.TripleCleared
                    : DockJamResolutions.FailedToClear;

                logger.Append(new DockJamResolvedEvent(
                    LevelId: levelId,
                    TimestampMs: timestampMs,
                    ActionIndex: actionIndex,
                    ResolvedBy: resolvedBy));
            }

            // target_lost (water on target)
            if (result.Outcome == ActionOutcome.LossWaterOnTarget)
            {
                string? lostTargetId = FindLostTarget(stateBefore, result.State);
                if (lostTargetId is not null)
                {
                    logger.Append(new TargetLostEvent(
                        LevelId: levelId,
                        TimestampMs: timestampMs,
                        ActionIndex: actionIndex,
                        TargetId: lostTargetId));
                }
            }

            // hazard_proximity_to_target (once per unextracted target per action)
            foreach (TargetState target in result.State.Targets)
            {
                if (target.Extracted)
                {
                    continue;
                }

                int waterDistanceRows = ComputeWaterDistance(target.Coord, result.State.Board, result.State.Water);
                bool vineAdjacency = HasVineAdjacency(target.Coord, result.State.Board);

                logger.Append(new HazardProximityToTargetEvent(
                    LevelId: levelId,
                    TimestampMs: timestampMs,
                    ActionIndex: actionIndex,
                    TargetId: target.TargetId,
                    WaterDistanceRows: waterDistanceRows,
                    VineAdjacency: vineAdjacency));
            }

            // level_win / level_loss
            if (result.Outcome == ActionOutcome.Win)
            {
                ImmutableArray<string> order = result.State.ExtractedTargetOrder;
                string[] orderArray = new string[order.Length];
                for (int i = 0; i < order.Length; i++)
                {
                    orderArray[i] = order[i];
                }

                logger.Append(new LevelWinEvent(
                    LevelId: levelId,
                    TimestampMs: timestampMs,
                    ActionCount: actionIndex,
                    ExtractedTargetOrder: orderArray,
                    UndoUsed: !result.State.UndoAvailable));
            }
            else if (result.Outcome != ActionOutcome.Ok)
            {
                string reason = MapLossReason(result.Outcome, dockJamWasActive);
                string? lostTargetId = result.Outcome == ActionOutcome.LossWaterOnTarget
                    ? FindLostTarget(stateBefore, result.State)
                    : null;

                logger.Append(new LevelLossEvent(
                    LevelId: levelId,
                    TimestampMs: timestampMs,
                    ActionCount: actionIndex,
                    Reason: reason,
                    LostTargetId: lostTargetId));
            }

            // capture_snapshot
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logger.Config.CaptureSnapshotsEnabled
                && actionIndex % logger.Config.CaptureSnapshotEveryNActions == 0)
            {
                string gameStateJson = SerializeGameState(result.State);
                logger.Append(new CaptureSnapshotEvent(
                    LevelId: levelId,
                    TimestampMs: timestampMs,
                    ActionIndex: actionIndex,
                    GameStateJson: gameStateJson));
            }
#endif
        }

        public static void OnLevelAbandoned(
            string levelId,
            int actionCount,
            long timestampMs,
            TelemetryLogger logger)
        {
            if (levelId is null) throw new ArgumentNullException(nameof(levelId));
            if (logger is null) throw new ArgumentNullException(nameof(logger));

            logger.Append(new LevelLossEvent(
                LevelId: levelId,
                TimestampMs: timestampMs,
                ActionCount: actionCount,
                Reason: LossReasons.ManualAbandon,
                LostTargetId: null));
        }

        public static void OnUndoUsed(
            string levelId,
            int actionIndexBeforeUndo,
            long timestampMs,
            TelemetryLogger logger)
        {
            if (levelId is null) throw new ArgumentNullException(nameof(levelId));
            if (logger is null) throw new ArgumentNullException(nameof(logger));

            logger.Append(new UndoUsedEvent(
                LevelId: levelId,
                TimestampMs: timestampMs,
                ActionIndexBeforeUndo: actionIndexBeforeUndo));
        }

        private static string MapInvalidReason(InvalidInputReason reason)
        {
            return reason switch
            {
                InvalidInputReason.OutOfBounds => InvalidTapReasons.OutOfBounds,
                InvalidInputReason.Flooded => InvalidTapReasons.FloodedTile,
                InvalidInputReason.Ice => InvalidTapReasons.BlockerTile,
                InvalidInputReason.Blocker => InvalidTapReasons.BlockerTile,
                InvalidInputReason.Target => InvalidTapReasons.TargetTile,
                InvalidInputReason.Empty => InvalidTapReasons.NoGroup,
                InvalidInputReason.SingleTile => InvalidTapReasons.IsolatedTile,
                InvalidInputReason.Frozen => InvalidTapReasons.Frozen,
                _ => reason.ToString().ToLowerInvariant(),
            };
        }

        private static string MapLossReason(ActionOutcome outcome, bool dockJamWasActive)
        {
            return outcome switch
            {
                ActionOutcome.LossDockOverflow when dockJamWasActive => LossReasons.DockJamUnresolved,
                ActionOutcome.LossDockOverflow => LossReasons.DockOverflow,
                ActionOutcome.LossWaterOnTarget => LossReasons.WaterOnTarget,
                _ => LossReasons.DockOverflow,
            };
        }

        private static string? FindLostTarget(GameState stateBefore, GameState stateAfter)
        {
            int floodStartRow = stateAfter.Board.Height - stateAfter.Water.FloodedRows;
            foreach (TargetState target in stateBefore.Targets)
            {
                if (!target.Extracted && target.Coord.Row >= floodStartRow)
                {
                    return target.TargetId;
                }
            }

            return null;
        }

        private static int ComputeWaterDistance(TileCoord targetCoord, Board board, WaterState water)
        {
            // Positive = target is in or below the waterline (dangerous).
            // Negative = target is above the waterline (safe, magnitude = rows of buffer).
            int lastDryRowFromTop = board.Height - water.FloodedRows - 1;
            return targetCoord.Row - lastDryRowFromTop;
        }

        private static bool HasVineAdjacency(TileCoord coord, Board board)
        {
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, coord);
            foreach (TileCoord neighbor in neighbors)
            {
                if (BoardHelpers.GetTile(board, neighbor) is BlockerTile bt && bt.Type == BlockerType.Vine)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountOccupancy(Dock dock)
        {
            int count = 0;
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                if (dock.Slots[i].HasValue)
                {
                    count++;
                }
            }

            return count;
        }

        private static string SerializeGameState(GameState state)
        {
            try
            {
                JsonSerializerOptions opts = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter(), new TileJsonConverter() },
                    WriteIndented = false,
                };
                return JsonSerializer.Serialize(state, opts);
            }
            catch (Exception ex)
            {
                return $"{{\"serializationError\":\"{ex.Message}\"}}";
            }
        }

        private sealed class TileJsonConverter : JsonConverter<Tile>
        {
            public override Tile? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using JsonDocument doc = JsonDocument.ParseValue(ref reader);
                JsonElement root = doc.RootElement;
                string? type = root.TryGetProperty("type", out JsonElement tp) ? tp.GetString() : null;

                return type switch
                {
                    "empty" => new EmptyTile(),
                    "flooded" => new FloodedTile(),
                    "debris" => new DebrisTile(
                        Enum.Parse<DebrisType>(root.GetProperty("debrisType").GetString()!)),
                    "blocker" => new BlockerTile(
                        Enum.Parse<BlockerType>(root.GetProperty("blockerType").GetString()!),
                        root.GetProperty("hp").GetInt32(),
                        root.TryGetProperty("hidden", out JsonElement h) && h.ValueKind != JsonValueKind.Null
                            ? new DebrisTile(Enum.Parse<DebrisType>(h.GetProperty("debrisType").GetString()!))
                            : null),
                    "target" => new TargetTile(
                        root.GetProperty("targetId").GetString()!,
                        root.GetProperty("extracted").GetBoolean()),
                    _ => throw new JsonException($"Unknown tile type: {type}"),
                };
            }

            public override void Write(Utf8JsonWriter writer, Tile value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                switch (value)
                {
                    case EmptyTile:
                        writer.WriteString("type", "empty");
                        break;

                    case FloodedTile:
                        writer.WriteString("type", "flooded");
                        break;

                    case DebrisTile d:
                        writer.WriteString("type", "debris");
                        writer.WriteString("debrisType", d.Type.ToString());
                        break;

                    case BlockerTile b:
                        writer.WriteString("type", "blocker");
                        writer.WriteString("blockerType", b.Type.ToString());
                        writer.WriteNumber("hp", b.Hp);
                        if (b.Hidden is not null)
                        {
                            writer.WritePropertyName("hidden");
                            writer.WriteStartObject();
                            writer.WriteString("type", "debris");
                            writer.WriteString("debrisType", b.Hidden.Type.ToString());
                            writer.WriteEndObject();
                        }
                        else
                        {
                            writer.WriteNull("hidden");
                        }
                        break;

                    case TargetTile t:
                        writer.WriteString("type", "target");
                        writer.WriteString("targetId", t.TargetId);
                        writer.WriteBoolean("extracted", t.Extracted);
                        break;

                    default:
                        throw new JsonException($"Unknown Tile subtype: {value.GetType().Name}");
                }

                writer.WriteEndObject();
            }
        }
    }
}
