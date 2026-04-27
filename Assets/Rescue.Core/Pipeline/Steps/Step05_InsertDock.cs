using System.Collections.Immutable;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step05_InsertDock
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            if (!context.IsValidInput || context.RemovedDebris.IsDefaultOrEmpty)
            {
                return new StepResult(state, context, ImmutableArray<ActionEvent>.Empty);
            }

            DockInsertResult insert = DockInsertOps.Insert(state.Dock, context.RemovedDebris);
            GameState updatedState = state with { Dock = insert.Dock };
            StepContext updatedContext = context;

            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();
            int occupancy = DockHelpers.Occupancy(state.Dock);
            for (int i = 0; i < insert.InsertedPieces.Length; i++)
            {
                occupancy++;
                events.Add(new DockInserted(
                    ImmutableArray.Create(insert.InsertedPieces[i]),
                    OccupancyAfterInsert: occupancy,
                    OverflowCount: insert.OverflowCount));
            }

            return new StepResult(updatedState, updatedContext, events.ToImmutable());
        }
    }
}
