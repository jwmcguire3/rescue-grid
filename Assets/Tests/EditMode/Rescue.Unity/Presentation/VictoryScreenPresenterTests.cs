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
        public void VictoryScreenPresenter_ProvidesRescueFramingAndAftercareCopy()
        {
            VictoryScreenPresenter presenter = CreatePresenter();

            presenter.Show();

            Assert.That(presenter.HeadlineText, Is.EqualTo("Rescue complete"));
            Assert.That(presenter.AftercareText, Does.Contain("Aftercare"));
            Assert.That(presenter.AftercareText, Does.Contain("kennel"));
        }

        private VictoryScreenPresenter CreatePresenter()
        {
            root = new GameObject("VictoryScreenTest");
            root.AddComponent<UIDocument>();
            return root.AddComponent<VictoryScreenPresenter>();
        }
    }
}
