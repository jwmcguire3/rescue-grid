using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Rules
{
    public static class GroupOps
    {
        public static ImmutableArray<TileCoord>? FindGroup(Board board, TileCoord coord)
        {
            if (!IsExposed(board, coord) || BoardHelpers.GetTile(board, coord) is not DebrisTile start)
            {
                return null;
            }

            Stack<TileCoord> frontier = new Stack<TileCoord>();
            HashSet<TileCoord> visited = new HashSet<TileCoord>();
            ImmutableArray<TileCoord>.Builder group = ImmutableArray.CreateBuilder<TileCoord>();

            frontier.Push(coord);
            visited.Add(coord);

            while (frontier.Count > 0)
            {
                TileCoord current = frontier.Pop();
                group.Add(current);

                ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, current);
                for (int i = 0; i < neighbors.Length; i++)
                {
                    TileCoord neighbor = neighbors[i];
                    if (visited.Contains(neighbor) || !IsExposed(board, neighbor))
                    {
                        continue;
                    }

                    if (BoardHelpers.GetTile(board, neighbor) is DebrisTile debris && debris.Type == start.Type)
                    {
                        visited.Add(neighbor);
                        frontier.Push(neighbor);
                    }
                }
            }

            return group.Count >= 2 ? group.ToImmutable() : null;
        }

        public static bool HasValidGroup(Board board, WaterState water)
        {
            for (int row = 0; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (!IsDry(board, water, coord) || BoardHelpers.GetTile(board, coord) is not DebrisTile start)
                    {
                        continue;
                    }

                    if (HasValidGroupFrom(board, water, coord, start.Type))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasValidGroupFrom(Board board, WaterState water, TileCoord coord, DebrisType type)
        {
            Stack<TileCoord> frontier = new Stack<TileCoord>();
            HashSet<TileCoord> visited = new HashSet<TileCoord>();
            int groupSize = 0;

            frontier.Push(coord);
            visited.Add(coord);

            while (frontier.Count > 0)
            {
                TileCoord current = frontier.Pop();
                groupSize++;
                if (groupSize >= 2)
                {
                    return true;
                }

                ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, current);
                for (int i = 0; i < neighbors.Length; i++)
                {
                    TileCoord neighbor = neighbors[i];
                    if (visited.Contains(neighbor) || !IsDry(board, water, neighbor))
                    {
                        continue;
                    }

                    if (BoardHelpers.GetTile(board, neighbor) is DebrisTile debris && debris.Type == type)
                    {
                        visited.Add(neighbor);
                        frontier.Push(neighbor);
                    }
                }
            }

            return false;
        }

        public static ImmutableArray<TileCoord> FindAdjacentBlockers(Board board, ImmutableArray<TileCoord> coords)
        {
            if (coords.IsDefaultOrEmpty)
            {
                return ImmutableArray<TileCoord>.Empty;
            }

            HashSet<TileCoord> seen = new HashSet<TileCoord>();
            ImmutableArray<TileCoord>.Builder adjacentBlockers = ImmutableArray.CreateBuilder<TileCoord>();

            for (int i = 0; i < coords.Length; i++)
            {
                ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, coords[i]);
                for (int j = 0; j < neighbors.Length; j++)
                {
                    TileCoord neighbor = neighbors[j];
                    if (seen.Contains(neighbor))
                    {
                        continue;
                    }

                    if (BoardHelpers.GetTile(board, neighbor) is BlockerTile)
                    {
                        seen.Add(neighbor);
                        adjacentBlockers.Add(neighbor);
                    }
                }
            }

            return adjacentBlockers.ToImmutable();
        }

        public static bool IsExposed(Board board, TileCoord coord)
        {
            if (!BoardHelpers.InBounds(board, coord))
            {
                return false;
            }

            return BoardHelpers.GetTile(board, coord) switch
            {
                DebrisTile => true,
                FloodedTile => false,
                BlockerTile => false,
                TargetTile => false,
                EmptyTile => false,
                _ => false,
            };
        }

        private static bool IsDry(Board board, WaterState water, TileCoord coord)
        {
            int floodStartRow = board.Height - water.FloodedRows;
            return water.FloodedRows <= 0 || coord.Row < floodStartRow;
        }
    }
}
