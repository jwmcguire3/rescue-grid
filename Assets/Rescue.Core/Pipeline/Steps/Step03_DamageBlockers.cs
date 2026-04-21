using System.Collections.Immutable;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step03_DamageBlockers
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            if (!context.IsValidInput || context.ValidatedGroupCoords.IsDefaultOrEmpty)
            {
                return new StepResult(state, context, ImmutableArray<ActionEvent>.Empty);
            }

            ImmutableArray<TileCoord> adjacentBlockers = GroupOps.FindAdjacentBlockers(state.Board, context.ValidatedGroupCoords);
            if (adjacentBlockers.IsDefaultOrEmpty)
            {
                return new StepResult(
                    state,
                    context with { AdjacentBlockersHit = ImmutableArray<TileCoord>.Empty },
                    ImmutableArray<ActionEvent>.Empty);
            }

            Board updatedBoard = state.Board;
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>(adjacentBlockers.Length);

            for (int i = 0; i < adjacentBlockers.Length; i++)
            {
                TileCoord coord = adjacentBlockers[i];
                if (BoardHelpers.GetTile(updatedBoard, coord) is not BlockerTile blocker)
                {
                    continue;
                }

                BlockerTile damagedBlocker = blocker with { Hp = blocker.Hp - 1 };
                updatedBoard = BoardHelpers.SetTile(updatedBoard, coord, damagedBlocker);
                events.Add(new BlockerDamaged(coord, blocker.Type, damagedBlocker.Hp));
            }

            GameState updatedState = state with { Board = updatedBoard };
            StepContext updatedContext = context with { AdjacentBlockersHit = adjacentBlockers };
            return new StepResult(updatedState, updatedContext, events.ToImmutable());
        }
    }
}
