using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Unity.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class LossScreenPresenterTests
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
        public void LossScreenPresenter_ShowHideTracksVisibility()
        {
            LossScreenPresenter presenter = CreatePresenter();

            presenter.Show();
            Assert.That(presenter.IsVisible, Is.True);

            presenter.Hide();
            Assert.That(presenter.IsVisible, Is.False);
        }

        [Test]
        public void LossScreenPresenter_ButtonsRaiseRequests()
        {
            LossScreenPresenter presenter = CreatePresenter();
            int replayCount = 0;
            int tryAgainCount = 0;
            presenter.ReplayRequested += () => replayCount++;
            presenter.TryAgainRequested += () => tryAgainCount++;

            presenter.RequestReplay();
            presenter.RequestTryAgain();

            Assert.That(replayCount, Is.EqualTo(1));
            Assert.That(tryAgainCount, Is.EqualTo(1));
        }

        [TestCase(ActionOutcome.LossDockOverflow, "Dock overflow.")]
        [TestCase(ActionOutcome.LossWaterOnTarget, "Water reached a puppy.")]
        [TestCase(ActionOutcome.LossDistressedExpired, "Distressed puppy was not rescued in time.")]
        public void LossScreenPresenter_ShowUsesOneSpecificReason(ActionOutcome outcome, string expected)
        {
            LossScreenPresenter presenter = CreatePresenter();

            presenter.Show(outcome);

            Assert.That(presenter.ExplanationText, Is.EqualTo(expected));
        }

        [Test]
        public void LossScreenPresenter_UsesPngOnlyWithoutGeneratedCopyLayers()
        {
            LossScreenPresenter presenter = CreatePresenter();

            presenter.Show(ActionOutcome.LossWaterOnTarget);

            VisualElement rootElement = root!.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(rootElement.Q<VisualElement>("loss-frame"), Is.Not.Null);
            Assert.That(rootElement.Q<Label>("loss-headline-label"), Is.Null);
            Assert.That(rootElement.Q<Label>("loss-explanation-label"), Is.Null);
        }

        [Test]
        public void LossScreenPresenter_KeepsReplayAndTryAgainHitZones()
        {
            LossScreenPresenter presenter = CreatePresenter();

            presenter.Show();

            VisualElement rootElement = root!.GetComponent<UIDocument>().rootVisualElement;
            VisualElement? frame = rootElement.Q<VisualElement>("loss-frame");
            Button? replayButton = frame?.Q<Button>("loss-replay-button");
            Button? tryAgainButton = frame?.Q<Button>("loss-try-again-button");

            Assert.That(frame, Is.Not.Null);
            Assert.That(rootElement.Q<Image>("loss-screen-image"), Is.Not.Null);
            Assert.That(replayButton, Is.Not.Null);
            Assert.That(tryAgainButton, Is.Not.Null);
        }

        private LossScreenPresenter CreatePresenter()
        {
            root = new GameObject("LossScreenTest");
            root.AddComponent<UIDocument>();
            return root.AddComponent<LossScreenPresenter>();
        }
    }
}
