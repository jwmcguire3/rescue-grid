using System;
using System.Collections.Generic;
using Rescue.Content;
using Rescue.Core.Pipeline;
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
        };

        public static IReadOnlyList<string> LevelIds { get; } = Array.AsReadOnly(PacketLevelIds);

        [SerializeField] private GameStateViewPresenter? gameStateView;
        [SerializeField] private BoardInputPresenter? boardInput;
        [SerializeField] private VictoryScreenPresenter? victoryScreen;
        [SerializeField] private LossScreenPresenter? lossScreen;
        [SerializeField] private L00IntroImagePresenter? l00IntroImage;
        [SerializeField] private HapticEventRouter? hapticEventRouter;
        [SerializeField] private string startingLevelId = InitialLevelId;
        [SerializeField] private int seed = InitialSeed;

        private readonly Stack<Snapshot> undoSnapshots = new Stack<Snapshot>();

        private string currentLevelId = InitialLevelId;
        private GameState? initialState;

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
            SyncL00IntroImage();
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
            boardInput?.SetInputBlocked(true);
            l00IntroImage?.Show();
        }

        public bool TryUndo()
        {
            ResolveSceneReferences();
            SyncTerminalInputLock();
            if (IsTerminalInputLocked)
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
            if (IsTerminalInputLocked)
            {
                return false;
            }

            GameState? current = CurrentState;
            if (current is null || gameStateView is null)
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

        private void Awake()
        {
            ResolveSceneReferences();
            currentLevelId = string.IsNullOrWhiteSpace(startingLevelId) ? InitialLevelId : startingLevelId;
            BindTerminalButtons();
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
            if (victoryScreen is not null)
            {
                victoryScreen.ReplayRequested -= ReplayCurrentLevel;
                victoryScreen.NextLevelRequested -= HandleNextRequested;
            }

            if (lossScreen is not null)
            {
                lossScreen.ReplayRequested -= ReplayCurrentLevel;
                lossScreen.TryAgainRequested -= HandleRetryRequested;
            }

            if (l00IntroImage is not null)
            {
                l00IntroImage.Dismissed -= HandleL00IntroDismissed;
            }
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
            if (existing is not null)
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
            if (gameStateView is null)
            {
                gameStateView = FindAnyObjectByType<GameStateViewPresenter>();
            }

            if (boardInput is null)
            {
                boardInput = FindAnyObjectByType<BoardInputPresenter>();
            }

            if (victoryScreen is null)
            {
                victoryScreen = VictoryScreenPresenter.EnsureInstance();
            }

            if (lossScreen is null)
            {
                lossScreen = LossScreenPresenter.EnsureInstance();
            }

            if (l00IntroImage is null)
            {
                l00IntroImage = L00IntroImagePresenter.EnsureInstance();
            }

            if (hapticEventRouter is null)
            {
                hapticEventRouter = FindAnyObjectByType<HapticEventRouter>();
            }

            if (hapticEventRouter is null && gameStateView is not null)
            {
                hapticEventRouter = gameStateView.GetComponent<HapticEventRouter>();
                if (hapticEventRouter is null)
                {
                    hapticEventRouter = gameStateView.gameObject.AddComponent<HapticEventRouter>();
                }
            }

            BindTerminalButtons();
            BindL00IntroImage();
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
            if (victoryScreen is not null)
            {
                victoryScreen.ReplayRequested -= ReplayCurrentLevel;
                victoryScreen.NextLevelRequested -= HandleNextRequested;
                victoryScreen.ReplayRequested += ReplayCurrentLevel;
                victoryScreen.NextLevelRequested += HandleNextRequested;
            }

            if (lossScreen is not null)
            {
                lossScreen.ReplayRequested -= ReplayCurrentLevel;
                lossScreen.TryAgainRequested -= HandleRetryRequested;
                lossScreen.ReplayRequested += ReplayCurrentLevel;
                lossScreen.TryAgainRequested += HandleRetryRequested;
            }
        }

        private void BindL00IntroImage()
        {
            if (l00IntroImage is null)
            {
                return;
            }

            l00IntroImage.Dismissed -= HandleL00IntroDismissed;
            l00IntroImage.Dismissed += HandleL00IntroDismissed;
        }

        private void SyncL00IntroImage()
        {
            if (l00IntroImage is null)
            {
                return;
            }

            bool shouldShow = string.Equals(currentLevelId, InitialLevelId, StringComparison.Ordinal);
            boardInput?.SetInputBlocked(shouldShow);
            if (shouldShow)
            {
                l00IntroImage.Show();
            }
            else
            {
                l00IntroImage.Hide();
            }
        }

        private void HandleL00IntroDismissed()
        {
            boardInput?.SetInputBlocked(false);
        }

        private void SyncTerminalInputLock()
        {
            boardInput?.SetTerminalInputLocked(IsTerminalScreenVisible());
        }

        private bool IsTerminalScreenVisible()
        {
            return (victoryScreen is not null && victoryScreen.IsVisible)
                || (lossScreen is not null && lossScreen.IsVisible);
        }

        private void HandleNextRequested()
        {
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
