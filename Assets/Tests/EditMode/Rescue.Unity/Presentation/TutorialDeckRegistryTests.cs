using NUnit.Framework;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class TutorialDeckRegistryTests
    {
        [TestCase("L00", 3)]
        [TestCase("L01", 2)]
        [TestCase("L02", 2)]
        [TestCase("L03", 2)]
        [TestCase("L04", 2)]
        [TestCase("L07", 2)]
        [TestCase("L08", 3)]
        public void TutorialDeckRegistry_LoadsPreLevelDecks(string levelId, int expectedCardCount)
        {
            bool found = TutorialDeckRegistry.TryGetDeck(levelId, TutorialShowTiming.Pre, out TutorialDeck deck);

            Assert.That(found, Is.True);
            Assert.That(deck.cards, Has.Length.EqualTo(expectedCardCount));
        }

        [Test]
        public void TutorialDeckRegistry_LoadsL10PostWinDeck()
        {
            bool found = TutorialDeckRegistry.TryGetDeck("L10", TutorialShowTiming.PostWin, out TutorialDeck deck);

            Assert.That(found, Is.True);
            Assert.That(deck.cards, Has.Length.EqualTo(1));
            Assert.That(deck.cards[0].title, Is.EqualTo("STILL STANDING"));
        }

        [TestCase("L05")]
        [TestCase("L06")]
        [TestCase("L09")]
        public void TutorialDeckRegistry_MissingLevelsReturnNoDeck(string levelId)
        {
            Assert.That(TutorialDeckRegistry.TryGetAnyDeck(levelId, out _), Is.False);
        }
    }
}
