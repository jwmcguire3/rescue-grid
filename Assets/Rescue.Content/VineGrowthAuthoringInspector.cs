using System;
using System.Collections.Immutable;
using Rescue.Core.Rng;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.Content
{
    public static class VineGrowthAuthoringInspector
    {
        public static VineGrowthAuthoringInfo Inspect(LevelJson level)
        {
            if (level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            int vineCount = CountVines(level);
            bool staticGrowthDisabled = level.Vine.GrowthThreshold >= 999;
            bool authoredPriorityPresent = level.Vine.GrowthPriority.Length > 0;
            bool authoredFallbackPossible = HasInitiallyGrowableAuthoredPriority(level);
            VineGrowthPlan? plan = null;

            if (!staticGrowthDisabled)
            {
                GameState state = BuildPlannerState(level);
                plan = VineGrowthPlanner.Plan(state);
            }

            return new VineGrowthAuthoringInfo(
                VineCount: vineCount,
                GrowthThreshold: level.Vine.GrowthThreshold,
                AuthoredPriorityPresent: authoredPriorityPresent,
                AuthoredFallbackPossible: authoredFallbackPossible,
                StaticGrowthDisabled: staticGrowthDisabled,
                SystemicPlanAvailable: plan is not null && !plan.UsedAuthoredFallback,
                AuthoredFallbackUsed: plan?.UsedAuthoredFallback ?? false,
                ValidGrowthPlanAvailable: plan is not null,
                PlannedTile: plan?.NextGrowthTile,
                SourceTile: plan?.SourceTile,
                GoalTile: plan?.GoalTile);
        }

        private static GameState BuildPlannerState(LevelJson level)
        {
            Board board = BuildBoard(level);
            board = ApplyInitialFlood(board, level.InitialFloodedRows);
            ImmutableArray<TargetState> targets = InitializeTargetStates(board, BuildTargets(level));
            return new GameState(
                Board: board,
                Dock: new Dock(ImmutableArray.Create<DebrisType?>(null, null, null, null, null, null, null), Size: 7),
                Water: new WaterState(level.InitialFloodedRows, level.Water.RiseInterval, level.Water.RiseInterval),
                Vine: new VineState(
                    ActionsSinceLastClear: 0,
                    GrowthThreshold: level.Vine.GrowthThreshold,
                    GrowthPriorityList: BuildGrowthPriority(level),
                    PriorityCursor: 0,
                    PendingGrowthTile: null),
                Targets: targets,
                LevelConfig: new LevelConfig(
                    level.DebrisTypePool.ToImmutableArray(),
                    BaseDistribution: null,
                    AssistanceChance: level.Assistance.Chance,
                    ConsecutiveEmergencyCap: level.Assistance.ConsecutiveEmergencyCap),
                RngState: new RngState(1u, 2u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }

        private static Board BuildBoard(LevelJson level)
        {
            ImmutableArray<ImmutableArray<Tile>>.Builder rows = ImmutableArray.CreateBuilder<ImmutableArray<Tile>>(level.Board.Height);
            for (int row = 0; row < level.Board.Height; row++)
            {
                ImmutableArray<Tile>.Builder tiles = ImmutableArray.CreateBuilder<Tile>(level.Board.Width);
                for (int col = 0; col < level.Board.Width; col++)
                {
                    tiles.Add(ParseTile(level.Board.Tiles[row][col]));
                }

                rows.Add(tiles.ToImmutable());
            }

            return new Board(level.Board.Width, level.Board.Height, rows.ToImmutable());
        }

        private static Tile ParseTile(string code)
        {
            if (!ContentTileParser.TryParseCell(code, out ContentCellInfo cell))
            {
                return new EmptyTile();
            }

            return cell.Kind switch
            {
                ContentCellKind.Empty => new EmptyTile(),
                ContentCellKind.Debris => new DebrisTile(cell.DebrisType!.Value),
                ContentCellKind.Crate => new BlockerTile(BlockerType.Crate, cell.Hp, Hidden: null),
                ContentCellKind.Ice => new BlockerTile(BlockerType.Ice, cell.Hp, new DebrisTile(cell.HiddenDebrisType!.Value)),
                ContentCellKind.Vine => new BlockerTile(BlockerType.Vine, cell.Hp, Hidden: null),
                ContentCellKind.Target => new TargetTile(cell.TargetId ?? string.Empty, Extracted: false),
                _ => new EmptyTile(),
            };
        }

        private static Board ApplyInitialFlood(Board board, int floodedRows)
        {
            Board floodedBoard = board;
            for (int row = board.Height - floodedRows; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    floodedBoard = BoardHelpers.SetTile(floodedBoard, new TileCoord(row, col), new FloodedTile());
                }
            }

            return floodedBoard;
        }

        private static ImmutableArray<TargetState> BuildTargets(LevelJson level)
        {
            ImmutableArray<TargetState>.Builder targets = ImmutableArray.CreateBuilder<TargetState>(level.Targets.Length);
            for (int i = 0; i < level.Targets.Length; i++)
            {
                TargetJson target = level.Targets[i];
                targets.Add(new TargetState(target.Id, new TileCoord(target.Row, target.Col), TargetReadiness.Trapped));
            }

            return targets.ToImmutable();
        }

        private static ImmutableArray<TargetState> InitializeTargetStates(Board board, ImmutableArray<TargetState> targets)
        {
            ImmutableArray<TargetState>.Builder initialized = ImmutableArray.CreateBuilder<TargetState>(targets.Length);
            for (int i = 0; i < targets.Length; i++)
            {
                TargetState target = targets[i];
                int blockedNeighbors = CountBlockedRequiredNeighbors(board, target.Coord);
                int requiredNeighbors = BoardHelpers.OrthogonalNeighbors(board, target.Coord).Length;
                int openNeighbors = requiredNeighbors - blockedNeighbors;
                TargetReadiness readiness = blockedNeighbors == 1
                    ? TargetReadiness.OneClearAway
                    : openNeighbors * 2 >= requiredNeighbors
                        ? TargetReadiness.Progressing
                        : TargetReadiness.Trapped;
                initialized.Add(target with { Readiness = readiness });
            }

            return initialized.ToImmutable();
        }

        private static int CountBlockedRequiredNeighbors(Board board, TileCoord targetCoord)
        {
            int blocked = 0;
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, targetCoord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (BoardHelpers.GetTile(board, neighbors[i]) is not EmptyTile)
                {
                    blocked++;
                }
            }

            return blocked;
        }

        private static ImmutableArray<TileCoord> BuildGrowthPriority(LevelJson level)
        {
            ImmutableArray<TileCoord>.Builder coords = ImmutableArray.CreateBuilder<TileCoord>(level.Vine.GrowthPriority.Length);
            for (int i = 0; i < level.Vine.GrowthPriority.Length; i++)
            {
                TileCoordJson coord = level.Vine.GrowthPriority[i];
                coords.Add(new TileCoord(coord.Row, coord.Col));
            }

            return coords.ToImmutable();
        }

        private static int CountVines(LevelJson level)
        {
            int count = 0;
            for (int row = 0; row < level.Board.Height; row++)
            {
                for (int col = 0; col < level.Board.Width; col++)
                {
                    if (string.Equals(level.Board.Tiles[row][col], "V", StringComparison.Ordinal)
                        && !ContentTileParser.IsFloodedRow(level, row))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool HasInitiallyGrowableAuthoredPriority(LevelJson level)
        {
            for (int i = 0; i < level.Vine.GrowthPriority.Length; i++)
            {
                TileCoordJson coord = level.Vine.GrowthPriority[i];
                if (!ContentTileParser.IsInBounds(level.Board.Height, level.Board.Width, coord.Row, coord.Col)
                    || ContentTileParser.IsFloodedRow(level, coord.Row))
                {
                    continue;
                }

                if (IsInitiallyGrowableTile(level.Board.Tiles[coord.Row][coord.Col]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInitiallyGrowableTile(string tileCode)
        {
            return string.Equals(tileCode, ".", StringComparison.Ordinal)
                || ContentTileParser.TryParseDebris(tileCode, out _);
        }
    }

    public sealed record VineGrowthAuthoringInfo(
        int VineCount,
        int GrowthThreshold,
        bool AuthoredPriorityPresent,
        bool AuthoredFallbackPossible,
        bool StaticGrowthDisabled,
        bool SystemicPlanAvailable,
        bool AuthoredFallbackUsed,
        bool ValidGrowthPlanAvailable,
        TileCoord? PlannedTile,
        TileCoord? SourceTile,
        TileCoord? GoalTile)
    {
        public bool ActiveGrowthConfigured => GrowthThreshold < 999;
    }
}
