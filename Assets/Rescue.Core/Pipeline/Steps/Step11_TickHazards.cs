using System.Collections.Immutable;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step11_TickHazards
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            GameState updatedState = state;
            StepContext updatedContext = context;
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();

            if (updatedState.Water.PauseUntilFirstAction)
            {
                updatedState = updatedState with
                {
                    Water = updatedState.Water with
                    {
                        PauseUntilFirstAction = false,
                    },
                };
            }

            if (updatedState.Water.FloodedRows < updatedState.Board.Height && updatedState.Water.ActionsUntilRise > 0)
            {
                int actionsUntilRise = updatedState.Water.ActionsUntilRise - 1;
                bool waterRisePending = actionsUntilRise == 0;
                updatedState = updatedState with
                {
                    Water = updatedState.Water with
                    {
                        ActionsUntilRise = actionsUntilRise,
                    },
                };
                updatedContext = updatedContext with
                {
                    WaterRisePending = waterRisePending,
                };

                if (!waterRisePending && actionsUntilRise == 1)
                {
                    int? nextFloodRow = WaterHelpers.GetNextFloodRow(updatedState.Board, updatedState.Water);
                    if (nextFloodRow.HasValue)
                    {
                        events.Add(new WaterWarning(actionsUntilRise, nextFloodRow.Value));
                    }
                }
            }

            VineState updatedVine;
            if (context.VineClearedThisAction)
            {
                updatedVine = updatedState.Vine with
                {
                    ActionsSinceLastClear = 0,
                    PendingGrowthTile = null,
                    PlannedGrowthTile = null,
                    GrowthSourceTile = null,
                    GrowthGoalTile = null,
                };
                updatedContext = updatedContext with
                {
                    VineGrowthPreviewPending = false,
                    VineGrowthPending = false,
                };
            }
            else
            {
                int actionsSinceLastClear = updatedState.Vine.ActionsSinceLastClear + 1;
                TileCoord? pendingGrowthTile = updatedState.Vine.PendingGrowthTile;
                TileCoord? plannedGrowthTile = updatedState.Vine.PlannedGrowthTile;
                TileCoord? growthSourceTile = updatedState.Vine.GrowthSourceTile;
                TileCoord? growthGoalTile = updatedState.Vine.GrowthGoalTile;
                bool previewPending = false;
                bool growthPending = false;
                bool pendingWasCleared = false;

                if (plannedGrowthTile is not null
                    && !IsPlanStillValid(
                        updatedState.Board,
                        updatedState.Vine,
                        updatedState.Targets,
                        plannedGrowthTile.Value,
                        growthSourceTile,
                        growthGoalTile))
                {
                    pendingWasCleared = pendingGrowthTile is not null;
                    pendingGrowthTile = null;
                    plannedGrowthTile = null;
                    growthSourceTile = null;
                    growthGoalTile = null;
                }

                if (plannedGrowthTile is null && !pendingWasCleared)
                {
                    VineGrowthPlan? plan = VineGrowthPlanner.Plan(updatedState);
                    if (plan is not null)
                    {
                        plannedGrowthTile = plan.NextGrowthTile;
                        growthSourceTile = plan.SourceTile;
                        growthGoalTile = plan.GoalTile;
                    }
                }

                if (actionsSinceLastClear >= updatedState.Vine.GrowthThreshold - 1
                    && pendingGrowthTile is null
                    && plannedGrowthTile is not null)
                {
                    pendingGrowthTile = plannedGrowthTile;
                    previewPending = true;
                    events.Add(new VinePreviewChanged(pendingGrowthTile));
                }

                if (actionsSinceLastClear >= updatedState.Vine.GrowthThreshold
                    && pendingGrowthTile is not null)
                {
                    growthPending = true;
                }

                updatedVine = updatedState.Vine with
                {
                    ActionsSinceLastClear = actionsSinceLastClear,
                    PendingGrowthTile = pendingGrowthTile,
                    PlannedGrowthTile = plannedGrowthTile,
                    GrowthSourceTile = growthSourceTile,
                    GrowthGoalTile = growthGoalTile,
                };
                updatedContext = updatedContext with
                {
                    VineGrowthPreviewPending = previewPending,
                    VineGrowthPending = growthPending,
                };
            }

            updatedState = updatedState with
            {
                Vine = updatedVine,
            };

            return new StepResult(updatedState, updatedContext, events.ToImmutable());
        }

        private static bool IsPlanStillValid(
            Board board,
            VineState vine,
            ImmutableArray<TargetState> targets,
            TileCoord plannedGrowthTile,
            TileCoord? growthSourceTile,
            TileCoord? growthGoalTile)
        {
            if (!VineGrowthTiles.IsValidGrowthTile(board, vine, targets, plannedGrowthTile))
            {
                return false;
            }

            if (growthSourceTile is TileCoord source
                && (!BoardHelpers.InBounds(board, source)
                    || BoardHelpers.GetTile(board, source) is not BlockerTile { Type: BlockerType.Vine }))
            {
                return false;
            }

            return growthGoalTile is not TileCoord goal
                || VineGrowthTiles.IsValidGrowthTile(board, vine, targets, goal);
        }
    }
}
