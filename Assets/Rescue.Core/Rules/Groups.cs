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
    }
}
