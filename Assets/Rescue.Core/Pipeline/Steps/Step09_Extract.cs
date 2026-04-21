using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step09_Extract
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            // TODO(B9): implement per Phase 1 spec sections 1.4 and 1.8.
            return new StepResult(state, context, ImmutableArray<ActionEvent>.Empty);
        }
    }
}
