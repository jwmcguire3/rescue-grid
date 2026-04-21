using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step10_CheckWin
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            bool isWin = true;
            for (int i = 0; i < state.Targets.Length; i++)
            {
                if (!state.Targets[i].Extracted)
                {
                    isWin = false;
                    break;
                }
            }

            if (!isWin)
            {
                return new StepResult(state, context, ImmutableArray<ActionEvent>.Empty);
            }

            GameState updatedState = state with { Frozen = true };
            StepContext updatedContext = context with { IsWin = true };
            string? finalExtractedTargetId = null;
            if (!context.ExtractedTargetIdsThisAction.IsDefaultOrEmpty)
            {
                finalExtractedTargetId = context.ExtractedTargetIdsThisAction[^1];
            }
            else if (!updatedState.ExtractedTargetOrder.IsDefaultOrEmpty)
            {
                finalExtractedTargetId = updatedState.ExtractedTargetOrder[^1];
            }

            ImmutableArray<ActionEvent> events = ImmutableArray.Create<ActionEvent>(
                new Won(finalExtractedTargetId, updatedState.ActionCount + 1, updatedState.ExtractedTargetOrder));
            return new StepResult(updatedState, updatedContext, events);
        }
    }
}
