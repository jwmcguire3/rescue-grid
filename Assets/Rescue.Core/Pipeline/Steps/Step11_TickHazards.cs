using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step11_TickHazards
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            if (state.Water.FloodedRows >= state.Board.Height || state.Water.ActionsUntilRise <= 0)
            {
                return new StepResult(state, context, ImmutableArray<ActionEvent>.Empty);
            }

            int actionsUntilRise = state.Water.ActionsUntilRise - 1;
            bool waterRisePending = actionsUntilRise == 0;
            GameState updatedState = state with
            {
                Water = state.Water with
                {
                    ActionsUntilRise = actionsUntilRise,
                },
            };
            StepContext updatedContext = context with
            {
                WaterRisePending = waterRisePending,
            };

            if (!waterRisePending && actionsUntilRise == 1)
            {
                int nextFloodRow = state.Board.Height - state.Water.FloodedRows - 1;
                ImmutableArray<ActionEvent> warningEvents = ImmutableArray.Create<ActionEvent>(
                    new WaterWarning(actionsUntilRise, nextFloodRow));
                return new StepResult(updatedState, updatedContext, warningEvents);
            }

            return new StepResult(updatedState, updatedContext, ImmutableArray<ActionEvent>.Empty);
        }
    }
}
