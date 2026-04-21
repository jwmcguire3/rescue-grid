using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step08_Spawn
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            // TODO(B8): implement per Phase 1 spec section 1.4.
            return new StepResult(state, context, ImmutableArray<ActionEvent>.Empty);
        }
    }
}
