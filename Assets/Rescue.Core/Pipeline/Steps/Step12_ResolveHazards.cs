using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step12_ResolveHazards
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            // TODO(B12): implement per Phase 1 spec sections 1.4, 1.6, and 1.7.
            return new StepResult(state, context, ImmutableArray<ActionEvent>.Empty);
        }
    }
}
