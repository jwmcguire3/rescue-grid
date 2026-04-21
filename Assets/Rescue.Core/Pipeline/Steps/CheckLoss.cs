using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal sealed record CheckLossResult(
        GameState State,
        ImmutableArray<ActionEvent> Events,
        ActionOutcome Outcome);

    internal static class CheckLoss
    {
        public static CheckLossResult Run(GameState state, StepContext context)
        {
            GameState resolvedState = state;
            bool dockJamRecoveryAction = state.DockJamActive;
            bool clearedTripleThisAction = context.ClearedDockTriplesThisAction > 0;

            if (dockJamRecoveryAction)
            {
                resolvedState = state with
                {
                    Frozen = !clearedTripleThisAction,
                    DockJamActive = false,
                };
            }

            for (int i = 0; i < resolvedState.Targets.Length; i++)
            {
                TargetState target = resolvedState.Targets[i];
                if (!target.Extracted && IsFloodedTarget(resolvedState.Board, resolvedState.Water, target.Coord))
                {
                    ImmutableArray<ActionEvent> waterLossEvents = ImmutableArray.Create<ActionEvent>(
                        new Lost(ActionOutcome.LossWaterOnTarget));
                    return new CheckLossResult(resolvedState, waterLossEvents, ActionOutcome.LossWaterOnTarget);
                }
            }

            if (dockJamRecoveryAction)
            {
                if (clearedTripleThisAction)
                {
                    return new CheckLossResult(resolvedState, ImmutableArray<ActionEvent>.Empty, ActionOutcome.Ok);
                }

                ImmutableArray<ActionEvent> jamLossEvents = ImmutableArray.Create<ActionEvent>(
                    new Lost(ActionOutcome.LossDockOverflow));
                return new CheckLossResult(resolvedState, jamLossEvents, ActionOutcome.LossDockOverflow);
            }

            if (context.PendingDockOverflowCount > 0)
            {
                if (state.DockJamEnabled && !state.DockJamUsed)
                {
                    ImmutableArray<ActionEvent> jamTriggeredEvents = ImmutableArray.Create<ActionEvent>(
                        new DockJamTriggered(context.PendingDockOverflowCount));
                    return new CheckLossResult(
                        state with
                        {
                            Frozen = true,
                            DockJamUsed = true,
                            DockJamActive = true,
                        },
                        jamTriggeredEvents,
                        ActionOutcome.Ok);
                }

                ImmutableArray<ActionEvent> dockLossEvents = ImmutableArray.Create<ActionEvent>(
                    new Lost(ActionOutcome.LossDockOverflow));
                return new CheckLossResult(state, dockLossEvents, ActionOutcome.LossDockOverflow);
            }

            return new CheckLossResult(resolvedState, ImmutableArray<ActionEvent>.Empty, ActionOutcome.Ok);
        }

        private static bool IsFloodedTarget(Board board, WaterState water, TileCoord coord)
        {
            if (water.FloodedRows <= 0)
            {
                return false;
            }

            int floodStartRow = board.Height - water.FloodedRows;
            return coord.Row >= floodStartRow;
        }
    }
}
