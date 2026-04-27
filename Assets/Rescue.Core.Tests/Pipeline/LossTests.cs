using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Pipeline.Steps;
using Rescue.Core.State;

namespace Rescue.Core.Tests.Pipeline
{
    public sealed class LossTests
    {
        [Test]
        public void WaterOnTargetBeatsDockOverflowInPrecedence()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile()),
                    PipelineTestFixtures.Row(new TargetTile("target", Extracted: false), new EmptyTile())),
                targets: ImmutableArray.Create(new TargetState("target", new TileCoord(1, 0), Extracted: false, OneClearAway: false)))
                with
                {
                    Water = new WaterState(FloodedRows: 1, ActionsUntilRise: 2, RiseInterval: 2),
                };
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                PendingDockOverflowCount = 1,
            };

            CheckLossResult result = CheckLoss.Run(state, context);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.LossWaterOnTarget));
            Assert.That(result.State.Frozen, Is.True);
            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new Lost(ActionOutcome.LossWaterOnTarget),
            }).AsCollection);
        }

        [Test]
        public void DockOverflowWithoutDockJamEligibilityLosesImmediately()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                dockJamEnabled: true) with
            {
                DockJamUsed = true,
            };
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                PendingDockOverflowCount = 1,
            };

            CheckLossResult result = CheckLoss.Run(state, context);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.LossDockOverflow));
            Assert.That(result.State.Frozen, Is.True);
            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new Lost(ActionOutcome.LossDockOverflow),
            }).AsCollection);
        }

        [Test]
        public void DockJamEligibleFirstOverflowFreezesWithoutLoss()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                dockJamEnabled: true);
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                PendingDockOverflowCount = 1,
            };

            CheckLossResult result = CheckLoss.Run(state, context);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Ok));
            Assert.That(result.State.Frozen, Is.True);
            Assert.That(result.State.DockJamUsed, Is.True);
            Assert.That(result.State.DockJamActive, Is.True);
            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new DockJamTriggered(1),
            }).AsCollection);
        }

        [Test]
        public void RecoveryActionThatClearsTripleUnfreezesAndContinues()
        {
            GameState state = CreateActiveDockJamState();
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                ClearedDockTriplesThisAction = 1,
            };

            CheckLossResult result = CheckLoss.Run(state, context);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Ok));
            Assert.That(result.State.Frozen, Is.False);
            Assert.That(result.State.DockJamActive, Is.False);
            Assert.That(result.Events, Is.Empty);
        }

        [Test]
        public void RecoveryActionWithoutTripleClearLosesToDockOverflow()
        {
            GameState state = CreateActiveDockJamState();
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0)));

            CheckLossResult result = CheckLoss.Run(state, context);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.LossDockOverflow));
            Assert.That(result.State.Frozen, Is.True);
            Assert.That(result.State.DockJamActive, Is.False);
            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new Lost(ActionOutcome.LossDockOverflow),
            }).AsCollection);
        }

        [Test]
        public void DockJamFiresOnlyOncePerLevel()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                dockJamEnabled: true) with
            {
                DockJamUsed = true,
            };
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                PendingDockOverflowCount = 1,
            };

            CheckLossResult result = CheckLoss.Run(state, context);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.LossDockOverflow));
            Assert.That(result.State.DockJamUsed, Is.True);
            Assert.That(result.State.Frozen, Is.True);
            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new Lost(ActionOutcome.LossDockOverflow),
            }).AsCollection);
        }

        [Test]
        public void LevelsWithoutDockJamLoseOnFirstOverflow()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                dockJamEnabled: false);
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                PendingDockOverflowCount = 1,
            };

            CheckLossResult result = CheckLoss.Run(state, context);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.LossDockOverflow));
            Assert.That(result.State.DockJamUsed, Is.False);
            Assert.That(result.State.Frozen, Is.True);
            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new Lost(ActionOutcome.LossDockOverflow),
            }).AsCollection);
        }

        private static GameState CreateState(
            Board board,
            ImmutableArray<TargetState>? targets = null,
            bool dockJamEnabled = false)
        {
            return PipelineTestFixtures.CreateState(board, targets, dockJamEnabled: dockJamEnabled);
        }

        private static GameState CreateActiveDockJamState()
        {
            return CreateState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                dockJamEnabled: true) with
            {
                DockJamUsed = true,
                DockJamActive = true,
                Frozen = true,
            };
        }
    }
}
