using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step04_ResolveBreaks
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            ImmutableArray<TileCoord> brokenBlockers = BoardHelpers.FindAll(
                state.Board,
                tile => tile is BlockerTile { Hp: <= 0 });

            if (brokenBlockers.IsDefaultOrEmpty)
            {
                return new StepResult(
                    state,
                    context with
                    {
                        BrokenBlockers = ImmutableArray<TileCoord>.Empty,
                    },
                    ImmutableArray<ActionEvent>.Empty);
            }

            Board updatedBoard = state.Board;
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();
            bool vineCleared = context.VineClearedThisAction;

            for (int i = 0; i < brokenBlockers.Length; i++)
            {
                TileCoord coord = brokenBlockers[i];
                if (BoardHelpers.GetTile(updatedBoard, coord) is not BlockerTile blocker)
                {
                    continue;
                }

                BreakResolution resolution = Resolve(blocker, coord);
                updatedBoard = BoardHelpers.SetTile(updatedBoard, coord, resolution.Replacement);
                for (int j = 0; j < resolution.Events.Length; j++)
                {
                    events.Add(resolution.Events[j]);
                }

                vineCleared |= resolution.ClearedVine;
            }

            GameState updatedState = state with
            {
                Board = updatedBoard,
                Vine = vineCleared
                    ? state.Vine with { ActionsSinceLastClear = 0 }
                    : state.Vine,
            };

            StepContext updatedContext = context with
            {
                BrokenBlockers = brokenBlockers,
                VineClearedThisAction = vineCleared,
            };

            return new StepResult(updatedState, updatedContext, events.ToImmutable());
        }

        private static BreakResolution Resolve(BlockerTile blocker, TileCoord coord)
        {
            return blocker.Type switch
            {
                BlockerType.Crate => new BreakResolution(
                    new EmptyTile(),
                    ImmutableArray.Create<ActionEvent>(new BlockerBroken(coord, BlockerType.Crate)),
                    ClearedVine: false),
                BlockerType.Ice => ResolveIce(blocker, coord),
                BlockerType.Vine => new BreakResolution(
                    new EmptyTile(),
                    ImmutableArray.Create<ActionEvent>(new BlockerBroken(coord, BlockerType.Vine)),
                    ClearedVine: true),
                _ => throw new System.ArgumentOutOfRangeException(nameof(blocker.Type), blocker.Type, "Unknown blocker type."),
            };
        }

        private static BreakResolution ResolveIce(BlockerTile blocker, TileCoord coord)
        {
            if (blocker.Hidden is not DebrisTile hidden)
            {
                throw new System.InvalidOperationException($"Ice at {coord} must reveal a hidden debris tile.");
            }

            return new BreakResolution(
                hidden,
                ImmutableArray.Create<ActionEvent>(
                    new BlockerBroken(coord, BlockerType.Ice),
                    new IceRevealed(coord, hidden.Type)),
                ClearedVine: false);
        }

        private readonly record struct BreakResolution(
            Tile Replacement,
            ImmutableArray<ActionEvent> Events,
            bool ClearedVine);
    }
}
