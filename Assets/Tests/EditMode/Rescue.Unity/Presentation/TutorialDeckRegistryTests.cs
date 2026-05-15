using NUnit.Framework;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class TutorialDeckRegistryTests
    {
        [TestCase("L00", 4)]
        [TestCase("L01", 3)]
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

        [Test]
        public void TutorialDeckRegistry_L00ThirdCardTeachesTwoOrMoreBeforeThinkingIsFree()
        {
            bool found = TutorialDeckRegistry.TryGetDeck("L00", TutorialShowTiming.Pre, out TutorialDeck deck);

            Assert.That(found, Is.True);
            Assert.That(deck.cards, Has.Length.EqualTo(4));
            TutorialCard card = deck.cards[2];
            Assert.That(card.title, Is.EqualTo("TWO OR MORE"));
            Assert.That(
                card.body,
                Is.EqualTo("Tap any connected group of 2 or more matching supplies to clear it.\n\nThe water only rises when you move, so look as long as you need. Thinking is free."));
            Assert.That(
                card.imageResourcePath,
                Is.EqualTo("Rescue.Unity/UI/Tutorial/L00_two_are_valid_free_thinking_screeshot"));
        }

        [Test]
        public void TutorialDeckRegistry_L00FourthCardTeachesForecastRow()
        {
            bool found = TutorialDeckRegistry.TryGetDeck("L00", TutorialShowTiming.Pre, out TutorialDeck deck);

            Assert.That(found, Is.True);
            Assert.That(deck.cards, Has.Length.EqualTo(4));
            TutorialCard card = deck.cards[3];
            Assert.That(card.title, Is.EqualTo("The Forecast Row"));
            Assert.That(
                card.body,
                Is.EqualTo("The wet shimmer shows where water is going next.\n\nFlooded rows are already lost. Forecast rows are your warning."));
            Assert.That(
                card.imageResourcePath,
                Is.EqualTo("Rescue.Unity/UI/Tutorial/L00_forecast_row_screeshot"));
        }

        [Test]
        public void TutorialDeckRegistry_L01ThirdCardTeachesCrateNeighborClears()
        {
            bool found = TutorialDeckRegistry.TryGetDeck("L01", TutorialShowTiming.Pre, out TutorialDeck deck);

            Assert.That(found, Is.True);
            Assert.That(deck.cards, Has.Length.EqualTo(3));
            TutorialCard card = deck.cards[2];
            Assert.That(card.title, Is.EqualTo("Crates Need a Neighbor Clear"));
            Assert.That(
                card.body,
                Is.EqualTo("Crates don\u2019t break when you tap the crate.\n\nClear supplies beside them. The hit comes from the neighboring clear."));
            Assert.That(
                card.imageResourcePath,
                Is.EqualTo("Rescue.Unity/UI/Tutorial/L00_what_counts_as_clear_screeshot"));
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
