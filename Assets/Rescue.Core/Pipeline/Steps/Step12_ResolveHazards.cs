using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step12_ResolveHazards
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            if (!context.WaterRisePending || state.Water.FloodedRows >= state.Board.Height)
            {
                return new StepResult(state, context, ImmutableArray<ActionEvent>.Empty);
            }

            int floodedRow = state.Board.Height - state.Water.FloodedRows - 1;
            Board updatedBoard = state.Board;
            for (int col = 0; col < updatedBoard.Width; col++)
            {
                updatedBoard = BoardHelpers.SetTile(updatedBoard, new TileCoord(floodedRow, col), new FloodedTile());
            }

            GameState updatedState = state with
            {
                Board = updatedBoard,
                Water = state.Water with
                {
                    FloodedRows = state.Water.FloodedRows + 1,
                    ActionsUntilRise = state.Water.RiseInterval,
                },
            };
            StepContext updatedContext = context with
            {
                WaterRisePending = false,
            };
            ImmutableArray<ActionEvent> events = ImmutableArray.Create<ActionEvent>(
                new WaterRose(floodedRow));
            return new StepResult(updatedState, updatedContext, events);
        }
    }
}
