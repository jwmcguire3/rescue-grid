using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step02_RemoveGroup
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            if (!context.IsValidInput || context.ValidatedGroupType is null || context.ValidatedGroupCoords.IsDefaultOrEmpty)
            {
                return new StepResult(state, context, ImmutableArray<ActionEvent>.Empty);
            }

            Board updatedBoard = state.Board;
            ImmutableArray<DebrisType>.Builder removedDebris = ImmutableArray.CreateBuilder<DebrisType>(context.ValidatedGroupCoords.Length);
            for (int i = 0; i < context.ValidatedGroupCoords.Length; i++)
            {
                TileCoord coord = context.ValidatedGroupCoords[i];
                updatedBoard = BoardHelpers.SetTile(updatedBoard, coord, new EmptyTile());
                removedDebris.Add(context.ValidatedGroupType.Value);
            }

            GameState updatedState = state with { Board = updatedBoard };
            StepContext updatedContext = context with
            {
                RemovedDebris = removedDebris.ToImmutable(),
            };

            ImmutableArray<ActionEvent> events = ImmutableArray.Create<ActionEvent>(
                new GroupRemoved(context.ValidatedGroupType.Value, context.ValidatedGroupCoords));
            return new StepResult(updatedState, updatedContext, events);
        }
    }
}
