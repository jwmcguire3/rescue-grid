using System;
using System.Collections.Generic;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Core.Undo;
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
        };

        [SerializeField] private GameStateViewPresenter? gameStateView;
        [SerializeField] private BoardInputPresenter? boardInput;
        [SerializeField] private VictoryScreenPresenter? victoryScreen;
        [SerializeField] private LossScreenPresenter? lossScreen;
        [SerializeField] private string startingLevelId = InitialLevelId;
        [SerializeField] private int seed = InitialSeed;

        private readonly Stack<Snapshot> undoSnapshots = new Stack<Snapshot>();

        private string currentLevelId = InitialLevelId;
        private GameState? initialState;

        public string CurrentLevelId => currentLevelId;

        public int Seed => seed;

        public GameState? CurrentState => gameStateView?.CurrentState;

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
            SyncVictoryAvailability();
            gameStateView?.Rebuild(loaded);
            boardInput?.SetCurrentState(loaded, refreshView: false);
        }

        public void ReplayCurrentLevel()
        {
            if (string.IsNullOrWhiteSpace(currentLevelId))
            {
                currentLevelId = startingLevelId;
            }

            LoadLevel(currentLevelId, seed);
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

        public bool TryUndo()
        {
            ResolveSceneReferences();
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
            return true;
        }

        public bool TryRunAction(TileCoord coord)
        {
            ResolveSceneReferences();
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

            BindTerminalButtons();
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
