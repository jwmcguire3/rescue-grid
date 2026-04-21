using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step10_CheckWin
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            // TODO(B10): expand win-specific bookkeeping per Phase 1 spec section 1.4.
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

            StepContext updatedContext = context with { IsWin = true };
            ImmutableArray<ActionEvent> events = ImmutableArray.Create<ActionEvent>(
                new Won(state.ExtractedTargetOrder));
            return new StepResult(state, updatedContext, events);
        }
    }
}
