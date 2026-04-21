using System.Collections.Immutable;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step01_AcceptInput
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            TileCoord tapped = context.Input.TappedCoord;
            if (state.Frozen)
            {
                return Invalid(state, context, InvalidInputReason.Frozen);
            }

            if (!BoardHelpers.InBounds(state.Board, tapped))
            {
                return Invalid(state, context, InvalidInputReason.OutOfBounds);
            }

            if (IsFlooded(state.Board, state.Water, tapped))
            {
                return Invalid(state, context, InvalidInputReason.Flooded);
            }

            Tile tile = BoardHelpers.GetTile(state.Board, tapped);
            if (!GroupOps.IsExposed(state.Board, tapped))
            {
                return Invalid(state, context, GetInvalidReason(tile));
            }

            ImmutableArray<TileCoord>? group = GroupOps.FindGroup(state.Board, tapped);
            if (group is null || tile is not DebrisTile tappedDebris)
            {
                return Invalid(state, context, InvalidInputReason.SingleTile);
            }

            StepContext updatedContext = context with
            {
                IsValidInput = true,
                ValidatedGroupType = tappedDebris.Type,
                ValidatedGroupCoords = group.Value,
            };

            return new StepResult(state, updatedContext, ImmutableArray<ActionEvent>.Empty);
        }

        private static StepResult Invalid(GameState state, StepContext context, InvalidInputReason reason)
        {
            ImmutableArray<ActionEvent> events = ImmutableArray.Create<ActionEvent>(
                new InvalidInput(context.Input.TappedCoord, reason));
            return new StepResult(state, context, events);
        }

        private static InvalidInputReason GetInvalidReason(Tile tile)
        {
            return tile switch
            {
                FloodedTile => InvalidInputReason.Flooded,
                BlockerTile { Type: BlockerType.Ice } => InvalidInputReason.Ice,
                BlockerTile => InvalidInputReason.Blocker,
                TargetTile => InvalidInputReason.Target,
                EmptyTile => InvalidInputReason.Empty,
                _ => InvalidInputReason.Empty,
            };
        }

        private static bool IsFlooded(Board board, WaterState water, TileCoord coord)
        {
            int floodStartRow = board.Height - water.FloodedRows;
            return water.FloodedRows > 0 && coord.Row >= floodStartRow;
        }
    }
}
