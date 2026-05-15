using NUnit.Framework;
using UnityEngine;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class TutorialCardPresenterTests
    {
        private GameObject? root;

        [TearDown]
        public void TearDown()
        {
            if (root is not null)
            {
                Object.DestroyImmediate(root);
                root = null;
            }
        }

        [Test]
        public void TutorialCardPresenter_ShowDeckDisplaysFirstCard()
        {
            TutorialCardPresenter presenter = CreatePresenter();

            presenter.ShowDeck(CreateDeck());

            Assert.That(presenter.IsVisible, Is.True);
            Assert.That(presenter.CurrentCardCount, Is.EqualTo(2));
            Assert.That(presenter.CurrentCardIndex, Is.EqualTo(0));
            Assert.That(presenter.CurrentTitle, Is.EqualTo("FIRST TITLE"));
            Assert.That(presenter.CurrentBody, Does.Contain("First body"));
            Assert.That(presenter.CurrentPageLabel, Is.EqualTo("1 / 2"));
        }

        [Test]
        public void TutorialCardPresenter_ContinueAdvancesThenDismisses()
        {
            TutorialCardPresenter presenter = CreatePresenter();
            int dismissCount = 0;
            presenter.Dismissed += () => dismissCount++;

            presenter.ShowDeck(CreateDeck());
            presenter.Continue();

            Assert.That(presenter.IsVisible, Is.True);
            Assert.That(presenter.CurrentCardIndex, Is.EqualTo(1));
            Assert.That(presenter.CurrentTitle, Is.EqualTo("SECOND TITLE"));
            Assert.That(presenter.CurrentPageLabel, Is.EqualTo("2 / 2"));

            presenter.Continue();

            Assert.That(presenter.IsVisible, Is.False);
            Assert.That(dismissCount, Is.EqualTo(1));
        }

        [Test]
        public void TutorialCardPresenter_MissingImageUsesPlaceholder()
        {
            TutorialCardPresenter presenter = CreatePresenter();
            TutorialDeck deck = new TutorialDeck
            {
                levelId = "LX",
                showTiming = "pre",
                cards = new[]
                {
                    new TutorialCard
                    {
                        header = "LX TUTORIAL",
                        title = "MISSING IMAGE",
                        body = "The card should still render.",
                        imageResourcePath = "Rescue.Unity/UI/Tutorial/does_not_exist",
                    },
                },
            };

            presenter.ShowDeck(deck);

            Assert.That(presenter.CurrentImageTexture, Is.Not.Null);
            Assert.That(presenter.CurrentImageTexture!.name, Is.EqualTo("blank_placeholder"));
        }

        [Test]
        public void TutorialCardPresenter_LayoutKeepsImageAndControlsOutOfText()
        {
            TutorialCardPresenter presenter = CreatePresenter();
            foreach (TutorialDeck deck in TutorialDeckRegistry.Decks)
            {
                presenter.ShowDeck(deck);
                for (int i = 0; i < deck.cards.Length; i++)
                {
                    if (i > 0)
                    {
                        presenter.Continue();
                    }

                    Rect image = GetRect("TutorialOverlay/TutorialPanel/TutorialImageArea");
                    Rect title = GetRect("TutorialOverlay/TutorialPanel/TutorialTitleLabel");
                    Rect page = GetRect("TutorialOverlay/TutorialPanel/TutorialPageLabel");
                    Rect body = GetRect("TutorialOverlay/TutorialPanel/TutorialBodyLabel");
                    Rect button = GetRect("TutorialOverlay/TutorialPanel/TutorialContinueButton");
                    Rect indicators = GetRect("TutorialOverlay/TutorialPanel/TutorialIndicators");

                    Assert.That(
                        image.yMin,
                        Is.GreaterThanOrEqualTo(title.yMax + 16f),
                        $"{deck.levelId} card {i + 1}: image frame should not cover title or page text.");
                    Assert.That(
                        image.yMin,
                        Is.GreaterThanOrEqualTo(page.yMax + 16f),
                        $"{deck.levelId} card {i + 1}: image frame should not cover page count.");
                    Assert.That(
                        image.yMin,
                        Is.GreaterThanOrEqualTo(body.yMax + 16f),
                        $"{deck.levelId} card {i + 1}: image frame should not cover body text.");
                    Assert.That(
                        body.yMin,
                        Is.GreaterThanOrEqualTo(button.yMax + 16f),
                        $"{deck.levelId} card {i + 1}: continue button should not cover body text.");
                    Assert.That(
                        button.yMin,
                        Is.GreaterThanOrEqualTo(indicators.yMax + 8f),
                        $"{deck.levelId} card {i + 1}: indicators should not sit under the continue button.");
                }
            }
        }

        [Test]
        public void TutorialCardPresenter_ImageBleedsUnderFrameAndFrameDrawsOnTop()
        {
            TutorialCardPresenter presenter = CreatePresenter();
            foreach (TutorialDeck deck in TutorialDeckRegistry.Decks)
            {
                presenter.ShowDeck(deck);
                for (int i = 0; i < deck.cards.Length; i++)
                {
                    if (i > 0)
                    {
                        presenter.Continue();
                    }

                    Rect image = GetRect("TutorialOverlay/TutorialPanel/TutorialImageArea/TutorialImage");
                    Rect frame = GetRect("TutorialOverlay/TutorialPanel/TutorialImageArea/TutorialCardFrame");
                    Transform imageTransform = GetTransform("TutorialOverlay/TutorialPanel/TutorialImageArea/TutorialImage");
                    Transform frameTransform = GetTransform("TutorialOverlay/TutorialPanel/TutorialImageArea/TutorialCardFrame");

                    Assert.That(
                        image.xMin,
                        Is.LessThanOrEqualTo(frame.xMin + 12f),
                        $"{deck.levelId} card {i + 1}: image should bleed under the left frame edge.");
                    Assert.That(
                        image.xMax,
                        Is.GreaterThanOrEqualTo(frame.xMax - 12f),
                        $"{deck.levelId} card {i + 1}: image should bleed under the right frame edge.");
                    Assert.That(
                        image.yMin,
                        Is.LessThanOrEqualTo(frame.yMin + 12f),
                        $"{deck.levelId} card {i + 1}: image should bleed under the bottom frame edge.");
                    Assert.That(
                        image.yMax,
                        Is.GreaterThanOrEqualTo(frame.yMax - 12f),
                        $"{deck.levelId} card {i + 1}: image should bleed under the top frame edge.");
                    Assert.That(
                        frameTransform.GetSiblingIndex(),
                        Is.GreaterThan(imageTransform.GetSiblingIndex()),
                        $"{deck.levelId} card {i + 1}: frame should draw over the image.");
                }
            }
        }

        private TutorialCardPresenter CreatePresenter()
        {
            root = new GameObject("TutorialCardPresenterTest");
            return root.AddComponent<TutorialCardPresenter>();
        }

        private Rect GetRect(string path)
        {
            Transform transform = GetTransform(path);
            RectTransform rect = (RectTransform)transform!;
            Vector3[] corners = new Vector3[4];
            rect.GetLocalCorners(corners);
            Vector3 localPosition = rect.localPosition;
            float xMin = localPosition.x + corners[0].x;
            float yMin = localPosition.y + corners[0].y;
            float xMax = localPosition.x + corners[2].x;
            float yMax = localPosition.y + corners[2].y;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private Transform GetTransform(string path)
        {
            Assert.That(root, Is.Not.Null);
            Transform? transform = root!.transform.Find(path);
            Assert.That(transform, Is.Not.Null, $"Expected to find '{path}'.");
            return transform!;
        }

        private static TutorialDeck CreateDeck()
        {
            return new TutorialDeck
            {
                levelId = "LX",
                showTiming = "pre",
                cards = new[]
                {
                    new TutorialCard
                    {
                        header = "LX TUTORIAL",
                        title = "FIRST TITLE",
                        body = "First body.",
                        imageResourcePath = "Rescue.Unity/UI/Tutorial/blank_placeholder",
                    },
                    new TutorialCard
                    {
                        header = "LX TUTORIAL",
                        title = "SECOND TITLE",
                        body = "Second body.",
                        imageResourcePath = "Rescue.Unity/UI/Tutorial/blank_placeholder",
                    },
                },
            };
        }
    }
}
