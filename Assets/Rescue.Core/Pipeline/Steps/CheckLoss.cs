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
                return new CheckLossResult(
                    state with { Frozen = true },
                    dockLossEvents,
                    ActionOutcome.LossDockOverflow);
            }

            return new CheckLossResult(resolvedState, ImmutableArray<ActionEvent>.Empty, ActionOutcome.Ok);
        }
    }

    internal static class WaterTargetConsequence
    {
        public static CheckLossResult Run(GameState state, StepContext context)
        {
            ImmutableArray<TargetState>.Builder? updatedTargets = null;
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();

            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                if (!target.Extracted && IsFloodedTarget(state.Board, state.Water, target.Coord))
                {
                    if (state.LevelConfig.WaterContactMode == WaterContactMode.OneTickGrace)
                    {
                        if (target.Readiness == TargetReadiness.Distressed)
                        {
                            events.Add(new TargetDistressedExpired(target.TargetId, target.Coord));
                            events.Add(new Lost(ActionOutcome.LossDistressedExpired));
                            return new CheckLossResult(
                                state with { Frozen = true },
                                events.ToImmutable(),
                                ActionOutcome.LossDistressedExpired);
                        }

                        updatedTargets ??= state.Targets.ToBuilder();
                        TargetState distressed = target with { Readiness = TargetReadiness.Distressed };
                        updatedTargets[i] = distressed;
                        events.Add(new TargetDistressedEntered(distressed.TargetId, distressed.Coord));
                        continue;
                    }

                    events.Add(new Lost(ActionOutcome.LossWaterOnTarget));
                    return new CheckLossResult(
                        state with { Frozen = true },
                        events.ToImmutable(),
                        ActionOutcome.LossWaterOnTarget);
                }
            }

            GameState resolvedState = updatedTargets is null
                ? state
                : state with { Targets = updatedTargets.ToImmutable() };
            return new CheckLossResult(resolvedState, events.ToImmutable(), ActionOutcome.Ok);
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
