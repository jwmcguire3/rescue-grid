using NUnit.Framework;
using Rescue.Unity.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class VictoryScreenPresenterTests
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
        public void VictoryScreenPresenter_ShowHideTracksVisibility()
        {
            VictoryScreenPresenter presenter = CreatePresenter();

            presenter.Show();
            Assert.That(presenter.IsVisible, Is.True);

            presenter.Hide();
            Assert.That(presenter.IsVisible, Is.False);
        }

        [Test]
        public void VictoryScreenPresenter_ButtonsRaiseRequests()
        {
            VictoryScreenPresenter presenter = CreatePresenter();
            int replayCount = 0;
            int nextCount = 0;
            presenter.ReplayRequested += () => replayCount++;
            presenter.NextLevelRequested += () => nextCount++;

            presenter.RequestReplay();
            presenter.RequestNextLevel();

            Assert.That(replayCount, Is.EqualTo(1));
            Assert.That(nextCount, Is.EqualTo(1));
        }

        [Test]
        public void VictoryScreenPresenter_DisabledNextSuppressesRequest()
        {
            VictoryScreenPresenter presenter = CreatePresenter();
            int nextCount = 0;
            presenter.NextLevelRequested += () => nextCount++;

            presenter.SetNextLevelAvailable(false);
            presenter.RequestNextLevel();

            Assert.That(presenter.IsNextLevelAvailable, Is.False);
            Assert.That(nextCount, Is.EqualTo(0));
        }

        [Test]
        public void VictoryScreenPresenter_UsesPngOnlyWithoutGeneratedCopyLayers()
        {
            VictoryScreenPresenter presenter = CreatePresenter();

            presenter.Show();

            VisualElement rootElement = root!.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(rootElement.Q<VisualElement>("victory-frame"), Is.Not.Null);
            Assert.That(rootElement.Q<Label>("victory-headline-label"), Is.Null);
            Assert.That(rootElement.Q<Label>("victory-framing-label"), Is.Null);
            Assert.That(rootElement.Q<Label>("victory-aftercare-card"), Is.Null);
        }

        [Test]
        public void VictoryScreenPresenter_KeepsReplayAndNextLevelHitZones()
        {
            VictoryScreenPresenter presenter = CreatePresenter();

            presenter.Show();

            VisualElement rootElement = root!.GetComponent<UIDocument>().rootVisualElement;
            VisualElement? frame = rootElement.Q<VisualElement>("victory-frame");
            Button? replayButton = frame?.Q<Button>("victory-replay-button");
            Button? nextLevelButton = frame?.Q<Button>("victory-next-level-button");

            Assert.That(frame, Is.Not.Null);
            Assert.That(rootElement.Q<Image>("victory-screen-image"), Is.Not.Null);
            Assert.That(replayButton, Is.Not.Null);
            Assert.That(nextLevelButton, Is.Not.Null);
        }

        private VictoryScreenPresenter CreatePresenter()
        {
            root = new GameObject("VictoryScreenTest");
            root.AddComponent<UIDocument>();
            return root.AddComponent<VictoryScreenPresenter>();
        }
    }
}
