using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.Rules;
using Rescue.Core.State;
using Rescue.Core.Undo;

namespace Rescue.Content.Tests
{
    public sealed class VineGrowthPipelineTests
    {
        [Test]
        public void VineState_DefaultPlanningFieldsAreNull()
        {
            VineState vine = new VineState(
                ActionsSinceLastClear: 0,
                GrowthThreshold: 4,
                GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                PriorityCursor: 0,
                PendingGrowthTile: null);

            Assert.That(vine.PlannedGrowthTile, Is.Null);
            Assert.That(vine.GrowthSourceTile, Is.Null);
            Assert.That(vine.GrowthGoalTile, Is.Null);
        }

        [Test]
        public void Loader_LoadsExistingVineJsonWithNullPlanningFields()
        {
            GameState state = Loader.LoadLevel("L08", seed: 1);

            Assert.That(state.Vine.GrowthPriorityList, Is.Not.Empty);
            Assert.That(state.Vine.PendingGrowthTile, Is.Null);
            Assert.That(state.Vine.PlannedGrowthTile, Is.Null);
            Assert.That(state.Vine.GrowthSourceTile, Is.Null);
            Assert.That(state.Vine.GrowthGoalTile, Is.Null);
        }

        [Test]
        public void Undo_RestoresManuallyPresentVinePlanningFields()
        {
            TileCoord planned = new TileCoord(0, 2);
            TileCoord source = new TileCoord(1, 0);
            TileCoord goal = new TileCoord(2, 2);
            GameState state = CreateVineState(
                CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Vine, 1, null), new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.B)),
                    Row(new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.C), new EmptyTile())),
                growthThreshold: 4,
                growthPriority: ImmutableArray.Create(planned)) with
            {
                Vine = new VineState(
                    ActionsSinceLastClear: 1,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray.Create(planned),
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: planned,
                    GrowthSourceTile: source,
                    GrowthGoalTile: goal),
            };

            ActionResult result = Pipeline.RunAction(
                state,
                new ActionInput(new TileCoord(0, 0)),
                new RunOptions(RecordSnapshot: true));
            Assert.That(result.Snapshot, Is.Not.Null);
            Snapshot snapshot = result.Snapshot ?? throw new AssertionException("Expected undo snapshot.");
            GameState restored = UndoGuard.PerformUndo(result.State, snapshot);

            Assert.That(restored.Vine.PlannedGrowthTile, Is.EqualTo(planned));
            Assert.That(restored.Vine.GrowthSourceTile, Is.EqualTo(source));
            Assert.That(restored.Vine.GrowthGoalTile, Is.EqualTo(goal));
            Assert.That(restored.Vine.PendingGrowthTile, Is.Null);
            Assert.That(restored.UndoAvailable, Is.False);
        }

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
            ActionResult result = Pipeline.RunAction(state, new ActionInput(new TileCoord(2, 0)), new RunOptions(RecordSnapshot: false));

            Assert.That(result.State.Vine.PendingGrowthTile, Is.EqualTo(new TileCoord(2, 2)));
            Assert.That(result.State.Vine.PlannedGrowthTile, Is.EqualTo(new TileCoord(2, 2)));
            Assert.That(result.State.Vine.GrowthSourceTile, Is.Null);
            Assert.That(result.State.Vine.GrowthGoalTile, Is.EqualTo(new TileCoord(2, 2)));
            Assert.That(result.Events, Has.Some.EqualTo(new VinePreviewChanged(new TileCoord(2, 2))));
        }

        [Test]
        public void RunAction_PlansSystemicVineBeforePreviewWindow()
        {
            GameState state = CreateVineState(
                CreateBoard(
                    Row(new BlockerTile(BlockerType.Vine, 1, null), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.C), new TargetTile("0", Extracted: false)),
                    Row(new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.B), new EmptyTile(), new DebrisTile(DebrisType.D))),
                growthThreshold: 4,
                growthPriority: ImmutableArray<TileCoord>.Empty,
                targets: ImmutableArray.Create(new TargetState("0", new TileCoord(1, 3), TargetReadiness.Trapped)));

            ActionResult result = Pipeline.RunAction(state, new ActionInput(new TileCoord(2, 0)), new RunOptions(RecordSnapshot: false));

            Assert.That(result.State.Vine.PlannedGrowthTile, Is.EqualTo(new TileCoord(0, 1)));
            Assert.That(result.State.Vine.GrowthSourceTile, Is.EqualTo(new TileCoord(0, 0)));
            Assert.That(result.State.Vine.GrowthGoalTile, Is.EqualTo(new TileCoord(0, 3)));
            Assert.That(result.State.Vine.PendingGrowthTile, Is.Null);
            Assert.That(result.Events.OfType<VinePreviewChanged>(), Is.Empty);
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
            Assert.That(result.State.Vine.PlannedGrowthTile, Is.Null);
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
            Assert.That(result.State.Vine.PlannedGrowthTile, Is.Null);
            Assert.That(result.State.Vine.GrowthSourceTile, Is.Null);
            Assert.That(result.State.Vine.GrowthGoalTile, Is.Null);
        }

        [Test]
        public void Undo_RestoresLivePlannedVineState()
        {
            GameState state = CreateVineState(
                CreateBoard(
                    Row(new BlockerTile(BlockerType.Vine, 1, null), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.C), new TargetTile("0", Extracted: false)),
                    Row(new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.B), new EmptyTile(), new DebrisTile(DebrisType.D))),
                growthThreshold: 4,
                growthPriority: ImmutableArray<TileCoord>.Empty,
                targets: ImmutableArray.Create(new TargetState("0", new TileCoord(1, 3), TargetReadiness.Trapped)));

            ActionResult plannedResult = Pipeline.RunAction(
                state,
                new ActionInput(new TileCoord(2, 0)),
                new RunOptions(RecordSnapshot: false));
            Assert.That(plannedResult.State.Vine.PlannedGrowthTile, Is.EqualTo(new TileCoord(0, 1)));

            ActionResult nextResult = Pipeline.RunAction(
                plannedResult.State,
                new ActionInput(new TileCoord(1, 0)),
                new RunOptions(RecordSnapshot: true));

            Snapshot snapshot = nextResult.Snapshot ?? throw new AssertionException("Expected undo snapshot.");
            GameState restored = UndoGuard.PerformUndo(nextResult.State, snapshot);

            Assert.That(restored.Vine.PlannedGrowthTile, Is.EqualTo(plannedResult.State.Vine.PlannedGrowthTile));
            Assert.That(restored.Vine.GrowthSourceTile, Is.EqualTo(plannedResult.State.Vine.GrowthSourceTile));
            Assert.That(restored.Vine.GrowthGoalTile, Is.EqualTo(plannedResult.State.Vine.GrowthGoalTile));
            Assert.That(restored.Vine.PendingGrowthTile, Is.EqualTo(plannedResult.State.Vine.PendingGrowthTile));
        }

        [TestCase("L08")]
        [TestCase("L13")]
        [TestCase("L15")]
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
