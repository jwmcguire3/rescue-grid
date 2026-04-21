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
                    int blockedRequiredNeighbors = CountBlockedRequiredNeighbors(board, before.Coord);
                    bool nowExtracted = blockedRequiredNeighbors == 0;
                    bool oneClearAway = blockedRequiredNeighbors == 1;
                    after = before with
                    {
                        Extracted = nowExtracted,
                        OneClearAway = oneClearAway,
                    };

                    if (!before.OneClearAway && after.OneClearAway)
                    {
                        events.Add(new TargetOneClearAway(after.TargetId, after.Coord));
                    }

                    if (!before.Extracted && after.Extracted)
                    {
                        board = BoardHelpers.SetTile(board, after.Coord, new EmptyTile());
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

        private static int CountBlockedRequiredNeighbors(Board board, TileCoord targetCoord)
        {
            int blocked = 0;
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, targetCoord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (BoardHelpers.GetTile(board, neighbors[i]) is not EmptyTile)
                {
                    blocked++;
                }
            }

            return blocked;
        }
    }
}
