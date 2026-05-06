#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !CAPTURE_BUILD
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Reflection;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.Rules;
using Rescue.Core.State;
using Rescue.Core.Undo;
using Rescue.Replay;
using Rescue.Telemetry;
using Rescue.Unity.Presentation;
using Rescue.Unity.FX;
using Rescue.Unity.Telemetry;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rescue.Unity.Debugging
{
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class DebugPanel : MonoBehaviour
    {
        private const string UxmlAssetPath = "Assets/Rescue.Unity/Debug/DebugPanel.uxml";
        private const string UssAssetPath = "Assets/Rescue.Unity/Debug/DebugPanel.uss";
        private const string RuntimeThemeResourcePath = "Rescue.Unity/Debug/UnityDefaultRuntimeTheme";
        private const int EventLogCapacity = 20;
        private const int DockSize = 7;
        private const int LossReplayRetentionCap = 20;
        private static readonly string[] SpeedChoices = { "0.25x", "0.5x", "1x", "2x", "4x" };
        private static readonly string[] AssistanceChoices = { "Current", "0", "1" };
        private static readonly string[] EmergencyChoices = { "Auto", "On", "Off" };
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
        private static readonly DebrisType[] OverflowDockPattern =
        {
            DebrisType.B,
            DebrisType.C,
            DebrisType.D,
            DebrisType.E,
            DebrisType.B,
            DebrisType.C,
            DebrisType.D,
        };

        private static DebugPanel? _instance;

        private readonly List<DebugActionLogEntry> _eventLog = new List<DebugActionLogEntry>(EventLogCapacity);
        private readonly Stack<DebugUndoEntry> _debugUndo = new Stack<DebugUndoEntry>();
        private readonly List<InputAction> _inputActions = new List<InputAction>();

        private UIDocument? _document;
        private VisualElement? _documentRoot;
        private PanelSettings? _panelSettings;
        private VisualElement? _panelRoot;
        private VisualElement? _panelBody;
        private Button? _minimizeButton;
        private DropdownField? _levelSelector;
        private IntegerField? _seedField;
        private Button? _randomSeedButton;
        private Button? _playPauseButton;
        private Button? _stepButton;
        private DropdownField? _speedSelector;
        private Toggle? _fastForwardToggle;
        private Toggle? _playbackEnabledToggle;
        private Slider? _playbackSpeedSlider;
        private Label? _playbackSpeedValue;
        private Slider? _playbackBoardActionSpeedSlider;
        private Label? _playbackBoardActionSpeedValue;
        private Slider? _playbackDockSpeedSlider;
        private Label? _playbackDockSpeedValue;
        private Slider? _playbackTargetSpeedSlider;
        private Label? _playbackTargetSpeedValue;
        private Slider? _playbackHazardSpeedSlider;
        private Label? _playbackHazardSpeedValue;
        private Slider? _playbackTerminalSpeedSlider;
        private Label? _playbackTerminalSpeedValue;
        private Slider? _playbackGravitySpawnSpeedSlider;
        private Label? _playbackGravitySpawnSpeedValue;
        private Slider? _fxPlaybackSpeedSlider;
        private Label? _fxPlaybackSpeedValue;
        private Label? _playbackStepValue;
        private Toggle? _fxDiagnosticsToggle;
        private Button? _playAllFxButton;
        private Button? _debugUndoButton;
        private Button? _resetButton;
        private TextField? _replayPathField;
        private Button? _loadReplayButton;
        private Button? _stepReplayButton;
        private Button? _clearReplayButton;
        private Label? _replayStatusValue;
        private Label? _statusLabel;
        private Label? _waterActionsValue;
        private Label? _waterRiseIntervalValue;
        private Label? _waterNextFloodRowValue;
        private Label? _waterForecastValue;
        private Label? _ruleTeachValue;
        private Label? _vineActionsValue;
        private Label? _vineThresholdValue;
        private Label? _vinePendingValue;
        private Label? _dockOccupancyValue;
        private Label? _dockWarningValue;
        private Label? _dockContentsValue;
        private Label? _dockJamUsedValue;
        private Label? _dockJamEnabledValue;
        private Label? _nearRescueTargetsValue;
        private Label? _rngStateValue;
        private Button? _copyRngButton;
        private DropdownField? _assistanceOverrideField;
        private DropdownField? _forceEmergencyField;
        private Label? _consecutiveEmergencyValue;
        private Label? _spawnRecoveryValue;
        private Button? _overflowButton;
        private Button? _copyStateButton;
        private Button? _copyFullStateButton;
        private VisualElement? _eventLogList;
        private VictoryScreenPresenter? _victoryScreenPresenter;
        private LossScreenPresenter? _lossScreenPresenter;
        [SerializeField] private GameStateViewPresenter? _gameStateViewPresenter;

        private bool _initialized;
        private bool _isPanelMinimized = true;
        private bool _isPlaying;
        private float _playAccumulator;
        private string _currentLevelId = string.Empty;
        private int _currentSeed = 1;
        private GameState? _currentState;
        private GameState? _initialState;
        private LevelJson? _testLevel;
        private TelemetryLogger? _telemetryLogger;
        private TelemetrySessionState? _telemetrySession;
        private ReplayResult? _loadedReplay;
        private int _replayFrameIndex;
        private string _replaySessionPath = string.Empty;
        private double _lastActionEndMs;

        public static DebugPanel? Instance => _instance;

        public GameState CurrentState => _currentState ?? throw new InvalidOperationException("Debug panel state is not initialized.");

        public string CurrentLevelId => _currentLevelId;

        public int CurrentSeed => _currentSeed;

        public LevelTuningOverrides CurrentTuningOverrides => _tuningOverrides;

        public int LoadRevision => _loadRevision;

        public string CurrentWaterForecastSummary => DebugPanelDisplay.GetWaterForecastSummary(CurrentState);

        public string CurrentNearRescueSummary => DebugPanelDisplay.GetNearRescueTargetsSummary(CurrentState);

        public bool IsPanelMinimized => _isPanelMinimized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapRuntimePanel()
        {
            if (ShouldBootstrapRuntimePanel())
            {
                EnsureInstance();
            }
        }

        private static bool ShouldBootstrapRuntimePanel()
        {
            if (string.Equals(SceneManager.GetActiveScene().name, "DebugGameplay", StringComparison.Ordinal))
            {
                return true;
            }

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-rescue-debug-panel", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static DebugPanel EnsureInstance()
        {
            if (_instance is not null)
            {
                return _instance;
            }

            GameObject host = new GameObject("DebugPanel");
            DontDestroyOnLoad(host);
            host.AddComponent<UIDocument>();
            return host.AddComponent<DebugPanel>();
        }

        private void Awake()
        {
            if (_instance is not null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _document = GetComponent<UIDocument>();
            _document.panelSettings = CreatePanelSettings();
            _panelSettings = _document.panelSettings;
            BuildPanelTree();
            BindVictoryScreen();
            BindLossScreen();
            ConfigureInputs();
            InitializeDefaultStateIfNeeded();
            RefreshUi();
            _initialized = true;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _inputActions.Count; i++)
            {
                _inputActions[i].Dispose();
            }

            _inputActions.Clear();
            _telemetryLogger?.Dispose();
            _telemetryLogger = null;

            if (_victoryScreenPresenter is not null)
            {
                _victoryScreenPresenter.ReplayRequested -= ReplayCurrentLevel;
                _victoryScreenPresenter.NextLevelRequested -= HandleVictoryNextLevelRequested;
            }

            if (_lossScreenPresenter is not null)
            {
                _lossScreenPresenter.ReplayRequested -= ReplayCurrentLevel;
                _lossScreenPresenter.TryAgainRequested -= ReplayCurrentLevel;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            RefreshPlaybackDebugUi();
            if (TrySyncCurrentStateFromVisualPresenter())
            {
                RefreshUi();
            }

            if (IsTerminalScreenVisible())
            {
                if (_isPlaying)
                {
                    _isPlaying = false;
                    UpdatePlayButtonLabel();
                }

                return;
            }

            if (!_isPlaying || _currentState is null)
            {
                return;
            }

            _playAccumulator += Time.unscaledDeltaTime;
            float secondsPerAction = 1.0f / GetEffectiveSpeed();
            while (_playAccumulator >= secondsPerAction)
            {
                _playAccumulator -= secondsPerAction;
                if (!StepOneAction())
                {
                    _isPlaying = false;
                    UpdatePlayButtonLabel();
                    break;
                }
            }
        }

        public void LoadLevel(LevelJson level, int seed)
        {
            _testLevel = level ?? throw new ArgumentNullException(nameof(level));
            _currentLevelId = level.Id;
            _currentSeed = seed;
            GameState loadedState = Loader.LoadLevel(level, seed, _tuningOverrides);
            SetLoadedState(loadedState, level.Id, seed, $"Loaded {_currentLevelId} with seed {seed}.");
        }

        public void ReloadCurrentLevel()
        {
            ReloadCurrentLevelInternal(emitTuneTelemetry: false, changeSource: "manual_reload", presetName: null);
        }

        private void ReloadCurrentLevelInternal(bool emitTuneTelemetry, string changeSource, string? presetName)
        {
            if (_testLevel is not null)
            {
                LoadLevel(_testLevel, _currentSeed);
                if (emitTuneTelemetry)
                {
                    EmitTuningTelemetry(changeSource, presetName);
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(_currentLevelId))
            {
                InitializeDefaultStateIfNeeded(forceReload: true);
                if (emitTuneTelemetry)
                {
                    EmitTuningTelemetry(changeSource, presetName);
                }

                return;
            }

            GameState loadedState = Loader.LoadLevel(_currentLevelId, _currentSeed, _tuningOverrides);
            SetLoadedState(loadedState, _currentLevelId, _currentSeed, $"Reloaded {_currentLevelId} with seed {_currentSeed}.");
            if (emitTuneTelemetry)
            {
                EmitTuningTelemetry(changeSource, presetName);
            }
        }

        public void ResetLevel()
        {
            HideTerminalScreens();

            if (_loadedReplay is not null)
            {
                _replayFrameIndex = 0;
                _eventLog.Clear();
                _currentState = _loadedReplay.InitialFrame.State;
                _initialState = _loadedReplay.InitialFrame.State;
                _isPlaying = false;
                _playAccumulator = 0.0f;
                UpdatePlayButtonLabel();
                SetStatus($"Reset replay {_currentLevelId} to frame 0.");
                RefreshVisualPresenter();
                RefreshUi();
                return;
            }

            if (_initialState is null)
            {
                return;
            }

            _debugUndo.Clear();
            _eventLog.Clear();
            _currentState = _initialState;
            _isPlaying = false;
            _playAccumulator = 0.0f;
            UpdatePlayButtonLabel();
            SetStatus($"Reset {_currentLevelId} to initial state for seed {_currentSeed}.");
            RefreshVisualPresenter();
            RefreshUi();
        }

        public void ReplayCurrentLevel()
        {
            ResetLevel();
        }

        public bool HasNextLevel()
        {
            return GetNextLevelId() is not null;
        }

        public bool LoadNextLevel()
        {
            string? nextLevelId = GetNextLevelId();
            if (string.IsNullOrWhiteSpace(nextLevelId))
            {
                SetStatus("No next level available.");
                SyncVictoryScreenNextAvailability();
                RefreshUi();
                return false;
            }

            HideTerminalScreens();
            ClearReplayState();
            _testLevel = null;
            _currentLevelId = nextLevelId;
            GameState loaded = LoadLevelById(_currentLevelId, _currentSeed);
            SetLoadedState(loaded, _currentLevelId, _currentSeed, $"Loaded {_currentLevelId} with seed {_currentSeed}.");
            return true;
        }

        public bool StepOneAction()
        {
            if (IsTerminalScreenVisible())
            {
                SetStatus("Terminal screen is active; only its buttons accept input.");
                RefreshUi();
                return false;
            }

            if (_loadedReplay is not null)
            {
                return StepReplayAction();
            }

            if (_currentState is null)
            {
                return false;
            }

            TileCoord? nextTap = FindFirstValidAction(_currentState);
            if (!nextTap.HasValue)
            {
                SetStatus("No valid action available.");
                RefreshUi();
                return false;
            }

            PushDebugUndo("Step one action");
            GameState stateBefore = _currentState;
            long actionStartMs = (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
            ActionResult result = Pipeline.RunAction(_currentState, new ActionInput(nextTap.Value));
            long actionEndMs = (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
            _currentState = result.State;
            AppendActionLog("Step 1 Action", result.Events, result.Outcome);

            if (_telemetryLogger is not null && _telemetrySession is not null)
            {
                try
                {
                    TelemetryHooks.OnAction(
                        _currentLevelId,
                        stateBefore,
                        new ActionInput(nextTap.Value),
                        result,
                        (ulong)(uint)_currentSeed,
                        actionStartMs,
                        actionEndMs,
                        _telemetrySession,
                        _telemetryLogger);
                }
                catch (Exception ex) when (IsTelemetryUnavailable(ex))
                {
                    DisableTelemetryAfterFailure(ex);
                }
            }

            string? capturedLossReplayPath = CaptureLossReplayIfNeeded(result.Outcome);
            string status = $"Stepped action at ({nextTap.Value.Row}, {nextTap.Value.Col}) -> {result.Outcome}.";
            if (!string.IsNullOrWhiteSpace(capturedLossReplayPath))
            {
                status += $" Captured loss replay to {capturedLossReplayPath}.";
            }

            SetStatus(status);
            ApplyActionResultToVisualPresenter(stateBefore, new ActionInput(nextTap.Value), result);
            RefreshUi();
            return true;
        }

        public void LoadReplaySession(string sessionJsonlPath)
        {
            if (string.IsNullOrWhiteSpace(sessionJsonlPath))
            {
                throw new ArgumentException("Replay session path is required.", nameof(sessionJsonlPath));
            }

            ReplayResult replay = ReplayRunner.ReplaySession(sessionJsonlPath, LoadLevelById);
            _loadedReplay = replay;
            _replayFrameIndex = 0;
            _replaySessionPath = sessionJsonlPath;
            HideTerminalScreens();
            _currentLevelId = replay.LevelId;
            _currentSeed = replay.Seed;
            _testLevel = null;
            _debugUndo.Clear();
            _eventLog.Clear();
            _isPlaying = false;
            _playAccumulator = 0.0f;
            _currentState = replay.InitialFrame.State;
            _initialState = replay.InitialFrame.State;
            UpdatePlayButtonLabel();
            SetStatus($"Loaded replay {Path.GetFileName(sessionJsonlPath)} for {replay.LevelId} seed {replay.Seed}.");
            RefreshVisualPresenter();
            RefreshUi();
        }

        public bool StepReplayAction()
        {
            if (_loadedReplay is null)
            {
                SetStatus("No replay is loaded.");
                RefreshUi();
                return false;
            }

            if (_replayFrameIndex >= _loadedReplay.Frames.Length - 1)
            {
                SetStatus("Replay already reached the final frame.");
                RefreshUi();
                return false;
            }

            _replayFrameIndex++;
            ReplayFrame frame = _loadedReplay.Frames[_replayFrameIndex];
            _currentState = frame.State;
            AppendActionLog($"Replay {_replayFrameIndex}", frame.Events, frame.Outcome ?? ActionOutcome.Ok);
            SetStatus($"Replay stepped to frame {_replayFrameIndex}/{_loadedReplay.Frames.Length - 1}.");
            RefreshVisualPresenter();
            if (IsLossOutcome(frame.Outcome ?? ActionOutcome.Ok))
            {
                ResolveLossScreenPresenter()?.Show();
            }
            RefreshUi();
            return true;
        }

        public bool DebugUndo()
        {
            if (IsTerminalScreenVisible())
            {
                SetStatus("Terminal screen is active; only its buttons accept input.");
                RefreshUi();
                return false;
            }

            if (_debugUndo.Count == 0)
            {
                SetStatus("Debug undo stack is empty.");
                RefreshUi();
                return false;
            }

            DebugUndoEntry entry = _debugUndo.Pop();
            _currentState = entry.State;
            HideTerminalScreens();
            SetStatus($"Debug undo restored: {entry.Reason}.");
            RefreshVisualPresenter();
            RefreshUi();
            return true;
        }

        public string ExportStateJson()
        {
            TrySyncCurrentStateFromVisualPresenter();
            return DebugJson.Serialize(DebugPanelExportBuilder.BuildBugReport(
                _currentLevelId,
                _currentSeed,
                DateTime.UtcNow.ToString("O"),
                CurrentState));
        }

        public string ExportFullGameStateJson()
        {
            TrySyncCurrentStateFromVisualPresenter();
            return DebugJson.Serialize(DebugPanelExportBuilder.BuildGameState(CurrentState));
        }

        public void CopyStateJsonToClipboard()
        {
            GUIUtility.systemCopyBuffer = ExportStateJson();
            SetStatus("Copied state JSON.");
            RefreshUi();
        }

        public void CopyFullGameStateJsonToClipboard()
        {
            GUIUtility.systemCopyBuffer = ExportFullGameStateJson();
            SetStatus("Copied full GameState JSON.");
            RefreshUi();
        }

        public void ConfigureForTest(LevelJson level, int seed)
        {
            LoadLevel(level, seed);
        }

        private void InitializeDefaultStateIfNeeded(bool forceReload = false)
        {
            if (!forceReload && _currentState is not null)
            {
                return;
            }

            List<string> levels = EnumerateLevelIds();
            for (int i = 0; i < levels.Count; i++)
            {
                try
                {
                    _currentLevelId = levels[i];
                    _currentSeed = 1;
                    GameState loaded = LoadLevelById(_currentLevelId, _currentSeed);
                    SetLoadedState(loaded, _currentLevelId, _currentSeed, $"Loaded {_currentLevelId} with seed {_currentSeed}.");
                    return;
                }
                catch (Exception)
                {
                    // Keep scanning until a valid level is found.
                }
            }

            LevelJson fallback = CreateFallbackLevel();
            LoadLevel(fallback, seed: 1);
        }

        private void SetLoadedState(GameState state, string levelId, int seed, string status)
        {
            HideTerminalScreens();
            ClearReplayState();
            _currentState = state;
            _initialState = state;
            _currentLevelId = levelId;
            _currentSeed = seed;
            _debugUndo.Clear();
            _eventLog.Clear();
            _isPlaying = false;
            _playAccumulator = 0.0f;
            _loadRevision++;
            UpdatePlayButtonLabel();
            SyncLevelSelectorChoices(levelId);
            SetStatus(status);
            RefreshVisualPresenter();
            RefreshUi();
            SyncVictoryScreenNextAvailability();
            StartTelemetrySession(state, levelId, seed);
            OnLevelLoadedForTuning();
        }

        private void RefreshVisualPresenter()
        {
            if (_currentState is null)
            {
                return;
            }

            GameStateViewPresenter? presenter = ResolveGameStateViewPresenter();
            if (presenter is null)
            {
                return;
            }

            presenter.Rebuild(_currentState);
        }

        private void ApplyActionResultToVisualPresenter(GameState previousState, ActionInput input, ActionResult result)
        {
            GameStateViewPresenter? presenter = ResolveGameStateViewPresenter();
            if (presenter is null)
            {
                return;
            }

            presenter.ApplyActionResult(previousState, input, result);
        }

        private bool TrySyncCurrentStateFromVisualPresenter()
        {
            GameStateViewPresenter? presenter = ResolveGameStateViewPresenter();
            if (presenter is null || presenter.IsPlaybackActive || presenter.CurrentState is null)
            {
                return false;
            }

            if (ReferenceEquals(_currentState, presenter.CurrentState))
            {
                return false;
            }

            _currentState = presenter.CurrentState;
            return true;
        }

        private GameStateViewPresenter? ResolveGameStateViewPresenter()
        {
            if (_gameStateViewPresenter != null)
            {
                return _gameStateViewPresenter;
            }

            _gameStateViewPresenter = UnityEngine.Object.FindFirstObjectByType<GameStateViewPresenter>();
            return _gameStateViewPresenter;
        }

        private ActionPlaybackController? ResolveActionPlaybackController()
        {
            GameStateViewPresenter? presenter = ResolveGameStateViewPresenter();
            if (presenter is not null && presenter.TryGetComponent(out ActionPlaybackController controller))
            {
                return controller;
            }

            return UnityEngine.Object.FindFirstObjectByType<ActionPlaybackController>();
        }

        private VictoryScreenPresenter? ResolveVictoryScreenPresenter()
        {
            if (_victoryScreenPresenter is not null)
            {
                return _victoryScreenPresenter;
            }

            _victoryScreenPresenter = VictoryScreenPresenter.EnsureInstance();
            return _victoryScreenPresenter;
        }

        private LossScreenPresenter? ResolveLossScreenPresenter()
        {
            if (_lossScreenPresenter is not null)
            {
                return _lossScreenPresenter;
            }

            _lossScreenPresenter = LossScreenPresenter.EnsureInstance();
            return _lossScreenPresenter;
        }

        private void BindVictoryScreen()
        {
            VictoryScreenPresenter? presenter = ResolveVictoryScreenPresenter();
            if (presenter is null)
            {
                return;
            }

            presenter.ReplayRequested -= ReplayCurrentLevel;
            presenter.ReplayRequested += ReplayCurrentLevel;
            presenter.NextLevelRequested -= HandleVictoryNextLevelRequested;
            presenter.NextLevelRequested += HandleVictoryNextLevelRequested;
            SyncVictoryScreenNextAvailability();
        }

        private void BindLossScreen()
        {
            LossScreenPresenter? presenter = ResolveLossScreenPresenter();
            if (presenter is null)
            {
                return;
            }

            presenter.ReplayRequested -= ReplayCurrentLevel;
            presenter.ReplayRequested += ReplayCurrentLevel;
            presenter.TryAgainRequested -= ReplayCurrentLevel;
            presenter.TryAgainRequested += ReplayCurrentLevel;
        }

        private void HandleVictoryNextLevelRequested()
        {
            LoadNextLevel();
        }

        private void HideTerminalScreens()
        {
            ResolveVictoryScreenPresenter()?.Hide();
            ResolveLossScreenPresenter()?.Hide();
        }

        private bool IsTerminalScreenVisible()
        {
            return (_victoryScreenPresenter is not null && _victoryScreenPresenter.IsVisible)
                || (_lossScreenPresenter is not null && _lossScreenPresenter.IsVisible);
        }

        private static bool IsLossOutcome(ActionOutcome outcome)
        {
            return outcome == ActionOutcome.LossDockOverflow
                || outcome == ActionOutcome.LossWaterOnTarget
                || outcome == ActionOutcome.LossRescuePathFlooded
                || outcome == ActionOutcome.LossDistressedExpired;
        }

        private void SyncVictoryScreenNextAvailability()
        {
            ResolveVictoryScreenPresenter()?.SetNextLevelAvailable(HasNextLevel());
        }

        private void ApplyPlaybackControlsFromUi()
        {
            ActionPlaybackController? controller = ResolveActionPlaybackController();
            if (controller is null)
            {
                SetStatus("No action playback controller found.");
                RefreshPlaybackDebugUi();
                return;
            }

            bool enabled = _playbackEnabledToggle?.value ?? controller.Settings.PlaybackEnabled;
            float speed = ReadSpeedSlider(_playbackSpeedSlider, controller.Settings.PlaybackSpeedMultiplier);
            float boardActionSpeed = ReadSpeedSlider(_playbackBoardActionSpeedSlider, controller.Settings.BoardActionSpeedMultiplier);
            float dockSpeed = ReadSpeedSlider(_playbackDockSpeedSlider, controller.Settings.DockSpeedMultiplier);
            float targetSpeed = ReadSpeedSlider(_playbackTargetSpeedSlider, controller.Settings.TargetSpeedMultiplier);
            float hazardSpeed = ReadSpeedSlider(_playbackHazardSpeedSlider, controller.Settings.HazardSpeedMultiplier);
            float terminalSpeed = ReadSpeedSlider(_playbackTerminalSpeedSlider, controller.Settings.TerminalSpeedMultiplier);
            float gravitySpawnSpeed = ReadSpeedSlider(_playbackGravitySpawnSpeedSlider, controller.Settings.GravitySpawnSpeedMultiplier);
            controller.ConfigureDebugPlayback(
                enabled,
                speed,
                boardActionSpeed,
                dockSpeed,
                targetSpeed,
                hazardSpeed,
                terminalSpeed,
                gravitySpawnSpeed);
            FxEventRouter? router = ResolveFxEventRouter();
            if (router is not null)
            {
                router.FxPlaybackSpeedMultiplier = ReadSpeedSlider(_fxPlaybackSpeedSlider, router.FxPlaybackSpeedMultiplier);
            }

            SetStatus($"Action playback {(enabled ? "enabled" : "disabled")} at {FormatSpeed(controller.Settings.PlaybackSpeedMultiplier)}.");
            RefreshPlaybackDebugUi();
        }

        private void RefreshPlaybackDebugUi()
        {
            ActionPlaybackController? controller = ResolveActionPlaybackController();
            if (controller is null)
            {
                DebugPanelReplayStatus.UpdatePlaybackUnavailable(_playbackStepValue);
                return;
            }

            if (_playbackEnabledToggle is not null)
            {
                _playbackEnabledToggle.SetValueWithoutNotify(controller.Settings.PlaybackEnabled);
            }

            RefreshSpeedSlider(_playbackSpeedSlider, _playbackSpeedValue, controller.Settings.PlaybackSpeedMultiplier);
            RefreshSpeedSlider(_playbackBoardActionSpeedSlider, _playbackBoardActionSpeedValue, controller.Settings.BoardActionSpeedMultiplier);
            RefreshSpeedSlider(_playbackDockSpeedSlider, _playbackDockSpeedValue, controller.Settings.DockSpeedMultiplier);
            RefreshSpeedSlider(_playbackTargetSpeedSlider, _playbackTargetSpeedValue, controller.Settings.TargetSpeedMultiplier);
            RefreshSpeedSlider(_playbackHazardSpeedSlider, _playbackHazardSpeedValue, controller.Settings.HazardSpeedMultiplier);
            RefreshSpeedSlider(_playbackTerminalSpeedSlider, _playbackTerminalSpeedValue, controller.Settings.TerminalSpeedMultiplier);
            RefreshSpeedSlider(_playbackGravitySpawnSpeedSlider, _playbackGravitySpawnSpeedValue, controller.Settings.GravitySpawnSpeedMultiplier);
            FxEventRouter? router = ResolveFxEventRouter();
            if (router is not null)
            {
                RefreshSpeedSlider(_fxPlaybackSpeedSlider, _fxPlaybackSpeedValue, router.FxPlaybackSpeedMultiplier);
            }

            DebugPanelReplayStatus.UpdatePlaybackStep(_playbackStepValue, controller.CurrentStepName);
        }

        private static float ReadSpeedSlider(Slider? slider, float fallback)
        {
            return slider is null
                ? fallback
                : Mathf.Clamp(slider.value, FxEventRouter.MinFxPlaybackSpeedMultiplier, FxEventRouter.MaxFxPlaybackSpeedMultiplier);
        }

        private static void RefreshSpeedSlider(Slider? slider, Label? valueLabel, float speed)
        {
            float clampedSpeed = Mathf.Clamp(speed, FxEventRouter.MinFxPlaybackSpeedMultiplier, FxEventRouter.MaxFxPlaybackSpeedMultiplier);
            slider?.SetValueWithoutNotify(clampedSpeed);
            if (valueLabel is not null)
            {
                valueLabel.text = FormatSpeed(clampedSpeed);
            }
        }

        private void PlayAllFxDiagnostics()
        {
            FxEventRouter? router = ResolveFxEventRouter();
            if (router is null)
            {
                SetStatus("No FX event router found.");
                return;
            }

            Vector3 position = ResolveFxDiagnosticPosition(router);
            router.PlayAllRegisteredFxForDiagnostics(position);
            _fxDiagnosticsToggle?.SetValueWithoutNotify(router.DiagnosticsEnabled);
            SetStatus($"Playing all FX diagnostics at {position}.");
        }

        private void ApplyFxDiagnosticsFromUi()
        {
            FxEventRouter? router = ResolveFxEventRouter();
            if (router is null)
            {
                SetStatus("No FX event router found.");
                return;
            }

            bool enabled = _fxDiagnosticsToggle?.value ?? router.DiagnosticsEnabled;
            router.DiagnosticsEnabled = enabled;
            router.DiagnosticMinimumVisibleSeconds = enabled ? Mathf.Max(router.DiagnosticMinimumVisibleSeconds, 0.5f) : 0f;
            SetStatus(enabled ? "FX diagnostics enabled." : "FX diagnostics disabled.");
        }

        private FxEventRouter? ResolveFxEventRouter()
        {
            ActionPlaybackController? controller = ResolveActionPlaybackController();
            if (controller is not null && controller.TryGetComponent(out FxEventRouter router))
            {
                return router;
            }

            return UnityEngine.Object.FindFirstObjectByType<FxEventRouter>();
        }

        private Vector3 ResolveFxDiagnosticPosition(FxEventRouter router)
        {
            if (_currentState is not null
                && _currentState.Board.Height > 0
                && _currentState.Board.Width > 0
                && router.BoardGrid is not null)
            {
                int row = Mathf.Clamp(_currentState.Board.Height / 2, 0, _currentState.Board.Height - 1);
                int col = Mathf.Clamp(_currentState.Board.Width / 2, 0, _currentState.Board.Width - 1);
                if (router.BoardGrid.TryGetCellWorldPosition(new TileCoord(row, col), out Vector3 cellPosition))
                {
                    return cellPosition;
                }
            }

            if (router.FxRoot is not null)
            {
                return router.FxRoot.position;
            }

            return router.transform.position;
        }

        private static float ParseSpeedMultiplier(string? speed)
        {
            return speed switch
            {
                "0.25x" => 0.25f,
                "0.5x" => 0.5f,
                "2x" => 2.0f,
                "4x" => 4.0f,
                _ => 1.0f,
            };
        }

        private static string FormatSpeed(float speed)
        {
            if (Mathf.Approximately(speed, 0.25f))
            {
                return "0.25x";
            }

            if (Mathf.Approximately(speed, 0.5f))
            {
                return "0.5x";
            }

            if (Mathf.Approximately(speed, 2.0f))
            {
                return "2x";
            }

            if (Mathf.Approximately(speed, 4.0f))
            {
                return "4x";
            }

            return speed.ToString("0.##", CultureInfo.InvariantCulture) + "x";
        }

        private void StartTelemetrySession(GameState state, string levelId, int seed)
        {
            _telemetryLogger?.Dispose();
            _telemetryLogger = null;
            _telemetrySession = null;

#if UNITY_ANDROID && !UNITY_EDITOR
            UnityEngine.Debug.LogWarning(
                "DebugPanel telemetry is disabled on Android dev builds because the current telemetry JSON backend is not AOT-safe.");
            return;
#else

            string sessionId = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N")[..6];
            string logPath = Path.Combine(
                Application.persistentDataPath, "telemetry", sessionId + ".jsonl");

            try
            {
                TelemetryConfig config = TelemetryConfig.DevDefaults;
                _telemetryLogger = new TelemetryLogger(logPath, config);

                long nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
                _lastActionEndMs = nowMs;
                _telemetrySession = new TelemetrySessionState { LevelStartMs = nowMs };

                TelemetryHooks.OnLevelStart(levelId, (ulong)(uint)seed, state, nowMs, _telemetryLogger);
            }
            catch (Exception ex) when (IsTelemetryUnavailable(ex))
            {
                DisableTelemetryAfterFailure(ex);
            }
#endif
        }

        private void DisableTelemetryAfterFailure(Exception ex)
        {
            _telemetryLogger?.Dispose();
            _telemetryLogger = null;
            _telemetrySession = null;
            UnityEngine.Debug.LogWarning($"DebugPanel telemetry disabled after startup/action failure: {ex.GetType().Name}: {ex.Message}");
        }

        private static bool IsTelemetryUnavailable(Exception ex)
        {
            Exception? current = ex;
            while (current is not null)
            {
                if (current is PlatformNotSupportedException || current is NotSupportedException)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        private void ClearReplayState()
        {
            _loadedReplay = null;
            _replayFrameIndex = 0;
            _replaySessionPath = string.Empty;
        }

        private string? CaptureLossReplayIfNeeded(ActionOutcome outcome)
        {
            if (outcome != ActionOutcome.LossDockOverflow
                && outcome != ActionOutcome.LossWaterOnTarget
                && outcome != ActionOutcome.LossRescuePathFlooded
                && outcome != ActionOutcome.LossDistressedExpired)
            {
                return null;
            }

            if (_telemetryLogger is null || string.IsNullOrWhiteSpace(_telemetryLogger.OutputPath) || !File.Exists(_telemetryLogger.OutputPath))
            {
                return null;
            }

            string lossesDirectory = Path.Combine(Application.persistentDataPath, "telemetry", "losses");
            return ReplayRunner.CaptureLossSession(
                _telemetryLogger.OutputPath,
                _currentLevelId,
                _currentSeed,
                lossesDirectory,
                DateTimeOffset.UtcNow,
                retentionCap: LossReplayRetentionCap);
        }

        private void ConfigureInputs()
        {
            RegisterAction("Toggle Panel", "<Keyboard>/f1", _ => TogglePanelMinimized());
            RegisterAction("Step Action", "<Keyboard>/f2", _ => StepOneAction());
            RegisterAction("Reset Level", "<Keyboard>/f3", _ => ResetLevel());
            RegisterAction("Debug Undo", "<Keyboard>/f4", _ => DebugUndo());

            InputAction fastForwardAction = new InputAction("Fast Forward", binding: "<Keyboard>/f");
            fastForwardAction.performed += _ =>
            {
                if (IsTerminalScreenVisible())
                {
                    return;
                }

                Keyboard? keyboard = Keyboard.current;
                bool shiftPressed = keyboard is not null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
                if (shiftPressed)
                {
                    ToggleFastForward();
                }
            };

            fastForwardAction.Enable();
            _inputActions.Add(fastForwardAction);
        }

        private void RegisterAction(string name, string binding, Action<InputAction.CallbackContext> callback)
        {
            InputAction action = new InputAction(name, binding: binding);
            action.performed += context =>
            {
                if (IsTerminalScreenVisible())
                {
                    return;
                }

                callback(context);
            };
            action.Enable();
            _inputActions.Add(action);
        }

        private PanelSettings CreatePanelSettings()
        {
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 1000;
            settings.clearColor = false;
            settings.colorClearValue = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            ApplyRuntimeTheme(settings);
            return settings;
        }

        private static void ApplyRuntimeTheme(PanelSettings settings)
        {
            Type? themeStyleSheetType = Type.GetType("UnityEngine.UIElements.ThemeStyleSheet, UnityEngine.UIElementsModule");
            if (themeStyleSheetType is null)
            {
                return;
            }

            UnityEngine.Object? themeStyleSheet = Resources.Load(RuntimeThemeResourcePath, themeStyleSheetType);
            if (themeStyleSheet is null)
            {
                return;
            }

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type settingsType = settings.GetType();

            PropertyInfo? themeProperty = settingsType.GetProperty("themeStyleSheet", Flags)
                ?? settingsType.GetProperty("themeUss", Flags);
            if (themeProperty is not null && themeProperty.CanWrite && themeProperty.PropertyType.IsInstanceOfType(themeStyleSheet))
            {
                themeProperty.SetValue(settings, themeStyleSheet);
                return;
            }

            FieldInfo? themeField = settingsType.GetField("themeStyleSheet", Flags)
                ?? settingsType.GetField("themeUss", Flags);
            if (themeField is not null && themeField.FieldType.IsInstanceOfType(themeStyleSheet))
            {
                themeField.SetValue(settings, themeStyleSheet);
            }
        }

        private void BuildPanelTree()
        {
            if (_document is null)
            {
                throw new InvalidOperationException("UIDocument is missing.");
            }

            VisualElement root = _document.rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1.0f;
            root.UnregisterCallback<PointerDownEvent>(OnRootPointerDown);
            root.RegisterCallback<PointerDownEvent>(OnRootPointerDown);
            _documentRoot = root;

            VisualTreeAsset? asset = TryLoadUxmlAsset();
            VisualElement panel = asset is not null ? asset.CloneTree() : CreatePanelFallback();
            panel.name = "debug-panel-root";
            panel.style.display = DisplayStyle.Flex;
            root.Add(panel);
            _panelRoot = panel;

            StyleSheet? styleSheet = TryLoadStyleSheet();
            if (styleSheet is not null)
            {
                root.styleSheets.Add(styleSheet);
            }

            BindUi(panel);
        }

        private VisualTreeAsset? TryLoadUxmlAsset()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlAssetPath);
#else
            return null;
#endif
        }

        private StyleSheet? TryLoadStyleSheet()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(UssAssetPath);
#else
            return null;
#endif
        }

        private void BindUi(VisualElement panel)
        {
            if (panel is TemplateContainer template)
            {
                panel = template;
            }

            _levelSelector = panel.Q<DropdownField>("level-selector");
            _seedField = panel.Q<IntegerField>("seed-field");
            _randomSeedButton = panel.Q<Button>("random-seed-button");
            _playPauseButton = panel.Q<Button>("play-pause-button");
            _stepButton = panel.Q<Button>("step-button");
            _speedSelector = panel.Q<DropdownField>("speed-selector");
            _fastForwardToggle = panel.Q<Toggle>("fast-forward-toggle");
            _playbackEnabledToggle = panel.Q<Toggle>("playback-enabled-toggle");
            _playbackSpeedSlider = panel.Q<Slider>("playback-speed-slider");
            _playbackSpeedValue = panel.Q<Label>("playback-speed-value");
            _playbackBoardActionSpeedSlider = panel.Q<Slider>("playback-board-action-speed-slider");
            _playbackBoardActionSpeedValue = panel.Q<Label>("playback-board-action-speed-value");
            _playbackDockSpeedSlider = panel.Q<Slider>("playback-dock-speed-slider");
            _playbackDockSpeedValue = panel.Q<Label>("playback-dock-speed-value");
            _playbackTargetSpeedSlider = panel.Q<Slider>("playback-target-speed-slider");
            _playbackTargetSpeedValue = panel.Q<Label>("playback-target-speed-value");
            _playbackHazardSpeedSlider = panel.Q<Slider>("playback-hazard-speed-slider");
            _playbackHazardSpeedValue = panel.Q<Label>("playback-hazard-speed-value");
            _playbackTerminalSpeedSlider = panel.Q<Slider>("playback-terminal-speed-slider");
            _playbackTerminalSpeedValue = panel.Q<Label>("playback-terminal-speed-value");
            _playbackGravitySpawnSpeedSlider = panel.Q<Slider>("playback-gravity-spawn-speed-slider");
            _playbackGravitySpawnSpeedValue = panel.Q<Label>("playback-gravity-spawn-speed-value");
            _fxPlaybackSpeedSlider = panel.Q<Slider>("fx-playback-speed-slider");
            _fxPlaybackSpeedValue = panel.Q<Label>("fx-playback-speed-value");
            _playbackStepValue = panel.Q<Label>("playback-step-value");
            _fxDiagnosticsToggle = panel.Q<Toggle>("fx-diagnostics-toggle");
            _playAllFxButton = panel.Q<Button>("play-all-fx-button");
            _debugUndoButton = panel.Q<Button>("debug-undo-button");
            _resetButton = panel.Q<Button>("reset-button");
            _replayPathField = panel.Q<TextField>("replay-path-field");
            _loadReplayButton = panel.Q<Button>("load-replay-button");
            _stepReplayButton = panel.Q<Button>("step-replay-button");
            _clearReplayButton = panel.Q<Button>("clear-replay-button");
            _replayStatusValue = panel.Q<Label>("replay-status-value");
            _statusLabel = panel.Q<Label>("status-label");
            _waterActionsValue = panel.Q<Label>("water-actions-value");
            _waterRiseIntervalValue = panel.Q<Label>("water-rise-interval-value");
            _waterNextFloodRowValue = panel.Q<Label>("water-next-row-value");
            _waterForecastValue = panel.Q<Label>("water-forecast-value");
            _ruleTeachValue = panel.Q<Label>("rule-teach-value");
            _vineActionsValue = panel.Q<Label>("vine-actions-value");
            _vineThresholdValue = panel.Q<Label>("vine-threshold-value");
            _vinePendingValue = panel.Q<Label>("vine-pending-value");
            _dockOccupancyValue = panel.Q<Label>("dock-occupancy-value");
            _dockWarningValue = panel.Q<Label>("dock-warning-value");
            _dockContentsValue = panel.Q<Label>("dock-contents-value");
            _dockJamUsedValue = panel.Q<Label>("dock-jam-used-value");
            _dockJamEnabledValue = panel.Q<Label>("dock-jam-enabled-value");
            _nearRescueTargetsValue = panel.Q<Label>("near-rescue-targets-value");
            _rngStateValue = panel.Q<Label>("rng-state-value");
            _copyRngButton = panel.Q<Button>("copy-rng-button");
            _assistanceOverrideField = panel.Q<DropdownField>("assistance-override-field");
            _forceEmergencyField = panel.Q<DropdownField>("force-emergency-field");
            _consecutiveEmergencyValue = panel.Q<Label>("consecutive-emergency-value");
            _spawnRecoveryValue = panel.Q<Label>("spawn-recovery-value");
            _overflowButton = panel.Q<Button>("overflow-button");
            _copyStateButton = panel.Q<Button>("copy-state-button");
            _copyFullStateButton = panel.Q<Button>("copy-full-state-button");
            _eventLogList = panel.Q<VisualElement>("event-log-list");
            _panelBody = panel.Q<VisualElement>("debug-panel-body");
            _minimizeButton = panel.Q<Button>("debug-minimize-button");
            BindFxDebugUi(panel);

            if (_minimizeButton is not null)
            {
                _minimizeButton.clicked += TogglePanelMinimized;
            }

            if (_levelSelector is not null)
            {
                _levelSelector.choices = EnumerateLevelIds();
                _levelSelector.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized || string.Equals(evt.previousValue, evt.newValue, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _testLevel = null;
                    _currentLevelId = evt.newValue;
                    ReloadCurrentLevel();
                });
            }

            if (_seedField is not null)
            {
                _seedField.isDelayed = true;
                _seedField.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized || evt.newValue == evt.previousValue)
                    {
                        return;
                    }

                    _currentSeed = evt.newValue;
                    ReloadCurrentLevel();
                });
            }

            if (_randomSeedButton is not null)
            {
                _randomSeedButton.clicked += () =>
                {
                    int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    Debug.Log($"[DebugPanel] Seed randomized to {seed}");
                    _currentSeed = seed;
                    ReloadCurrentLevel();
                };
            }

            if (_playPauseButton is not null)
            {
                _playPauseButton.clicked += TogglePlayPause;
            }

            if (_stepButton is not null)
            {
                _stepButton.clicked += () => StepOneAction();
            }

            if (_speedSelector is not null)
            {
                _speedSelector.choices = new List<string>(SpeedChoices);
                _speedSelector.value = "1x";
            }

            if (_fastForwardToggle is not null)
            {
                _fastForwardToggle.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized)
                    {
                        return;
                    }

                    SetStatus(evt.newValue ? "Fast-forward enabled." : "Fast-forward disabled.");
                    RefreshUi();
                });
            }

            if (_playbackEnabledToggle is not null)
            {
                _playbackEnabledToggle.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized || evt.previousValue == evt.newValue)
                    {
                        return;
                    }

                    ApplyPlaybackControlsFromUi();
                });
            }

            RegisterPlaybackSpeedSlider(_playbackSpeedSlider, _playbackSpeedValue, ActionPlaybackSettings.DefaultPlaybackSpeedMultiplier);
            RegisterPlaybackSpeedSlider(_playbackBoardActionSpeedSlider, _playbackBoardActionSpeedValue, ActionPlaybackSettings.DefaultGroupSpeedMultiplier);
            RegisterPlaybackSpeedSlider(_playbackDockSpeedSlider, _playbackDockSpeedValue, ActionPlaybackSettings.DefaultGroupSpeedMultiplier);
            RegisterPlaybackSpeedSlider(_playbackTargetSpeedSlider, _playbackTargetSpeedValue, ActionPlaybackSettings.DefaultGroupSpeedMultiplier);
            RegisterPlaybackSpeedSlider(_playbackHazardSpeedSlider, _playbackHazardSpeedValue, ActionPlaybackSettings.DefaultGroupSpeedMultiplier);
            RegisterPlaybackSpeedSlider(_playbackTerminalSpeedSlider, _playbackTerminalSpeedValue, ActionPlaybackSettings.DefaultGroupSpeedMultiplier);
            RegisterPlaybackSpeedSlider(_playbackGravitySpawnSpeedSlider, _playbackGravitySpawnSpeedValue, ActionPlaybackSettings.DefaultGravitySpawnSpeedMultiplier);
            RegisterPlaybackSpeedSlider(_fxPlaybackSpeedSlider, _fxPlaybackSpeedValue, FxEventRouter.DefaultFxPlaybackSpeedMultiplier);

            if (_playAllFxButton is not null)
            {
                _playAllFxButton.clicked += PlayAllFxDiagnostics;
            }

            if (_fxDiagnosticsToggle is not null)
            {
                FxEventRouter? router = ResolveFxEventRouter();
                _fxDiagnosticsToggle.SetValueWithoutNotify(router is not null && router.DiagnosticsEnabled);
                _fxDiagnosticsToggle.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized || evt.previousValue == evt.newValue)
                    {
                        return;
                    }

                    ApplyFxDiagnosticsFromUi();
                });
            }

            if (_debugUndoButton is not null)
            {
                _debugUndoButton.clicked += () => DebugUndo();
            }

            if (_resetButton is not null)
            {
                _resetButton.clicked += ResetLevel;
            }

            if (_loadReplayButton is not null)
            {
                _loadReplayButton.clicked += () =>
                {
                    if (_replayPathField is null)
                    {
                        return;
                    }

                    LoadReplaySession(_replayPathField.value);
                };
            }

            if (_stepReplayButton is not null)
            {
                _stepReplayButton.clicked += () => StepReplayAction();
            }

            if (_clearReplayButton is not null)
            {
                _clearReplayButton.clicked += () =>
                {
                    if (_loadedReplay is null)
                    {
                        SetStatus("No replay is loaded.");
                        RefreshUi();
                        return;
                    }

                    ClearReplayState();
                    ReloadCurrentLevel();
                };
            }

            if (_copyRngButton is not null)
            {
                _copyRngButton.clicked += CopyRngStateToClipboard;
            }

            if (_assistanceOverrideField is not null)
            {
                _assistanceOverrideField.choices = new List<string>(AssistanceChoices);
                _assistanceOverrideField.value = "Current";
                _assistanceOverrideField.RegisterValueChangedCallback(_ => ApplySpawnOverrideFromUi());
            }

            if (_forceEmergencyField is not null)
            {
                _forceEmergencyField.choices = new List<string>(EmergencyChoices);
                _forceEmergencyField.value = "Auto";
                _forceEmergencyField.RegisterValueChangedCallback(_ => ApplySpawnOverrideFromUi());
            }

            if (_overflowButton is not null)
            {
                _overflowButton.clicked += RunInstantOverflowTest;
            }

            if (_copyStateButton is not null)
            {
                _copyStateButton.clicked += CopyStateJsonToClipboard;
            }

            if (_copyFullStateButton is not null)
            {
                _copyFullStateButton.clicked += CopyFullGameStateJsonToClipboard;
            }

            BindTuningUi(panel);
            SyncPanelMinimizedState();
        }

        private VisualElement CreatePanelFallback()
        {
            return CreateFallbackWithTuningTabs();
        }

        public void SetPanelMinimized(bool minimized)
        {
            _isPanelMinimized = minimized;
            SyncPanelMinimizedState();
        }

        public void SimulateOutsideClickForTest()
        {
            HandleDocumentPointerDown(_documentRoot);
        }

        public void SimulateInsideClickForTest()
        {
            HandleDocumentPointerDown(_panelRoot);
        }

        private void TogglePanelMinimized()
        {
            SetPanelMinimized(!_isPanelMinimized);
        }

        private void SyncPanelMinimizedState()
        {
            if (_panelBody is not null)
            {
                _panelBody.style.display = _isPanelMinimized ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (_minimizeButton is not null)
            {
                _minimizeButton.text = _isPanelMinimized ? "Open" : "Minimize";
            }
        }

        private void RegisterPlaybackSpeedSlider(Slider? slider, Label? valueLabel, float initialSpeed)
        {
            if (slider is null)
            {
                return;
            }

            slider.lowValue = FxEventRouter.MinFxPlaybackSpeedMultiplier;
            slider.highValue = FxEventRouter.MaxFxPlaybackSpeedMultiplier;
            RefreshSpeedSlider(slider, valueLabel, initialSpeed);
            slider.RegisterValueChangedCallback(evt =>
            {
                RefreshSpeedSlider(null, valueLabel, evt.newValue);
                if (!_initialized || Mathf.Approximately(evt.previousValue, evt.newValue))
                {
                    return;
                }

                ApplyPlaybackControlsFromUi();
            });
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            HandleDocumentPointerDown(evt.target as VisualElement);
        }

        private void HandleDocumentPointerDown(VisualElement? clickedElement)
        {
            if (_isPanelMinimized || _panelRoot is null || clickedElement is null)
            {
                return;
            }

            if (clickedElement == _panelRoot || _panelRoot.Contains(clickedElement))
            {
                return;
            }

            SetPanelMinimized(true);
        }

        private void TogglePlayPause()
        {
            if (_loadedReplay is not null && _replayFrameIndex >= _loadedReplay.Frames.Length - 1)
            {
                SetStatus("Replay is already at the final frame.");
                RefreshUi();
                return;
            }

            _isPlaying = !_isPlaying;
            _playAccumulator = 0.0f;
            UpdatePlayButtonLabel();
            SetStatus(_loadedReplay is null
                ? (_isPlaying ? "Auto-step playing." : "Auto-step paused.")
                : (_isPlaying ? "Replay auto-step playing." : "Replay auto-step paused."));
            RefreshUi();
        }

        private void ToggleFastForward()
        {
            if (_fastForwardToggle is null)
            {
                return;
            }

            _fastForwardToggle.value = !_fastForwardToggle.value;
        }

        private void UpdatePlayButtonLabel()
        {
            if (_playPauseButton is not null)
            {
                string playLabel = _loadedReplay is null ? "Play" : "Play Replay";
                _playPauseButton.text = _isPlaying ? "Pause" : playLabel;
            }
        }

        private float GetEffectiveSpeed()
        {
            float speed = _speedSelector?.value switch
            {
                "0.25x" => 0.25f,
                "0.5x" => 0.5f,
                "2x" => 2.0f,
                "4x" => 4.0f,
                _ => 1.0f,
            };

            if (_fastForwardToggle?.value == true)
            {
                return Mathf.Max(speed, 4.0f);
            }

            return speed;
        }

        private void ApplySpawnOverrideFromUi()
        {
            if (_currentState is null)
            {
                return;
            }

            SpawnOverride? spawnOverride = BuildSpawnOverrideFromUi();
            PushDebugUndo("Spawn override changed");
            _currentState = _currentState with { DebugSpawnOverride = spawnOverride };
            SetStatus(spawnOverride is null ? "Spawn override cleared." : "Spawn override updated.");
            RefreshUi();
        }

        private SpawnOverride? BuildSpawnOverrideFromUi()
        {
            double? assistanceChance = _assistanceOverrideField?.value switch
            {
                "0" => 0.0d,
                "1" => 1.0d,
                _ => null,
            };

            bool? forceEmergency = _forceEmergencyField?.value switch
            {
                "On" => true,
                "Off" => false,
                _ => null,
            };

            if (!assistanceChance.HasValue && !forceEmergency.HasValue)
            {
                return null;
            }

            return new SpawnOverride(forceEmergency, assistanceChance);
        }

        private void RunInstantOverflowTest()
        {
            if (_currentState is null)
            {
                return;
            }

            if (!TryPrepareOverflowState(_currentState, out GameState preparedState, out TileCoord tapCoord))
            {
                SetStatus("Unable to prepare overflow test on this board.");
                RefreshUi();
                return;
            }

            PushDebugUndo("Instant overflow test");
            GameState stateBefore = preparedState;
            ActionInput input = new ActionInput(tapCoord);
            ActionResult result = Pipeline.RunAction(preparedState, input);
            _currentState = result.State;
            AppendActionLog("Instant Overflow Test", result.Events, result.Outcome);
            SetStatus($"Instant overflow test executed -> {result.Outcome}.");
            ApplyActionResultToVisualPresenter(stateBefore, input, result);
            RefreshUi();
        }

        private bool TryPrepareOverflowState(GameState source, out GameState preparedState, out TileCoord tapCoord)
        {
            Board board = source.Board;
            TileCoord first = default;
            TileCoord second = default;
            bool found = false;

            for (int row = 0; row < board.Height && !found; row++)
            {
                for (int col = 0; col < board.Width && !found; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (!IsOverflowCandidate(board, coord))
                    {
                        continue;
                    }

                    ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, coord);
                    for (int i = 0; i < neighbors.Length; i++)
                    {
                        if (IsOverflowCandidate(board, neighbors[i]))
                        {
                            first = coord;
                            second = neighbors[i];
                            found = true;
                            break;
                        }
                    }
                }
            }

            if (!found)
            {
                preparedState = source;
                tapCoord = default;
                return false;
            }

            Board updatedBoard = BoardHelpers.SetTile(board, first, new DebrisTile(DebrisType.A));
            updatedBoard = BoardHelpers.SetTile(updatedBoard, second, new DebrisTile(DebrisType.A));
            Dock fullDock = new Dock(ImmutableArray.Create<DebrisType?>(
                OverflowDockPattern[0],
                OverflowDockPattern[1],
                OverflowDockPattern[2],
                OverflowDockPattern[3],
                OverflowDockPattern[4],
                OverflowDockPattern[5],
                OverflowDockPattern[6]), DockSize);

            preparedState = source with
            {
                Board = updatedBoard,
                Dock = fullDock,
            };
            tapCoord = first;
            return true;
        }

        private static bool IsOverflowCandidate(Board board, TileCoord coord)
        {
            if (!BoardHelpers.InBounds(board, coord))
            {
                return false;
            }

            return BoardHelpers.GetTile(board, coord) switch
            {
                FloodedTile => false,
                TargetTile => false,
                _ => true,
            };
        }

        private void CopyRngStateToClipboard()
        {
            if (_currentState is null)
            {
                return;
            }

            string serialized = DebugPanelDisplay.SerializeRngState(_currentState.RngState);
            GUIUtility.systemCopyBuffer = serialized;
            SetStatus("Copied RNG state.");
            RefreshUi();
        }

        private void PushDebugUndo(string reason)
        {
            if (_currentState is null)
            {
                return;
            }

            _debugUndo.Push(new DebugUndoEntry(_currentState, reason));
        }

        private void AppendActionLog(string actionLabel, ImmutableArray<ActionEvent> events, ActionOutcome outcome)
        {
            ImmutableArray<DebugEventLogLine>.Builder lines = ImmutableArray.CreateBuilder<DebugEventLogLine>(events.Length);
            for (int i = 0; i < events.Length; i++)
            {
                lines.Add(DebugPanelDisplay.BuildEventLogLine(events[i]));
            }

            _eventLog.Insert(0, new DebugActionLogEntry(actionLabel, outcome, lines.ToImmutable()));
            if (_eventLog.Count > EventLogCapacity)
            {
                _eventLog.RemoveAt(_eventLog.Count - 1);
            }
        }

        private void RefreshUi()
        {
            if (_currentState is null)
            {
                return;
            }

            if (_seedField is not null)
            {
                _seedField.SetValueWithoutNotify(_currentSeed);
            }

            if (_replayPathField is not null)
            {
                _replayPathField.SetValueWithoutNotify(_replaySessionPath);
            }

            SyncLevelSelectorChoices(_currentLevelId);
            SyncOverrideFields();
            RefreshTuningUi();
            RefreshFxDebugUi();
            RefreshPlaybackDebugUi();
            UpdatePlayButtonLabel();

            DebugPanelReplayStatus.UpdateReplayStatus(_replayStatusValue, _loadedReplay, _replayFrameIndex);

            DebugPanelReadouts.Update(_currentState,
                _waterActionsValue, _waterRiseIntervalValue, _waterNextFloodRowValue, _waterForecastValue, _ruleTeachValue, _vineActionsValue, _vineThresholdValue, _vinePendingValue,
                _dockOccupancyValue, _dockWarningValue, _dockContentsValue, _dockJamUsedValue, _dockJamEnabledValue, _nearRescueTargetsValue, _rngStateValue, _consecutiveEmergencyValue, _spawnRecoveryValue);

            DebugPanelEventLogView.Render(_eventLogList, _eventLog);
        }

        private void SyncLevelSelectorChoices(string selectedLevel)
        {
            if (_levelSelector is null)
            {
                return;
            }

            List<string> choices = EnumerateLevelIds();
            if (!string.IsNullOrWhiteSpace(selectedLevel) && !choices.Contains(selectedLevel))
            {
                choices.Insert(0, selectedLevel);
            }

            _levelSelector.choices = choices;
            if (!string.IsNullOrWhiteSpace(selectedLevel))
            {
                _levelSelector.SetValueWithoutNotify(selectedLevel);
            }
        }

        private void SyncOverrideFields()
        {
            if (_currentState is null)
            {
                return;
            }

            if (_assistanceOverrideField is not null)
            {
                _assistanceOverrideField.SetValueWithoutNotify(_currentState.DebugSpawnOverride?.OverrideAssistanceChance switch
                {
                    0.0d => "0",
                    1.0d => "1",
                    _ => "Current",
                });
            }

            if (_forceEmergencyField is not null)
            {
                _forceEmergencyField.SetValueWithoutNotify(_currentState.DebugSpawnOverride?.ForceEmergency switch
                {
                    true => "On",
                    false => "Off",
                    _ => "Auto",
                });
            }
        }

        private void SetStatus(string message)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.text = message;
            }
        }

        private static TileCoord? FindFirstValidAction(GameState state)
        {
            for (int row = 0; row < state.Board.Height; row++)
            {
                for (int col = 0; col < state.Board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (GroupOps.FindGroup(state.Board, coord) is { Length: >= 2 })
                    {
                        return coord;
                    }
                }
            }

            return null;
        }

        private static List<string> EnumerateLevelIds()
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

            AddIdsFromDirectory(Path.Combine(Application.dataPath, "StreamingAssets", "Levels"), ids);
            if (ids.Count == 0)
            {
                for (int i = 0; i < PacketLevelIds.Length; i++)
                {
                    ids.Add(PacketLevelIds[i]);
                }
            }

            List<string> sorted = new List<string>(ids);
            sorted.Sort(StringComparer.Ordinal);
            return sorted;
        }

        private string? GetNextLevelId()
        {
            if (string.IsNullOrWhiteSpace(_currentLevelId))
            {
                return null;
            }

            List<string> levels = EnumerateLevelIds();
            int currentIndex = levels.IndexOf(_currentLevelId);
            if (currentIndex < 0 || currentIndex >= levels.Count - 1)
            {
                return null;
            }

            return levels[currentIndex + 1];
        }

        private static void AddIdsFromDirectory(string directoryPath, ISet<string> ids)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            string[] files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                ids.Add(Path.GetFileNameWithoutExtension(files[i]));
            }
        }

        private static GameState LoadLevelById(string levelId, int seed)
        {
            try
            {
                return Loader.LoadLevel(levelId, seed);
            }
            catch (Exception)
            {
                string[] candidatePaths =
                {
                    Path.Combine(Application.dataPath, "StreamingAssets", "Levels", levelId + ".json"),
                };

                for (int i = 0; i < candidatePaths.Length; i++)
                {
                    if (!File.Exists(candidatePaths[i]))
                    {
                        continue;
                    }

                    LevelJson level = ContentJson.DeserializeLevel(File.ReadAllText(candidatePaths[i]));
                    return Loader.LoadLevel(level, seed);
                }

                throw;
            }
        }

        private static LevelJson CreateFallbackLevel()
        {
            return new LevelJson
            {
                Id = "DBG",
                Name = "Debug Fallback",
                Board = new BoardJson
                {
                    Width = 3,
                    Height = 3,
                    Tiles = new[]
                    {
                        new[] { "A", "A", "." },
                        new[] { "B", "CR", "." },
                        new[] { ".", "T0", "." },
                    },
                },
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E },
                Targets = new[]
                {
                    new TargetJson
                    {
                        Id = "0",
                        Row = 2,
                        Col = 1,
                    },
                },
                Water = new WaterJson
                {
                    RiseInterval = 4,
                },
                Vine = new VineJson
                {
                    GrowthThreshold = 4,
                    GrowthPriority = new[]
                    {
                        new TileCoordJson { Row = 0, Col = 2 },
                    },
                },
                Dock = new DockJson
                {
                    Size = DockSize,
                    JamEnabled = true,
                },
                Assistance = new AssistanceJson
                {
                    Chance = 0.7d,
                    ConsecutiveEmergencyCap = 2,
                },
                Meta = new MetaJson
                {
                    Intent = "Fallback debug level.",
                    ExpectedPath = "Clear the starting pair.",
                    ExpectedFailMode = "Overflow or water loss during debug validation.",
                    WhatItProves = "The debug panel can bootstrap a valid state.",
                    IsRuleTeach = false,
                },
            };
        }

    }

    internal sealed record DebugUndoEntry(GameState State, string Reason);

    internal sealed record DebugActionLogEntry(
        string ActionLabel,
        ActionOutcome Outcome,
        ImmutableArray<DebugEventLogLine> Lines);

    internal sealed record DebugEventLogLine(
        string EventType,
        string Message,
        Color Color,
        bool DevOnly);

    internal static class DebugJson
    {
        private static readonly object Gate = new object();
        private static Assembly? _jsonAssembly;
        private static object? _serializerOptions;
        private static MethodInfo? _serializeMethod;

        public static string Serialize<T>(T value)
        {
            EnsureInitialized();
            object? serialized = _serializeMethod!.Invoke(null, new object?[] { value!, typeof(T), _serializerOptions });
            if (serialized is not string json)
            {
                throw new InvalidOperationException("JSON serialization returned a non-string value.");
            }

            return json;
        }

        private static void EnsureInitialized()
        {
            if (_jsonAssembly is not null)
            {
                return;
            }

            lock (Gate)
            {
                if (_jsonAssembly is not null)
                {
                    return;
                }

                Assembly jsonAssembly = LoadJsonAssembly();
                Type serializerType = RequireType(jsonAssembly, "System.Text.Json.JsonSerializer");
                Type optionsType = RequireType(jsonAssembly, "System.Text.Json.JsonSerializerOptions");
                Type namingPolicyType = RequireType(jsonAssembly, "System.Text.Json.JsonNamingPolicy");
                Type converterType = RequireType(jsonAssembly, "System.Text.Json.Serialization.JsonStringEnumConverter");

                object options = Activator.CreateInstance(optionsType)
                    ?? throw new InvalidOperationException("Unable to create JsonSerializerOptions.");
                SetProperty(optionsType, options, "WriteIndented", true);
                object? camelCase = namingPolicyType.GetProperty("CamelCase", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                SetProperty(optionsType, options, "PropertyNamingPolicy", camelCase);

                object converters = optionsType.GetProperty("Converters", BindingFlags.Public | BindingFlags.Instance)?.GetValue(options)
                    ?? throw new InvalidOperationException("Unable to access JsonSerializerOptions.Converters.");
                object converter = Activator.CreateInstance(converterType)
                    ?? throw new InvalidOperationException("Unable to create JsonStringEnumConverter.");
                converters.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance)?.Invoke(converters, new[] { converter });

                MethodInfo serializeMethod = serializerType.GetMethod(
                    "Serialize",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(object), typeof(Type), optionsType },
                    modifiers: null)
                    ?? throw new MissingMethodException("JsonSerializer.Serialize(object, Type, JsonSerializerOptions) was not found.");

                _jsonAssembly = jsonAssembly;
                _serializerOptions = options;
                _serializeMethod = serializeMethod;
            }
        }

        private static Assembly LoadJsonAssembly()
        {
            Assembly? loaded = FindLoadedAssembly("System.Text.Json");
            if (loaded is not null)
            {
                return loaded;
            }

            try
            {
                return Assembly.Load(new AssemblyName("System.Text.Json"));
            }
            catch
            {
                // Fall through to Unity plugin loading.
            }

            string[] dependencyNames =
            {
                "Microsoft.Bcl.AsyncInterfaces.dll",
                "System.Memory.dll",
                "System.Buffers.dll",
                "System.IO.Pipelines.dll",
                "System.Runtime.CompilerServices.Unsafe.dll",
                "System.Text.Encodings.Web.dll",
                "System.Threading.Tasks.Extensions.dll",
                "System.Text.Json.dll",
            };

            Assembly? mainAssembly = null;
            string pluginDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Plugins");
            for (int i = 0; i < dependencyNames.Length; i++)
            {
                string path = Path.Combine(pluginDirectory, dependencyNames[i]);
                if (!File.Exists(path))
                {
                    continue;
                }

                Assembly assembly = Assembly.LoadFrom(path);
                if (string.Equals(assembly.GetName().Name, "System.Text.Json", StringComparison.Ordinal))
                {
                    mainAssembly = assembly;
                }
            }

            return mainAssembly ?? throw new FileNotFoundException("System.Text.Json.dll could not be located.");
        }

        private static Assembly? FindLoadedAssembly(string simpleName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (string.Equals(assemblies[i].GetName().Name, simpleName, StringComparison.Ordinal))
                {
                    return assemblies[i];
                }
            }

            return null;
        }

        private static Type RequireType(Assembly assembly, string fullName)
        {
            return assembly.GetType(fullName, throwOnError: true)
                ?? throw new TypeLoadException($"Type '{fullName}' was not found.");
        }

        private static void SetProperty(Type declaringType, object instance, string propertyName, object? value)
        {
            PropertyInfo property = declaringType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMemberException(declaringType.FullName, propertyName);
            property.SetValue(instance, value);
        }
    }
}
#endif
