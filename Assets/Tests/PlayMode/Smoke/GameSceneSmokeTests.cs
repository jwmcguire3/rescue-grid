#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Reflection;
using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.Audio;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Debugging;
using Rescue.Unity.Feedback;
using Rescue.Unity.FX;
using Rescue.Unity.Input;
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
            PlayerPrefs.DeleteKey(AudioSettingsController.MusicVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.FxVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsEnabledPrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsStrengthPrefsKey);
            PlayerPrefs.Save();

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
            PlayerPrefs.DeleteKey(AudioSettingsController.MusicVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.FxVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsEnabledPrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsStrengthPrefsKey);
            PlayerPrefs.Save();

            if (DebugPanel.Instance is not null)
            {
                UnityObject.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator GameScene_SettingsMenuRestartAndLevelSelectDrivePlayerFlow()
        {
            PlayableLevelSession session = FindRequired<PlayableLevelSession>();
            SettingsMenuPresenter settings = FindRequired<SettingsMenuPresenter>();
            SettingsMenuView settingsView = FindRequired<SettingsMenuView>();
            Assert.That(settings.View, Is.SameAs(settingsView));
            Assert.That(settingsView.RestartButton, Is.Not.Null);
            Assert.That(settingsView.SettingsButton, Is.Not.Null);
            Assert.That(settingsView.ShowTutorialButton, Is.Not.Null);

            Assert.That(settings.IsOpen, Is.False);
            settings.Toggle();
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(settings.LevelChoices, Has.Count.EqualTo(PlayableLevelSession.LevelIds.Count));
            for (int i = 0; i < PlayableLevelSession.LevelIds.Count; i++)
            {
                Assert.That(settings.LevelChoices[i], Does.StartWith(PlayableLevelSession.LevelIds[i]));
            }

            Assert.That(session.TryRunAction(new TileCoord(4, 0)), Is.True);
            yield return WaitForPlayback();
            GameState currentState = session.CurrentState ?? throw new AssertionException("Action did not leave a state.");
            Assert.That(currentState.ActionCount, Is.GreaterThan(0));

            settings.RequestRestart();
            yield return null;
            currentState = session.CurrentState ?? throw new AssertionException("Restart did not reload a state.");
            Assert.That(session.CurrentLevelId, Is.EqualTo("L00"));
            Assert.That(currentState.ActionCount, Is.EqualTo(0));
            Assert.That(settings.IsOpen, Is.False, "Restart should close settings.");

            settings.SetOpen(true);
            settings.SelectLevel(settings.LevelChoices[3]);
            yield return null;
            currentState = session.CurrentState ?? throw new AssertionException("Level select did not load a state.");
            Assert.That(session.CurrentLevelId, Is.EqualTo("L03"));
            Assert.That(currentState.ActionCount, Is.EqualTo(0));
            Assert.That(settings.IsOpen, Is.False, "Level selection should close settings.");
        }

        [UnityTest]
        public System.Collections.IEnumerator GameScene_SettingsAudioPersistsAndKeepsMusicAndFxSeparate()
        {
            SettingsMenuPresenter settings = FindRequired<SettingsMenuPresenter>();
            AudioSettingsController audioSettings = FindRequired<AudioSettingsController>();
            AudioEventRouter audioRouter = FindRequired<AudioEventRouter>();
            MusicPlayer musicPlayer = FindRequired<MusicPlayer>();

            settings.SetOpen(true);
            audioSettings.SetMusicVolume(0.25f);
            audioSettings.SetFxVolume(0.6f);
            settings.SetHapticsEnabled(false);
            Assert.That(PlayerPrefs.GetFloat(AudioSettingsController.MusicVolumePrefsKey), Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(PlayerPrefs.GetFloat(AudioSettingsController.FxVolumePrefsKey), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(PlayerPrefs.GetInt(AudioSettingsController.HapticsEnabledPrefsKey), Is.EqualTo(0));
            settings.SetMusicMuted(true);
            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.0f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(audioSettings.HapticsEnabled, Is.False);
            settings.SetMusicMuted(false);
            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(audioSettings.HapticsEnabled, Is.False);
            settings.SetFxMuted(true);
            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0.0f).Within(0.001f));
            Assert.That(audioSettings.HapticsEnabled, Is.False);
            settings.SetFxMuted(false);
            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(audioSettings.HapticsEnabled, Is.False);
            Assert.That(audioRouter.AudioSource, Is.Not.Null, "Game.unity should provide an AudioSource for routed feedback.");
            Assert.That(musicPlayer.AudioSource, Is.Not.Null, "Game.unity should provide a dedicated AudioSource for music.");
            Assert.That(musicPlayer.AudioSource, Is.Not.SameAs(audioRouter.AudioSource));

            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            yield return null;

            AudioSettingsController reloadedSettings = FindRequired<AudioSettingsController>();
            Assert.That(reloadedSettings.MusicVolume, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(reloadedSettings.FxVolume, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(reloadedSettings.HapticsEnabled, Is.False);
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
        public System.Collections.IEnumerator GameScene_L00IntroImageBlocksInputUntilDismissedAndReturnsOnReload()
        {
            PlayableLevelSession session = FindRequired<PlayableLevelSession>();
            BoardInputPresenter boardInput = FindRequired<BoardInputPresenter>();
            L00IntroImagePresenter intro = FindRequired<L00IntroImagePresenter>();
            SettingsMenuPresenter settings = FindRequired<SettingsMenuPresenter>();

            Assert.That(session.CurrentLevelId, Is.EqualTo("L00"));
            Assert.That(intro.IsVisible, Is.True, "L00 should show the intro image before play.");
            Assert.That(boardInput.IsInputBlocked, Is.True, "L00 intro should block board input while visible.");

            GameState initialState = session.CurrentState ?? throw new AssertionException("Game scene did not load L00.");
            Assert.That(boardInput.TryRunActionAt(new TileCoord(4, 0)), Is.False, "The first board tap should be consumed by the intro gate.");
            Assert.That(session.CurrentState?.ActionCount, Is.EqualTo(initialState.ActionCount));

            intro.Dismiss();
            yield return null;

            Assert.That(intro.IsVisible, Is.False);
            Assert.That(boardInput.IsInputBlocked, Is.False);
            Assert.That(boardInput.TryRunActionAt(new TileCoord(4, 0)), Is.True, "After dismissing the intro, L00 should accept normal board input.");
            yield return WaitForPlayback();
            Assert.That(session.CurrentState?.ActionCount, Is.EqualTo(1));

            session.LoadLevel("L00", session.Seed);
            yield return null;
            Assert.That(intro.IsVisible, Is.True, "Reloading L00 should show the intro again.");
            Assert.That(boardInput.IsInputBlocked, Is.True);

            session.LoadLevel("L01", session.Seed);
            yield return null;
            Assert.That(intro.IsVisible, Is.False, "Loading a non-L00 level should hide the intro.");
            Assert.That(boardInput.IsInputBlocked, Is.False);

            settings.SetOpen(true);
            settings.RequestShowTutorial();
            yield return null;

            Assert.That(settings.IsOpen, Is.False, "Show Tutorial should close settings before displaying the image.");
            Assert.That(intro.IsVisible, Is.True, "Show Tutorial should reuse the L00 intro image overlay.");
            Assert.That(boardInput.IsInputBlocked, Is.True, "Show Tutorial should block board input until dismissed.");

            intro.Dismiss();
            yield return null;

            Assert.That(intro.IsVisible, Is.False);
            Assert.That(boardInput.IsInputBlocked, Is.False);
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
            Assert.That(Vector3.Distance(stageRoot.localPosition, PortraitGameSceneLayout.BoardPortraitPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(stageRoot.localRotation, Quaternion.identity), Is.LessThan(0.1f), "BoardStageRoot should match the screenshot rotation.");
            Assert.That(Vector3.Distance(stageRoot.localScale, PortraitGameSceneLayout.BoardPortraitScale), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(dockRoot.localPosition, PortraitGameSceneLayout.DockPortraitPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(dockRoot.localRotation, PortraitGameSceneLayout.DockPortraitRotation), Is.LessThan(0.1f), "DockRoot should match the portrait staged dock tilt.");
            Assert.That(Vector3.Distance(dockRoot.localScale, PortraitGameSceneLayout.DockPortraitScale), Is.LessThan(0.001f));
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
            Assert.That(Quaternion.Angle(light.transform.localRotation, Quaternion.Euler(60f, 60f, 0f)), Is.LessThan(0.1f));
            Assert.That(Vector3.Distance(light.transform.localScale, Vector3.one), Is.LessThan(0.001f));
            Assert.That(light.color.r, Is.EqualTo(1f).Within(0.001f));
            Assert.That(light.color.g, Is.EqualTo(217f / 255f).Within(0.001f));
            Assert.That(light.color.b, Is.EqualTo(173f / 255f).Within(0.001f));
            Assert.That(light.intensity, Is.EqualTo(1f).Within(0.001f));
            Assert.That(light.bounceIntensity, Is.EqualTo(1f).Within(0.001f));
            Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));
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
