#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Reflection;
using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Debugging;
using Rescue.Unity.FX;
using Rescue.Unity.Presentation;
using Rescue.Unity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
            AssertDirectionalLightMatchesStaging();

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator GameScene_HasFxPlaybackWiring()
        {
            GameStateViewPresenter gameStateView = FindRequired<GameStateViewPresenter>();
            ActionPlaybackController playbackController = FindRequired<ActionPlaybackController>();
            FxEventRouter fxEventRouter = FindRequired<FxEventRouter>();
            BoardGridViewPresenter boardGrid = FindRequired<BoardGridViewPresenter>();

            Assert.That(
                GetSerializedReference<GameStateViewPresenter, ActionPlaybackController>(gameStateView, "playbackController")
                    ?? gameStateView.GetComponent<ActionPlaybackController>(),
                Is.SameAs(playbackController),
                "GameStateViewPresenter should use the scene ActionPlaybackController so FX route through playback beats.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, BoardGridViewPresenter>(playbackController, "boardGrid"),
                Is.SameAs(boardGrid),
                "ActionPlaybackController should have the board grid presenter assigned for FX beat positioning.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, BoardContentViewPresenter>(playbackController, "boardContent"),
                Is.SameAs(FindRequired<BoardContentViewPresenter>()),
                "ActionPlaybackController should have board content assigned for playback beats.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, WaterViewPresenter>(playbackController, "waterView"),
                Is.SameAs(FindRequired<WaterViewPresenter>()),
                "ActionPlaybackController should have water presentation assigned for water FX beats.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, DockViewPresenter>(playbackController, "dockView"),
                Is.SameAs(FindRequired<DockViewPresenter>()),
                "ActionPlaybackController should have dock presentation assigned for dock FX beats.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, FxEventRouter>(playbackController, "fxEventRouter"),
                Is.SameAs(fxEventRouter),
                "ActionPlaybackController should route playback beats through the scene FxEventRouter.");

            FxVisualRegistry? fxRegistry = fxEventRouter.FxRegistry;
            Assert.That(fxRegistry, Is.Not.Null, "Game.unity should assign the Phase 1 FX registry asset.");
            if (fxRegistry is null)
            {
                throw new AssertionException("Game.unity should assign the Phase 1 FX registry asset.");
            }

            Assert.That(fxRegistry.WinFx, Is.Not.Null, "Game.unity should have terminal win FX assigned.");
            Assert.That(fxRegistry.LossFx, Is.Not.Null, "Game.unity should have terminal loss FX assigned.");
            Assert.That(fxEventRouter.BoardGrid, Is.SameAs(boardGrid), "FxEventRouter should resolve cell and row positions through the scene board grid presenter.");
            Assert.That(
                fxEventRouter.DockView,
                Is.SameAs(FindRequired<DockViewPresenter>()),
                "FxEventRouter should resolve dock-owned FX through the scene dock presenter.");
            Assert.That(fxEventRouter.FxRoot, Is.Not.Null, "Game.unity should assign an FX root for runtime-spawned FX instances.");

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator GameScene_L00ActionSpawnsRuntimeFxUnderFxRoot()
        {
            PlayableLevelSession session = FindRequired<PlayableLevelSession>();
            GameStateViewPresenter presenter = FindRequired<GameStateViewPresenter>();
            FxEventRouter fxEventRouter = FindRequired<FxEventRouter>();
            Transform fxRoot = fxEventRouter.FxRoot ?? fxEventRouter.transform;
            int childCountBefore = fxRoot.childCount;

            Assert.That(session.CurrentLevelId, Is.EqualTo("L00"));
            Assert.That(session.TryRunAction(new TileCoord(4, 0)), Is.True);

            GameObject? spawnedFx = null;
            float deadline = Time.realtimeSinceStartup + 1.0f;
            while (spawnedFx is null && Time.realtimeSinceStartup < deadline)
            {
                spawnedFx = FindChildByName(fxRoot, nameof(FxVisualRegistry.GroupClearFx));
                if (spawnedFx is null)
                {
                    yield return null;
                }
            }

            Assert.That(spawnedFx, Is.Not.Null, "A valid L00 action should spawn the group-clear FX prefab during playback.");
            if (spawnedFx is null)
            {
                throw new AssertionException("Expected a spawned group-clear FX instance.");
            }

            Assert.That(spawnedFx.GetComponent<SpriteSequenceFxPlayer>(), Is.Not.Null, "Spawned FX should use the sprite sequence player.");
            Assert.That(spawnedFx.transform.parent, Is.SameAs(fxRoot), "Runtime FX should spawn under the scene FX root.");
            Assert.That(fxRoot.childCount, Is.GreaterThan(childCountBefore), "FX root should contain a runtime FX instance before it auto-destroys.");

            yield return WaitForPlayback();
            GameState? stateAfterAction = presenter.CurrentState;
            Assert.That(stateAfterAction, Is.Not.Null);
            if (stateAfterAction is not null)
            {
                Assert.That(stateAfterAction.ActionCount, Is.EqualTo(1), "FX playback should not prevent the action from reaching final state sync.");
            }
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

        private static GameObject? FindChildByName(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static TReference? GetSerializedReference<TOwner, TReference>(TOwner owner, string fieldName)
            where TOwner : class
            where TReference : class
        {
            return GetPrivateField<TOwner, TReference>(owner, fieldName);
        }

        private static TValue? GetPrivateField<TOwner, TValue>(TOwner owner, string fieldName)
            where TOwner : class
            where TValue : class
        {
            FieldInfo? field = typeof(TOwner).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' on {typeof(TOwner).Name}.");
            if (field is null)
            {
                return null;
            }

            object? value = field.GetValue(owner);
            Assert.That(
                value is null || value is TValue,
                Is.True,
                $"Expected private field '{fieldName}' on {typeof(TOwner).Name} to hold {typeof(TValue).Name}.");
            return value as TValue;
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
            Assert.That(Vector3.Distance(stageRoot.localPosition, new Vector3(0f, -0.28f, -2.4f)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(stageRoot.localRotation, Quaternion.identity), Is.LessThan(0.1f), "BoardStageRoot should match the screenshot rotation.");
            Assert.That(Vector3.Distance(stageRoot.localScale, new Vector3(1.4f, 1.4f, 1.4f)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(dockRoot.localPosition, new Vector3(0f, -0.5f, -10.5f)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(dockRoot.localRotation, Quaternion.Euler(15f, 0f, 0f)), Is.LessThan(0.1f), "DockRoot should match the staged dock tilt.");
            Assert.That(Vector3.Distance(dockRoot.localScale, new Vector3(1.8f, 1.8f, 1.8f)), Is.LessThan(0.001f));
        }

        private static void AssertDirectionalLightMatchesStaging()
        {
            Light[] lights = UnityObject.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light? light = System.Array.Find(lights, candidate => candidate.name == "Directional Light");
            Assert.That(light, Is.Not.Null, "Game.unity should include the staged Directional Light.");
            if (light is null)
            {
                throw new AssertionException("Game.unity should include the staged Directional Light.");
            }

            Assert.That(light.type, Is.EqualTo(LightType.Directional));
            Assert.That(Vector3.Distance(light.transform.localPosition, new Vector3(0f, 3f, 0f)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(light.transform.localRotation, Quaternion.Euler(50f, 120f, 0f)), Is.LessThan(0.1f));
            Assert.That(Vector3.Distance(light.transform.localScale, Vector3.one), Is.LessThan(0.001f));
            Assert.That(light.color.r, Is.EqualTo(101f / 255f).Within(0.001f));
            Assert.That(light.color.g, Is.EqualTo(54f / 255f).Within(0.001f));
            Assert.That(light.color.b, Is.EqualTo(0f).Within(0.001f));
            Assert.That(light.intensity, Is.EqualTo(1f).Within(0.001f));
            Assert.That(light.bounceIntensity, Is.EqualTo(1f).Within(0.001f));
            Assert.That(light.shadows, Is.EqualTo(LightShadows.Hard));
            Assert.That(light.cookie, Is.Null);
            Assert.That(light.flare, Is.Null);
            Assert.That(light.renderMode, Is.EqualTo(LightRenderMode.Auto));
            Assert.That(light.cullingMask, Is.EqualTo(-1));
            Assert.That(light.lightmapBakeType, Is.EqualTo(LightmapBakeType.Baked));
#if UNITY_EDITOR
            SerializedObject serializedLight = new SerializedObject(light);
            Assert.That(serializedLight.FindProperty("m_DrawHalo").boolValue, Is.False);
#endif
        }
    }
}
#endif
