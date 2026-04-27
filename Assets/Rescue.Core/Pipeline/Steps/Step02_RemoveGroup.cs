using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step02_RemoveGroup
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            if (!context.IsValidInput || context.ValidatedGroupType is null || context.ValidatedGroupCoords.IsDefaultOrEmpty)
            {
                return new StepResult(state, context, ImmutableArray<ActionEvent>.Empty);
            }

            Board updatedBoard = state.Board;
            ImmutableDictionary<TileCoord, SpawnLineage> lineageByCoord = state.SpawnLineageByCoord;
            ImmutableArray<DebrisType>.Builder removedDebris = ImmutableArray.CreateBuilder<DebrisType>(context.ValidatedGroupCoords.Length);
            ImmutableArray<int>.Builder removedLineages = ImmutableArray.CreateBuilder<int>();
            for (int i = 0; i < context.ValidatedGroupCoords.Length; i++)
            {
                TileCoord coord = context.ValidatedGroupCoords[i];
                updatedBoard = BoardHelpers.SetTile(updatedBoard, coord, new EmptyTile());
                removedDebris.Add(context.ValidatedGroupType.Value);
                if (lineageByCoord.TryGetValue(coord, out SpawnLineage lineage))
                {
                    removedLineages.Add(lineage.LineageId);
                    lineageByCoord = lineageByCoord.Remove(coord);
                }
            }

            GameState updatedState = state with
            {
                Board = updatedBoard,
                SpawnLineageByCoord = lineageByCoord,
            };
            StepContext updatedContext = context with
            {
                RemovedDebris = removedDebris.ToImmutable(),
            };

            ImmutableArray<ActionEvent> events = ImmutableArray.Create<ActionEvent>(
                new GroupRemoved(
                    context.ValidatedGroupType.Value,
                    context.ValidatedGroupCoords,
                    removedLineages.ToImmutable()));
            return new StepResult(updatedState, updatedContext, events);
        }
    }
}
