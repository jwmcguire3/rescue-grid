using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    public sealed class VineGrowthPipelineTests
    {
        [Test]
        public void RunAction_PreviewsVineOneActionBeforeThreshold()
        {
            GameState state = CreateVineState(
                CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.E), new DebrisTile(DebrisType.E)),
                    Row(new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.C)),
                    Row(new DebrisTile(DebrisType.D), new DebrisTile(DebrisType.D), new EmptyTile(), new DebrisTile(DebrisType.F))),
                growthThreshold: 3,
                growthPriority: ImmutableArray.Create(new TileCoord(2, 2)));

            state = Pipeline.RunAction(state, new ActionInput(new TileCoord(0, 0)), new RunOptions(RecordSnapshot: false)).State;
            ActionResult result = Pipeline.RunAction(state, new ActionInput(new TileCoord(1, 0)), new RunOptions(RecordSnapshot: false));

            Assert.That(result.State.Vine.PendingGrowthTile, Is.EqualTo(new TileCoord(2, 2)));
            Assert.That(result.Events, Has.Some.EqualTo(new VinePreviewChanged(new TileCoord(2, 2))));
        }

        [Test]
        public void RunAction_GrowsVineAtThreshold()
        {
            GameState state = CreateVineState(
                CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.E), new DebrisTile(DebrisType.E)),
                    Row(new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.C)),
                    Row(new DebrisTile(DebrisType.D), new DebrisTile(DebrisType.D), new EmptyTile(), new DebrisTile(DebrisType.F))),
                growthThreshold: 3,
                growthPriority: ImmutableArray.Create(new TileCoord(2, 2)));

            state = Pipeline.RunAction(state, new ActionInput(new TileCoord(0, 0)), new RunOptions(RecordSnapshot: false)).State;
            state = Pipeline.RunAction(state, new ActionInput(new TileCoord(1, 0)), new RunOptions(RecordSnapshot: false)).State;
            ActionResult result = Pipeline.RunAction(state, new ActionInput(new TileCoord(2, 0)), new RunOptions(RecordSnapshot: false));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 2)), Is.EqualTo(new BlockerTile(BlockerType.Vine, 1, null)));
            Assert.That(result.Events, Has.Some.EqualTo(new VineGrown(new TileCoord(2, 2))));
            Assert.That(result.State.Vine.ActionsSinceLastClear, Is.EqualTo(0));
        }

        [Test]
        public void RunAction_GrowsVineOverDebrisAtPriorityTile()
        {
            TileCoord priority = new TileCoord(2, 2);
            GameState state = CreateVineState(
                CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.E), new DebrisTile(DebrisType.E)),
                    Row(new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.C)),
                    Row(new DebrisTile(DebrisType.D), new DebrisTile(DebrisType.D), new DebrisTile(DebrisType.F), new EmptyTile())),
                actionsSinceLastClear: 2,
                growthThreshold: 3,
                growthPriority: ImmutableArray.Create(priority),
                pendingGrowthTile: priority);

            ActionResult result = Pipeline.RunAction(state, new ActionInput(new TileCoord(0, 0)), new RunOptions(RecordSnapshot: false));

            Assert.That(BoardHelpers.GetTile(result.State.Board, priority), Is.EqualTo(new BlockerTile(BlockerType.Vine, 1, null)));
            Assert.That(result.Events, Has.Some.EqualTo(new VineGrown(priority)));
        }

        [Test]
        public void RunAction_GrowsIntoUnlatchedRescuePath()
        {
            GameState state = CreateVineState(
                CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.C)),
                    Row(new EmptyTile(), new TargetTile("0", Extracted: false), new BlockerTile(BlockerType.Crate, 1, null)),
                    Row(new DebrisTile(DebrisType.D), new BlockerTile(BlockerType.Crate, 1, null), new DebrisTile(DebrisType.E))),
                actionsSinceLastClear: 1,
                growthThreshold: 2,
                growthPriority: ImmutableArray.Create(new TileCoord(1, 0)),
                pendingGrowthTile: new TileCoord(1, 0),
                targets: ImmutableArray.Create(new TargetState("0", new TileCoord(1, 1), TargetReadiness.Trapped)));

            ActionResult result = Pipeline.RunAction(state, new ActionInput(new TileCoord(0, 0)), new RunOptions(RecordSnapshot: false));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 0)), Is.EqualTo(new BlockerTile(BlockerType.Vine, 1, null)));
            Assert.That(result.Events, Has.Some.EqualTo(new VineGrown(new TileCoord(1, 0))));
            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Ok));
        }

        [Test]
        public void RunAction_DoesNotFillFutureVinePriorityCellsDuringGravityOrSpawn()
        {
            TileCoord priority = new TileCoord(2, 1);
            GameState state = CreateVineState(
                CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.C)),
                    Row(new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.C)),
                    Row(new DebrisTile(DebrisType.D), new EmptyTile(), new DebrisTile(DebrisType.D))),
                growthThreshold: 4,
                growthPriority: ImmutableArray.Create(priority));

            ActionResult result = Pipeline.RunAction(state, new ActionInput(new TileCoord(0, 0)), new RunOptions(RecordSnapshot: false));

            Assert.That(BoardHelpers.GetTile(result.State.Board, priority), Is.TypeOf<EmptyTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 1)), Is.EqualTo(new DebrisTile(DebrisType.C)));
        }

        [Test]
        public void RunAction_ClearingVineResetsCounterAndPendingPreview()
        {
            GameState state = CreateVineState(
                CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Vine, 1, null), new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.B))),
                actionsSinceLastClear: 2,
                growthThreshold: 3,
                growthPriority: ImmutableArray.Create(new TileCoord(0, 2)),
                pendingGrowthTile: new TileCoord(0, 2));

            ActionResult result = Pipeline.RunAction(state, new ActionInput(new TileCoord(0, 0)), new RunOptions(RecordSnapshot: false));

            Assert.That(result.Events, Has.Some.EqualTo(new BlockerBroken(new TileCoord(1, 0), BlockerType.Vine)));
            Assert.That(result.Events.OfType<VineGrown>(), Is.Empty);
            Assert.That(result.State.Vine.ActionsSinceLastClear, Is.EqualTo(0));
            Assert.That(result.State.Vine.PendingGrowthTile, Is.Null);
        }

        [TestCase("L08")]
        [TestCase("L13")]
        public void AuthoredVineLevels_IgnoreVinePathProducesPreviewAndGrowth(string levelId)
        {
            GameState state = Loader.LoadLevel(levelId, seed: 1);
            bool previewed = false;
            bool grown = false;

            for (int i = 0; i < 8 && !grown; i++)
            {
                TileCoord input = FindNonVineClearInput(state);
                ActionResult result = Pipeline.RunAction(state, new ActionInput(input), new RunOptions(RecordSnapshot: false));
                previewed |= result.Events.OfType<VinePreviewChanged>().Any(ev => ev.PendingTile.HasValue);
                grown |= result.Events.OfType<VineGrown>().Any();
                Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Ok), $"Unexpected outcome after tapping {input} on {levelId}.");
                state = result.State;
            }

            Assert.That(previewed, Is.True, $"{levelId} should produce a vine preview when vine is ignored.");
            Assert.That(grown, Is.True, $"{levelId} should grow a vine when vine is ignored.");
        }

        private static TileCoord FindNonVineClearInput(GameState state)
        {
            for (int row = 0; row < state.Board.Height; row++)
            {
                for (int col = 0; col < state.Board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    ImmutableArray<TileCoord>? group = GroupOps.FindGroup(state.Board, coord);
                    if (group is not { IsDefaultOrEmpty: false } coords || WouldDamageVine(state.Board, coords))
                    {
                        continue;
                    }

                    return coord;
                }
            }

            Assert.Fail("Could not find a valid non-vine-clearing input.");
            return default;
        }

        private static bool WouldDamageVine(Board board, ImmutableArray<TileCoord> group)
        {
            for (int i = 0; i < group.Length; i++)
            {
                ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, group[i]);
                for (int j = 0; j < neighbors.Length; j++)
                {
                    if (BoardHelpers.GetTile(board, neighbors[j]) is BlockerTile { Type: BlockerType.Vine })
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static GameState CreateVineState(
            Board board,
            int growthThreshold,
            ImmutableArray<TileCoord> growthPriority,
            ImmutableArray<TargetState>? targets = null,
            int actionsSinceLastClear = 0,
            TileCoord? pendingGrowthTile = null)
        {
            return new GameState(
                Board: board,
                Dock: new Dock(ImmutableArray.Create<DebrisType?>(null, null, null, null, null, null, null), Size: 7),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 20, RiseInterval: 20),
                Vine: new VineState(actionsSinceLastClear, growthThreshold, growthPriority, PriorityCursor: 0, pendingGrowthTile),
                Targets: targets ?? ImmutableArray<TargetState>.Empty,
                LevelConfig: new LevelConfig(
                    DebrisTypePool: ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E, DebrisType.F),
                    BaseDistribution: null,
                    AssistanceChance: 0.0d,
                    ConsecutiveEmergencyCap: 2),
                RngState: new RngState(1u, 2u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }

        private static Board CreateBoard(params ImmutableArray<Tile>[] rows)
        {
            return new Board(rows[0].Length, rows.Length, rows.ToImmutableArray());
        }

        private static ImmutableArray<Tile> Row(params Tile[] tiles)
        {
            return tiles.ToImmutableArray();
        }
    }
}
