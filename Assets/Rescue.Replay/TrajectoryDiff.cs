using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Rescue.Core.Pipeline;
using Rescue.Core.State;

namespace Rescue.Replay
{
    public enum TrajectoryDiffKind
    {
        LevelIdMismatch,
        SeedMismatch,
        FrameCountMismatch,
        ActionIndexMismatch,
        InputMismatch,
        OutcomeMismatch,
        StateMismatch,
        RngVerificationMismatch,
    }

    public sealed record TrajectoryDiff(
        TrajectoryDiffKind Kind,
        int FrameIndex,
        string Message,
        string? Expected = null,
        string? Actual = null);

    internal static class TrajectoryFormatter
    {
        public static string SummarizeFrame(ReplayFrame frame)
        {
            return $"frame={frame.FrameIndex}; action={frame.ActionIndex}; input={FormatInput(frame.Input)}; outcome={frame.Outcome?.ToString() ?? "initial"}; state={Fingerprint(frame.State)}";
        }

        public static string Fingerprint(GameState state)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("board=").Append(BoardFingerprint(state.Board));
            builder.Append("|dock=").Append(DockFingerprint(state.Dock));
            builder.Append("|water=").Append(state.Water.FloodedRows).Append('/').Append(state.Water.ActionsUntilRise).Append('/').Append(state.Water.RiseInterval).Append('/').Append(state.Water.PauseUntilFirstAction);
            builder.Append("|waterMode=").Append(state.LevelConfig.WaterContactMode);
            builder.Append("|vine=").Append(state.Vine.ActionsSinceLastClear).Append('/').Append(state.Vine.GrowthThreshold).Append('/').Append(state.Vine.PriorityCursor).Append('/').Append(FormatCoord(state.Vine.PendingGrowthTile));
            builder.Append("|targets=").Append(TargetFingerprint(state.Targets));
            builder.Append("|rng=").Append(state.RngState.S0).Append(':').Append(state.RngState.S1);
            builder.Append("|actionCount=").Append(state.ActionCount);
            builder.Append("|undo=").Append(state.UndoAvailable);
            builder.Append("|dockJam=").Append(state.DockJamUsed).Append('/').Append(state.DockJamEnabled).Append('/').Append(state.DockJamActive);
            builder.Append("|frozen=").Append(state.Frozen);
            builder.Append("|emergency=").Append(state.ConsecutiveEmergencySpawns).Append('/').Append(state.SpawnRecoveryCounter);
            builder.Append("|spawnLineage=").Append(state.NextSpawnLineageId).Append('/').Append(SpawnLineageFingerprint(state.SpawnLineageByCoord));
            builder.Append("|extracted=").Append(string.Join(",", state.ExtractedTargetOrder));
            builder.Append("|spawnOverride=").Append(SpawnOverrideFingerprint(state.DebugSpawnOverride));
            return builder.ToString();
        }

        private static string BoardFingerprint(Board board)
        {
            List<string> rows = new List<string>(board.Height);
            for (int row = 0; row < board.Height; row++)
            {
                StringBuilder builder = new StringBuilder();
                for (int col = 0; col < board.Width; col++)
                {
                    builder.Append(TileCode(BoardHelpers.GetTile(board, new TileCoord(row, col))));
                    if (col < board.Width - 1)
                    {
                        builder.Append(',');
                    }
                }

                rows.Add(builder.ToString());
            }

            return string.Join("/", rows);
        }

        private static string DockFingerprint(Dock dock)
        {
            List<string> slots = new List<string>(dock.Slots.Length);
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                slots.Add(dock.Slots[i]?.ToString() ?? ".");
            }

            return string.Join(",", slots);
        }

        private static string TargetFingerprint(ImmutableArray<TargetState> targets)
        {
            List<string> values = new List<string>(targets.Length);
            for (int i = 0; i < targets.Length; i++)
            {
                TargetState target = targets[i];
                // Target readiness is player-visible rules state, so replay diffs must catch it.
                values.Add($"{target.TargetId}@{target.Coord.Row},{target.Coord.Col}:{target.Readiness}");
            }

            return string.Join(";", values);
        }

        private static string SpawnOverrideFingerprint(SpawnOverride? spawnOverride)
        {
            return spawnOverride is null
                ? "none"
                : $"{spawnOverride.ForceEmergency?.ToString() ?? "null"}/{spawnOverride.OverrideAssistanceChance?.ToString("G17") ?? "null"}";
        }

        private static string SpawnLineageFingerprint(ImmutableDictionary<TileCoord, SpawnLineage> lineageByCoord)
        {
            List<string> entries = new List<string>(lineageByCoord.Count);
            foreach (KeyValuePair<TileCoord, SpawnLineage> entry in lineageByCoord)
            {
                TileCoord coord = entry.Key;
                SpawnLineage lineage = entry.Value;
                entries.Add($"{coord.Row},{coord.Col}:{lineage.LineageId}:{lineage.Type}:{lineage.OriginalCoord.Row},{lineage.OriginalCoord.Col}");
            }

            entries.Sort(StringComparer.Ordinal);
            return string.Join(";", entries);
        }

        private static string TileCode(Tile tile)
        {
            return tile switch
            {
                EmptyTile => ".",
                FloodedTile => "~",
                DebrisTile debris => debris.Type.ToString(),
                BlockerTile blocker when blocker.Type == BlockerType.Crate => blocker.Hp > 1 ? "CX" : "CR",
                BlockerTile blocker when blocker.Type == BlockerType.Vine => "V",
                BlockerTile blocker when blocker.Type == BlockerType.Ice && blocker.Hidden is not null => "I" + blocker.Hidden.Type,
                BlockerTile blocker when blocker.Type == BlockerType.Ice => "I?",
                TargetTile target => "T" + target.TargetId + (target.Extracted ? "!" : string.Empty),
                _ => tile.GetType().Name,
            };
        }

        private static string FormatInput(ActionInput? input)
        {
            return input.HasValue
                ? $"{input.Value.TappedCoord.Row},{input.Value.TappedCoord.Col}"
                : "none";
        }

        private static string FormatCoord(TileCoord? coord)
        {
            return coord.HasValue ? $"{coord.Value.Row},{coord.Value.Col}" : "none";
        }
    }
}
