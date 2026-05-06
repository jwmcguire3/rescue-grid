using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Rules
{
    public sealed record VineGrowthPlan(
        TileCoord? SourceTile,
        TileCoord GoalTile,
        TileCoord NextGrowthTile,
        int Score,
        string Reason,
        bool UsedAuthoredFallback = false);

    public static class VineGrowthPlanner
    {
        public static VineGrowthPlan? Plan(GameState state)
        {
            return Plan(
                state.Board,
                state.Vine,
                state.Targets,
                state.Water);
        }

        public static VineGrowthPlan? Plan(
            Board board,
            VineState vine,
            ImmutableArray<TargetState> targets,
            WaterState water)
        {
            if (board is null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (vine is null)
            {
                throw new ArgumentNullException(nameof(vine));
            }

            ImmutableArray<GoalCandidate> goals = CreateGoalCandidates(board, targets, water);
            ImmutableArray<TileCoord> sources = FindVineSources(board);
            VineGrowthPlan? bestPlan = null;
            PlanRank bestRank = default;
            bool hasBest = false;

            for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                TileCoord source = sources[sourceIndex];
                for (int goalIndex = 0; goalIndex < goals.Length; goalIndex++)
                {
                    GoalCandidate goal = goals[goalIndex];
                    PathResult? maybePath = FindPath(board, targets, source, goal.Coord);
                    if (!maybePath.HasValue)
                    {
                        continue;
                    }

                    PathResult path = maybePath.Value;
                    int authoredRank = GetAuthoredRank(vine, goal.Coord, path.NextGrowthTile);
                    PlanRank rank = new PlanRank(
                        goal.KindRank,
                        goal.ReadinessRank,
                        goal.WaterMargin,
                        path.Distance,
                        authoredRank,
                        goal.Coord.Row,
                        goal.Coord.Col,
                        path.NextGrowthTile.Row,
                        path.NextGrowthTile.Col,
                        source.Row,
                        source.Col);

                    if (!hasBest || rank.CompareTo(bestRank) < 0)
                    {
                        int score = CalculateScore(rank);
                        bestPlan = new VineGrowthPlan(
                            source,
                            goal.Coord,
                            path.NextGrowthTile,
                            score,
                            goal.Kind == GoalKind.RescuePath ? "RescuePath" : "RequiredNeighbor");
                        bestRank = rank;
                        hasBest = true;
                    }
                }
            }

            return bestPlan ?? CreateAuthoredFallbackPlan(board, vine, targets);
        }

        private static ImmutableArray<GoalCandidate> CreateGoalCandidates(
            Board board,
            ImmutableArray<TargetState> targets,
            WaterState water)
        {
            Dictionary<TileCoord, GoalCandidate> byCoord = new Dictionary<TileCoord, GoalCandidate>();
            int nextFloodRow = board.Height - water.FloodedRows - 1;
            if (nextFloodRow < 0)
            {
                nextFloodRow = 0;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                TargetState target = targets[i];
                if (target.Extracted || target.ExtractableLatched)
                {
                    continue;
                }

                int readinessRank = GetReadinessRank(target.Readiness);
                int waterMargin = nextFloodRow - target.Coord.Row;
                if (waterMargin < 0)
                {
                    waterMargin = 0;
                }

                AddRescuePathGoals(board, targets, target, readinessRank, waterMargin, byCoord);
                AddRequiredNeighborGoals(board, targets, target, readinessRank, waterMargin, byCoord);
            }

            ImmutableArray<GoalCandidate>.Builder result = ImmutableArray.CreateBuilder<GoalCandidate>(byCoord.Count);
            foreach (GoalCandidate candidate in byCoord.Values)
            {
                result.Add(candidate);
            }

            result.Sort(static (a, b) => a.CompareTo(b));
            return result.ToImmutable();
        }

        private static void AddRescuePathGoals(
            Board board,
            ImmutableArray<TargetState> targets,
            TargetState target,
            int readinessRank,
            int waterMargin,
            Dictionary<TileCoord, GoalCandidate> byCoord)
        {
            for (int row = 0; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (BoardHelpers.GetTile(board, coord) is RescuePathTile rescuePath
                        && ContainsTargetId(rescuePath.TargetIds, target.TargetId)
                        && IsValidGrowthTile(board, targets, coord))
                    {
                        AddBetterCandidate(
                            byCoord,
                            new GoalCandidate(
                                coord,
                                GoalKind.RescuePath,
                                KindRank: 0,
                                readinessRank,
                                waterMargin));
                    }
                }
            }
        }

        private static void AddRequiredNeighborGoals(
            Board board,
            ImmutableArray<TargetState> targets,
            TargetState target,
            int readinessRank,
            int waterMargin,
            Dictionary<TileCoord, GoalCandidate> byCoord)
        {
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, target.Coord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                TileCoord coord = neighbors[i];
                if (!IsValidGrowthTile(board, targets, coord))
                {
                    continue;
                }

                AddBetterCandidate(
                    byCoord,
                    new GoalCandidate(
                        coord,
                        GoalKind.RequiredNeighbor,
                        KindRank: 1,
                        readinessRank,
                        waterMargin));
            }
        }

        private static void AddBetterCandidate(
            Dictionary<TileCoord, GoalCandidate> byCoord,
            GoalCandidate candidate)
        {
            if (!byCoord.TryGetValue(candidate.Coord, out GoalCandidate existing)
                || candidate.CompareTo(existing) < 0)
            {
                byCoord[candidate.Coord] = candidate;
            }
        }

        private static ImmutableArray<TileCoord> FindVineSources(Board board)
        {
            ImmutableArray<TileCoord>.Builder sources = ImmutableArray.CreateBuilder<TileCoord>();
            for (int row = 0; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (BoardHelpers.GetTile(board, coord) is BlockerTile { Type: BlockerType.Vine })
                    {
                        sources.Add(coord);
                    }
                }
            }

            return sources.ToImmutable();
        }

        private static PathResult? FindPath(
            Board board,
            ImmutableArray<TargetState> targets,
            TileCoord source,
            TileCoord goal)
        {
            Queue<PathNode> queue = new Queue<PathNode>();
            HashSet<TileCoord> visited = new HashSet<TileCoord> { source };
            ImmutableArray<TileCoord> sourceNeighbors = BoardHelpers.OrthogonalNeighbors(board, source);

            for (int i = 0; i < sourceNeighbors.Length; i++)
            {
                TileCoord next = sourceNeighbors[i];
                if (!IsValidGrowthTile(board, targets, next) || !visited.Add(next))
                {
                    continue;
                }

                queue.Enqueue(new PathNode(next, next, Distance: 1));
            }

            while (queue.Count > 0)
            {
                PathNode current = queue.Dequeue();
                if (current.Coord == goal)
                {
                    return new PathResult(current.FirstStep, current.Distance);
                }

                ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, current.Coord);
                for (int i = 0; i < neighbors.Length; i++)
                {
                    TileCoord next = neighbors[i];
                    if (!IsValidGrowthTile(board, targets, next) || !visited.Add(next))
                    {
                        continue;
                    }

                    queue.Enqueue(new PathNode(next, current.FirstStep, current.Distance + 1));
                }
            }

            return null;
        }

        private static VineGrowthPlan? CreateAuthoredFallbackPlan(
            Board board,
            VineState vine,
            ImmutableArray<TargetState> targets)
        {
            int startIndex = vine.PriorityCursor < 0 ? 0 : vine.PriorityCursor;
            for (int i = startIndex; i < vine.GrowthPriorityList.Length; i++)
            {
                TileCoord coord = vine.GrowthPriorityList[i];
                if (IsValidGrowthTile(board, targets, coord))
                {
                    return new VineGrowthPlan(
                        SourceTile: null,
                        GoalTile: coord,
                        NextGrowthTile: coord,
                        Score: 0,
                        Reason: "AuthoredFallback",
                        UsedAuthoredFallback: true);
                }
            }

            return null;
        }

        private static bool IsValidGrowthTile(
            Board board,
            ImmutableArray<TargetState> targets,
            TileCoord coord)
        {
            if (!BoardHelpers.InBounds(board, coord))
            {
                return false;
            }

            Tile tile = BoardHelpers.GetTile(board, coord);
            return tile is EmptyTile
                || tile is DebrisTile
                || tile is RescuePathTile rescuePath && IsUnlatchedRescuePath(rescuePath, targets);
        }

        private static bool IsUnlatchedRescuePath(
            RescuePathTile rescuePath,
            ImmutableArray<TargetState> targets)
        {
            for (int i = 0; i < rescuePath.TargetIds.Length; i++)
            {
                string targetId = rescuePath.TargetIds[i];
                for (int j = 0; j < targets.Length; j++)
                {
                    TargetState target = targets[j];
                    if (target.TargetId == targetId && (target.Extracted || target.ExtractableLatched))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static int GetAuthoredRank(VineState vine, TileCoord goal, TileCoord nextGrowthTile)
        {
            int startIndex = vine.PriorityCursor < 0 ? 0 : vine.PriorityCursor;
            int best = int.MaxValue;
            for (int i = startIndex; i < vine.GrowthPriorityList.Length; i++)
            {
                TileCoord coord = vine.GrowthPriorityList[i];
                if ((coord == goal || coord == nextGrowthTile) && i < best)
                {
                    best = i;
                }
            }

            return best;
        }

        private static int GetReadinessRank(TargetReadiness readiness)
        {
            return readiness switch
            {
                TargetReadiness.OneClearAway => 0,
                TargetReadiness.Progressing => 1,
                TargetReadiness.Distressed => 1,
                _ => 2,
            };
        }

        private static bool ContainsTargetId(ImmutableArray<string> targetIds, string targetId)
        {
            for (int i = 0; i < targetIds.Length; i++)
            {
                if (targetIds[i] == targetId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CalculateScore(PlanRank rank)
        {
            int authoredBonus = rank.AuthoredRank == int.MaxValue ? 0 : 10;
            return 1000
                - (rank.KindRank * 100)
                - (rank.ReadinessRank * 40)
                - (rank.WaterMargin * 5)
                - rank.PathLength
                + authoredBonus;
        }

        private enum GoalKind
        {
            RescuePath,
            RequiredNeighbor,
        }

        private readonly record struct GoalCandidate(
            TileCoord Coord,
            GoalKind Kind,
            int KindRank,
            int ReadinessRank,
            int WaterMargin) : IComparable<GoalCandidate>
        {
            public int CompareTo(GoalCandidate other)
            {
                int comparison = KindRank.CompareTo(other.KindRank);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = ReadinessRank.CompareTo(other.ReadinessRank);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = WaterMargin.CompareTo(other.WaterMargin);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = Coord.Row.CompareTo(other.Coord.Row);
                return comparison != 0 ? comparison : Coord.Col.CompareTo(other.Coord.Col);
            }
        }

        private readonly record struct PathNode(TileCoord Coord, TileCoord FirstStep, int Distance);

        private readonly record struct PathResult(TileCoord NextGrowthTile, int Distance);

        private readonly record struct PlanRank(
            int KindRank,
            int ReadinessRank,
            int WaterMargin,
            int PathLength,
            int AuthoredRank,
            int GoalRow,
            int GoalCol,
            int NextRow,
            int NextCol,
            int SourceRow,
            int SourceCol) : IComparable<PlanRank>
        {
            public int CompareTo(PlanRank other)
            {
                int comparison = KindRank.CompareTo(other.KindRank);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = ReadinessRank.CompareTo(other.ReadinessRank);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = WaterMargin.CompareTo(other.WaterMargin);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = PathLength.CompareTo(other.PathLength);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = AuthoredRank.CompareTo(other.AuthoredRank);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = GoalRow.CompareTo(other.GoalRow);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = GoalCol.CompareTo(other.GoalCol);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = NextRow.CompareTo(other.NextRow);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = NextCol.CompareTo(other.NextCol);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = SourceRow.CompareTo(other.SourceRow);
                return comparison != 0 ? comparison : SourceCol.CompareTo(other.SourceCol);
            }
        }
    }
}
