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
                    TargetReadiness readiness = before.ExtractableLatched
                        ? TargetReadiness.ExtractableLatched
                        : CalculateReadiness(board, before.Coord, blockedRequiredNeighbors);
                    after = before with
                    {
                        Readiness = readiness,
                    };

                    if (before.Readiness != after.Readiness)
                    {
                        AddReadinessTransitionEvent(events, after);
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

        private static TargetReadiness CalculateReadiness(Board board, TileCoord targetCoord, int blockedRequiredNeighbors)
        {
            if (blockedRequiredNeighbors == 0)
            {
                return TargetReadiness.ExtractableLatched;
            }

            if (blockedRequiredNeighbors == 1)
            {
                return TargetReadiness.OneClearAway;
            }

            int requiredNeighbors = BoardHelpers.OrthogonalNeighbors(board, targetCoord).Length;
            int openNeighbors = requiredNeighbors - blockedRequiredNeighbors;
            return openNeighbors * 2 >= requiredNeighbors
                ? TargetReadiness.Progressing
                : TargetReadiness.Trapped;
        }

        private static void AddReadinessTransitionEvent(
            ImmutableArray<ActionEvent>.Builder events,
            TargetState target)
        {
            switch (target.Readiness)
            {
                case TargetReadiness.Progressing:
                    events.Add(new TargetProgressed(target.TargetId, target.Coord));
                    break;
                case TargetReadiness.OneClearAway:
                    events.Add(new TargetOneClearAway(target.TargetId, target.Coord));
                    break;
                case TargetReadiness.ExtractableLatched:
                    events.Add(new TargetExtractionLatched(target.TargetId, target.Coord));
                    break;
            }
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
