#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !CAPTURE_BUILD
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.Unity.Debugging
{
    internal static class DebugPanelExportBuilder
    {
        public static DebugBugReportExport BuildBugReport(string levelId, int seed, string exportedAtUtc, GameState state)
        {
            return new DebugBugReportExport(levelId, seed, exportedAtUtc, BuildGameState(state));
        }

        public static GameStateExport BuildGameState(GameState state)
        {
            TileExport[][] tiles = new TileExport[state.Board.Height][];
            for (int row = 0; row < state.Board.Height; row++)
            {
                tiles[row] = new TileExport[state.Board.Width];
                for (int col = 0; col < state.Board.Width; col++)
                {
                    tiles[row][col] = ExportTile(BoardHelpers.GetTile(state.Board, new TileCoord(row, col)));
                }
            }

            TargetStateExport[] targets = new TargetStateExport[state.Targets.Length];
            for (int i = 0; i < state.Targets.Length; i++)
            {
                targets[i] = new TargetStateExport(
                    state.Targets[i].TargetId,
                    state.Targets[i].Coord.Row,
                    state.Targets[i].Coord.Col,
                    state.Targets[i].Readiness.ToString(),
                    state.Targets[i].Extracted,
                    state.Targets[i].OneClearAway);
            }

            string?[] dockSlots = new string?[state.Dock.Slots.Length];
            for (int i = 0; i < state.Dock.Slots.Length; i++)
            {
                dockSlots[i] = state.Dock.Slots[i]?.ToString();
            }

            string[] extractedTargetOrder = new string[state.ExtractedTargetOrder.Length];
            for (int i = 0; i < state.ExtractedTargetOrder.Length; i++)
            {
                extractedTargetOrder[i] = state.ExtractedTargetOrder[i];
            }

            string[] debrisPool = new string[state.LevelConfig.DebrisTypePool.Length];
            for (int i = 0; i < state.LevelConfig.DebrisTypePool.Length; i++)
            {
                debrisPool[i] = state.LevelConfig.DebrisTypePool[i].ToString();
            }

            Dictionary<string, double>? baseDistribution = null;
            if (state.LevelConfig.BaseDistribution is not null)
            {
                baseDistribution = new Dictionary<string, double>(state.LevelConfig.BaseDistribution.Count, StringComparer.Ordinal);
                foreach (KeyValuePair<DebrisType, double> entry in state.LevelConfig.BaseDistribution)
                {
                    baseDistribution[entry.Key.ToString()] = entry.Value;
                }
            }

            return new GameStateExport(
                new BoardExport(state.Board.Width, state.Board.Height, tiles),
                new DockExport(dockSlots, state.Dock.Size),
                new WaterExport(state.Water.FloodedRows, state.Water.ActionsUntilRise, state.Water.RiseInterval, state.Water.PauseUntilFirstAction),
                new VineExport(
                    state.Vine.ActionsSinceLastClear,
                    state.Vine.GrowthThreshold,
                    ExportCoords(state.Vine.GrowthPriorityList),
                    state.Vine.PriorityCursor,
                    ExportNullableCoord(state.Vine.PendingGrowthTile)),
                targets,
                new LevelConfigExport(
                    debrisPool,
                    baseDistribution,
                    state.LevelConfig.AssistanceChance,
                    state.LevelConfig.ConsecutiveEmergencyCap,
                    state.LevelConfig.IsRuleTeach,
                    state.LevelConfig.WaterContactMode.ToString()),
                new RngExport(state.RngState.S0, state.RngState.S1),
                state.ActionCount,
                state.DockJamUsed,
                state.UndoAvailable,
                extractedTargetOrder,
                state.Frozen,
                state.ConsecutiveEmergencySpawns,
                state.SpawnRecoveryCounter,
                state.DockJamEnabled,
                state.DockJamActive,
                state.DebugSpawnOverride is null
                    ? null
                    : new SpawnOverrideExport(state.DebugSpawnOverride.ForceEmergency, state.DebugSpawnOverride.OverrideAssistanceChance));
        }

        private static TileExport ExportTile(Tile tile)
        {
            return tile switch
            {
                EmptyTile => new TileExport("Empty", null, null, null, null, null, null),
                FloodedTile => new TileExport("Flooded", null, null, null, null, null, null),
                DebrisTile debris => new TileExport("Debris", debris.Type.ToString(), null, null, null, null, null),
                BlockerTile blocker => new TileExport("Blocker", null, blocker.Type.ToString(), blocker.Hp, null, null, blocker.Hidden?.Type.ToString()),
                TargetTile target => new TileExport("Target", null, null, null, target.TargetId, target.Extracted, null),
                _ => new TileExport(tile.GetType().Name, null, null, null, null, null, null),
            };
        }

        private static CoordExport[] ExportCoords(ImmutableArray<TileCoord> coords)
        {
            CoordExport[] exported = new CoordExport[coords.Length];
            for (int i = 0; i < coords.Length; i++)
            {
                exported[i] = new CoordExport(coords[i].Row, coords[i].Col);
            }

            return exported;
        }

        private static CoordExport? ExportNullableCoord(TileCoord? coord)
        {
            return coord.HasValue ? new CoordExport(coord.Value.Row, coord.Value.Col) : null;
        }
    }

    internal sealed record DebugBugReportExport(
        string LevelId,
        int Seed,
        string ExportedAtUtc,
        GameStateExport State);

    internal sealed record GameStateExport(
        BoardExport Board,
        DockExport Dock,
        WaterExport Water,
        VineExport Vine,
        TargetStateExport[] Targets,
        LevelConfigExport LevelConfig,
        RngExport RngState,
        int ActionCount,
        bool DockJamUsed,
        bool UndoAvailable,
        string[] ExtractedTargetOrder,
        bool Frozen,
        int ConsecutiveEmergencySpawns,
        int SpawnRecoveryCounter,
        bool DockJamEnabled,
        bool DockJamActive,
        SpawnOverrideExport? DebugSpawnOverride);

    internal sealed record BoardExport(int Width, int Height, TileExport[][] Tiles);

    internal sealed record DockExport(string?[] Slots, int Size);

    internal sealed record WaterExport(int FloodedRows, int ActionsUntilRise, int RiseInterval, bool PauseUntilFirstAction);

    internal sealed record VineExport(
        int ActionsSinceLastClear,
        int GrowthThreshold,
        CoordExport[] GrowthPriorityList,
        int PriorityCursor,
        CoordExport? PendingGrowthTile);

    internal sealed record TargetStateExport(
        string TargetId,
        int Row,
        int Col,
        string Readiness,
        bool Extracted,
        bool OneClearAway);

    internal sealed record LevelConfigExport(
        string[] DebrisTypePool,
        Dictionary<string, double>? BaseDistribution,
        double AssistanceChance,
        int ConsecutiveEmergencyCap,
        bool IsRuleTeach,
        string WaterContactMode);

    internal sealed record RngExport(uint S0, uint S1);

    internal sealed record SpawnOverrideExport(bool? ForceEmergency, double? OverrideAssistanceChance);

    internal sealed record CoordExport(int Row, int Col);

    internal sealed record TileExport(
        string Kind,
        string? DebrisType,
        string? BlockerType,
        int? Hp,
        string? TargetId,
        bool? Extracted,
        string? HiddenDebrisType);
}
#endif
