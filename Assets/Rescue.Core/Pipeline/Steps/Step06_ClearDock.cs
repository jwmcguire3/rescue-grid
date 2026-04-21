using System.Collections.Immutable;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step06_ClearDock
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            DockClearResult clear = DockClearOps.ClearTriples(state.Dock);
            DockWarningLevel warningAfter = DockHelpers.GetWarningLevel(clear.Dock);
            GameState updatedState = state with { Dock = clear.Dock };
            StepContext updatedContext = context with
            {
                ClearedDockTriplesThisAction = clear.ClearedTriples.Length,
                DockWarningAfter = warningAfter,
            };

            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();
            int occupancyAfterClear = DockHelpers.Occupancy(clear.Dock);
            for (int i = 0; i < clear.ClearedTriples.Length; i++)
            {
                events.Add(new DockCleared(clear.ClearedTriples[i], SetsCleared: 1, occupancyAfterClear));
            }

            if (warningAfter != context.DockWarningBefore)
            {
                events.Add(new DockWarningChanged(context.DockWarningBefore, warningAfter));
            }

            return new StepResult(updatedState, updatedContext, events.ToImmutable());
        }
    }
}
