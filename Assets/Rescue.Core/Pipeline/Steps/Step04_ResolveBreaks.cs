using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step04_ResolveBreaks
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            // TODO(B4): implement per Phase 1 spec section 1.4.
            return new StepResult(state, context, ImmutableArray<ActionEvent>.Empty);
        }
    }
}
