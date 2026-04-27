using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.Telemetry
{
    public sealed class TelemetrySessionState
    {
        public bool FirstActionFired { get; set; }
        public long? PreviousActionEndMs { get; set; }
        public long LevelStartMs { get; set; }
        public List<PendingAssistedSpawn> PendingAssistedSpawns { get; } = new List<PendingAssistedSpawn>();
    }

    public sealed class PendingAssistedSpawn
    {
        public PendingAssistedSpawn(int actionIndex, ImmutableArray<PendingAssistedSpawnPiece> pieces)
        {
            ActionIndex = actionIndex;
            Pieces = pieces;
        }

        public int ActionIndex { get; }
        public ImmutableArray<PendingAssistedSpawnPiece> Pieces { get; }
    }

    public readonly record struct PendingAssistedSpawnPiece(
        int LineageId,
        DebrisType Type,
        TileCoord OriginalCoord);

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
                TargetCount: initialState.Targets.Length,
                WaterMode: initialState.LevelConfig.WaterContactMode.ToString()));
            AppendWaterForecast(
                levelId,
                timestampMs,
                actionIndex: 0,
                initialState,
                WaterForecastTiming.LevelStart,
                logger);
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

            AppendWaterForecast(
                levelId,
                timestampMs,
                actionIndex,
                stateBefore,
                WaterForecastTiming.PreAction,
                logger);

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

            EmitAssistedSpawnFollowUps(levelId, timestampMs, actionIndex, result.Events, session, logger);

            // dock_occupancy
            int occupancy = CountOccupancy(result.State.Dock);
            logger.Append(new DockOccupancyEvent(
                LevelId: levelId,
                TimestampMs: timestampMs,
                ActionIndex: actionIndex,
                Occupancy: occupancy,
                WarningLevel: DockHelpers.GetWarningLevel(result.State.Dock),
                DockSize: result.State.Dock.Size));

            AppendWaterForecast(
                levelId,
                timestampMs,
                actionIndex,
                result.State,
                WaterForecastTiming.PostAction,
                logger);

            // Pipeline event scan
            bool dockJamWasActive = stateBefore.DockJamActive;
            int finalRescueOverflowCount = 0;
            DebugSpawnOverrideApplied? spawnOverrideApplied = null;
            Spawned? spawned = null;

            foreach (ActionEvent ev in result.Events)
            {
                switch (ev)
                {
                    case DockOverflowTriggered overflow:
                        finalRescueOverflowCount = overflow.OverflowCount;
                        break;

                    case TargetProgressed progressed:
                        AppendTargetTransition(levelId, timestampMs, actionIndex, progressed.TargetId, progressed.Coord, TargetReadiness.Progressing, stateBefore, logger);
                        break;

                    case TargetOneClearAway oneClearAway:
                        AppendTargetTransition(levelId, timestampMs, actionIndex, oneClearAway.TargetId, oneClearAway.Coord, TargetReadiness.OneClearAway, stateBefore, logger);
                        break;

                    case TargetExtractionLatched latched:
                        AppendTargetTransition(levelId, timestampMs, actionIndex, latched.TargetId, latched.Coord, TargetReadiness.ExtractableLatched, stateBefore, logger);
                        break;

                    case WaterRose:
                        logger.Append(new WaterRiseEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            NewFloodedRows: result.State.Water.FloodedRows));
                        break;

                    case VinePreviewChanged preview:
                        logger.Append(new VinePreviewEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            PendingTile: preview.PendingTile));
                        break;

                    case VineGrown vg:
                        logger.Append(new VineGrowthEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            GrownTile: vg.Coord));
                        break;

                    case TargetExtracted te:
                        logger.Append(new TargetStateTransitionEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            TargetId: te.TargetId,
                            Coord: te.Coord,
                            FromState: TargetReadiness.ExtractableLatched,
                            ToState: TargetReadiness.Extracted));
                        logger.Append(new TargetExtractedEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            TargetId: te.TargetId));
                        break;

                    case TargetDistressedEntered entered:
                        logger.Append(new TargetDistressedEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            TargetId: entered.TargetId,
                            Transition: "entered"));
                        logger.Append(new TargetStateTransitionEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            TargetId: entered.TargetId,
                            Coord: entered.Coord,
                            FromState: FindTargetReadiness(stateBefore, entered.TargetId),
                            ToState: TargetReadiness.Distressed));
                        logger.Append(new GraceEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            TargetId: entered.TargetId,
                            Outcome: "entered"));
                        break;

                    case TargetDistressedRecovered recovered:
                        logger.Append(new TargetDistressedEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            TargetId: recovered.TargetId,
                            Transition: "recovered"));
                        logger.Append(new TargetStateTransitionEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            TargetId: recovered.TargetId,
                            Coord: recovered.Coord,
                            FromState: TargetReadiness.Distressed,
                            ToState: TargetReadiness.ExtractableLatched));
                        logger.Append(new GraceEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            TargetId: recovered.TargetId,
                            Outcome: "recovered"));
                        break;

                    case TargetDistressedExpired expired:
                        logger.Append(new TargetDistressedEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            TargetId: expired.TargetId,
                            Transition: "expired"));
                        logger.Append(new GraceEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            TargetId: expired.TargetId,
                            Outcome: "failed"));
                        break;

                    case DockJamTriggered _:
                        logger.Append(new DockJamTriggeredEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex));
                        break;

                    case DebugSpawnOverrideApplied applied:
                        spawnOverrideApplied = applied;
                        break;

                    case Spawned spawnedEvent:
                        spawned = spawnedEvent;
                        break;

                    case DeadboardDiagnosticDetected diagnostic:
                        logger.Append(new DeadboardLikeStateEvent(
                            LevelId: levelId,
                            TimestampMs: timestampMs,
                            ActionIndex: actionIndex,
                            Reason: FormatDeadboardReason(diagnostic.Reason),
                            TargetId: diagnostic.TargetId));
                        break;
                }
            }

            EmitAssistedSpawn(levelId, timestampMs, actionIndex, stateBefore, spawnOverrideApplied, spawned, session, logger);

            if (result.Outcome == ActionOutcome.Win)
            {
                string? finalTargetId = GetFinalExtractedTargetId(result.Events);
                bool overflowWouldHaveFailed = finalRescueOverflowCount > 0;
                logger.Append(new FinalRescueEvent(
                    LevelId: levelId,
                    TimestampMs: timestampMs,
                    ActionIndex: actionIndex,
                    TargetId: finalTargetId,
                    DockOverflowWouldHaveFailed: overflowWouldHaveFailed,
                    HazardAdvanceSkipped: true));
                logger.Append(new HazardAdvanceSkippedEvent(
                    LevelId: levelId,
                    TimestampMs: timestampMs,
                    ActionIndex: actionIndex,
                    Reason: "final_rescue",
                    WaterSkipped: true,
                    VineSkipped: true));

                if (overflowWouldHaveFailed)
                {
                    logger.Append(new FinalRescueDockOverflowOverrideEvent(
                        LevelId: levelId,
                        TimestampMs: timestampMs,
                        ActionIndex: actionIndex,
                        OverflowCount: finalRescueOverflowCount));
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
            if (result.Outcome == ActionOutcome.LossWaterOnTarget
                || result.Outcome == ActionOutcome.LossDistressedExpired)
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
                    || result.Outcome == ActionOutcome.LossDistressedExpired
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

        public static void OnTuningChanged(
            string levelId,
            ulong seed,
            LevelTuningOverrides overrides,
            string changeSource,
            string? presetName,
            long timestampMs,
            TelemetryLogger logger)
        {
            if (levelId is null) throw new ArgumentNullException(nameof(levelId));
            if (overrides is null) throw new ArgumentNullException(nameof(overrides));
            if (changeSource is null) throw new ArgumentNullException(nameof(changeSource));
            if (logger is null) throw new ArgumentNullException(nameof(logger));

            logger.Append(new TuningChangedEvent(
                LevelId: levelId,
                TimestampMs: timestampMs,
                Seed: seed,
                ChangeSource: changeSource,
                PresetName: presetName,
                WaterRiseInterval: overrides.WaterRiseInterval,
                InitialFloodedRows: overrides.InitialFloodedRows,
                AssistanceChance: overrides.AssistanceChance,
                ForceEmergencyAssistance: overrides.ForceEmergencyAssistance,
                DockJamEnabled: overrides.DockJamEnabled,
                DockSize: overrides.DockSize,
                DefaultCrateHp: overrides.DefaultCrateHp,
                VineGrowthThreshold: overrides.VineGrowthThreshold,
                WaterContactMode: overrides.WaterContactMode?.ToString()));
        }

        private static void AppendTargetTransition(
            string levelId,
            long timestampMs,
            int actionIndex,
            string targetId,
            TileCoord coord,
            TargetReadiness toState,
            GameState stateBefore,
            TelemetryLogger logger)
        {
            logger.Append(new TargetStateTransitionEvent(
                LevelId: levelId,
                TimestampMs: timestampMs,
                ActionIndex: actionIndex,
                TargetId: targetId,
                Coord: coord,
                FromState: FindTargetReadiness(stateBefore, targetId),
                ToState: toState));
        }

        private static void AppendWaterForecast(
            string levelId,
            long timestampMs,
            int actionIndex,
            GameState state,
            WaterForecastTiming timing,
            TelemetryLogger logger)
        {
            int? nextFloodRow = WaterHelpers.GetNextFloodRow(state.Board, state.Water);
            logger.Append(new WaterForecastEvent(
                LevelId: levelId,
                TimestampMs: timestampMs,
                ActionIndex: actionIndex,
                WaterMode: state.LevelConfig.WaterContactMode.ToString(),
                NextFloodRow: nextFloodRow,
                ForecastAvailable: nextFloodRow.HasValue,
                ActionsUntilRise: state.Water.ActionsUntilRise,
                Timing: timing));
        }

        private static TargetReadiness FindTargetReadiness(GameState state, string targetId)
        {
            for (int i = 0; i < state.Targets.Length; i++)
            {
                if (state.Targets[i].TargetId == targetId)
                {
                    return state.Targets[i].Readiness;
                }
            }

            return TargetReadiness.Trapped;
        }

        private static string? GetFinalExtractedTargetId(ImmutableArray<ActionEvent> events)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i] is Won won)
                {
                    return won.FinalExtractedTargetId;
                }
            }

            for (int i = events.Length - 1; i >= 0; i--)
            {
                if (events[i] is TargetExtracted extracted)
                {
                    return extracted.TargetId;
                }
            }

            return null;
        }

        private static void EmitAssistedSpawnFollowUps(
            string levelId,
            long timestampMs,
            int actionIndex,
            ImmutableArray<ActionEvent> events,
            TelemetrySessionState session,
            TelemetryLogger logger)
        {
            DebrisType? removedType = null;
            ImmutableArray<int> removedLineages = ImmutableArray<int>.Empty;
            ImmutableArray<TileCoord> removedCoords = ImmutableArray<TileCoord>.Empty;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i] is GroupRemoved removed)
                {
                    removedType = removed.Type;
                    removedLineages = removed.SpawnLineageIds;
                    removedCoords = removed.Coords;
                    break;
                }
            }

            for (int i = session.PendingAssistedSpawns.Count - 1; i >= 0; i--)
            {
                PendingAssistedSpawn pending = session.PendingAssistedSpawns[i];
                int actionsElapsed = actionIndex - pending.ActionIndex;
                if (actionsElapsed > 2)
                {
                    session.PendingAssistedSpawns.RemoveAt(i);
                    continue;
                }

                if (actionsElapsed <= 0 || !removedType.HasValue)
                {
                    continue;
                }

                PendingAssistedSpawnPiece? usedPiece = FindPendingPiece(pending.Pieces, removedLineages);
                if (!usedPiece.HasValue)
                {
                    continue;
                }

                logger.Append(new AssistedSpawnFollowUpEvent(
                    LevelId: levelId,
                    TimestampMs: timestampMs,
                    OriginalActionIndex: pending.ActionIndex,
                    FollowUpActionIndex: actionIndex,
                    UsedType: removedType.Value,
                    SpawnLineageId: usedPiece.Value.LineageId,
                    OriginalCoord: usedPiece.Value.OriginalCoord,
                    RemovedGroupCoords: ToArray(removedCoords)));
                session.PendingAssistedSpawns.RemoveAt(i);
            }
        }

        private static void EmitAssistedSpawn(
            string levelId,
            long timestampMs,
            int actionIndex,
            GameState stateBefore,
            DebugSpawnOverrideApplied? overrideApplied,
            Spawned? spawned,
            TelemetrySessionState session,
            TelemetryLogger logger)
        {
            if (spawned is null || spawned.Pieces.IsDefaultOrEmpty)
            {
                return;
            }

            bool emergencyRequested = IsEmergencyRequested(stateBefore);
            bool emergencyApplied = IsEmergencyApplied(stateBefore, emergencyRequested);
            double effectiveAssistanceChance = EffectiveAssistanceChance(stateBefore, emergencyApplied);
            if (overrideApplied is not null)
            {
                emergencyRequested = overrideApplied.EmergencyRequested;
                emergencyApplied = overrideApplied.EmergencyApplied;
                effectiveAssistanceChance = overrideApplied.EffectiveAssistanceChance;
            }

            if (effectiveAssistanceChance <= 0.0d && overrideApplied is null)
            {
                return;
            }

            ImmutableArray<PendingAssistedSpawnPiece>.Builder pendingPieces = ImmutableArray.CreateBuilder<PendingAssistedSpawnPiece>(spawned.Pieces.Length);
            AssistedSpawnPieceTelemetry[] piecePayloads = new AssistedSpawnPieceTelemetry[spawned.Pieces.Length];
            for (int i = 0; i < spawned.Pieces.Length; i++)
            {
                SpawnedPiece piece = spawned.Pieces[i];
                pendingPieces.Add(new PendingAssistedSpawnPiece(piece.LineageId, piece.Type, piece.Coord));
                piecePayloads[i] = ConvertSpawnedPiece(piece);
            }

            logger.Append(new AssistedSpawnEvent(
                LevelId: levelId,
                TimestampMs: timestampMs,
                ActionIndex: actionIndex,
                Reason: BuildAssistedSpawnReason(spawned),
                Context: BuildAssistedSpawnContext(stateBefore),
                SpawnCount: spawned.Pieces.Length,
                EmergencyRequested: emergencyRequested,
                EmergencyApplied: emergencyApplied,
                EffectiveAssistanceChance: effectiveAssistanceChance,
                Pieces: piecePayloads));

            session.PendingAssistedSpawns.Add(new PendingAssistedSpawn(actionIndex, pendingPieces.ToImmutable()));
        }

        private static bool IsEmergencyRequested(GameState state)
        {
            if (state.DebugSpawnOverride?.ForceEmergency == true)
            {
                return true;
            }

            if (state.DebugSpawnOverride?.ForceEmergency == false)
            {
                return false;
            }

            return CountOccupancy(state.Dock) >= 5 || HasTargetOnNextFloodRow(state);
        }

        private static bool IsEmergencyApplied(GameState state, bool emergencyRequested)
        {
            return emergencyRequested && state.ConsecutiveEmergencySpawns < state.LevelConfig.ConsecutiveEmergencyCap;
        }

        private static double EffectiveAssistanceChance(GameState state, bool emergencyApplied)
        {
            double chance = state.DebugSpawnOverride?.OverrideAssistanceChance ?? state.LevelConfig.AssistanceChance;
            if (emergencyApplied)
            {
                chance += 0.2d;
            }

            if (chance < 0.0d) return 0.0d;
            if (chance > 1.0d) return 1.0d;
            return chance;
        }

        private static string BuildAssistedSpawnReason(Spawned spawned)
        {
            SortedSet<string> reasons = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < spawned.Pieces.Length; i++)
            {
                SpawnedPiece piece = spawned.Pieces[i];
                for (int j = 0; j < piece.Reasons.Length; j++)
                {
                    reasons.Add(FormatAssistReason(piece.Reasons[j]));
                }
            }

            return reasons.Count == 0 ? "baseline_assistance" : string.Join("+", reasons);
        }

        private static string BuildAssistedSpawnContext(GameState state)
        {
            int? nextFloodRow = WaterHelpers.GetNextFloodRow(state.Board, state.Water);
            return $"dock={CountOccupancy(state.Dock)}/{state.Dock.Size};nextFloodRow={FormatNullableInt(nextFloodRow)};waterMode={state.LevelConfig.WaterContactMode};recovery={state.SpawnRecoveryCounter}";
        }

        private static string FormatNullableInt(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "none";
        }

        private static bool HasTargetOnNextFloodRow(GameState state)
        {
            int? nextFloodRow = WaterHelpers.GetNextFloodRow(state.Board, state.Water);
            if (!nextFloodRow.HasValue)
            {
                return false;
            }

            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                if (!target.Extracted && target.Coord.Row == nextFloodRow.Value)
                {
                    return true;
                }
            }

            return false;
        }

        private static PendingAssistedSpawnPiece? FindPendingPiece(
            ImmutableArray<PendingAssistedSpawnPiece> pendingPieces,
            ImmutableArray<int> removedLineages)
        {
            for (int i = 0; i < pendingPieces.Length; i++)
            {
                for (int j = 0; j < removedLineages.Length; j++)
                {
                    if (pendingPieces[i].LineageId == removedLineages[j])
                    {
                        return pendingPieces[i];
                    }
                }
            }

            return null;
        }

        private static TileCoord[] ToArray(ImmutableArray<TileCoord> coords)
        {
            TileCoord[] values = new TileCoord[coords.Length];
            for (int i = 0; i < coords.Length; i++)
            {
                values[i] = coords[i];
            }

            return values;
        }

        private static AssistedSpawnPieceTelemetry ConvertSpawnedPiece(SpawnedPiece piece)
        {
            string[] reasons = new string[piece.Reasons.Length];
            for (int i = 0; i < piece.Reasons.Length; i++)
            {
                reasons[i] = FormatAssistReason(piece.Reasons[i]);
            }

            string[] context = new string[piece.TriggerContext.Length];
            for (int i = 0; i < piece.TriggerContext.Length; i++)
            {
                context[i] = piece.TriggerContext[i];
            }

            return new AssistedSpawnPieceTelemetry(
                piece.Coord,
                piece.Type,
                piece.LineageId,
                reasons,
                context,
                piece.UrgentTargetId,
                piece.UrgentTargetCoord,
                piece.WaterRisesRemaining,
                piece.DockOccupancy,
                piece.RecoveryCounterBefore,
                piece.EmergencyRequested,
                piece.EmergencyApplied,
                piece.EffectiveAssistanceChance);
        }

        private static string FormatAssistReason(SpawnAssistReason reason)
        {
            return reason switch
            {
                SpawnAssistReason.BaselineAssistance => "baseline_assistance",
                SpawnAssistReason.DockCompletion => "dock_completion",
                SpawnAssistReason.RouteHardPair => "route_hard_pair",
                SpawnAssistReason.RouteSoftPair => "route_soft_pair",
                SpawnAssistReason.RouteAdjacency => "route_adjacency",
                SpawnAssistReason.SingletonRecovery => "singleton_recovery",
                SpawnAssistReason.EmergencyWaterPressure => "emergency_water_pressure",
                SpawnAssistReason.EmergencyDockPressure => "emergency_dock_pressure",
                SpawnAssistReason.DebugOverride => "debug_override",
                _ => reason.ToString().ToLowerInvariant(),
            };
        }

        private static string FormatDeadboardReason(DeadboardDiagnosticReason reason)
        {
            return reason switch
            {
                DeadboardDiagnosticReason.HardNoValidGroups => "hard_no_valid_groups",
                DeadboardDiagnosticReason.SoftNoRescueProgressMove => "soft_no_rescue_progress_move",
                DeadboardDiagnosticReason.RescueImpossibleStatic => "rescue_impossible_static",
                _ => reason.ToString().ToLowerInvariant(),
            };
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
                ActionOutcome.LossDistressedExpired => LossReasons.DistressedExpired,
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
