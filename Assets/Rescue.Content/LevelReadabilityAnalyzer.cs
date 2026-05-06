using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Content
{
    public static class LevelReadabilityAnalyzer
    {
        public static LevelReadabilityMetrics Analyze(LevelJson level)
        {
            if (level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            ContentCellInfo[,] cells = BuildCells(level);
            int totalCells = level.Board.Width * level.Board.Height;
            int occupiedVisualCells = 0;
            int floodedVisualCells = 0;
            ImmutableDictionary<BlockerType, int>.Builder blockerCounts = ImmutableDictionary.CreateBuilder<BlockerType, int>();
            ImmutableDictionary<DebrisType, int>.Builder debrisCounts = ImmutableDictionary.CreateBuilder<DebrisType, int>();

            for (int row = 0; row < level.Board.Height; row++)
            {
                for (int col = 0; col < level.Board.Width; col++)
                {
                    ContentCellInfo cell = cells[row, col];
                    bool flooded = ContentTileParser.IsFloodedRow(level, row);
                    if (flooded)
                    {
                        floodedVisualCells++;
                    }

                    if (flooded || IsVisuallyOccupied(cell))
                    {
                        occupiedVisualCells++;
                    }

                    if (cell.Kind == ContentCellKind.Debris)
                    {
                        Increment(debrisCounts, cell.DebrisType!.Value);
                    }
                    else if (TryGetBlockerType(cell, out BlockerType blockerType))
                    {
                        Increment(blockerCounts, blockerType);
                    }
                }
            }

            HashSet<ContentCellCoord> routeZones = BuildRouteZones(level);
            GroupSummary groupSummary = AnalyzeGroups(level, cells, routeZones);
            TargetReadinessSummary readinessSummary = AnalyzeTargetReadiness(level, cells);

            return new LevelReadabilityMetrics(
                TotalCells: totalCells,
                OccupiedVisualCells: occupiedVisualCells,
                EmptyCells: totalCells - occupiedVisualCells,
                FloodedVisualCells: floodedVisualCells,
                TargetCount: level.Targets.Length,
                BlockerCountByType: blockerCounts.ToImmutable(),
                DebrisCountByType: debrisCounts.ToImmutable(),
                VisualOccupancyRatio: totalCells == 0 ? 0.0d : occupiedVisualCells / (double)totalCells,
                LegalStartingGroupCount: groupSummary.LegalStartingGroups,
                RouteAffectingStartingGroupCount: groupSummary.RouteAffectingStartingGroups,
                TrappedTargetCount: readinessSummary.Trapped,
                ProgressingTargetCount: readinessSummary.Progressing,
                OneClearAwayTargetCount: readinessSummary.OneClearAway,
                ImmediateExactTripleCount: groupSummary.ExactTriples,
                OversizedGroupCount: groupSummary.OversizedGroups,
                SingletonDebrisTileCount: groupSummary.SingletonDebrisTiles);
        }

        private static ContentCellInfo[,] BuildCells(LevelJson level)
        {
            ContentCellInfo[,] cells = new ContentCellInfo[level.Board.Height, level.Board.Width];
            for (int row = 0; row < level.Board.Height; row++)
            {
                for (int col = 0; col < level.Board.Width; col++)
                {
                    if (!ContentTileParser.TryParseCell(level.Board.Tiles[row][col], out ContentCellInfo cell))
                    {
                        throw new InvalidOperationException($"Tile code '{level.Board.Tiles[row][col]}' is not recognized.");
                    }

                    cells[row, col] = cell;
                }
            }

            return cells;
        }

        private static bool IsVisuallyOccupied(ContentCellInfo cell)
        {
            return cell.Kind is ContentCellKind.Debris
                or ContentCellKind.Crate
                or ContentCellKind.Ice
                or ContentCellKind.Vine
                or ContentCellKind.Target;
        }

        private static bool TryGetBlockerType(ContentCellInfo cell, out BlockerType blockerType)
        {
            switch (cell.Kind)
            {
                case ContentCellKind.Crate:
                    blockerType = BlockerType.Crate;
                    return true;
                case ContentCellKind.Ice:
                    blockerType = BlockerType.Ice;
                    return true;
                case ContentCellKind.Vine:
                    blockerType = BlockerType.Vine;
                    return true;
                default:
                    blockerType = default;
                    return false;
            }
        }

        private static HashSet<ContentCellCoord> BuildRouteZones(LevelJson level)
        {
            HashSet<ContentCellCoord> routeZones = new HashSet<ContentCellCoord>();
            for (int i = 0; i < level.Targets.Length; i++)
            {
                TargetJson target = level.Targets[i];
                ImmutableArray<ContentCellCoord> neighbors = ContentTileParser.GetRequiredNeighbors(
                    level.Board.Height,
                    level.Board.Width,
                    new ContentCellCoord(target.Row, target.Col));

                for (int n = 0; n < neighbors.Length; n++)
                {
                    routeZones.Add(neighbors[n]);
                }
            }

            return routeZones;
        }

        private static GroupSummary AnalyzeGroups(
            LevelJson level,
            ContentCellInfo[,] cells,
            HashSet<ContentCellCoord> routeZones)
        {
            bool[,] visited = new bool[level.Board.Height, level.Board.Width];
            int legalStartingGroups = 0;
            int routeAffectingStartingGroups = 0;
            int exactTriples = 0;
            int oversizedGroups = 0;
            int singletonDebrisTiles = 0;

            for (int row = 0; row < level.Board.Height; row++)
            {
                for (int col = 0; col < level.Board.Width; col++)
                {
                    if (visited[row, col]
                        || ContentTileParser.IsFloodedRow(level, row)
                        || cells[row, col].Kind != ContentCellKind.Debris)
                    {
                        continue;
                    }

                    ImmutableArray<ContentCellCoord> group = FindDebrisGroup(level, cells, visited, new ContentCellCoord(row, col));
                    if (group.Length == 1)
                    {
                        singletonDebrisTiles++;
                        continue;
                    }

                    legalStartingGroups++;
                    if (group.Length == 3)
                    {
                        exactTriples++;
                    }

                    if (group.Length > 5)
                    {
                        oversizedGroups++;
                    }

                    if (IsRouteAffectingGroup(level, cells, routeZones, group))
                    {
                        routeAffectingStartingGroups++;
                    }
                }
            }

            return new GroupSummary(
                legalStartingGroups,
                routeAffectingStartingGroups,
                exactTriples,
                oversizedGroups,
                singletonDebrisTiles);
        }

        private static ImmutableArray<ContentCellCoord> FindDebrisGroup(
            LevelJson level,
            ContentCellInfo[,] cells,
            bool[,] visited,
            ContentCellCoord start)
        {
            DebrisType debrisType = cells[start.Row, start.Col].DebrisType!.Value;
            Queue<ContentCellCoord> frontier = new Queue<ContentCellCoord>();
            ImmutableArray<ContentCellCoord>.Builder group = ImmutableArray.CreateBuilder<ContentCellCoord>();
            frontier.Enqueue(start);
            visited[start.Row, start.Col] = true;

            while (frontier.Count > 0)
            {
                ContentCellCoord current = frontier.Dequeue();
                group.Add(current);

                ImmutableArray<ContentCellCoord> neighbors = ContentTileParser.GetRequiredNeighbors(
                    level.Board.Height,
                    level.Board.Width,
                    current);
                for (int i = 0; i < neighbors.Length; i++)
                {
                    ContentCellCoord neighbor = neighbors[i];
                    if (visited[neighbor.Row, neighbor.Col]
                        || ContentTileParser.IsFloodedRow(level, neighbor.Row)
                        || cells[neighbor.Row, neighbor.Col].Kind != ContentCellKind.Debris
                        || cells[neighbor.Row, neighbor.Col].DebrisType != debrisType)
                    {
                        continue;
                    }

                    visited[neighbor.Row, neighbor.Col] = true;
                    frontier.Enqueue(neighbor);
                }
            }

            return group.ToImmutable();
        }

        private static bool IsRouteAffectingGroup(
            LevelJson level,
            ContentCellInfo[,] cells,
            HashSet<ContentCellCoord> routeZones,
            ImmutableArray<ContentCellCoord> group)
        {
            for (int i = 0; i < group.Length; i++)
            {
                ContentCellCoord coord = group[i];
                if (routeZones.Contains(coord))
                {
                    return true;
                }

                ImmutableArray<ContentCellCoord> neighbors = ContentTileParser.GetRequiredNeighbors(
                    level.Board.Height,
                    level.Board.Width,
                    coord);
                for (int n = 0; n < neighbors.Length; n++)
                {
                    ContentCellCoord neighbor = neighbors[n];
                    if (routeZones.Contains(neighbor))
                    {
                        return true;
                    }

                    ContentCellInfo neighborCell = cells[neighbor.Row, neighbor.Col];
                    if (routeZones.Contains(neighbor) && ContentTileParser.IsBlocker(neighborCell))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static TargetReadinessSummary AnalyzeTargetReadiness(LevelJson level, ContentCellInfo[,] cells)
        {
            int trapped = 0;
            int progressing = 0;
            int oneClearAway = 0;

            for (int i = 0; i < level.Targets.Length; i++)
            {
                TargetJson target = level.Targets[i];
                ImmutableArray<ContentCellCoord> neighbors = ContentTileParser.GetRequiredNeighbors(
                    level.Board.Height,
                    level.Board.Width,
                    new ContentCellCoord(target.Row, target.Col));

                int openNeighbors = 0;
                for (int n = 0; n < neighbors.Length; n++)
                {
                    ContentCellCoord neighbor = neighbors[n];
                    if (!ContentTileParser.IsFloodedRow(level, neighbor.Row)
                        && cells[neighbor.Row, neighbor.Col].Kind == ContentCellKind.Empty)
                    {
                        openNeighbors++;
                    }
                }

                int blockedNeighbors = neighbors.Length - openNeighbors;
                if (blockedNeighbors == 1)
                {
                    oneClearAway++;
                }
                else if (blockedNeighbors > 1 && openNeighbors * 2 >= neighbors.Length)
                {
                    progressing++;
                }
                else if (blockedNeighbors > 0)
                {
                    trapped++;
                }
            }

            return new TargetReadinessSummary(trapped, progressing, oneClearAway);
        }

        private static void Increment<T>(ImmutableDictionary<T, int>.Builder counts, T key)
            where T : notnull
        {
            counts.TryGetValue(key, out int current);
            counts[key] = current + 1;
        }

        private readonly record struct GroupSummary(
            int LegalStartingGroups,
            int RouteAffectingStartingGroups,
            int ExactTriples,
            int OversizedGroups,
            int SingletonDebrisTiles);

        private readonly record struct TargetReadinessSummary(
            int Trapped,
            int Progressing,
            int OneClearAway);
    }

    public sealed record LevelReadabilityMetrics(
        int TotalCells,
        int OccupiedVisualCells,
        int EmptyCells,
        int FloodedVisualCells,
        int TargetCount,
        ImmutableDictionary<BlockerType, int> BlockerCountByType,
        ImmutableDictionary<DebrisType, int> DebrisCountByType,
        double VisualOccupancyRatio,
        int LegalStartingGroupCount,
        int RouteAffectingStartingGroupCount,
        int TrappedTargetCount,
        int ProgressingTargetCount,
        int OneClearAwayTargetCount,
        int ImmediateExactTripleCount,
        int OversizedGroupCount,
        int SingletonDebrisTileCount);
}
