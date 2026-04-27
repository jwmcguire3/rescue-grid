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
                        Extracted = before.ExtractableLatched,
                        OneClearAway = before.ExtractableLatched ? false : before.OneClearAway,
                        ExtractableLatched = false,
                    };

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
    }
}
