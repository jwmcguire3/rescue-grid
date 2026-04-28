#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Immutable;
using System.Reflection;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
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
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;
using UnityObject = UnityEngine.Object;

namespace Rescue.PlayMode.Tests.Smoke
{
    public sealed class DebugGameplayPlaybackSceneWiringSmokeTests
    {
        [UnitySetUp]
        public System.Collections.IEnumerator SetUp()
        {
            if (DebugPanel.Instance is not null)
            {
                UnityObject.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            VictoryScreenPresenter? victoryScreen = UnityObject.FindFirstObjectByType<VictoryScreenPresenter>();
            if (victoryScreen is not null)
            {
                UnityObject.DestroyImmediate(victoryScreen.gameObject);
            }

            LossScreenPresenter? lossScreen = UnityObject.FindFirstObjectByType<LossScreenPresenter>();
            if (lossScreen is not null)
            {
                UnityObject.DestroyImmediate(lossScreen.gameObject);
            }

            yield return SceneManager.LoadSceneAsync("DebugGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public System.Collections.IEnumerator TearDown()
        {
            if (DebugPanel.Instance is not null)
            {
                UnityObject.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            VictoryScreenPresenter? victoryScreen = UnityObject.FindFirstObjectByType<VictoryScreenPresenter>();
            if (victoryScreen is not null)
            {
                UnityObject.DestroyImmediate(victoryScreen.gameObject);
            }

            LossScreenPresenter? lossScreen = UnityObject.FindFirstObjectByType<LossScreenPresenter>();
            if (lossScreen is not null)
            {
                UnityObject.DestroyImmediate(lossScreen.gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator DebugGameplayScene_HasPlanPlaybackAndFxWiring()
        {
            GameStateViewPresenter gameStateView = FindRequired<GameStateViewPresenter>();
            BoardInputPresenter boardInput = FindRequired<BoardInputPresenter>();
            ActionPlaybackController playbackController = FindRequired<ActionPlaybackController>();
            FxEventRouter fxEventRouter = FindRequired<FxEventRouter>();
            VictoryScreenPresenter victoryScreen = VictoryScreenPresenter.EnsureInstance();
            LossScreenPresenter lossScreen = LossScreenPresenter.EnsureInstance();

            Assert.That(
                GetSerializedReference<GameStateViewPresenter, ActionPlaybackController>(gameStateView, "playbackController")
                    ?? gameStateView.GetComponent<ActionPlaybackController>(),
                Is.SameAs(playbackController),
                "GameStateViewPresenter should use the scene ActionPlaybackController instead of falling back to immediate final sync.");

            Assert.That(
                GetSerializedReference<ActionPlaybackController, BoardGridViewPresenter>(playbackController, "boardGrid"),
                Is.SameAs(FindRequired<BoardGridViewPresenter>()),
                "ActionPlaybackController should have the board grid presenter assigned for playback beat positioning.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, BoardContentViewPresenter>(playbackController, "boardContent"),
                Is.SameAs(FindRequired<BoardContentViewPresenter>()),
                "ActionPlaybackController should have the board content presenter assigned for removal, blocker, gravity, spawn, and extraction beats.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, WaterViewPresenter>(playbackController, "waterView"),
                Is.SameAs(FindRequired<WaterViewPresenter>()),
                "ActionPlaybackController should have the water presenter assigned for water rise beats.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, DockViewPresenter>(playbackController, "dockView"),
                Is.SameAs(FindRequired<DockViewPresenter>()),
                "ActionPlaybackController should have the dock presenter assigned for dock insertion, clear, warning, and jam beats.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, FxEventRouter>(playbackController, "fxEventRouter"),
                Is.SameAs(fxEventRouter),
                "ActionPlaybackController should route playback beats through the scene FxEventRouter.");

            Assert.That(fxEventRouter.FxRegistry, Is.Not.Null, "DebugGameplay should assign the Phase 1 FX registry asset.");
            if (fxEventRouter.FxRegistry is null)
            {
                throw new AssertionException("DebugGameplay should assign the Phase 1 FX registry asset.");
            }

            Assert.That(fxEventRouter.FxRegistry.WinFx, Is.Not.Null, "DebugGameplay should have terminal win FX assigned.");
            Assert.That(fxEventRouter.FxRegistry.LossFx, Is.Not.Null, "DebugGameplay should have terminal loss FX assigned.");
            Assert.That(
                fxEventRouter.BoardGrid,
                Is.SameAs(FindRequired<BoardGridViewPresenter>()),
                "FxEventRouter should resolve cell and row positions through the scene board grid presenter.");
            Assert.That(
                fxEventRouter.FxRoot != null || fxEventRouter.transform != null,
                Is.True,
                "FxEventRouter should have an FX root assigned or be able to safely use its own transform as the spawn root.");

            Assert.That(
                GetSerializedReference<BoardInputPresenter, GameStateViewPresenter>(boardInput, "gameStateView"),
                Is.SameAs(gameStateView),
                "BoardInputPresenter should route actions through GameStateViewPresenter so playback can lock input and apply Plan 1/2 beats.");
            Assert.That(victoryScreen.IsVisible, Is.False, "Victory screen should be present for terminal wins but hidden before a win.");
            Assert.That(lossScreen.IsVisible, Is.False, "Loss screen should be present for terminal losses but hidden before a loss.");

            LogAssert.NoUnexpectedReceived();
            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator DebugGameplayScene_HasAudioFeedbackWiringAndFailSoftSmoke()
        {
            GameStateViewPresenter gameStateView = FindRequired<GameStateViewPresenter>();
            BoardInputPresenter boardInput = FindRequired<BoardInputPresenter>();
            ActionPlaybackController playbackController = FindRequired<ActionPlaybackController>();
            AudioEventRouter audioRouter = FindRequired<AudioEventRouter>();
            MusicPlayer musicPlayer = FindRequired<MusicPlayer>();
            BoardGridViewPresenter boardGrid = FindRequired<BoardGridViewPresenter>();

            Assert.That(
                GetSerializedReference<ActionPlaybackController, AudioEventRouter>(playbackController, "audioEventRouter"),
                Is.SameAs(audioRouter),
                "ActionPlaybackController should route playback beats through the scene AudioEventRouter.");
            Assert.That(
                GetSerializedReference<GameStateViewPresenter, AudioEventRouter>(gameStateView, "audioEventRouter"),
                Is.SameAs(audioRouter),
                "GameStateViewPresenter should route result-only feedback signals through the scene AudioEventRouter.");
            AudioFeedbackRegistry? registry = audioRouter.Registry;
            Assert.That(registry, Is.Not.Null, "DebugGameplay should assign an audio registry, even when it is intentionally empty.");
            if (registry is null)
            {
                throw new AssertionException("DebugGameplay should assign an audio registry, even when it is intentionally empty.");
            }

            Assert.That(registry.Entries.Length, Is.EqualTo(0), "DebugGameplay intentionally uses an empty audio registry until production clips are available.");
            Assert.That(audioRouter.AudioSource, Is.Not.Null, "DebugGameplay should provide an AudioSource for routed feedback.");
            Assert.That(audioRouter.BoardGrid, Is.SameAs(boardGrid), "AudioEventRouter should resolve location-aware audio through the scene board grid presenter.");
            Assert.That(musicPlayer.Playlist, Is.Not.Null, "DebugGameplay should assign the gameplay music playlist asset.");
            if (musicPlayer.Playlist is null)
            {
                throw new AssertionException("DebugGameplay should assign the gameplay music playlist asset.");
            }

            Assert.That(musicPlayer.Playlist.Tracks.Length, Is.EqualTo(0), "GameplayMusicPlaylist is intentionally empty until production music clips are committed.");
            Assert.That(musicPlayer.AudioSource, Is.Not.Null, "DebugGameplay should provide a dedicated AudioSource for ambient music.");
            Assert.That(musicPlayer.AudioSource, Is.Not.SameAs(audioRouter.AudioSource), "Ambient music should stay separate from routed feedback SFX.");
            Assert.That(musicPlayer.PlayNext(), Is.False, "An empty gameplay music playlist should fail soft without starting playback.");

            GameState initialState = CreateAudioFeedbackSmokeState();
            boardInput.SetCurrentState(initialState);
            yield return null;

            string initialFingerprint = SmokeTestHarness.Fingerprint(initialState);
            LogAssert.Expect(LogType.Log, "Rejected board tap at (1, 0) because SingleTile.");

            Assert.That(
                boardInput.TryRunActionAt(new TileCoord(1, 0)),
                Is.True,
                "A single-tile in-bounds tap should travel through the input and feedback path as an invalid action.");

            float timeoutAt = Time.realtimeSinceStartup + 2f;
            while (gameStateView.IsPlaybackActive && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(gameStateView.IsPlaybackActive, Is.False, "Invalid tap feedback should not leave playback stuck active.");
            GameState? stateAfterInvalidTap = gameStateView.CurrentState;
            Assert.That(stateAfterInvalidTap, Is.Not.Null);
            if (stateAfterInvalidTap is null)
            {
                throw new AssertionException("Expected a current state after invalid tap feedback.");
            }

            Assert.That(
                SmokeTestHarness.Fingerprint(stateAfterInvalidTap),
                Is.EqualTo(initialFingerprint),
                "Invalid tap feedback must not mutate gameplay state.");

            GameState previousState = stateAfterInvalidTap;
            ActionInput validInput = new ActionInput(new TileCoord(0, 0));
            ActionResult expectedResult = Pipeline.RunAction(previousState, validInput);

            Assert.That(boardInput.TryRunActionAt(validInput.TappedCoord), Is.True);
            Assert.That(gameStateView.CurrentPlaybackPlan.Count, Is.GreaterThan(0));
            Assert.That(
                gameStateView.CurrentPlaybackPlan[^1].StepType,
                Is.EqualTo(ActionPlaybackStepType.FinalSync),
                "Scene playback should still end with an authoritative final sync.");

            timeoutAt = Time.realtimeSinceStartup + 3f;
            while (gameStateView.IsPlaybackActive && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(gameStateView.IsPlaybackActive, Is.False, "Valid action playback should complete.");
            GameState? stateAfterValidTap = gameStateView.CurrentState;
            Assert.That(stateAfterValidTap, Is.Not.Null);
            if (stateAfterValidTap is null)
            {
                throw new AssertionException("Expected a current state after valid action playback.");
            }

            Assert.That(
                SmokeTestHarness.Fingerprint(stateAfterValidTap),
                Is.EqualTo(SmokeTestHarness.Fingerprint(expectedResult.State)),
                "Playback final sync should repair the presenter to the authoritative action result.");

            LogAssert.NoUnexpectedReceived();
        }

        private static T FindRequired<T>()
            where T : UnityObject
        {
            T? component = UnityObject.FindFirstObjectByType<T>();
            Assert.That(component, Is.Not.Null, $"Expected DebugGameplay to include {typeof(T).Name}.");
            if (component is null)
            {
                throw new AssertionException($"Expected DebugGameplay to include {typeof(T).Name}.");
            }

            return component;
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

        private static GameState CreateAudioFeedbackSmokeState()
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A)),
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.B),
                    new EmptyTile()));

            CoreBoard board = new CoreBoard(2, 2, rows);

            return new GameState(
                Board: board,
                Dock: new CoreDock(
                    ImmutableArray.Create<DebrisType?>(
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null),
                    Size: 7),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
                Vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                Targets: ImmutableArray<TargetState>.Empty,
                LevelConfig: new LevelConfig(
                    ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C),
                    null,
                    0.0d,
                    2),
                RngState: new RngState(1u, 2u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }
    }
}
#endif
