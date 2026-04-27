#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.Debugging;
using Rescue.Unity.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;

namespace Rescue.PlayMode.Tests.Smoke
{
    public sealed class GameSceneSmokeTests
    {
        [UnitySetUp]
        public System.Collections.IEnumerator SetUp()
        {
            if (DebugPanel.Instance is not null)
            {
                UnityObject.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public System.Collections.IEnumerator TearDown()
        {
            if (DebugPanel.Instance is not null)
            {
                UnityObject.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator GameScene_LoadsL00WithoutDebugPanelDominating()
        {
            PlayableLevelSession session = FindRequired<PlayableLevelSession>();
            GameStateViewPresenter presenter = FindRequired<GameStateViewPresenter>();

            Assert.That(DebugPanel.Instance, Is.Null, "Game.unity should not auto-spawn the debug panel.");
            Assert.That(session.CurrentLevelId, Is.EqualTo("L00"));
            Assert.That(presenter.CurrentState, Is.Not.Null);
            GameState currentState = presenter.CurrentState ?? throw new AssertionException("Game scene did not load a state.");
            Assert.That(currentState.LevelConfig.IsRuleTeach, Is.True);
            Assert.That(GameObject.Find("BoardRoot").transform.childCount, Is.GreaterThan(0));
            Assert.That(GameObject.Find("BoardContentRoot").transform.childCount, Is.GreaterThan(0));
            Assert.That(GameObject.Find("WaterRoot").transform.childCount, Is.GreaterThan(0));

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator GameScene_L00WinNextLevelAndRetryButtonsDrivePlayerFlow()
        {
            PlayableLevelSession session = FindRequired<PlayableLevelSession>();
            VictoryScreenPresenter victoryScreen = VictoryScreenPresenter.EnsureInstance();
            LossScreenPresenter lossScreen = LossScreenPresenter.EnsureInstance();

            Assert.That(session.TryRunAction(new TileCoord(4, 0)), Is.True);
            yield return WaitForPlayback();
            Assert.That(session.TryRunAction(new TileCoord(1, 2)), Is.True);
            yield return WaitForPlayback();

            Assert.That(victoryScreen.IsVisible, Is.True, "L00 expected path should reach the victory screen.");
            victoryScreen.RequestNextLevel();
            yield return null;

            Assert.That(session.CurrentLevelId, Is.EqualTo("L01"));
            Assert.That(session.CurrentState, Is.Not.Null);
            GameState currentState = session.CurrentState ?? throw new AssertionException("Next level did not load a state.");
            Assert.That(currentState.ActionCount, Is.EqualTo(0));

            SolveScriptJson l01Solve = SmokeTestHarness.LoadSolve("L01");
            Assert.That(session.TryRunAction(new TileCoord(l01Solve.Actions[0].Row, l01Solve.Actions[0].Col)), Is.True);
            yield return WaitForPlayback();
            currentState = session.CurrentState ?? throw new AssertionException("Action did not leave a state.");
            Assert.That(currentState.ActionCount, Is.GreaterThan(0));

            lossScreen.Show();
            lossScreen.RequestTryAgain();
            yield return null;

            Assert.That(session.CurrentLevelId, Is.EqualTo("L01"));
            Assert.That(session.CurrentState, Is.Not.Null);
            currentState = session.CurrentState ?? throw new AssertionException("Retry did not reload a state.");
            Assert.That(currentState.ActionCount, Is.EqualTo(0));
            Assert.That(lossScreen.IsVisible, Is.False);
        }

        private static System.Collections.IEnumerator WaitForPlayback()
        {
            GameStateViewPresenter presenter = FindRequired<GameStateViewPresenter>();
            float deadline = Time.realtimeSinceStartup + 5.0f;
            while (presenter.IsPlaybackActive && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(presenter.IsPlaybackActive, Is.False, "Playback should finish inside the smoke-test timeout.");
        }

        private static T FindRequired<T>()
            where T : UnityObject
        {
            T? value = UnityObject.FindAnyObjectByType<T>();
            Assert.That(value, Is.Not.Null, $"Expected Game.unity to include {typeof(T).Name}.");
            if (value is null)
            {
                throw new AssertionException($"Expected Game.unity to include {typeof(T).Name}.");
            }

            return value;
        }
    }
}
#endif
