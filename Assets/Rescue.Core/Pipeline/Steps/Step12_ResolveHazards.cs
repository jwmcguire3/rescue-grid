using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step12_ResolveHazards
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            GameState updatedState = state;
            StepContext updatedContext = context;
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();

            if (context.WaterRisePending && updatedState.Water.FloodedRows < updatedState.Board.Height)
            {
                int? nextFloodRow = WaterHelpers.GetNextFloodRow(updatedState.Board, updatedState.Water);
                if (!nextFloodRow.HasValue)
                {
                    return new StepResult(updatedState, updatedContext, events.ToImmutable());
                }

                int floodedRow = nextFloodRow.Value;
                Board floodedBoard = updatedState.Board;
                for (int col = 0; col < floodedBoard.Width; col++)
                {
                    floodedBoard = BoardHelpers.SetTile(floodedBoard, new TileCoord(floodedRow, col), new FloodedTile());
                }

                updatedState = updatedState with
                {
                    Board = floodedBoard,
                    Water = updatedState.Water with
                    {
                        FloodedRows = updatedState.Water.FloodedRows + 1,
                        ActionsUntilRise = updatedState.Water.RiseInterval,
                    },
                };
                updatedContext = updatedContext with
                {
                    WaterRisePending = false,
                };
                events.Add(new WaterRose(floodedRow));
            }

            if (updatedContext.VineGrowthPending && updatedState.Vine.PendingGrowthTile is TileCoord pendingTile)
            {
                int nextCursor = FindNextCursor(updatedState.Vine, pendingTile);
                if (IsValidGrowthTile(updatedState.Board, pendingTile))
                {
                    Board boardWithVine = BoardHelpers.SetTile(
                        updatedState.Board,
                        pendingTile,
                        new BlockerTile(BlockerType.Vine, 1, Hidden: null));
                    updatedState = updatedState with
                    {
                        Board = boardWithVine,
                        Vine = updatedState.Vine with
                        {
                            ActionsSinceLastClear = 0,
                            PendingGrowthTile = null,
                            PriorityCursor = nextCursor,
                        },
                    };
                    events.Add(new VineGrown(pendingTile));
                }
                else
                {
                    updatedState = updatedState with
                    {
                        Vine = updatedState.Vine with
                        {
                            PendingGrowthTile = null,
                        },
                    };
                }
            }

            updatedContext = updatedContext with
            {
                VineGrowthPreviewPending = false,
                VineGrowthPending = false,
            };

            return new StepResult(updatedState, updatedContext, events.ToImmutable());
        }

        private static int FindNextCursor(VineState vine, TileCoord coord)
        {
            int startIndex = vine.PriorityCursor < 0 ? 0 : vine.PriorityCursor;
            for (int i = startIndex; i < vine.GrowthPriorityList.Length; i++)
            {
                if (vine.GrowthPriorityList[i] == coord)
                {
                    return i + 1;
                }
            }

            return startIndex;
        }

        private static bool IsValidGrowthTile(Board board, TileCoord coord)
        {
            return BoardHelpers.InBounds(board, coord)
                && BoardHelpers.GetTile(board, coord) is EmptyTile;
        }
    }
}
