using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.State;

namespace Rescue.Core.Tests.Integration
{
    public sealed class StabilizationBeatTests
    {
        [Test]
        public void ClearingOneBlockedNeighborEmitsTargetOneClearAwayBeforeExtractionTurn()
        {
            GameState state = IntegrationTestFixtures.CreateTwoBeatRescueState();

            ActionResult firstAction = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(2, 2)));

            int oneClearAwayIndex = FindEventIndex<TargetOneClearAway>(firstAction.Events);
            int spawnIndex = FindEventIndex<Spawned>(firstAction.Events);

            Assert.That(firstAction.Outcome, Is.EqualTo(ActionOutcome.Ok));
            Assert.That(spawnIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(oneClearAwayIndex, Is.GreaterThan(spawnIndex));
            Assert.That(firstAction.Events[^1], Is.EqualTo(new TargetOneClearAway("pup", new TileCoord(3, 3))));
        }

        [Test]
        public void NextActionClearsFinalNeighborThenEmitsTargetExtractedAndWon()
        {
            GameState state = IntegrationTestFixtures.CreateTwoBeatRescueState();
            ActionResult firstAction = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(2, 2)));

            ActionResult secondAction = Rescue.Core.Pipeline.Pipeline.RunAction(firstAction.State, new ActionInput(new TileCoord(3, 1)));

            int extractedIndex = FindEventIndex<TargetExtracted>(secondAction.Events);
            int wonIndex = FindEventIndex<Won>(secondAction.Events);

            Assert.That(secondAction.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(extractedIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(wonIndex, Is.GreaterThan(extractedIndex));
            Assert.That(secondAction.Events, Has.None.TypeOf<TargetOneClearAway>());
            Assert.That(secondAction.Events[extractedIndex], Is.EqualTo(new TargetExtracted("pup", new TileCoord(3, 3))));
        }

        private static int FindEventIndex<T>(System.Collections.Immutable.ImmutableArray<ActionEvent> events)
            where T : ActionEvent
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i] is T)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
