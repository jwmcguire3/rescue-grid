using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Pipeline.Steps;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Rules
{
    public sealed class BlockersTests
    {
        [Test]
        public void CrateWithOneAdjacentClearBreaks()
        {
            GameState state = PipelineTestFixtures.CreateState(CreateBoardWithBlocker(new BlockerTile(BlockerType.Crate, 1, Hidden: null)));

            (StepResult damage, StepResult resolve) = RunBlockerSteps(state);

            Assert.That(BoardHelpers.GetTile(damage.State.Board, new TileCoord(1, 1)), Is.EqualTo(new BlockerTile(BlockerType.Crate, 0, null)));
            Assert.That(BoardHelpers.GetTile(resolve.State.Board, new TileCoord(1, 1)), Is.TypeOf<EmptyTile>());
            Assert.That(damage.Events, Is.EqualTo(new ActionEvent[]
            {
                new BlockerDamaged(new TileCoord(1, 1), BlockerType.Crate, 0),
            }).AsCollection);
            Assert.That(resolve.Events, Is.EqualTo(new ActionEvent[]
            {
                new BlockerBroken(new TileCoord(1, 1), BlockerType.Crate),
            }).AsCollection);
        }

        [Test]
        public void CrateWithThreeAdjacentClearsInOneActionTakesOneHitNotThree()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A)),
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new BlockerTile(BlockerType.Crate, 2, Hidden: null),
                    new DebrisTile(DebrisType.A)),
                PipelineTestFixtures.DebrisRow(DebrisType.B, DebrisType.C, DebrisType.D));
            GameState state = PipelineTestFixtures.CreateState(board);

            (StepResult damage, StepResult resolve) = RunBlockerSteps(state, new TileCoord(0, 1));

            Assert.That(BoardHelpers.GetTile(damage.State.Board, new TileCoord(1, 1)), Is.EqualTo(new BlockerTile(BlockerType.Crate, 1, null)));
            Assert.That(BoardHelpers.GetTile(resolve.State.Board, new TileCoord(1, 1)), Is.EqualTo(new BlockerTile(BlockerType.Crate, 1, null)));
            Assert.That(damage.Events, Is.EqualTo(new ActionEvent[]
            {
                new BlockerDamaged(new TileCoord(1, 1), BlockerType.Crate, 1),
            }).AsCollection);
            Assert.That(resolve.Events, Is.Empty);
        }

        [Test]
        public void IceBreaksAndRevealsDebrisBeneath()
        {
            GameState state = PipelineTestFixtures.CreateState(
                CreateBoardWithBlocker(new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.D))));

            (StepResult _, StepResult resolve) = RunBlockerSteps(state);

            Assert.That(BoardHelpers.GetTile(resolve.State.Board, new TileCoord(1, 1)), Is.EqualTo(new DebrisTile(DebrisType.D)));
            Assert.That(resolve.Events, Is.EqualTo(new ActionEvent[]
            {
                new BlockerBroken(new TileCoord(1, 1), BlockerType.Ice),
                new IceRevealed(new TileCoord(1, 1), DebrisType.D),
            }).AsCollection);
        }

        [Test]
        public void VineBreaksAndResetsVineCounter()
        {
            GameState state = PipelineTestFixtures.CreateState(CreateBoardWithBlocker(new BlockerTile(BlockerType.Vine, 1, Hidden: null))) with
            {
                Vine = new VineState(
                    ActionsSinceLastClear: 2,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null),
            };

            (StepResult _, StepResult resolve) = RunBlockerSteps(state);

            Assert.That(BoardHelpers.GetTile(resolve.State.Board, new TileCoord(1, 1)), Is.TypeOf<EmptyTile>());
            Assert.That(resolve.State.Vine.ActionsSinceLastClear, Is.EqualTo(0));
            Assert.That(resolve.Context.VineClearedThisAction, Is.True);
            Assert.That(resolve.Events, Is.EqualTo(new ActionEvent[]
            {
                new BlockerBroken(new TileCoord(1, 1), BlockerType.Vine),
            }).AsCollection);
        }

        [Test]
        public void ReinforcedCrateTakesOneHitAndDoesNotBreakOnFirstAction()
        {
            GameState state = PipelineTestFixtures.CreateState(CreateBoardWithBlocker(new BlockerTile(BlockerType.Crate, 2, Hidden: null)));

            (StepResult damage, StepResult resolve) = RunBlockerSteps(state);

            Assert.That(BoardHelpers.GetTile(damage.State.Board, new TileCoord(1, 1)), Is.EqualTo(new BlockerTile(BlockerType.Crate, 1, null)));
            Assert.That(BoardHelpers.GetTile(resolve.State.Board, new TileCoord(1, 1)), Is.EqualTo(new BlockerTile(BlockerType.Crate, 1, null)));
            Assert.That(resolve.Events, Is.Empty);
        }

        [Test]
        public void ClearedGroupBorderingCrateAndIceDamagesBothOnce()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.C)),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A)),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new BlockerTile(BlockerType.Crate, 2, Hidden: null),
                    new EmptyTile()));
            GameState state = PipelineTestFixtures.CreateState(board);

            (StepResult damage, StepResult _) = RunBlockerSteps(state, new TileCoord(1, 1));

            Assert.That(BoardHelpers.GetTile(damage.State.Board, new TileCoord(0, 1)), Is.EqualTo(new BlockerTile(BlockerType.Ice, 0, new DebrisTile(DebrisType.C))));
            Assert.That(BoardHelpers.GetTile(damage.State.Board, new TileCoord(2, 1)), Is.EqualTo(new BlockerTile(BlockerType.Crate, 1, null)));
            Assert.That(damage.Events, Is.EqualTo(new ActionEvent[]
            {
                new BlockerDamaged(new TileCoord(0, 1), BlockerType.Ice, 0),
                new BlockerDamaged(new TileCoord(2, 1), BlockerType.Crate, 1),
            }).AsCollection);
        }

        [Test]
        public void BlockerNotAdjacentToAnyClearedTileTakesNoDamage()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile(),
                    new BlockerTile(BlockerType.Crate, 1, Hidden: null)),
                PipelineTestFixtures.DebrisRow(DebrisType.B, DebrisType.C, DebrisType.D));
            GameState state = PipelineTestFixtures.CreateState(board);

            (StepResult damage, StepResult resolve) = RunBlockerSteps(state);

            Assert.That(BoardHelpers.GetTile(damage.State.Board, new TileCoord(1, 2)), Is.EqualTo(new BlockerTile(BlockerType.Crate, 1, null)));
            Assert.That(damage.Context.AdjacentBlockersHit, Is.Empty);
            Assert.That(damage.Events, Is.Empty);
            Assert.That(resolve.Events, Is.Empty);
        }

        private static Board CreateBoardWithBlocker(BlockerTile blocker)
        {
            return PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B),
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.C),
                    blocker,
                    new DebrisTile(DebrisType.D)),
                PipelineTestFixtures.DebrisRow(DebrisType.B, DebrisType.C, DebrisType.D));
        }

        private static (StepResult Damage, StepResult Resolve) RunBlockerSteps(GameState state, TileCoord? tappedCoord = null)
        {
            TileCoord tap = tappedCoord ?? new TileCoord(0, 0);
            StepContext context = StepContext.Create(state, new ActionInput(tap));
            StepResult accepted = Step01_AcceptInput.Run(state, context);
            StepResult removed = Step02_RemoveGroup.Run(accepted.State, accepted.Context);
            StepResult damaged = Step03_DamageBlockers.Run(removed.State, removed.Context);
            StepResult resolved = Step04_ResolveBreaks.Run(damaged.State, damaged.Context);
            return (damaged, resolved);
        }
    }
}
