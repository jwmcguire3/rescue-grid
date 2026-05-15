using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.Rules;
using Rescue.Core.State;
using Rescue.Core.Undo;
using Rescue.Unity.Haptics;
using Rescue.Unity.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rescue.Unity.Presentation
{
    public sealed class PlayableLevelSession : MonoBehaviour
    {
        private const string GameSceneName = "Game";
        private const string InitialLevelId = "L00";
        private const int InitialSeed = 1;

        private static readonly string[] PacketLevelIds =
        {
            "L00",
            "L01",
            "L02",
            "L03",
            "L04",
            "L05",
            "L06",
            "L07",
            "L08",
            "L09",
            "L10",
            "L11",
            "L12",
            "L13",
            "L14",
            "L15",
            "L16",
            "L17",
            "L18",
            "L19",
            "L20",
            "L21",
            "L22",
            "L23",
            "L24",
            "L25",
            "L26",
            "L27",
            "L28",
            "L29",
            "L30",
            "L31",
            "L32",
            "L33",
            "L34",
            "L35",
            "L36",
            "L37",
            "L38",
            "L39",
            "L40",
        };

        public static IReadOnlyList<string> LevelIds { get; } = Array.AsReadOnly(PacketLevelIds);

        [SerializeField] private GameStateViewPresenter? gameStateView;
        [SerializeField] private BoardInputPresenter? boardInput;
        [SerializeField] private VictoryScreenPresenter? victoryScreen;
        [SerializeField] private LossScreenPresenter? lossScreen;
        [SerializeField] private TutorialCardPresenter? tutorialCards;
        [SerializeField] private HapticEventRouter? hapticEventRouter;
        [SerializeField] private string startingLevelId = InitialLevelId;
        [SerializeField] private int seed = InitialSeed;

        private readonly Stack<Snapshot> undoSnapshots = new Stack<Snapshot>();

        private string currentLevelId = InitialLevelId;
        private GameState? initialState;
        private Coroutine? releaseTutorialInputBlockCoroutine;
        private bool loadNextLevelAfterTutorialDismissed;

        public string CurrentLevelId => currentLevelId;

        public int Seed => seed;

        public GameState? CurrentState => gameStateView?.CurrentState;

        public bool IsTerminalInputLocked => IsTerminalScreenVisible();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        public static PlayableLevelSession? EnsureForActiveGameScene()
        {
            return EnsureForScene(SceneManager.GetActiveScene());
        }

        public void LoadCurrentLevel()
        {
            LoadLevel(currentLevelId, seed);
        }

        public void LoadLevel(string levelId, int levelSeed)
        {
            ResolveSceneReferences();
            GameState loaded = Loader.LoadLevel(levelId, levelSeed);
            currentLevelId = levelId;
            seed = levelSeed;
            initialState = loaded;
            undoSnapshots.Clear();
            victoryScreen?.Hide();
            lossScreen?.Hide();
            SettingsMenuPresenter? settingsMenu = FindAnyObjectByType<SettingsMenuPresenter>();
            settingsMenu?.SetOpen(false);
            SyncVictoryAvailability();
            gameStateView?.Rebuild(loaded);
            boardInput?.SetCurrentState(loaded, refreshView: false);
            SyncTerminalInputLock();
            SyncPreLevelTutorial();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Rescue.Unity.Diagnostics.AndroidWhiteoutDiagnostics.LogLevelVisualState(levelId);
#endif
        }

        public void ReplayCurrentLevel()
        {
            if (string.IsNullOrWhiteSpace(currentLevelId))
            {
                currentLevelId = startingLevelId;
            }

            LoadLevel(currentLevelId, seed);
            RouteManualHaptic(HapticEventId.RetryConfirmed);
        }

        public bool LoadNextLevel()
        {
            string? nextLevelId = GetNextLevelId(currentLevelId);
            if (string.IsNullOrWhiteSpace(nextLevelId))
            {
                SyncVictoryAvailability();
                return false;
            }

            LoadLevel(nextLevelId, seed);
            return true;
        }

        public bool Retry()
        {
            ReplayCurrentLevel();
            return true;
        }

        public void ShowTutorialImage()
        {
            ResolveSceneReferences();
            loadNextLevelAfterTutorialDismissed = false;
            if (TutorialDeckRegistry.TryGetAnyDeck(currentLevelId, out TutorialDeck deck))
            {
                ShowTutorialDeck(deck);
            }
        }

        public bool TryUndo()
        {
            ResolveSceneReferences();
            SyncTerminalInputLock();
            if (IsTerminalInputLocked || IsTutorialVisible())
            {
                return false;
            }

            GameState? current = CurrentState;
            if (current is null || undoSnapshots.Count == 0)
            {
                return false;
            }

            Snapshot snapshot = undoSnapshots.Pop();
            if (!UndoGuard.CanUndo(current, snapshot))
            {
                return false;
            }

            GameState restored = UndoGuard.PerformUndo(current, snapshot);
            gameStateView?.Rebuild(restored);
            boardInput?.SetCurrentState(restored, refreshView: false);
            RouteManualHaptic(HapticEventId.UndoUsed);
            return true;
        }

        public bool TryRunAction(TileCoord coord)
        {
            ResolveSceneReferences();
            SyncTerminalInputLock();
            if (IsTerminalInputLocked || IsTutorialVisible())
            {
                return false;
            }

            GameState? current = CurrentState;
            if (current is null || gameStateView == null)
            {
                return false;
            }

            ActionInput input = new ActionInput(coord);
            ActionResult result = Pipeline.RunAction(current, input);
            if (result.Snapshot is not null)
            {
                undoSnapshots.Push(result.Snapshot);
            }

            gameStateView.ApplyActionResult(current, input, result);
            boardInput?.SetCurrentState(result.State, refreshView: false);
            return true;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        public bool TryRunDiagnosticAction(TileCoord coord)
        {
            ResolveSceneReferences();
            GameState? current = CurrentState;
            if (current is null || gameStateView == null)
            {
                return false;
            }

            ActionResult result = Pipeline.RunAction(current, new ActionInput(coord));
            if (result.State.ActionCount <= current.ActionCount)
            {
                return false;
            }

            gameStateView.ForceSyncToState(
                result.State,
                "android whiteout diagnostics",
                cancelActivePlayback: true,
                clearPlaybackPlan: true);
            boardInput?.SetCurrentState(result.State, refreshView: false);
            return true;
        }

        public bool TryFindFirstDiagnosticMove(out TileCoord coord, out int groupSize)
        {
            coord = default;
            groupSize = 0;
            GameState? current = CurrentState;
            if (current is null)
            {
                return false;
            }

            for (int row = 0; row < current.Board.Height; row++)
            {
                for (int col = 0; col < current.Board.Width; col++)
                {
                    TileCoord candidate = new TileCoord(row, col);
                    ImmutableArray<TileCoord>? group = GroupOps.FindGroup(current.Board, candidate);
                    if (!group.HasValue)
                    {
                        continue;
                    }

                    coord = candidate;
                    groupSize = group.Value.Length;
                    return true;
                }
            }

            return false;
        }
#endif

        private void Awake()
        {
            ResolveSceneReferences();
            currentLevelId = string.IsNullOrWhiteSpace(startingLevelId) ? InitialLevelId : startingLevelId;
            BindTerminalButtons();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Rescue.Unity.Diagnostics.AndroidWhiteoutCommandBridge.EnsureInstance();
#endif
        }

        private void Start()
        {
            if (initialState is null && string.Equals(SceneManager.GetActiveScene().name, GameSceneName, StringComparison.Ordinal))
            {
                LoadCurrentLevel();
            }
        }

        private void LateUpdate()
        {
            SyncTerminalInputLock();
        }

        private void OnDestroy()
        {
            if (victoryScreen != null)
            {
                victoryScreen.ReplayRequested -= ReplayCurrentLevel;
                victoryScreen.NextLevelRequested -= HandleNextRequested;
            }

            if (lossScreen != null)
            {
                lossScreen.ReplayRequested -= ReplayCurrentLevel;
                lossScreen.TryAgainRequested -= HandleRetryRequested;
            }

            if (tutorialCards != null)
            {
                tutorialCards.Dismissed -= HandleTutorialDismissed;
            }

            CancelPendingTutorialInputRelease();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static PlayableLevelSession? EnsureForScene(Scene scene)
        {
            if (HasArgument("-capture-l15"))
            {
                return null;
            }

            if (!string.Equals(scene.name, GameSceneName, StringComparison.Ordinal))
            {
                return null;
            }

            PlayableLevelSession? existing = FindAnyObjectByType<PlayableLevelSession>();
            if (existing != null)
            {
                return existing;
            }

            GameObject host = new GameObject("PlayableLevelSession");
            return host.AddComponent<PlayableLevelSession>();
        }

        private static bool HasArgument(string argument)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveSceneReferences()
        {
            if (gameStateView == null)
            {
                gameStateView = FindAnyObjectByType<GameStateViewPresenter>();
            }

            if (boardInput == null)
            {
                boardInput = FindAnyObjectByType<BoardInputPresenter>();
            }

            if (victoryScreen == null)
            {
                victoryScreen = VictoryScreenPresenter.EnsureInstance();
            }

            if (lossScreen == null)
            {
                lossScreen = LossScreenPresenter.EnsureInstance();
            }

            if (tutorialCards == null)
            {
                tutorialCards = TutorialCardPresenter.EnsureInstance();
            }

            if (hapticEventRouter == null)
            {
                hapticEventRouter = FindAnyObjectByType<HapticEventRouter>();
            }

            if (hapticEventRouter == null && gameStateView != null)
            {
                hapticEventRouter = gameStateView.GetComponent<HapticEventRouter>();
                if (hapticEventRouter == null)
                {
                    hapticEventRouter = gameStateView.gameObject.AddComponent<HapticEventRouter>();
                }
            }

            BindTerminalButtons();
            BindTutorialCards();
        }

        private void RouteManualHaptic(HapticEventId eventId)
        {
            ResolveSceneReferences();
            try
            {
                hapticEventRouter?.RouteManual(eventId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"{nameof(PlayableLevelSession)} skipped manual haptic '{eventId}' after an exception: {exception.Message}",
                    this);
            }
        }

        private void BindTerminalButtons()
        {
            if (victoryScreen != null)
            {
                victoryScreen.ReplayRequested -= ReplayCurrentLevel;
                victoryScreen.NextLevelRequested -= HandleNextRequested;
                victoryScreen.ReplayRequested += ReplayCurrentLevel;
                victoryScreen.NextLevelRequested += HandleNextRequested;
            }

            if (lossScreen != null)
            {
                lossScreen.ReplayRequested -= ReplayCurrentLevel;
                lossScreen.TryAgainRequested -= HandleRetryRequested;
                lossScreen.ReplayRequested += ReplayCurrentLevel;
                lossScreen.TryAgainRequested += HandleRetryRequested;
            }
        }

        private void BindTutorialCards()
        {
            if (tutorialCards == null)
            {
                return;
            }

            tutorialCards.Dismissed -= HandleTutorialDismissed;
            tutorialCards.Dismissed += HandleTutorialDismissed;
        }

        private void SyncPreLevelTutorial()
        {
            if (tutorialCards == null)
            {
                return;
            }

            loadNextLevelAfterTutorialDismissed = false;
            if (TutorialDeckRegistry.TryGetDeck(currentLevelId, TutorialShowTiming.Pre, out TutorialDeck deck))
            {
                ShowTutorialDeck(deck);
                return;
            }

            tutorialCards.Hide();
            boardInput?.SetInputBlocked(false);
        }

        private void ShowTutorialDeck(TutorialDeck deck)
        {
            CancelPendingTutorialInputRelease();
            boardInput?.SetInputBlocked(true);
            tutorialCards?.ShowDeck(deck);
        }

        private void HandleTutorialDismissed()
        {
            CancelPendingTutorialInputRelease();
            releaseTutorialInputBlockCoroutine = StartCoroutine(ReleaseTutorialInputBlockNextFrame());
        }

        private IEnumerator ReleaseTutorialInputBlockNextFrame()
        {
            yield return null;
            releaseTutorialInputBlockCoroutine = null;
            if (tutorialCards == null || !tutorialCards.IsVisible)
            {
                boardInput?.SetInputBlocked(false);
                if (loadNextLevelAfterTutorialDismissed)
                {
                    loadNextLevelAfterTutorialDismissed = false;
                    LoadNextLevel();
                }
            }
        }

        private void CancelPendingTutorialInputRelease()
        {
            if (releaseTutorialInputBlockCoroutine is null)
            {
                return;
            }

            StopCoroutine(releaseTutorialInputBlockCoroutine);
            releaseTutorialInputBlockCoroutine = null;
        }

        private void SyncTerminalInputLock()
        {
            boardInput?.SetTerminalInputLocked(IsTerminalScreenVisible());
        }

        private bool IsTerminalScreenVisible()
        {
            return (victoryScreen != null && victoryScreen.IsVisible)
                || (lossScreen != null && lossScreen.IsVisible);
        }

        private bool IsTutorialVisible()
        {
            return tutorialCards != null && tutorialCards.IsVisible;
        }

        private void HandleNextRequested()
        {
            if (TutorialDeckRegistry.TryGetDeck(currentLevelId, TutorialShowTiming.PostWin, out TutorialDeck deck))
            {
                victoryScreen?.Hide();
                loadNextLevelAfterTutorialDismissed = true;
                ShowTutorialDeck(deck);
                return;
            }

            LoadNextLevel();
        }

        private void HandleRetryRequested()
        {
            Retry();
        }

        private void SyncVictoryAvailability()
        {
            victoryScreen?.SetNextLevelAvailable(GetNextLevelId(currentLevelId) is not null);
        }

        private static string? GetNextLevelId(string levelId)
        {
            for (int i = 0; i < PacketLevelIds.Length; i++)
            {
                if (string.Equals(PacketLevelIds[i], levelId, StringComparison.Ordinal))
                {
                    return i < PacketLevelIds.Length - 1 ? PacketLevelIds[i + 1] : null;
                }
            }

            return null;
        }
    }
}
