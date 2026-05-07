using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Pipeline.Steps;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Rules
{
    public sealed class VineTests
    {
        [Test]
        public void ClearingAVineResetsTheCounterToZero()
        {
            TileCoord planned = new TileCoord(1, 0);
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A)),
                    Row(new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 2,
                pendingGrowthTile: planned,
                plannedGrowthTile: planned,
                growthSourceTile: new TileCoord(0, 1),
                growthGoalTile: planned);

            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                VineClearedThisAction = true,
            };

            StepResult result = Step11_TickHazards.Run(state, context);

            Assert.That(result.State.Vine.ActionsSinceLastClear, Is.EqualTo(0));
            Assert.That(result.State.Vine.PendingGrowthTile, Is.Null);
            Assert.That(result.State.Vine.PlannedGrowthTile, Is.Null);
            Assert.That(result.State.Vine.GrowthSourceTile, Is.Null);
            Assert.That(result.State.Vine.GrowthGoalTile, Is.Null);
            Assert.That(result.Context.VineGrowthPending, Is.False);
        }

        [Test]
        public void Step11PlansBeforePreviewWindow()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(Vine(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile(), Target("t1")),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile())),
                growthThreshold: 4,
                targets: ImmutableArray.Create(new TargetState("t1", new TileCoord(1, 3), TargetReadiness.Trapped)));

            StepResult result = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(1, 0))));

            Assert.That(result.State.Vine.ActionsSinceLastClear, Is.EqualTo(1));
            Assert.That(result.State.Vine.PlannedGrowthTile, Is.EqualTo(new TileCoord(0, 1)));
            Assert.That(result.State.Vine.GrowthSourceTile, Is.EqualTo(new TileCoord(0, 0)));
            Assert.That(result.State.Vine.GrowthGoalTile, Is.EqualTo(new TileCoord(0, 3)));
            Assert.That(result.State.Vine.PendingGrowthTile, Is.Null);
            Assert.That(result.Context.VineGrowthPreviewPending, Is.False);
        }

        [Test]
        public void VineGrowsAfterThresholdActionsWithNoVineClear()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                growthThreshold: 3,
                growthPriorityList: ImmutableArray.Create(new TileCoord(1, 1), new TileCoord(1, 2)));

            GameState advanced = RunHazardAction(state, actionCount: 1);
            GameState previewed = RunHazardAction(advanced, actionCount: 2);
            GameState grown = RunHazardAction(previewed, actionCount: 3);

            Assert.That(BoardHelpers.GetTile(grown.Board, new TileCoord(1, 1)), Is.EqualTo(new BlockerTile(BlockerType.Vine, 1, null)));
            Assert.That(grown.Vine.ActionsSinceLastClear, Is.EqualTo(0));
            Assert.That(grown.Vine.PendingGrowthTile, Is.Null);
            Assert.That(grown.Vine.PlannedGrowthTile, Is.Null);
            Assert.That(grown.Vine.PriorityCursor, Is.EqualTo(1));
        }

        [Test]
        public void OneActionBeforeGrowthPendingTileIsSetFromPriorityList()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 1,
                growthThreshold: 3,
                growthPriorityList: ImmutableArray.Create(
                    new TileCoord(0, 2),
                    new TileCoord(1, 1),
                    new TileCoord(1, 2)));

            StepResult result = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(result.State.Vine.ActionsSinceLastClear, Is.EqualTo(2));
            Assert.That(result.State.Vine.PendingGrowthTile, Is.EqualTo(new TileCoord(0, 2)));
            Assert.That(result.State.Vine.PlannedGrowthTile, Is.EqualTo(new TileCoord(0, 2)));
            Assert.That(result.State.Vine.GrowthSourceTile, Is.Null);
            Assert.That(result.State.Vine.GrowthGoalTile, Is.EqualTo(new TileCoord(0, 2)));
            Assert.That(result.Context.VineGrowthPreviewPending, Is.True);
            Assert.That(result.Context.VineGrowthPending, Is.False);
        }

        [Test]
        public void VinePreviewChangedFiresWhenPendingTileIsSet()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 1,
                growthThreshold: 3,
                growthPriorityList: ImmutableArray.Create(new TileCoord(0, 2)));

            StepResult result = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new VinePreviewChanged(new TileCoord(0, 2)),
            }).AsCollection);
        }

        [Test]
        public void PreviewUsesPreviouslyPlannedTile()
        {
            TileCoord planned = new TileCoord(0, 1);
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(Vine(), new EmptyTile(), new EmptyTile(), Target("t1")),
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 2,
                growthThreshold: 4,
                plannedGrowthTile: planned,
                growthSourceTile: new TileCoord(0, 0),
                growthGoalTile: new TileCoord(0, 2),
                targets: ImmutableArray.Create(new TargetState("t1", new TileCoord(0, 3), TargetReadiness.Trapped)));

            StepResult result = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(1, 0))));

            Assert.That(result.State.Vine.PendingGrowthTile, Is.EqualTo(planned));
            Assert.That(result.State.Vine.PlannedGrowthTile, Is.EqualTo(planned));
            Assert.That(result.Events, Has.Some.EqualTo(new VinePreviewChanged(planned)));
        }

        [Test]
        public void VineGrownFiresExactlyOncePerGrowth()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 2,
                growthThreshold: 3,
                growthPriorityList: ImmutableArray.Create(new TileCoord(1, 1)),
                pendingGrowthTile: new TileCoord(1, 1));

            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                VineGrowthPending = true,
            };

            StepResult result = Step12_ResolveHazards.Run(state, context);

            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new VineGrown(new TileCoord(1, 1)),
            }).AsCollection);
        }

        [Test]
        public void Step12GrowsPlannedPendingTileAndClearsPlan()
        {
            TileCoord planned = new TileCoord(1, 1);
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 2,
                growthThreshold: 3,
                pendingGrowthTile: planned,
                plannedGrowthTile: planned,
                growthSourceTile: new TileCoord(0, 2),
                growthGoalTile: planned);

            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                VineGrowthPending = true,
            };

            StepResult result = Step12_ResolveHazards.Run(state, context);

            Assert.That(BoardHelpers.GetTile(result.State.Board, planned), Is.EqualTo(new BlockerTile(BlockerType.Vine, 1, null)));
            Assert.That(result.State.Vine.PendingGrowthTile, Is.Null);
            Assert.That(result.State.Vine.PlannedGrowthTile, Is.Null);
            Assert.That(result.State.Vine.GrowthSourceTile, Is.Null);
            Assert.That(result.State.Vine.GrowthGoalTile, Is.Null);
            Assert.That(result.Events, Has.Some.EqualTo(new VineGrown(planned)));
        }

        [Test]
        public void OnlyOneVineGrowsPerGrowthEvent()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 2,
                growthThreshold: 3,
                growthPriorityList: ImmutableArray.Create(new TileCoord(1, 1), new TileCoord(1, 2)),
                pendingGrowthTile: new TileCoord(1, 1));

            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                VineGrowthPending = true,
            };

            StepResult result = Step12_ResolveHazards.Run(state, context);

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 1)), Is.EqualTo(new BlockerTile(BlockerType.Vine, 1, null)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 2)), Is.TypeOf<EmptyTile>());
            Assert.That(result.Events, Has.Exactly(1).TypeOf<VineGrown>());
        }

        [Test]
        public void IfPriorityListIsExhaustedOrInvalidNoGrowthOccurs()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new BlockerTile(BlockerType.Crate, 1, null)),
                    Row(new DebrisTile(DebrisType.C), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 1,
                growthThreshold: 3,
                growthPriorityList: ImmutableArray.Create(
                    new TileCoord(0, 2),
                    new TileCoord(5, 5)),
                priorityCursor: 0);

            StepResult previewTick = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            StepResult growthTick = Step11_TickHazards.Run(previewTick.State, StepContext.Create(previewTick.State, new ActionInput(new TileCoord(0, 0))));
            StepResult resolve = Step12_ResolveHazards.Run(growthTick.State, growthTick.Context);

            Assert.That(previewTick.State.Vine.PendingGrowthTile, Is.Null);
            Assert.That(resolve.Events, Has.None.TypeOf<VineGrown>());
            Assert.That(BoardHelpers.FindAll(resolve.State.Board, tile => tile is BlockerTile { Type: BlockerType.Vine }), Is.Empty);
        }

        [Test]
        public void NoPlanAndNoFallbackDoesNotPreviewOrGrow()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 2,
                growthThreshold: 3);

            StepResult tick = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            StepResult resolve = Step12_ResolveHazards.Run(tick.State, tick.Context);

            Assert.That(tick.State.Vine.PlannedGrowthTile, Is.Null);
            Assert.That(tick.State.Vine.PendingGrowthTile, Is.Null);
            Assert.That(tick.Context.VineGrowthPending, Is.False);
            Assert.That(resolve.Events, Has.None.TypeOf<VineGrown>());
        }

        [Test]
        public void BlockedPriorityEntryFallsBackToNextValidPriorityTile()
        {
            TileCoord fallback = new TileCoord(1, 1);
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new BlockerTile(BlockerType.Crate, 1, null)),
                    Row(new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.B), new EmptyTile())),
                actionsSinceLastClear: 1,
                growthThreshold: 3,
                growthPriorityList: ImmutableArray.Create(
                    new TileCoord(0, 2),
                    fallback),
                priorityCursor: 0);

            StepResult previewTick = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            StepResult growthTick = Step11_TickHazards.Run(previewTick.State, StepContext.Create(previewTick.State, new ActionInput(new TileCoord(0, 0))));
            StepResult resolve = Step12_ResolveHazards.Run(growthTick.State, growthTick.Context);

            Assert.That(previewTick.State.Vine.PendingGrowthTile, Is.EqualTo(fallback));
            Assert.That(BoardHelpers.GetTile(resolve.State.Board, fallback), Is.EqualTo(new BlockerTile(BlockerType.Vine, 1, null)));
            Assert.That(resolve.Events, Has.Some.EqualTo(new VineGrown(fallback)));
        }

        [Test]
        public void InvalidUnpreviewedPlanRetargetsDeterministicallyOnHazardTick()
        {
            TileCoord invalidPlan = new TileCoord(0, 1);
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(Vine(), Crate(), new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), Target("t1")),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 1,
                growthThreshold: 4,
                plannedGrowthTile: invalidPlan,
                growthSourceTile: new TileCoord(0, 0),
                growthGoalTile: new TileCoord(0, 3),
                targets: ImmutableArray.Create(new TargetState("t1", new TileCoord(1, 3), TargetReadiness.Trapped)));

            StepResult result = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(1, 1))));

            Assert.That(result.State.Vine.PlannedGrowthTile, Is.EqualTo(new TileCoord(1, 0)));
            Assert.That(result.State.Vine.PendingGrowthTile, Is.Null);
            Assert.That(result.Context.VineGrowthPending, Is.False);
        }

        [Test]
        public void InvalidPreviewedPlanFizzlesWithoutUnpreviewedReplacement()
        {
            TileCoord invalidPlan = new TileCoord(0, 1);
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(Vine(), Crate(), new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), Target("t1")),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 2,
                growthThreshold: 3,
                pendingGrowthTile: invalidPlan,
                plannedGrowthTile: invalidPlan,
                growthSourceTile: new TileCoord(0, 0),
                growthGoalTile: new TileCoord(0, 3),
                targets: ImmutableArray.Create(new TargetState("t1", new TileCoord(1, 3), TargetReadiness.Trapped)));

            StepResult tick = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(1, 1))));
            StepResult resolve = Step12_ResolveHazards.Run(tick.State, tick.Context);

            Assert.That(tick.State.Vine.PlannedGrowthTile, Is.Null);
            Assert.That(tick.State.Vine.PendingGrowthTile, Is.Null);
            Assert.That(tick.Context.VineGrowthPending, Is.False);
            Assert.That(resolve.Events, Has.None.TypeOf<VineGrown>());
        }

        [Test]
        public void VineKeepsCheckingPriorityAfterMissedPreviewWindow()
        {
            TileCoord priority = new TileCoord(0, 2);
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.B)),
                    Row(new DebrisTile(DebrisType.C), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 4,
                growthThreshold: 4,
                growthPriorityList: ImmutableArray.Create(priority),
                pendingGrowthTile: null);

            StepResult tick = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            StepResult resolve = Step12_ResolveHazards.Run(tick.State, tick.Context);

            Assert.That(tick.State.Vine.PendingGrowthTile, Is.EqualTo(priority));
            Assert.That(tick.State.Vine.PlannedGrowthTile, Is.EqualTo(priority));
            Assert.That(tick.Context.VineGrowthPending, Is.True);
            Assert.That(BoardHelpers.GetTile(resolve.State.Board, priority), Is.EqualTo(new BlockerTile(BlockerType.Vine, 1, null)));
            Assert.That(resolve.Events, Has.Some.EqualTo(new VineGrown(priority)));
        }

        [Test]
        public void VineDoesNotMarkGrowthPendingWithoutPendingTileAfterMissedPreviewWindow()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new BlockerTile(BlockerType.Crate, 1, null)),
                    Row(new DebrisTile(DebrisType.C), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 4,
                growthThreshold: 4,
                growthPriorityList: ImmutableArray.Create(new TileCoord(0, 2)),
                pendingGrowthTile: null);

            StepResult tick = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(tick.State.Vine.PendingGrowthTile, Is.Null);
            Assert.That(tick.Context.VineGrowthPending, Is.False);
        }

        [Test]
        public void SameActionVineClearResetTakesPriorityOverGrowth()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                actionsSinceLastClear: 2,
                growthThreshold: 3,
                growthPriorityList: ImmutableArray.Create(new TileCoord(1, 1)),
                pendingGrowthTile: new TileCoord(1, 1));

            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                VineClearedThisAction = true,
                VineGrowthPending = true,
                VineGrowthPreviewPending = true,
            };

            StepResult tick = Step11_TickHazards.Run(state, context);
            StepResult resolve = Step12_ResolveHazards.Run(tick.State, tick.Context);

            Assert.That(tick.State.Vine.ActionsSinceLastClear, Is.EqualTo(0));
            Assert.That(tick.State.Vine.PendingGrowthTile, Is.Null);
            Assert.That(tick.State.Vine.PlannedGrowthTile, Is.Null);
            Assert.That(tick.Context.VineGrowthPending, Is.False);
            Assert.That(resolve.Events, Has.None.TypeOf<VineGrown>());
        }

        private static GameState RunHazardAction(GameState state, int actionCount)
        {
            GameState actionState = state with { ActionCount = actionCount - 1 };
            StepResult tick = Step11_TickHazards.Run(actionState, StepContext.Create(actionState, new ActionInput(new TileCoord(0, 0))));
            StepResult resolve = Step12_ResolveHazards.Run(tick.State, tick.Context);
            return resolve.State with { ActionCount = actionCount };
        }

        private static GameState CreateState(
            Board board,
            int actionsSinceLastClear = 0,
            int growthThreshold = 4,
            ImmutableArray<TileCoord>? growthPriorityList = null,
            int priorityCursor = 0,
            TileCoord? pendingGrowthTile = null,
            TileCoord? plannedGrowthTile = null,
            TileCoord? growthSourceTile = null,
            TileCoord? growthGoalTile = null,
            ImmutableArray<TargetState>? targets = null)
        {
            return PipelineTestFixtures.CreateState(board, targets: targets) with
            {
                Water = new WaterState(
                    FloodedRows: 0,
                    ActionsUntilRise: 99,
                    RiseInterval: 99),
                Vine = new VineState(
                    ActionsSinceLastClear: actionsSinceLastClear,
                    GrowthThreshold: growthThreshold,
                    GrowthPriorityList: growthPriorityList ?? ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: priorityCursor,
                    PendingGrowthTile: pendingGrowthTile,
                    PlannedGrowthTile: plannedGrowthTile,
                    GrowthSourceTile: growthSourceTile,
                    GrowthGoalTile: growthGoalTile),
            };
        }

        private static Tile Vine()
        {
            return new BlockerTile(BlockerType.Vine, 1, Hidden: null);
        }

        private static Tile Crate()
        {
            return new BlockerTile(BlockerType.Crate, 1, Hidden: null);
        }

        private static Tile Target(string id)
        {
            return new TargetTile(id, Extracted: false);
        }

        private static ImmutableArray<Tile> Row(params Tile[] tiles)
        {
            return tiles.ToImmutableArray();
        }
    }
}
