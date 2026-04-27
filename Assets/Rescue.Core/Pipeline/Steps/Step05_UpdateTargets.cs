using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step05_UpdateTargets
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            Board board = state.Board;
            ImmutableArray<TargetState>.Builder targets = ImmutableArray.CreateBuilder<TargetState>(state.Targets.Length);
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();
            ImmutableArray<string>.Builder latchedTargetIds = ImmutableArray.CreateBuilder<string>();

            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState before = state.Targets[i];
                TargetState after = before;

                if (!before.Extracted)
                {
                    int blockedRequiredNeighbors = CountBlockedRequiredNeighbors(board, before.Coord);
                    bool nowLatched = before.ExtractableLatched || blockedRequiredNeighbors == 0;
                    bool oneClearAway = !nowLatched && blockedRequiredNeighbors == 1;
                    after = before with
                    {
                        ExtractableLatched = nowLatched,
                        OneClearAway = oneClearAway,
                    };

                    if (!before.OneClearAway && after.OneClearAway)
                    {
                        events.Add(new TargetOneClearAway(after.TargetId, after.Coord));
                    }

                    if (!before.ExtractableLatched && after.ExtractableLatched)
                    {
                        latchedTargetIds.Add(after.TargetId);
                    }
                }

                targets.Add(after);
            }

            GameState updatedState = state with
            {
                Targets = targets.ToImmutable(),
            };
            StepContext updatedContext = context with
            {
                LatchedTargetIdsThisAction = latchedTargetIds.ToImmutable(),
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
