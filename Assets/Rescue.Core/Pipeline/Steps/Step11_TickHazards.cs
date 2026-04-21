using System.Collections.Immutable;
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

            if (state.Water.FloodedRows < state.Board.Height && state.Water.ActionsUntilRise > 0)
            {
                int actionsUntilRise = state.Water.ActionsUntilRise - 1;
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
                    int nextFloodRow = state.Board.Height - state.Water.FloodedRows - 1;
                    events.Add(new WaterWarning(actionsUntilRise, nextFloodRow));
                }
            }

            VineState updatedVine;
            if (context.VineClearedThisAction)
            {
                updatedVine = updatedState.Vine with
                {
                    ActionsSinceLastClear = 0,
                    PendingGrowthTile = null,
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
                bool previewPending = false;
                bool growthPending = false;

                if (actionsSinceLastClear == updatedState.Vine.GrowthThreshold - 1)
                {
                    pendingGrowthTile = FindFirstValidGrowthTile(updatedState.Board, updatedState.Vine);
                    if (pendingGrowthTile is not null)
                    {
                        previewPending = true;
                        events.Add(new VinePreviewChanged(pendingGrowthTile));
                    }
                }

                if (actionsSinceLastClear == updatedState.Vine.GrowthThreshold)
                {
                    growthPending = true;
                }

                updatedVine = updatedState.Vine with
                {
                    ActionsSinceLastClear = actionsSinceLastClear,
                    PendingGrowthTile = pendingGrowthTile,
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

        private static TileCoord? FindFirstValidGrowthTile(Board board, VineState vine)
        {
            int startIndex = vine.PriorityCursor < 0 ? 0 : vine.PriorityCursor;
            for (int i = startIndex; i < vine.GrowthPriorityList.Length; i++)
            {
                TileCoord coord = vine.GrowthPriorityList[i];
                if (IsValidGrowthTile(board, coord))
                {
                    return coord;
                }
            }

            return null;
        }

        private static bool IsValidGrowthTile(Board board, TileCoord coord)
        {
            return BoardHelpers.InBounds(board, coord)
                && BoardHelpers.GetTile(board, coord) is EmptyTile;
        }
    }
}
