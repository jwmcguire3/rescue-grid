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
            BoardInputPresenter boardInput = FindRequired<BoardInputPresenter>();
            SettingsMenuPresenter settings = FindRequired<SettingsMenuPresenter>();
            SettingsMenuView settingsView = FindRequired<SettingsMenuView>();
            TutorialCardPresenter tutorial = FindRequired<TutorialCardPresenter>();
            Assert.That(settings.View, Is.SameAs(settingsView));
            Assert.That(settingsView.RestartButton, Is.Not.Null);
            Assert.That(settingsView.SettingsButton, Is.Not.Null);
            Assert.That(settingsView.ShowTutorialButton, Is.Not.Null);

            Assert.That(settings.IsOpen, Is.False);
            settings.Toggle();
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(boardInput.IsInputBlocked, Is.True, "Opening settings should block board touches.");
            Assert.That(boardInput.TryRunActionAt(new TileCoord(4, 0)), Is.False, "Board input should be ignored while settings is open.");
            Assert.That(settings.LevelChoices, Has.Count.EqualTo(PlayableLevelSession.LevelIds.Count));
            for (int i = 0; i < PlayableLevelSession.LevelIds.Count; i++)
            {
                Assert.That(settings.LevelChoices[i], Does.StartWith(PlayableLevelSession.LevelIds[i]));
            }

            DismissAllTutorialCards(tutorial);
            yield return null;

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
            AssertBoardFitsGameplayViewport(boardRoot, currentState.Board.Width, currentState.Board.Height);
            AssertCameraRaysHitVisibleCells(boardRoot, currentState.Board.Width, currentState.Board.Height);
            AssertDirectionalLightMatchesStaging();

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator GameScene_L00RendersDaisyTargetPrefab()
        {
            GameStateViewPresenter presenter = FindRequired<GameStateViewPresenter>();
            BoardContentViewPresenter contentPresenter = FindRequired<BoardContentViewPresenter>();

            GameState currentState = presenter.CurrentState ?? throw new AssertionException("Game scene did not load a state.");

            DaisyTargetSceneAssertions.AssertLiveTargetsAreDaisyBacked(currentState, contentPresenter);
            LogAssert.NoUnexpectedReceived();

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator GameScene_TutorialCardsBlockInputUntilDismissedAndReturnOnReload()
        {
            PlayableLevelSession session = FindRequired<PlayableLevelSession>();
            BoardInputPresenter boardInput = FindRequired<BoardInputPresenter>();
            TutorialCardPresenter tutorial = FindRequired<TutorialCardPresenter>();
            SettingsMenuPresenter settings = FindRequired<SettingsMenuPresenter>();

            Assert.That(session.CurrentLevelId, Is.EqualTo("L00"));
            Assert.That(tutorial.IsVisible, Is.True, "L00 should show the tutorial cards before play.");
            Assert.That(tutorial.CurrentCardCount, Is.EqualTo(3));
            Assert.That(boardInput.IsInputBlocked, Is.True, "Tutorial cards should block board input while visible.");

            GameState initialState = session.CurrentState ?? throw new AssertionException("Game scene did not load L00.");
            Assert.That(boardInput.TryRunActionAt(new TileCoord(4, 0)), Is.False, "The first board tap should be consumed by the intro gate.");
            Assert.That(session.CurrentState?.ActionCount, Is.EqualTo(initialState.ActionCount));

            DismissAllTutorialCards(tutorial);
            Assert.That(boardInput.IsInputBlocked, Is.True, "Dismissing the tutorial should keep board input blocked for the dismissal frame.");
            Assert.That(boardInput.TryRunActionAt(new TileCoord(4, 0)), Is.False, "The dismissing tap should not pass through to the board.");
            yield return null;

            Assert.That(tutorial.IsVisible, Is.False);
            Assert.That(boardInput.IsInputBlocked, Is.False);
            Assert.That(boardInput.TryRunActionAt(new TileCoord(4, 0)), Is.True, "After dismissing the intro, L00 should accept normal board input.");
            yield return WaitForPlayback();
            Assert.That(session.CurrentState?.ActionCount, Is.EqualTo(1));

            session.LoadLevel("L00", session.Seed);
            yield return null;
            Assert.That(tutorial.IsVisible, Is.True, "Reloading L00 should show the tutorial again.");
            Assert.That(boardInput.IsInputBlocked, Is.True);

            session.LoadLevel("L01", session.Seed);
            yield return null;
            Assert.That(tutorial.IsVisible, Is.True, "Loading L01 should show its tutorial.");
            Assert.That(tutorial.CurrentCardCount, Is.EqualTo(2));
            Assert.That(boardInput.IsInputBlocked, Is.True);
            DismissAllTutorialCards(tutorial);
            yield return null;
            Assert.That(boardInput.IsInputBlocked, Is.False);

            session.LoadLevel("L05", session.Seed);
            yield return null;
            Assert.That(tutorial.IsVisible, Is.False, "Loading a level without tutorial cards should hide the tutorial.");
            Assert.That(boardInput.IsInputBlocked, Is.False);

            settings.SetOpen(true);
            settings.RequestShowTutorial();
            yield return null;

            Assert.That(settings.IsOpen, Is.False, "Show Tutorial should close settings before displaying the image.");
            Assert.That(tutorial.IsVisible, Is.False, "Show Tutorial on a level without cards should do nothing.");
            Assert.That(boardInput.IsInputBlocked, Is.False);

            session.LoadLevel("L01", session.Seed);
            yield return null;
            DismissAllTutorialCards(tutorial);
            yield return null;
            settings.SetOpen(true);
            settings.RequestShowTutorial();
            yield return null;

            Assert.That(tutorial.IsVisible, Is.True, "Show Tutorial should replay the current level tutorial.");
            Assert.That(boardInput.IsInputBlocked, Is.True, "Show Tutorial should block board input until dismissed.");
            DismissAllTutorialCards(tutorial);
            yield return null;

            Assert.That(tutorial.IsVisible, Is.False);
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
            DismissAllTutorialCards(FindRequired<TutorialCardPresenter>());
            yield return null;
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
            TutorialCardPresenter tutorial = FindRequired<TutorialCardPresenter>();

            DismissAllTutorialCards(tutorial);
            yield return null;
            Assert.That(session.TryRunAction(new TileCoord(4, 0)), Is.True);
            yield return WaitForPlayback();
            Assert.That(session.TryRunAction(new TileCoord(1, 2)), Is.True);
            yield return WaitForPlayback();

            Assert.That(victoryScreen.IsVisible, Is.True, "L00 expected path should reach the victory screen.");
            victoryScreen.RequestNextLevel();
            yield return null;

            Assert.That(session.CurrentLevelId, Is.EqualTo("L01"));
            Assert.That(tutorial.IsVisible, Is.True, "Loading L01 from the victory screen should show its tutorial.");
            DismissAllTutorialCards(tutorial);
            yield return null;
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

        [UnityTest]
        public System.Collections.IEnumerator GameScene_L10NextShowsPostWinTutorialBeforeAdvancing()
        {
            PlayableLevelSession session = FindRequired<PlayableLevelSession>();
            VictoryScreenPresenter victoryScreen = VictoryScreenPresenter.EnsureInstance();
            TutorialCardPresenter tutorial = FindRequired<TutorialCardPresenter>();

            session.LoadLevel("L10", session.Seed);
            yield return null;
            Assert.That(tutorial.IsVisible, Is.False, "L10 should not show pre-level cards.");

            victoryScreen.Show();
            victoryScreen.RequestNextLevel();
            yield return null;

            Assert.That(victoryScreen.IsVisible, Is.False);
            Assert.That(tutorial.IsVisible, Is.True, "L10 next should show the earned post-level card.");
            Assert.That(tutorial.CurrentTitle, Is.EqualTo("STILL STANDING"));
            Assert.That(session.CurrentLevelId, Is.EqualTo("L10"));

            DismissAllTutorialCards(tutorial);
            yield return null;

            Assert.That(session.CurrentLevelId, Is.EqualTo("L11"));
            Assert.That(tutorial.IsVisible, Is.False);
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

            string diagnostics = BuildCameraDiagnostics(camera);
            TestContext.Out.WriteLine(diagnostics);
            Assert.That(camera.name, Is.EqualTo("Main Camera"), diagnostics);
            Assert.That(camera.CompareTag("MainCamera"), Is.True, diagnostics);
            Assert.That(camera.enabled, Is.True, diagnostics);
            Assert.That(camera.gameObject.activeInHierarchy, Is.True, diagnostics);
            Assert.That(camera.targetDisplay, Is.EqualTo(0), diagnostics);
            Assert.That(camera.targetTexture, Is.Null, diagnostics);
            Assert.That(camera.orthographic, Is.True, $"Game camera should stay orthographic for grid readability.\n{diagnostics}");
            Assert.That(camera.orthographicSize, Is.EqualTo(PortraitGameSceneLayout.CameraPortraitOrthographicSize).Within(0.001f), diagnostics);
            Assert.That(Vector3.Distance(camera.transform.position, PortraitGameSceneLayout.CameraPortraitPosition), Is.LessThan(0.001f), diagnostics);
            Assert.That(Quaternion.Angle(camera.transform.rotation, PortraitGameSceneLayout.CameraPortraitRotation), Is.LessThan(0.1f), diagnostics);
            Assert.That(camera.transform.forward.y, Is.LessThan(-0.85f), $"Game camera should keep a readable downward angle.\n{diagnostics}");
            Assert.That(camera.transform.forward.z, Is.GreaterThan(0.45f), $"Game camera should use the front-table presentation pitch instead of a straight-down diagnostic view.\n{diagnostics}");
            Assert.That(camera.transform.up.z, Is.GreaterThan(0.80f), $"Game camera up should keep board rows reading top-to-bottom.\n{diagnostics}");
            AssertNoCompetingGameplayCamera(camera, diagnostics);
        }

        private static void AssertBoardStageLayout(Transform boardRoot, Transform boardContentRoot, Transform waterRoot, Transform dockRoot)
        {
            Transform? viewRoot = GameObject.Find("GameStateViewRoot")?.transform;
            Transform? stageRoot = boardRoot.parent;
            Assert.That(stageRoot, Is.Not.Null, "BoardRoot should be parented under BoardStageRoot.");
            if (stageRoot is null)
            {
                throw new AssertionException("BoardRoot should be parented under BoardStageRoot.");
            }

            string diagnostics = BuildRootDiagnostics(viewRoot, stageRoot, boardRoot, boardContentRoot, waterRoot, dockRoot, Camera.main?.transform);
            TestContext.Out.WriteLine(diagnostics);
            Assert.That(viewRoot, Is.Not.Null, diagnostics);
            Assert.That(Quaternion.Angle(viewRoot!.localRotation, Quaternion.identity), Is.LessThan(0.1f), "GameStateViewRoot should not rotate board/input space.");
            Assert.That(stageRoot.name, Is.EqualTo("BoardStageRoot"));
            Assert.That(boardContentRoot.parent, Is.SameAs(stageRoot), "Board content should share the board stage transform.");
            Assert.That(waterRoot.parent, Is.SameAs(stageRoot), "Water overlays should share the board stage transform.");
            Assert.That(dockRoot.parent, Is.Not.SameAs(stageRoot), "DockRoot should stay separate so its staging can be tuned independently.");
            Assert.That(Vector3.Distance(stageRoot.localPosition, PortraitGameSceneLayout.BoardPortraitPosition), Is.LessThan(0.001f), diagnostics);
            Assert.That(Quaternion.Angle(stageRoot.localRotation, PortraitGameSceneLayout.BoardPortraitRotation), Is.LessThan(0.1f), $"BoardStageRoot should keep the gameplay/input coordinate contract aligned.\n{diagnostics}");
            Assert.That(Vector3.Distance(stageRoot.localScale, PortraitGameSceneLayout.BoardPortraitScale), Is.LessThan(0.001f), diagnostics);
            Assert.That(Vector3.Distance(boardRoot.localPosition, Vector3.zero), Is.LessThan(0.001f), diagnostics);
            Assert.That(Quaternion.Angle(boardRoot.localRotation, Quaternion.identity), Is.LessThan(0.1f), diagnostics);
            Assert.That(Vector3.Distance(boardContentRoot.localPosition, Vector3.zero), Is.LessThan(0.001f), diagnostics);
            Assert.That(Quaternion.Angle(boardContentRoot.localRotation, Quaternion.identity), Is.LessThan(0.1f), diagnostics);
            Assert.That(Vector3.Distance(waterRoot.localPosition, Vector3.zero), Is.LessThan(0.001f), diagnostics);
            Assert.That(Quaternion.Angle(waterRoot.localRotation, Quaternion.identity), Is.LessThan(0.1f), diagnostics);
            Assert.That(Vector3.Distance(dockRoot.localPosition, PortraitGameSceneLayout.DockPortraitPosition), Is.LessThan(0.001f), diagnostics);
            Assert.That(Quaternion.Angle(dockRoot.localRotation, PortraitGameSceneLayout.DockPortraitRotation), Is.LessThan(0.1f), $"DockRoot should match the portrait staged dock tilt.\n{diagnostics}");
            Assert.That(Vector3.Distance(dockRoot.localScale, PortraitGameSceneLayout.DockPortraitScale), Is.LessThan(0.001f), diagnostics);
            AssertPlanarAxesAgree(stageRoot, dockRoot, diagnostics);
        }

        private static void AssertBoardFitsGameplayViewport(Transform boardRoot, int boardWidth, int boardHeight)
        {
            Camera? camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "Game.unity should include a tagged Main Camera.");
            if (camera is null)
            {
                throw new AssertionException("Game.unity should include a tagged Main Camera.");
            }

            Transform topLeft = boardRoot.Find("Cell_00_00") ?? throw new AssertionException("Expected top-left board cell.");
            Transform topRight = boardRoot.Find($"Cell_00_{boardWidth - 1:00}") ?? throw new AssertionException("Expected top-right board cell.");
            Transform bottomLeft = boardRoot.Find($"Cell_{boardHeight - 1:00}_00") ?? throw new AssertionException("Expected bottom-left board cell.");
            Transform bottomRight = boardRoot.Find($"Cell_{boardHeight - 1:00}_{boardWidth - 1:00}") ?? throw new AssertionException("Expected bottom-right board cell.");

            Vector3 topLeftViewport = camera.WorldToViewportPoint(topLeft.position);
            Vector3 topRightViewport = camera.WorldToViewportPoint(topRight.position);
            Vector3 bottomLeftViewport = camera.WorldToViewportPoint(bottomLeft.position);
            Vector3 bottomRightViewport = camera.WorldToViewportPoint(bottomRight.position);
            string diagnostics = BuildProjectionDiagnostics(camera, topLeft, topRight, bottomLeft, bottomRight);
            TestContext.Out.WriteLine(diagnostics);

            AssertViewportPointVisible(topLeftViewport, "top-left", diagnostics);
            AssertViewportPointVisible(topRightViewport, "top-right", diagnostics);
            AssertViewportPointVisible(bottomLeftViewport, "bottom-left", diagnostics);
            AssertViewportPointVisible(bottomRightViewport, "bottom-right", diagnostics);

            Assert.That(topRightViewport.x, Is.GreaterThan(topLeftViewport.x), $"Top board row should project left-to-right.\n{diagnostics}");
            Assert.That(Mathf.Abs(topRightViewport.y - topLeftViewport.y), Is.LessThan(0.01f), $"Top board row should project horizontally.\n{diagnostics}");
            Assert.That(Mathf.Abs(bottomLeftViewport.x - topLeftViewport.x), Is.LessThan(0.01f), $"Left board column should project vertically.\n{diagnostics}");
            Assert.That(topLeftViewport.y, Is.GreaterThan(bottomLeftViewport.y), $"Board rows should advance top-to-bottom.\n{diagnostics}");
        }

        private static void AssertViewportPointVisible(Vector3 viewportPoint, string label, string diagnostics)
        {
            Assert.That(viewportPoint.z, Is.GreaterThan(0f), $"{label} board corner should be in front of the camera.\n{diagnostics}");
            Assert.That(viewportPoint.x, Is.InRange(0.05f, 0.95f), $"{label} board corner should stay inside the gameplay viewport horizontally.\n{diagnostics}");
            Assert.That(viewportPoint.y, Is.InRange(0.05f, 0.95f), $"{label} board corner should stay inside the gameplay viewport vertically.\n{diagnostics}");
        }

        private static void AssertCameraRaysHitVisibleCells(Transform boardRoot, int boardWidth, int boardHeight)
        {
            Camera? camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "Game.unity should include a tagged Main Camera.");
            if (camera is null)
            {
                throw new AssertionException("Game.unity should include a tagged Main Camera.");
            }

            Physics.SyncTransforms();
            AssertCameraRayHitsCell(camera, boardRoot.Find("Cell_00_00"), new TileCoord(0, 0), "top-left");
            AssertCameraRayHitsCell(camera, boardRoot.Find($"Cell_00_{boardWidth - 1:00}"), new TileCoord(0, boardWidth - 1), "top-right");
            AssertCameraRayHitsCell(camera, boardRoot.Find($"Cell_{boardHeight - 1:00}_00"), new TileCoord(boardHeight - 1, 0), "bottom-left");
            AssertCameraRayHitsCell(camera, boardRoot.Find($"Cell_{boardHeight - 1:00}_{boardWidth - 1:00}"), new TileCoord(boardHeight - 1, boardWidth - 1), "bottom-right");
            int centerRow = boardHeight / 2;
            int centerCol = boardWidth / 2;
            AssertCameraRayHitsCell(camera, boardRoot.Find($"Cell_{centerRow:00}_{centerCol:00}"), new TileCoord(centerRow, centerCol), "center");
        }

        private static void AssertCameraRayHitsCell(Camera camera, Transform? anchor, TileCoord expectedCoord, string label)
        {
            Assert.That(anchor, Is.Not.Null, $"Expected {label} board cell.");
            if (anchor is null)
            {
                throw new AssertionException($"Expected {label} board cell.");
            }

            Vector3 screen = camera.WorldToScreenPoint(anchor.position);
            Ray ray = camera.ScreenPointToRay(screen);
            Assert.That(Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore), Is.True, $"{label} ray should hit a board cell from screen={FormatVector(screen)}.");
            BoardCellView? cellView = hit.collider.GetComponentInParent<BoardCellView>();
            Assert.That(cellView, Is.Not.Null, $"{label} ray hit '{hit.collider.name}' without a BoardCellView parent.");
            Assert.That(cellView!.Coord, Is.EqualTo(expectedCoord), $"{label} ray hit '{hit.collider.name}' at {FormatVector(hit.point)}.");
        }

        private static void AssertNoCompetingGameplayCamera(Camera mainCamera, string diagnostics)
        {
            Camera[] cameras = UnityObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (!IsGameplayRenderCamera(candidate, mainCamera))
                {
                    continue;
                }

                Assert.That(candidate.depth, Is.LessThan(mainCamera.depth), $"An enabled camera can render over Camera.main.\n{diagnostics}");
            }
        }

        private static bool IsGameplayRenderCamera(Camera candidate, Camera mainCamera)
        {
            if (candidate == mainCamera ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy ||
                candidate.targetTexture is not null ||
                candidate.targetDisplay != mainCamera.targetDisplay)
            {
                return false;
            }

            if (candidate.hideFlags != HideFlags.None ||
                candidate.gameObject.hideFlags != HideFlags.None)
            {
                return false;
            }

            Scene candidateScene = candidate.gameObject.scene;
            Scene mainScene = mainCamera.gameObject.scene;
            if (!candidateScene.IsValid() || !candidateScene.isLoaded)
            {
                return false;
            }

            return candidateScene == mainScene ||
                string.Equals(candidateScene.name, "DontDestroyOnLoad", System.StringComparison.Ordinal);
        }

        private static void AssertPlanarAxesAgree(Transform boardStageRoot, Transform dockRoot, string diagnostics)
        {
            Vector3 boardRight = Vector3.ProjectOnPlane(boardStageRoot.right, Vector3.up).normalized;
            Vector3 dockRight = Vector3.ProjectOnPlane(dockRoot.right, Vector3.up).normalized;
            Vector3 boardForward = Vector3.ProjectOnPlane(boardStageRoot.forward, Vector3.up).normalized;
            Vector3 dockForward = Vector3.ProjectOnPlane(dockRoot.forward, Vector3.up).normalized;
            Assert.That(Vector3.Dot(boardRight, dockRight), Is.GreaterThan(0.99f), $"Board and dock right axes should agree.\n{diagnostics}");
            Assert.That(Vector3.Dot(boardForward, dockForward), Is.GreaterThan(0.96f), $"Board and dock forward axes should agree in the gameplay plane.\n{diagnostics}");
        }

        private static string BuildCameraDiagnostics(Camera mainCamera)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine($"[CameraDiagnostics] activeScene={SceneManager.GetActiveScene().name} screen={Screen.width}x{Screen.height}");
            Camera[] cameras = UnityObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                builder.AppendLine(
                    $"camera[{i}] name='{camera.name}' path='{GetHierarchyPath(camera.transform)}' scene='{camera.gameObject.scene.name}' scenePath='{camera.gameObject.scene.path}' hideFlags={camera.hideFlags} objectHideFlags={camera.gameObject.hideFlags} isMain={camera == mainCamera} tag='{camera.tag}' active={camera.gameObject.activeInHierarchy} enabled={camera.enabled} depth={camera.depth:0.###} display={camera.targetDisplay} targetTexture={(camera.targetTexture == null ? "<null>" : camera.targetTexture.name)} ortho={camera.orthographic} orthoSize={camera.orthographicSize:0.###} pos={FormatVector(camera.transform.position)} euler={FormatVector(camera.transform.eulerAngles)} forward={FormatVector(camera.transform.forward)} up={FormatVector(camera.transform.up)}");
            }

            return builder.ToString();
        }

        private static void DismissAllTutorialCards(TutorialCardPresenter tutorial)
        {
            int guard = 0;
            while (tutorial.IsVisible && guard < 10)
            {
                tutorial.Continue();
                guard++;
            }
        }

        private static string BuildRootDiagnostics(
            Transform? viewRoot,
            Transform boardStageRoot,
            Transform boardRoot,
            Transform boardContentRoot,
            Transform waterRoot,
            Transform dockRoot,
            Transform? cameraRoot)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine("[RootDiagnostics]");
            AppendTransformDiagnostics(builder, "GameStateViewRoot", viewRoot);
            AppendTransformDiagnostics(builder, "BoardStageRoot", boardStageRoot);
            AppendTransformDiagnostics(builder, "BoardRoot", boardRoot);
            AppendTransformDiagnostics(builder, "BoardContentRoot", boardContentRoot);
            AppendTransformDiagnostics(builder, "WaterRoot", waterRoot);
            AppendTransformDiagnostics(builder, "DockRoot", dockRoot);
            AppendTransformDiagnostics(builder, "Main Camera", cameraRoot);
            return builder.ToString();
        }

        private static string BuildProjectionDiagnostics(Camera camera, Transform topLeft, Transform topRight, Transform bottomLeft, Transform bottomRight)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine("[ProjectionDiagnostics]");
            AppendProjectionDiagnostics(builder, camera, "Cell_00_00", topLeft);
            AppendProjectionDiagnostics(builder, camera, topRight.name, topRight);
            AppendProjectionDiagnostics(builder, camera, bottomLeft.name, bottomLeft);
            AppendProjectionDiagnostics(builder, camera, bottomRight.name, bottomRight);
            return builder.ToString();
        }

        private static void AppendProjectionDiagnostics(System.Text.StringBuilder builder, Camera camera, string label, Transform transform)
        {
            builder.AppendLine($"{label}: world={FormatVector(transform.position)} viewport={FormatVector(camera.WorldToViewportPoint(transform.position))} screen={FormatVector(camera.WorldToScreenPoint(transform.position))}");
        }

        private static void AppendTransformDiagnostics(System.Text.StringBuilder builder, string label, Transform? transform)
        {
            if (transform is null)
            {
                builder.AppendLine($"{label}: <missing>");
                return;
            }

            builder.AppendLine($"{label}: parent='{(transform.parent is null ? "<none>" : transform.parent.name)}' localPos={FormatVector(transform.localPosition)} localEuler={FormatVector(transform.localEulerAngles)} localScale={FormatVector(transform.localScale)} worldPos={FormatVector(transform.position)} worldEuler={FormatVector(transform.eulerAngles)} right={FormatVector(transform.right)} forward={FormatVector(transform.forward)}");
        }

        private static string GetHierarchyPath(Transform transform)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(transform.name);
            Transform? current = transform.parent;
            while (current is not null)
            {
                builder.Insert(0, $"{current.name}/");
                current = current.parent;
            }

            return builder.ToString();
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.###},{value.y:0.###},{value.z:0.###})";
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
