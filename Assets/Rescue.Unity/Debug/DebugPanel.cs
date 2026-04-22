#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.Rules;
using Rescue.Core.State;
using Rescue.Core.Undo;
using Rescue.Telemetry;
using Rescue.Unity.Telemetry;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rescue.Unity.Debugging
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugPanel : MonoBehaviour
    {
        private const string UxmlAssetPath = "Assets/Rescue.Unity/Debug/DebugPanel.uxml";
        private const string UssAssetPath = "Assets/Rescue.Unity/Debug/DebugPanel.uss";
        private const int EventLogCapacity = 20;
        private const int DockSize = 7;
        private static readonly string[] SpeedChoices = { "0.25x", "0.5x", "1x", "2x", "4x" };
        private static readonly string[] AssistanceChoices = { "Current", "0", "1" };
        private static readonly string[] EmergencyChoices = { "Auto", "On", "Off" };
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
        private PanelSettings? _panelSettings;
        private VisualElement? _panelRoot;
        private DropdownField? _levelSelector;
        private IntegerField? _seedField;
        private Button? _randomSeedButton;
        private Button? _playPauseButton;
        private Button? _stepButton;
        private DropdownField? _speedSelector;
        private Toggle? _fastForwardToggle;
        private Button? _debugUndoButton;
        private Button? _resetButton;
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

        private bool _initialized;
        private bool _panelVisible = true;
        private bool _isPlaying;
        private float _playAccumulator;
        private string _currentLevelId = string.Empty;
        private int _currentSeed = 1;
        private GameState? _currentState;
        private GameState? _initialState;
        private LevelJson? _testLevel;
        private TelemetryLogger? _telemetryLogger;
        private TelemetrySessionState? _telemetrySession;
        private double _lastActionEndMs;

        public static DebugPanel? Instance => _instance;

        public GameState CurrentState => _currentState ?? throw new InvalidOperationException("Debug panel state is not initialized.");

        public string CurrentLevelId => _currentLevelId;

        public int CurrentSeed => _currentSeed;

        public string CurrentWaterForecastSummary => GetWaterForecastSummary(CurrentState);

        public string CurrentNearRescueSummary => GetNearRescueTargetsSummary(CurrentState);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapRuntimePanel()
        {
            EnsureInstance();
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

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
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
            GameState loadedState = Loader.LoadLevel(level, seed);
            SetLoadedState(loadedState, level.Id, seed, $"Loaded {_currentLevelId} with seed {seed}.");
        }

        public void ReloadCurrentLevel()
        {
            if (_testLevel is not null)
            {
                LoadLevel(_testLevel, _currentSeed);
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentLevelId))
            {
                InitializeDefaultStateIfNeeded(forceReload: true);
                return;
            }

            GameState loadedState = LoadLevelById(_currentLevelId, _currentSeed);
            SetLoadedState(loadedState, _currentLevelId, _currentSeed, $"Reloaded {_currentLevelId} with seed {_currentSeed}.");
        }

        public void ResetLevel()
        {
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
            RefreshUi();
        }

        public bool StepOneAction()
        {
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
            SetStatus($"Stepped action at ({nextTap.Value.Row}, {nextTap.Value.Col}) -> {result.Outcome}.");
            RefreshUi();
            return true;
        }

        public bool DebugUndo()
        {
            if (_debugUndo.Count == 0)
            {
                SetStatus("Debug undo stack is empty.");
                RefreshUi();
                return false;
            }

            DebugUndoEntry entry = _debugUndo.Pop();
            _currentState = entry.State;
            SetStatus($"Debug undo restored: {entry.Reason}.");
            RefreshUi();
            return true;
        }

        public string ExportStateJson()
        {
            return DebugJson.Serialize(BuildBugReportExport(CurrentState));
        }

        public string ExportFullGameStateJson()
        {
            return DebugJson.Serialize(BuildGameStateExport(CurrentState));
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
            _currentState = state;
            _initialState = state;
            _currentLevelId = levelId;
            _currentSeed = seed;
            _debugUndo.Clear();
            _eventLog.Clear();
            _isPlaying = false;
            _playAccumulator = 0.0f;
            UpdatePlayButtonLabel();
            SyncLevelSelectorChoices(levelId);
            SetStatus(status);
            RefreshUi();
            StartTelemetrySession(state, levelId, seed);
        }

        private void StartTelemetrySession(GameState state, string levelId, int seed)
        {
            _telemetryLogger?.Dispose();

            string sessionId = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N")[..6];
            string logPath = Path.Combine(
                Application.persistentDataPath, "telemetry", sessionId + ".jsonl");

            TelemetryConfig config = TelemetryConfig.DevDefaults;
            _telemetryLogger = new TelemetryLogger(logPath, config);

            long nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
            _lastActionEndMs = nowMs;
            _telemetrySession = new TelemetrySessionState { LevelStartMs = nowMs };

            TelemetryHooks.OnLevelStart(levelId, (ulong)(uint)seed, state, nowMs, _telemetryLogger);
        }

        private void ConfigureInputs()
        {
            RegisterAction("Toggle Panel", "<Keyboard>/f1", _ => TogglePanelVisibility());
            RegisterAction("Step Action", "<Keyboard>/f2", _ => StepOneAction());
            RegisterAction("Reset Level", "<Keyboard>/f3", _ => ResetLevel());
            RegisterAction("Debug Undo", "<Keyboard>/f4", _ => DebugUndo());

            InputAction fastForwardAction = new InputAction("Fast Forward", binding: "<Keyboard>/f");
            fastForwardAction.performed += _ =>
            {
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
            action.performed += callback;
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
            settings.clearColor = true;
            settings.colorClearValue = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            return settings;
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

            VisualTreeAsset? asset = TryLoadUxmlAsset();
            VisualElement panel = asset is not null ? asset.CloneTree() : CreatePanelFallback();
            panel.name = "debug-panel-root";
            panel.style.display = _panelVisible ? DisplayStyle.Flex : DisplayStyle.None;
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
            _debugUndoButton = panel.Q<Button>("debug-undo-button");
            _resetButton = panel.Q<Button>("reset-button");
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

            if (_debugUndoButton is not null)
            {
                _debugUndoButton.clicked += () => DebugUndo();
            }

            if (_resetButton is not null)
            {
                _resetButton.clicked += ResetLevel;
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
        }

        private VisualElement CreatePanelFallback()
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("debug-panel");
            root.style.position = Position.Absolute;
            root.style.top = 12.0f;
            root.style.right = 12.0f;
            root.style.width = 420.0f;
            root.style.maxHeight = 920.0f;
            root.style.paddingLeft = 12.0f;
            root.style.paddingRight = 12.0f;
            root.style.paddingTop = 12.0f;
            root.style.paddingBottom = 12.0f;
            root.style.backgroundColor = new Color(0.07f, 0.10f, 0.14f, 0.93f);

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "debug-scroll",
            };
            scroll.style.flexGrow = 1.0f;
            root.Add(scroll);

            scroll.Add(MakeHeader("Rescue Grid Debug"));
            scroll.Add(MakeRow(out _statusLabel, "status-label", "Ready."));

            scroll.Add(MakeSection("Level"));
            scroll.Add(MakeFieldRow("Level", out _levelSelector, "level-selector"));
            scroll.Add(MakeFieldRow("Seed", out _seedField, "seed-field"));
            scroll.Add(MakeButton("Randomize Seed", "random-seed-button", out _randomSeedButton));

            scroll.Add(MakeSection("Step Through"));
            VisualElement stepRow = new VisualElement();
            stepRow.AddToClassList("button-row");
            stepRow.Add(MakeButton("Play", "play-pause-button", out _playPauseButton));
            stepRow.Add(MakeButton("Step 1 Action", "step-button", out _stepButton));
            scroll.Add(stepRow);
            scroll.Add(MakeFieldRow("Speed", out _speedSelector, "speed-selector"));
            Toggle fastForward = new Toggle("Fast Forward") { name = "fast-forward-toggle" };
            _fastForwardToggle = fastForward;
            scroll.Add(fastForward);
            scroll.Add(MakeButton("Debug Undo", "debug-undo-button", out _debugUndoButton));
            scroll.Add(MakeButton("Reset Level", "reset-button", out _resetButton));

            scroll.Add(MakeSection("Hazards"));
            scroll.Add(MakeRow(out _waterActionsValue, "water-actions-value"));
            scroll.Add(MakeRow(out _waterRiseIntervalValue, "water-rise-interval-value"));
            scroll.Add(MakeRow(out _waterNextFloodRowValue, "water-next-row-value"));
            scroll.Add(MakeRow(out _waterForecastValue, "water-forecast-value"));
            scroll.Add(MakeRow(out _ruleTeachValue, "rule-teach-value"));
            scroll.Add(MakeRow(out _vineActionsValue, "vine-actions-value"));
            scroll.Add(MakeRow(out _vineThresholdValue, "vine-threshold-value"));
            scroll.Add(MakeRow(out _vinePendingValue, "vine-pending-value"));

            scroll.Add(MakeSection("Dock"));
            scroll.Add(MakeRow(out _dockOccupancyValue, "dock-occupancy-value"));
            scroll.Add(MakeRow(out _dockWarningValue, "dock-warning-value"));
            scroll.Add(MakeRow(out _dockContentsValue, "dock-contents-value"));
            scroll.Add(MakeRow(out _dockJamUsedValue, "dock-jam-used-value"));
            scroll.Add(MakeRow(out _dockJamEnabledValue, "dock-jam-enabled-value"));
            scroll.Add(MakeRow(out _nearRescueTargetsValue, "near-rescue-targets-value"));

            scroll.Add(MakeSection("RNG"));
            scroll.Add(MakeRow(out _rngStateValue, "rng-state-value"));
            scroll.Add(MakeButton("Copy RNG State", "copy-rng-button", out _copyRngButton));

            scroll.Add(MakeSection("Spawn Overrides"));
            scroll.Add(MakeFieldRow("Assist Chance", out _assistanceOverrideField, "assistance-override-field"));
            scroll.Add(MakeFieldRow("Force Emergency", out _forceEmergencyField, "force-emergency-field"));
            scroll.Add(MakeRow(out _consecutiveEmergencyValue, "consecutive-emergency-value"));
            scroll.Add(MakeRow(out _spawnRecoveryValue, "spawn-recovery-value"));

            scroll.Add(MakeSection("Overflow"));
            scroll.Add(MakeButton("Instant Overflow Test", "overflow-button", out _overflowButton));

            scroll.Add(MakeSection("State Export"));
            scroll.Add(MakeButton("Copy State JSON", "copy-state-button", out _copyStateButton));
            scroll.Add(MakeButton("Copy Full GameState JSON", "copy-full-state-button", out _copyFullStateButton));

            scroll.Add(MakeSection("Event Log"));
            _eventLogList = new VisualElement { name = "event-log-list" };
            _eventLogList.style.flexDirection = FlexDirection.Column;
            scroll.Add(_eventLogList);

            return root;
        }

        private static Label MakeHeader(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("debug-title");
            return label;
        }

        private static Label MakeSection(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("debug-section");
            return label;
        }

        private static VisualElement MakeRow(out Label label, string name, string text = "")
        {
            label = new Label(text) { name = name };
            label.AddToClassList("debug-value");
            return label;
        }

        private static VisualElement MakeFieldRow(string title, out DropdownField field, string name)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("field-row");
            row.Add(new Label(title));
            field = new DropdownField { name = name };
            field.style.flexGrow = 1.0f;
            row.Add(field);
            return row;
        }

        private static VisualElement MakeFieldRow(string title, out IntegerField field, string name)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("field-row");
            row.Add(new Label(title));
            field = new IntegerField { name = name };
            field.style.flexGrow = 1.0f;
            row.Add(field);
            return row;
        }

        private static Button MakeButton(string text, string name, out Button button)
        {
            button = new Button { text = text, name = name };
            return button;
        }

        private void TogglePanelVisibility()
        {
            _panelVisible = !_panelVisible;
            if (_panelRoot is not null)
            {
                _panelRoot.style.display = _panelVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void TogglePlayPause()
        {
            _isPlaying = !_isPlaying;
            _playAccumulator = 0.0f;
            UpdatePlayButtonLabel();
            SetStatus(_isPlaying ? "Auto-step playing." : "Auto-step paused.");
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
                _playPauseButton.text = _isPlaying ? "Pause" : "Play";
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
            ActionResult result = Pipeline.RunAction(preparedState, new ActionInput(tapCoord));
            _currentState = result.State;
            AppendActionLog("Instant Overflow Test", result.Events, result.Outcome);
            SetStatus($"Instant overflow test executed -> {result.Outcome}.");
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

            string serialized = SerializeRngState(_currentState.RngState);
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
                ActionEvent actionEvent = events[i];
                lines.Add(new DebugEventLogLine(
                    actionEvent.GetType().Name,
                    DescribeActionEvent(actionEvent),
                    DetermineEventColor(actionEvent),
                    TelemetryEventClassifier.IsDevOnly(actionEvent)));
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

            SyncLevelSelectorChoices(_currentLevelId);
            SyncOverrideFields();
            UpdatePlayButtonLabel();

            if (_waterActionsValue is not null) _waterActionsValue.text = $"Water actions until rise: {_currentState.Water.ActionsUntilRise}";
            if (_waterRiseIntervalValue is not null) _waterRiseIntervalValue.text = $"Water rise interval: {_currentState.Water.RiseInterval}";
            if (_waterNextFloodRowValue is not null) _waterNextFloodRowValue.text = $"Next row to flood: {GetNextFloodRowLabel(_currentState)}";
            if (_waterForecastValue is not null) _waterForecastValue.text = $"Water forecast: {GetWaterForecastSummary(_currentState)}";
            if (_ruleTeachValue is not null) _ruleTeachValue.text = $"Rule teach active: {_currentState.LevelConfig.IsRuleTeach}; waiting for first action: {_currentState.Water.PauseUntilFirstAction}";
            if (_vineActionsValue is not null) _vineActionsValue.text = $"Vine actions since clear: {_currentState.Vine.ActionsSinceLastClear}";
            if (_vineThresholdValue is not null) _vineThresholdValue.text = $"Vine growth threshold: {_currentState.Vine.GrowthThreshold}";
            if (_vinePendingValue is not null) _vinePendingValue.text = $"Pending growth tile: {FormatCoord(_currentState.Vine.PendingGrowthTile)}";
            if (_dockOccupancyValue is not null) _dockOccupancyValue.text = $"Dock occupancy: {DockHelpers.Occupancy(_currentState.Dock)}/{_currentState.Dock.Size}";
            if (_dockWarningValue is not null) _dockWarningValue.text = $"Dock warning level: {DockHelpers.GetWarningLevel(_currentState.Dock)}";
            if (_dockContentsValue is not null) _dockContentsValue.text = $"Dock contents: {FormatDockContents(_currentState.Dock)}";
            if (_dockJamUsedValue is not null) _dockJamUsedValue.text = $"Dock jam used: {_currentState.DockJamUsed}";
            if (_dockJamEnabledValue is not null) _dockJamEnabledValue.text = $"Dock jam enabled: {_currentState.DockJamEnabled}";
            if (_nearRescueTargetsValue is not null) _nearRescueTargetsValue.text = $"Near-rescue targets: {GetNearRescueTargetsSummary(_currentState)}";
            if (_rngStateValue is not null) _rngStateValue.text = $"RNG state: {SerializeRngState(_currentState.RngState)}";
            if (_consecutiveEmergencyValue is not null) _consecutiveEmergencyValue.text = $"Consecutive emergency spawns: {_currentState.ConsecutiveEmergencySpawns}";
            if (_spawnRecoveryValue is not null) _spawnRecoveryValue.text = $"Spawn recovery counter: {_currentState.SpawnRecoveryCounter}";

            if (_eventLogList is not null)
            {
                _eventLogList.Clear();
                for (int i = 0; i < _eventLog.Count; i++)
                {
                    _eventLogList.Add(CreateLogEntryElement(_eventLog[i]));
                }
            }
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

        private VisualElement CreateLogEntryElement(DebugActionLogEntry entry)
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("event-log-entry");

            Label header = new Label($"{entry.ActionLabel} -> {entry.Outcome}");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(header);

            for (int i = 0; i < entry.Lines.Length; i++)
            {
                DebugEventLogLine line = entry.Lines[i];
                Label label = new Label(line.DevOnly ? $"[dev] {line.Message}" : line.Message);
                label.style.color = line.Color;
                container.Add(label);
            }

            return container;
        }

        private static string DescribeActionEvent(ActionEvent actionEvent)
        {
            return actionEvent switch
            {
                InvalidInput invalid => $"Invalid input at {FormatCoord(invalid.TappedCoord)} ({invalid.Reason})",
                GroupRemoved removed => $"Removed {removed.Type} group of {removed.Coords.Length}",
                DockInserted inserted => $"Dock inserted {inserted.Pieces.Length}; occupancy {inserted.OccupancyAfterInsert}",
                DockCleared cleared => $"Dock cleared {cleared.SetsCleared}x {cleared.Type}",
                DockJamTriggered triggered => $"Dock jam triggered ({triggered.OverflowCount} overflow)",
                WaterWarning warning => $"Water warning: {warning.ActionsUntilRise} action left; row {warning.NextFloodRow}",
                WaterRose rose => $"Water rose into row {rose.FloodedRow}",
                VinePreviewChanged preview => $"Vine preview -> {FormatCoord(preview.PendingTile)}",
                VineGrown grown => $"Vine grew at {FormatCoord(grown.Coord)}",
                TargetExtracted extracted => $"Target {extracted.TargetId} extracted",
                TargetOneClearAway almost => $"Target {almost.TargetId} is one clear away",
                DebugSpawnOverrideApplied applied => $"Spawn override active (requested={applied.EmergencyRequested}, applied={applied.EmergencyApplied}, chance={applied.EffectiveAssistanceChance:0.##})",
                Lost lost => $"Loss: {lost.Outcome}",
                Won won => $"Win after {won.TotalActions} actions",
                Spawned spawned => $"Spawned {spawned.Pieces.Length} pieces",
                _ => actionEvent.ToString() ?? actionEvent.GetType().Name,
            };
        }

        private static Color DetermineEventColor(ActionEvent actionEvent)
        {
            return actionEvent switch
            {
                Lost => new Color(0.90f, 0.33f, 0.29f),
                Won => new Color(0.43f, 0.78f, 0.47f),
                WaterWarning or DockWarningChanged or VinePreviewChanged => new Color(0.98f, 0.79f, 0.31f),
                WaterRose or VineGrown or DockJamTriggered => new Color(0.45f, 0.73f, 0.95f),
                DebugSpawnOverrideApplied => new Color(0.88f, 0.55f, 0.97f),
                _ => new Color(0.88f, 0.92f, 0.96f),
            };
        }

        private void SetStatus(string message)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.text = message;
            }
        }

        private static string FormatDockContents(Dock dock)
        {
            string[] contents = new string[dock.Slots.Length];
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                contents[i] = dock.Slots[i]?.ToString() ?? "-";
            }

            return string.Join(" ", contents);
        }

        private static string SerializeRngState(Rescue.Core.Rng.RngState rngState)
        {
            return $"{rngState.S0}:{rngState.S1}";
        }

        private static string GetNextFloodRowLabel(GameState state)
        {
            int? nextFloodRow = WaterHelpers.GetNextFloodRow(state.Board, state.Water);
            if (!nextFloodRow.HasValue)
            {
                return "none";
            }

            return nextFloodRow.Value.ToString();
        }

        private static string GetWaterForecastSummary(GameState state)
        {
            int? nextFloodRow = WaterHelpers.GetNextFloodRow(state.Board, state.Water);
            if (!nextFloodRow.HasValue)
            {
                return "board fully flooded";
            }

            if (state.Water.PauseUntilFirstAction)
            {
                return $"row {nextFloodRow.Value} will flood on the first valid action";
            }

            if (state.Water.RiseInterval <= 0)
            {
                return $"row {nextFloodRow.Value} queued, water disabled";
            }

            string actionLabel = state.Water.ActionsUntilRise == 1 ? "action" : "actions";
            return $"row {nextFloodRow.Value} in {state.Water.ActionsUntilRise} {actionLabel}";
        }

        private static string GetNearRescueTargetsSummary(GameState state)
        {
            ImmutableArray<string>.Builder targetIds = ImmutableArray.CreateBuilder<string>();
            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                if (!target.Extracted && target.OneClearAway)
                {
                    targetIds.Add(target.TargetId);
                }
            }

            return targetIds.Count == 0 ? "none" : string.Join(", ", targetIds);
        }

        private static string FormatCoord(TileCoord? coord)
        {
            return coord.HasValue ? $"({coord.Value.Row}, {coord.Value.Col})" : "none";
        }

        private static string FormatCoord(TileCoord coord)
        {
            return $"({coord.Row}, {coord.Col})";
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

            List<string> sorted = new List<string>(ids);
            sorted.Sort(StringComparer.Ordinal);
            return sorted;
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
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D },
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

        private DebugBugReportExport BuildBugReportExport(GameState state)
        {
            return new DebugBugReportExport(
                _currentLevelId,
                _currentSeed,
                ExportedAtUtc: DateTime.UtcNow.ToString("O"),
                BuildGameStateExport(state));
        }

        private static GameStateExport BuildGameStateExport(GameState state)
        {
            TileExport[][] tiles = new TileExport[state.Board.Height][];
            for (int row = 0; row < state.Board.Height; row++)
            {
                tiles[row] = new TileExport[state.Board.Width];
                for (int col = 0; col < state.Board.Width; col++)
                {
                    tiles[row][col] = ExportTile(BoardHelpers.GetTile(state.Board, new TileCoord(row, col)));
                }
            }

            TargetStateExport[] targets = new TargetStateExport[state.Targets.Length];
            for (int i = 0; i < state.Targets.Length; i++)
            {
                targets[i] = new TargetStateExport(
                    state.Targets[i].TargetId,
                    state.Targets[i].Coord.Row,
                    state.Targets[i].Coord.Col,
                    state.Targets[i].Extracted,
                    state.Targets[i].OneClearAway);
            }

            string?[] dockSlots = new string?[state.Dock.Slots.Length];
            for (int i = 0; i < state.Dock.Slots.Length; i++)
            {
                dockSlots[i] = state.Dock.Slots[i]?.ToString();
            }

            string[] extractedTargetOrder = new string[state.ExtractedTargetOrder.Length];
            for (int i = 0; i < state.ExtractedTargetOrder.Length; i++)
            {
                extractedTargetOrder[i] = state.ExtractedTargetOrder[i];
            }

            string[] debrisPool = new string[state.LevelConfig.DebrisTypePool.Length];
            for (int i = 0; i < state.LevelConfig.DebrisTypePool.Length; i++)
            {
                debrisPool[i] = state.LevelConfig.DebrisTypePool[i].ToString();
            }

            Dictionary<string, double>? baseDistribution = null;
            if (state.LevelConfig.BaseDistribution is not null)
            {
                baseDistribution = new Dictionary<string, double>(state.LevelConfig.BaseDistribution.Count, StringComparer.Ordinal);
                foreach (KeyValuePair<DebrisType, double> entry in state.LevelConfig.BaseDistribution)
                {
                    baseDistribution[entry.Key.ToString()] = entry.Value;
                }
            }

            return new GameStateExport(
                new BoardExport(state.Board.Width, state.Board.Height, tiles),
                new DockExport(dockSlots, state.Dock.Size),
                new WaterExport(state.Water.FloodedRows, state.Water.ActionsUntilRise, state.Water.RiseInterval, state.Water.PauseUntilFirstAction),
                new VineExport(
                    state.Vine.ActionsSinceLastClear,
                    state.Vine.GrowthThreshold,
                    ExportCoords(state.Vine.GrowthPriorityList),
                    state.Vine.PriorityCursor,
                    ExportNullableCoord(state.Vine.PendingGrowthTile)),
                targets,
                new LevelConfigExport(
                    debrisPool,
                    baseDistribution,
                    state.LevelConfig.AssistanceChance,
                    state.LevelConfig.ConsecutiveEmergencyCap,
                    state.LevelConfig.IsRuleTeach),
                new RngExport(state.RngState.S0, state.RngState.S1),
                state.ActionCount,
                state.DockJamUsed,
                state.UndoAvailable,
                extractedTargetOrder,
                state.Frozen,
                state.ConsecutiveEmergencySpawns,
                state.SpawnRecoveryCounter,
                state.DockJamEnabled,
                state.DockJamActive,
                state.DebugSpawnOverride is null
                    ? null
                    : new SpawnOverrideExport(state.DebugSpawnOverride.ForceEmergency, state.DebugSpawnOverride.OverrideAssistanceChance));
        }

        private static TileExport ExportTile(Tile tile)
        {
            return tile switch
            {
                EmptyTile => new TileExport("Empty", null, null, null, null, null, null),
                FloodedTile => new TileExport("Flooded", null, null, null, null, null, null),
                DebrisTile debris => new TileExport("Debris", debris.Type.ToString(), null, null, null, null, null),
                BlockerTile blocker => new TileExport("Blocker", null, blocker.Type.ToString(), blocker.Hp, null, null, blocker.Hidden?.Type.ToString()),
                TargetTile target => new TileExport("Target", null, null, null, target.TargetId, target.Extracted, null),
                _ => new TileExport(tile.GetType().Name, null, null, null, null, null, null),
            };
        }

        private static CoordExport[] ExportCoords(ImmutableArray<TileCoord> coords)
        {
            CoordExport[] exported = new CoordExport[coords.Length];
            for (int i = 0; i < coords.Length; i++)
            {
                exported[i] = new CoordExport(coords[i].Row, coords[i].Col);
            }

            return exported;
        }

        private static CoordExport? ExportNullableCoord(TileCoord? coord)
        {
            return coord.HasValue ? new CoordExport(coord.Value.Row, coord.Value.Col) : null;
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

    internal sealed record DebugBugReportExport(
        string LevelId,
        int Seed,
        string ExportedAtUtc,
        GameStateExport State);

    internal sealed record GameStateExport(
        BoardExport Board,
        DockExport Dock,
        WaterExport Water,
        VineExport Vine,
        TargetStateExport[] Targets,
        LevelConfigExport LevelConfig,
        RngExport RngState,
        int ActionCount,
        bool DockJamUsed,
        bool UndoAvailable,
        string[] ExtractedTargetOrder,
        bool Frozen,
        int ConsecutiveEmergencySpawns,
        int SpawnRecoveryCounter,
        bool DockJamEnabled,
        bool DockJamActive,
        SpawnOverrideExport? DebugSpawnOverride);

    internal sealed record BoardExport(int Width, int Height, TileExport[][] Tiles);

    internal sealed record DockExport(string?[] Slots, int Size);

    internal sealed record WaterExport(int FloodedRows, int ActionsUntilRise, int RiseInterval, bool PauseUntilFirstAction);

    internal sealed record VineExport(
        int ActionsSinceLastClear,
        int GrowthThreshold,
        CoordExport[] GrowthPriorityList,
        int PriorityCursor,
        CoordExport? PendingGrowthTile);

    internal sealed record TargetStateExport(
        string TargetId,
        int Row,
        int Col,
        bool Extracted,
        bool OneClearAway);

    internal sealed record LevelConfigExport(
        string[] DebrisTypePool,
        Dictionary<string, double>? BaseDistribution,
        double AssistanceChance,
        int ConsecutiveEmergencyCap,
        bool IsRuleTeach);

    internal sealed record RngExport(uint S0, uint S1);

    internal sealed record SpawnOverrideExport(bool? ForceEmergency, double? OverrideAssistanceChance);

    internal sealed record CoordExport(int Row, int Col);

    internal sealed record TileExport(
        string Kind,
        string? DebrisType,
        string? BlockerType,
        int? Hp,
        string? TargetId,
        bool? Extracted,
        string? HiddenDebrisType);

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
