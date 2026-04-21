using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step01_AcceptInput
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            TileCoord tapped = context.Input.TappedCoord;
            if (state.Frozen || !BoardHelpers.InBounds(state.Board, tapped))
            {
                return Invalid(state, context);
            }

            if (IsFlooded(state.Board, state.Water, tapped))
            {
                return Invalid(state, context);
            }

            if (BoardHelpers.GetTile(state.Board, tapped) is not DebrisTile tappedDebris)
            {
                return Invalid(state, context);
            }

            ImmutableArray<TileCoord> group = FindGroup(state.Board, tapped, tappedDebris.Type, state.Water);
            if (group.Length < 2)
            {
                return Invalid(state, context);
            }

            StepContext updatedContext = context with
            {
                IsValidInput = true,
                ValidatedGroupType = tappedDebris.Type,
                ValidatedGroupCoords = group,
            };

            GameState updatedState = state with { ActionCount = state.ActionCount + 1 };
            return new StepResult(updatedState, updatedContext, ImmutableArray<ActionEvent>.Empty);
        }

        private static StepResult Invalid(GameState state, StepContext context)
        {
            ImmutableArray<ActionEvent> events = ImmutableArray.Create<ActionEvent>(
                new InvalidInput(context.Input.TappedCoord));
            return new StepResult(state, context, events);
        }

        private static ImmutableArray<TileCoord> FindGroup(Board board, TileCoord start, DebrisType type, WaterState water)
        {
            Queue<TileCoord> frontier = new Queue<TileCoord>();
            HashSet<TileCoord> visited = new HashSet<TileCoord>();
            ImmutableArray<TileCoord>.Builder coords = ImmutableArray.CreateBuilder<TileCoord>();

            frontier.Enqueue(start);
            visited.Add(start);

            while (frontier.Count > 0)
            {
                TileCoord current = frontier.Dequeue();
                coords.Add(current);

                ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, current);
                for (int i = 0; i < neighbors.Length; i++)
                {
                    TileCoord neighbor = neighbors[i];
                    if (visited.Contains(neighbor) || IsFlooded(board, water, neighbor))
                    {
                        continue;
                    }

                    if (BoardHelpers.GetTile(board, neighbor) is DebrisTile debris && debris.Type == type)
                    {
                        visited.Add(neighbor);
                        frontier.Enqueue(neighbor);
                    }
                }
            }

            return coords.ToImmutable();
        }

        private static bool IsFlooded(Board board, WaterState water, TileCoord coord)
        {
            int floodStartRow = board.Height - water.FloodedRows;
            return water.FloodedRows > 0 && coord.Row >= floodStartRow;
        }
    }
}
