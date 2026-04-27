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

        private LossScreenPresenter CreatePresenter()
        {
            root = new GameObject("LossScreenTest");
            root.AddComponent<UIDocument>();
            return root.AddComponent<LossScreenPresenter>();
        }
    }
}
