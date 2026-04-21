using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Pipeline.Steps;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Rules
{
    public sealed class DockJamTests
    {
        [Test]
        public void DockJamEnabledFirstOverflowFreezesWithoutImmediateLoss()
        {
            GameState state = PipelineTestFixtures.CreateState(
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
        public void NextActionThatClearsTripleContinuesPlay()
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
        public void NextActionThatDoesNotClearTripleLoses()
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
        public void DockJamFiresOnceOnly()
        {
            GameState state = CreateActiveDockJamState() with
            {
                DockJamActive = false,
                Frozen = false,
            };
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                PendingDockOverflowCount = 1,
            };

            CheckLossResult result = CheckLoss.Run(state, context);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.LossDockOverflow));
            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new Lost(ActionOutcome.LossDockOverflow),
            }).AsCollection);
        }

        [Test]
        public void DockJamDisabledFirstOverflowBecomesNormalLoss()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                dockJamEnabled: false);
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                PendingDockOverflowCount = 1,
            };

            CheckLossResult result = CheckLoss.Run(state, context);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.LossDockOverflow));
            Assert.That(result.State.DockJamUsed, Is.False);
            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new Lost(ActionOutcome.LossDockOverflow),
            }).AsCollection);
        }

        private static GameState CreateActiveDockJamState()
        {
            return PipelineTestFixtures.CreateState(
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
