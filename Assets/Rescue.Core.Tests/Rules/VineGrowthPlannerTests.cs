using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Rules;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Rules
{
    public sealed class VineGrowthPlannerTests
    {
        [Test]
        public void ChoosesTileAlongPathTowardTargetNeighbor()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(Vine(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new DebrisTile(DebrisType.A), Target("t1")),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile())),
                TargetState("t1", new TileCoord(1, 3), TargetReadiness.Trapped));

            VineGrowthPlan? plan = VineGrowthPlanner.Plan(state);

            Assert.That(plan, Is.Not.Null);
            Assert.That(plan!.SourceTile, Is.EqualTo(new TileCoord(0, 0)));
            Assert.That(plan.GoalTile, Is.EqualTo(new TileCoord(0, 3)));
            Assert.That(plan.NextGrowthTile, Is.EqualTo(new TileCoord(0, 1)));
            Assert.That(plan.UsedAuthoredFallback, Is.False);
        }

        [Test]
        public void PrefersActualRescuePathOverUnrelatedEmptyNeighbor()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(Vine(), RescuePath("t1"), Target("t1")),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                TargetState("t1", new TileCoord(1, 2), TargetReadiness.Progressing));

            VineGrowthPlan? plan = VineGrowthPlanner.Plan(state);

            Assert.That(plan, Is.Not.Null);
            Assert.That(plan!.GoalTile, Is.EqualTo(new TileCoord(1, 1)));
            Assert.That(plan.NextGrowthTile, Is.EqualTo(new TileCoord(1, 1)));
            Assert.That(plan.Reason, Is.EqualTo("RescuePath"));
        }

        [Test]
        public void PrefersCloserPathWithDeterministicTieBreak()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), Vine(), new EmptyTile(), Target("t1")),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile())),
                TargetState("t1", new TileCoord(1, 3), TargetReadiness.Trapped));

            VineGrowthPlan? plan = VineGrowthPlanner.Plan(state);

            Assert.That(plan, Is.Not.Null);
            Assert.That(plan!.GoalTile, Is.EqualTo(new TileCoord(1, 2)));
            Assert.That(plan.NextGrowthTile, Is.EqualTo(new TileCoord(1, 2)));
        }

        [Test]
        public void UsesAuthoredPriorityAsBiasWhenEquivalentCandidatesExist()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), Vine(), Crate(), Target("t1")),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile())),
                TargetState("t1", new TileCoord(1, 3), TargetReadiness.Trapped),
                growthPriorityList: ImmutableArray.Create(new TileCoord(2, 3)));

            VineGrowthPlan? plan = VineGrowthPlanner.Plan(state);

            Assert.That(plan, Is.Not.Null);
            Assert.That(plan!.GoalTile, Is.EqualTo(new TileCoord(2, 3)));
            Assert.That(plan.NextGrowthTile, Is.EqualTo(new TileCoord(2, 1)));
        }

        [Test]
        public void FallsBackToAuthoredPriorityWhenNoSystemicCandidateExists()
        {
            TileCoord fallback = new TileCoord(0, 1);
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), Crate())),
                growthPriorityList: ImmutableArray.Create(new TileCoord(0, 2), fallback));

            VineGrowthPlan? plan = VineGrowthPlanner.Plan(state);

            Assert.That(plan, Is.Not.Null);
            Assert.That(plan!.SourceTile, Is.Null);
            Assert.That(plan.GoalTile, Is.EqualTo(fallback));
            Assert.That(plan.NextGrowthTile, Is.EqualTo(fallback));
            Assert.That(plan.UsedAuthoredFallback, Is.True);
        }

        [Test]
        public void ReturnsNoPlanWhenNoVinesExist()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), Target("t1")),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                TargetState("t1", new TileCoord(1, 2), TargetReadiness.Trapped));

            VineGrowthPlan? plan = VineGrowthPlanner.Plan(state);

            Assert.That(plan, Is.Null);
        }

        [Test]
        public void ReturnsNoPlanWhenNoValidCandidatesExist()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), Flooded(), Crate()),
                    Row(Vine(), Target("other"), Target("t1")),
                    Row(new EmptyTile(), Vine(), Flooded())),
                TargetState("t1", new TileCoord(1, 2), TargetReadiness.Trapped));

            VineGrowthPlan? plan = VineGrowthPlanner.Plan(state);

            Assert.That(plan, Is.Null);
        }

        [Test]
        public void DoesNotTargetFloodedTargetBlockerOrAlreadyVineTiles()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), Flooded(), Crate()),
                    Row(Vine(), Target("other"), Target("t1")),
                    Row(new EmptyTile(), Vine(), Flooded())),
                TargetState("t1", new TileCoord(1, 2), TargetReadiness.Trapped));

            VineGrowthPlan? plan = VineGrowthPlanner.Plan(state);

            Assert.That(plan, Is.Null);
        }

        [Test]
        public void DoesNotTargetExtractedRescuePaths()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(Vine(), RescuePath("t1"), Target("t1"))),
                TargetState("t1", new TileCoord(0, 2), TargetReadiness.Extracted));

            VineGrowthPlan? plan = VineGrowthPlanner.Plan(state);

            Assert.That(plan, Is.Null);
        }

        [Test]
        public void DoesNotTargetLatchedRescuePaths()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(Vine(), RescuePath("t1"), Target("t1"))),
                TargetState("t1", new TileCoord(0, 2), TargetReadiness.ExtractableLatched));

            VineGrowthPlan? plan = VineGrowthPlanner.Plan(state);

            Assert.That(plan, Is.Null);
        }

        [Test]
        public void DeterministicSameInputGivesSamePlan()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), Vine(), Crate(), Target("t1")),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile())),
                TargetState("t1", new TileCoord(1, 3), TargetReadiness.Trapped),
                growthPriorityList: ImmutableArray.Create(new TileCoord(2, 3)));

            VineGrowthPlan? first = VineGrowthPlanner.Plan(state);
            VineGrowthPlan? second = VineGrowthPlanner.Plan(state);

            Assert.That(second, Is.EqualTo(first));
        }

        private static GameState CreateState(
            Board board,
            TargetState? target = null,
            ImmutableArray<TileCoord>? growthPriorityList = null)
        {
            ImmutableArray<TargetState> targets = target is null
                ? ImmutableArray<TargetState>.Empty
                : ImmutableArray.Create(target);

            return PipelineTestFixtures.CreateState(board, targets) with
            {
                Water = new WaterState(FloodedRows: 0, ActionsUntilRise: 99, RiseInterval: 99),
                Vine = new VineState(
                    ActionsSinceLastClear: 0,
                    GrowthThreshold: 4,
                    GrowthPriorityList: growthPriorityList ?? ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null),
            };
        }

        private static TargetState TargetState(string id, TileCoord coord, TargetReadiness readiness)
        {
            return new TargetState(id, coord, readiness);
        }

        private static Tile Vine()
        {
            return new BlockerTile(BlockerType.Vine, 1, Hidden: null);
        }

        private static Tile Crate()
        {
            return new BlockerTile(BlockerType.Crate, 1, Hidden: null);
        }

        private static Tile Flooded()
        {
            return new FloodedTile();
        }

        private static Tile Target(string id)
        {
            return new TargetTile(id, Extracted: false);
        }

        private static Tile RescuePath(string targetId)
        {
            return new RescuePathTile(ImmutableArray.Create(targetId));
        }

        private static ImmutableArray<Tile> Row(params Tile[] tiles)
        {
            return tiles.ToImmutableArray();
        }
    }
}
