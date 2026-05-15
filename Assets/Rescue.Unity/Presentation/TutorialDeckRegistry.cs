using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rescue.Unity.Presentation
{
    public enum TutorialShowTiming
    {
        Pre,
        PostWin
    }

    [Serializable]
    public sealed class TutorialDeck
    {
        public string levelId = string.Empty;
        public string showTiming = string.Empty;
        public TutorialCard[] cards = Array.Empty<TutorialCard>();

        public TutorialShowTiming Timing => ParseTiming(showTiming);

        private static TutorialShowTiming ParseTiming(string value)
        {
            return string.Equals(value, "postWin", StringComparison.OrdinalIgnoreCase)
                ? TutorialShowTiming.PostWin
                : TutorialShowTiming.Pre;
        }
    }

    [Serializable]
    public sealed class TutorialCard
    {
        public string header = string.Empty;
        public string title = string.Empty;
        public string body = string.Empty;
        public string imageResourcePath = string.Empty;
    }

    [Serializable]
    internal sealed class TutorialDeckCollection
    {
        public TutorialDeck[] decks = Array.Empty<TutorialDeck>();
    }

    public static class TutorialDeckRegistry
    {
        private const string RegistryResourcePath = "Rescue.Unity/UI/Tutorial/tutorial_decks";

        private static IReadOnlyList<TutorialDeck>? cachedDecks;

        public static IReadOnlyList<TutorialDeck> Decks => cachedDecks ??= LoadDecks();

        public static bool TryGetDeck(string levelId, TutorialShowTiming timing, out TutorialDeck deck)
        {
            IReadOnlyList<TutorialDeck> decks = Decks;
            for (int i = 0; i < decks.Count; i++)
            {
                TutorialDeck candidate = decks[i];
                if (string.Equals(candidate.levelId, levelId, StringComparison.Ordinal)
                    && candidate.Timing == timing
                    && candidate.cards.Length > 0)
                {
                    deck = candidate;
                    return true;
                }
            }

            deck = new TutorialDeck();
            return false;
        }

        public static bool TryGetAnyDeck(string levelId, out TutorialDeck deck)
        {
            return TryGetDeck(levelId, TutorialShowTiming.Pre, out deck)
                || TryGetDeck(levelId, TutorialShowTiming.PostWin, out deck);
        }

        internal static void ClearCacheForTests()
        {
            cachedDecks = null;
        }

        private static IReadOnlyList<TutorialDeck> LoadDecks()
        {
            TextAsset? asset = Resources.Load<TextAsset>(RegistryResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                return Array.Empty<TutorialDeck>();
            }

            TutorialDeckCollection? collection = JsonUtility.FromJson<TutorialDeckCollection>(asset.text);
            return collection?.decks ?? Array.Empty<TutorialDeck>();
        }
    }
}
