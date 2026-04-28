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
            Transform boardRoot = GameObject.Find("BoardRoot").transform;
            Transform boardContentRoot = GameObject.Find("BoardContentRoot").transform;
            Transform waterRoot = GameObject.Find("WaterRoot").transform;
            Transform dockRoot = GameObject.Find("DockRoot").transform;
            Assert.That(boardRoot.childCount, Is.GreaterThan(0));
            Assert.That(boardContentRoot.childCount, Is.GreaterThan(0));
            Assert.That(waterRoot.childCount, Is.GreaterThan(0));
            AssertCameraUsesFrontTableOrthographicView();
            AssertBoardStageLayout(boardRoot, boardContentRoot, waterRoot, dockRoot);

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

        private static void AssertCameraUsesFrontTableOrthographicView()
        {
            Camera? camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "Game.unity should include a tagged Main Camera.");
            if (camera is null)
            {
                throw new AssertionException("Game.unity should include a tagged Main Camera.");
            }

            Vector3 forward = camera.transform.forward;
            float horizontalForward = new Vector2(forward.x, forward.z).magnitude;
            Assert.That(camera.orthographic, Is.True, "Game camera should stay orthographic for grid readability.");
            Assert.That(horizontalForward, Is.GreaterThan(0.1f), "Game camera should have a table-facing horizontal component instead of top-down.");
            Assert.That(forward.y, Is.LessThan(-0.1f), "Game camera should look down toward the table.");
            Assert.That(Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(90f, 0f, 0f)), Is.GreaterThan(1f));
        }

        private static void AssertBoardStageLayout(Transform boardRoot, Transform boardContentRoot, Transform waterRoot, Transform dockRoot)
        {
            Transform? stageRoot = boardRoot.parent;
            Assert.That(stageRoot, Is.Not.Null, "BoardRoot should be parented under BoardStageRoot.");
            if (stageRoot is null)
            {
                throw new AssertionException("BoardRoot should be parented under BoardStageRoot.");
            }

            Assert.That(stageRoot.name, Is.EqualTo("BoardStageRoot"));
            Assert.That(boardContentRoot.parent, Is.SameAs(stageRoot), "Board content should share the board stage transform.");
            Assert.That(waterRoot.parent, Is.SameAs(stageRoot), "Water overlays should share the board stage transform.");
            Assert.That(dockRoot.parent, Is.Not.SameAs(stageRoot), "DockRoot should stay separate so its staging can be tuned independently.");
            Assert.That(Mathf.Abs(stageRoot.localEulerAngles.x), Is.GreaterThan(1f), "Board stage should carry the table-view tilt.");
            Assert.That(Quaternion.Angle(dockRoot.localRotation, Quaternion.Euler(15f, 0f, 0f)), Is.LessThan(0.1f), "DockRoot should match the staged dock tilt.");
        }
    }
}
#endif
