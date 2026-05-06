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
                ImmutableDictionary<TileCoord, SpawnLineage> lineageByCoord = updatedState.SpawnLineageByCoord;
                ImmutableArray<FloodedRescuePath>.Builder floodedRescuePaths = ImmutableArray.CreateBuilder<FloodedRescuePath>();
                for (int col = 0; col < floodedBoard.Width; col++)
                {
                    TileCoord coord = new TileCoord(floodedRow, col);
                    Tile tileBeforeFlood = BoardHelpers.GetTile(floodedBoard, coord);
                    AddBlockedRescuePathsForFloodedTile(
                        updatedState,
                        coord,
                        tileBeforeFlood,
                        floodedRescuePaths);

                    if (tileBeforeFlood is TargetTile)
                    {
                        continue;
                    }

                    floodedBoard = BoardHelpers.SetTile(floodedBoard, coord, new FloodedTile());
                    lineageByCoord = lineageByCoord.Remove(coord);
                }

                updatedState = updatedState with
                {
                    Board = floodedBoard,
                    SpawnLineageByCoord = lineageByCoord,
                    Water = updatedState.Water with
                    {
                        FloodedRows = updatedState.Water.FloodedRows + 1,
                        ActionsUntilRise = updatedState.Water.RiseInterval,
                    },
                };
                updatedContext = updatedContext with
                {
                    WaterRisePending = false,
                    FloodedRescuePathsThisAction = floodedRescuePaths.ToImmutable(),
                };
                events.Add(new WaterRose(floodedRow));
            }

            if (updatedContext.VineGrowthPending && updatedState.Vine.PendingGrowthTile is TileCoord pendingTile)
            {
                int nextCursor = FindNextCursor(updatedState.Vine, pendingTile);
                if (VineGrowthTiles.IsValidGrowthTile(updatedState.Board, updatedState.Vine, updatedState.Targets, pendingTile))
                {
                    Board boardWithVine = BoardHelpers.SetTile(
                        updatedState.Board,
                        pendingTile,
                        new BlockerTile(BlockerType.Vine, 1, Hidden: null));
                    updatedState = updatedState with
                    {
                        Board = boardWithVine,
                        SpawnLineageByCoord = updatedState.SpawnLineageByCoord.Remove(pendingTile),
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

        private static void AddBlockedRescuePathsForFloodedTile(
            GameState state,
            TileCoord coord,
            Tile tileBeforeFlood,
            ImmutableArray<FloodedRescuePath>.Builder floodedRescuePaths)
        {
            if (tileBeforeFlood is TargetTile)
            {
                return;
            }

            if (tileBeforeFlood is RescuePathTile rescuePath)
            {
                AddFloodedRescuePathTargets(
                    state,
                    coord,
                    rescuePath,
                    floodedRescuePaths);
                return;
            }

            if (tileBeforeFlood is EmptyTile)
            {
                return;
            }

            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                if (target.Extracted || target.ExtractableLatched)
                {
                    continue;
                }

                if (IsOrthogonalNeighbor(target.Coord, coord))
                {
                    floodedRescuePaths.Add(new FloodedRescuePath(
                        target.TargetId,
                        target.Coord,
                        coord));
                }
            }
        }

        private static void AddFloodedRescuePathTargets(
            GameState state,
            TileCoord coord,
            RescuePathTile rescuePath,
            ImmutableArray<FloodedRescuePath>.Builder floodedRescuePaths)
        {
            for (int i = 0; i < rescuePath.TargetIds.Length; i++)
            {
                string targetId = rescuePath.TargetIds[i];
                for (int j = 0; j < state.Targets.Length; j++)
                {
                    TargetState target = state.Targets[j];
                    if (target.TargetId != targetId
                        || target.Extracted
                        || target.ExtractableLatched)
                    {
                        continue;
                    }

                    floodedRescuePaths.Add(new FloodedRescuePath(
                        target.TargetId,
                        target.Coord,
                        coord));
                    break;
                }
            }
        }

        private static bool IsOrthogonalNeighbor(TileCoord a, TileCoord b)
        {
            int rowDelta = a.Row - b.Row;
            if (rowDelta < 0)
            {
                rowDelta = -rowDelta;
            }

            int colDelta = a.Col - b.Col;
            if (colDelta < 0)
            {
                colDelta = -colDelta;
            }

            return rowDelta + colDelta == 1;
        }
    }
}
