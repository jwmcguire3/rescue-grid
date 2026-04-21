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
            // TODO(B13): implement Dock Jam grace behavior per Phase 1 spec sections 1.5 and 1.10.
            if (context.PendingDockOverflowCount > 0)
            {
                ImmutableArray<ActionEvent> dockLossEvents = ImmutableArray.Create<ActionEvent>(
                    new Lost(ActionOutcome.LossDockOverflow));
                return new CheckLossResult(state, dockLossEvents, ActionOutcome.LossDockOverflow);
            }

            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                if (!target.Extracted && IsFloodedTarget(state.Board, state.Water, target.Coord))
                {
                    ImmutableArray<ActionEvent> waterLossEvents = ImmutableArray.Create<ActionEvent>(
                        new Lost(ActionOutcome.LossWaterOnTarget));
                    return new CheckLossResult(state, waterLossEvents, ActionOutcome.LossWaterOnTarget);
                }
            }

            return new CheckLossResult(state, ImmutableArray<ActionEvent>.Empty, ActionOutcome.Ok);
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
