using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step09_Extract
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            Board board = state.Board;
            ImmutableArray<TargetState>.Builder targets = ImmutableArray.CreateBuilder<TargetState>(state.Targets.Length);
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();
            ImmutableArray<string>.Builder extractedTargetIds = ImmutableArray.CreateBuilder<string>();
            ImmutableArray<string> extractedOrder = state.ExtractedTargetOrder;

            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState before = state.Targets[i];
                TargetState after = before;

                if (!before.Extracted)
                {
                    after = before with
                    {
                        Readiness = before.ExtractableLatched
                            ? TargetReadiness.Extracted
                            : before.Readiness,
                    };

                    if (!before.Extracted && after.Extracted)
                    {
                        board = BoardHelpers.SetTile(board, after.Coord, new EmptyTile());
                        board = ReleaseRescuePathTiles(board, after.TargetId, after.Coord);
                        extractedOrder = extractedOrder.Add(after.TargetId);
                        extractedTargetIds.Add(after.TargetId);
                        events.Add(new TargetExtracted(after.TargetId, after.Coord));
                    }
                }

                targets.Add(after);
            }

            GameState updatedState = state with
            {
                Board = board,
                Targets = targets.ToImmutable(),
                ExtractedTargetOrder = extractedOrder,
            };
            StepContext updatedContext = context with
            {
                ExtractedTargetIdsThisAction = extractedTargetIds.ToImmutable(),
            };
            return new StepResult(updatedState, updatedContext, events.ToImmutable());
        }

        private static Board ReleaseRescuePathTiles(Board board, string targetId, TileCoord targetCoord)
        {
            Board updatedBoard = board;
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, targetCoord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                TileCoord neighbor = neighbors[i];
                if (BoardHelpers.GetTile(updatedBoard, neighbor) is not RescuePathTile rescuePath)
                {
                    continue;
                }

                ImmutableArray<string> updatedTargetIds = RemoveTargetId(rescuePath.TargetIds, targetId);
                updatedBoard = BoardHelpers.SetTile(
                    updatedBoard,
                    neighbor,
                    updatedTargetIds.IsEmpty ? new EmptyTile() : rescuePath with { TargetIds = updatedTargetIds });
            }

            return updatedBoard;
        }

        private static ImmutableArray<string> RemoveTargetId(ImmutableArray<string> targetIds, string targetId)
        {
            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>(targetIds.Length);
            for (int i = 0; i < targetIds.Length; i++)
            {
                if (targetIds[i] != targetId)
                {
                    builder.Add(targetIds[i]);
                }
            }

            return builder.ToImmutable();
        }
    }
}
