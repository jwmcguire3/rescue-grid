#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !CAPTURE_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using Rescue.Content;
using Rescue.Telemetry;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rescue.Unity.Debugging
{
    public sealed partial class DebugPanel
    {
        private const string TunePresetDirectory = "Assets/Editor/TuningPresets";
        private const string PlayTabId = "play";
        private const string TuneTabId = "tune";

        private Button? _playTabButton;
        private Button? _tuneTabButton;
        private VisualElement? _playTabContent;
        private VisualElement? _tuneTabContent;
        private IntegerField? _tuneWaterRiseIntervalField;
        private IntegerField? _tuneInitialFloodedRowsField;
        private FloatField? _tuneAssistanceChanceField;
        private DropdownField? _tuneForceEmergencyField;
        private Toggle? _tuneDockJamEnabledToggle;
        private IntegerField? _tuneDockSizeField;
        private IntegerField? _tuneDefaultCrateHpField;
        private IntegerField? _tuneVineGrowthThresholdField;
        private DropdownField? _tunePresetSelector;
        private TextField? _tunePresetNameField;
        private Button? _tuneApplyButton;
        private Button? _tuneResetButton;
        private Button? _tuneSavePresetButton;
        private Button? _tuneLoadPresetButton;
        private Label? _tuneSummaryValue;

        private string _activeTab = PlayTabId;
        private LevelTuningOverrides _tuningOverrides = LevelTuningOverrides.None;
        private int _loadRevision;

        public void ApplyTuneOverrides(LevelTuningOverrides overrides)
        {
            _tuningOverrides = overrides ?? LevelTuningOverrides.None;
            ReloadCurrentLevelInternal(emitTuneTelemetry: true, changeSource: "api_apply", presetName: null);
        }

        public string SaveTunePreset(string presetName)
        {
#if UNITY_EDITOR
            return SaveTunePresetInternal(presetName);
#else
            throw new NotSupportedException("Tune presets can only be saved in the Unity Editor.");
#endif
        }

        public void LoadTunePreset(string presetName)
        {
#if UNITY_EDITOR
            TuningPresetAsset asset = LoadPresetAssetRequired(presetName);
            ApplyLoadedPreset(asset);
#else
            throw new NotSupportedException("Tune presets can only be loaded in the Unity Editor.");
#endif
        }

        private void BindTuningUi(VisualElement panel)
        {
            _playTabButton = panel.Q<Button>("tab-play-button");
            _tuneTabButton = panel.Q<Button>("tab-tune-button");
            _playTabContent = panel.Q<VisualElement>("play-tab-content");
            _tuneTabContent = panel.Q<VisualElement>("tune-tab-content");
            _tuneWaterRiseIntervalField = panel.Q<IntegerField>("tune-water-rise-interval-field");
            _tuneInitialFloodedRowsField = panel.Q<IntegerField>("tune-initial-flooded-rows-field");
            _tuneAssistanceChanceField = panel.Q<FloatField>("tune-assistance-chance-field");
            _tuneForceEmergencyField = panel.Q<DropdownField>("tune-force-emergency-field");
            _tuneDockJamEnabledToggle = panel.Q<Toggle>("tune-dock-jam-enabled-toggle");
            _tuneDockSizeField = panel.Q<IntegerField>("tune-dock-size-field");
            _tuneDefaultCrateHpField = panel.Q<IntegerField>("tune-default-crate-hp-field");
            _tuneVineGrowthThresholdField = panel.Q<IntegerField>("tune-vine-growth-threshold-field");
            _tunePresetSelector = panel.Q<DropdownField>("tune-preset-selector");
            _tunePresetNameField = panel.Q<TextField>("tune-preset-name-field");
            _tuneApplyButton = panel.Q<Button>("tune-apply-button");
            _tuneResetButton = panel.Q<Button>("tune-reset-button");
            _tuneSavePresetButton = panel.Q<Button>("tune-save-preset-button");
            _tuneLoadPresetButton = panel.Q<Button>("tune-load-preset-button");
            _tuneSummaryValue = panel.Q<Label>("tune-summary-value");

            if (_playTabButton is not null)
            {
                _playTabButton.clicked += () => SetActiveTab(PlayTabId);
            }

            if (_tuneTabButton is not null)
            {
                _tuneTabButton.clicked += () => SetActiveTab(TuneTabId);
            }

            ConfigureDelayedTuneField(_tuneWaterRiseIntervalField);
            ConfigureDelayedTuneField(_tuneInitialFloodedRowsField);
            ConfigureDelayedTuneField(_tuneDockSizeField);
            ConfigureDelayedTuneField(_tuneDefaultCrateHpField);
            ConfigureDelayedTuneField(_tuneVineGrowthThresholdField);

            if (_tuneAssistanceChanceField is not null)
            {
                _tuneAssistanceChanceField.isDelayed = true;
                _tuneAssistanceChanceField.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized || Mathf.Approximately(evt.previousValue, evt.newValue))
                    {
                        return;
                    }

                    ApplyTuneOverridesFromUi();
                });
            }

            if (_tuneForceEmergencyField is not null)
            {
                _tuneForceEmergencyField.choices = new List<string>(EmergencyChoices);
                _tuneForceEmergencyField.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized || string.Equals(evt.previousValue, evt.newValue, StringComparison.Ordinal))
                    {
                        return;
                    }

                    ApplyTuneOverridesFromUi();
                });
            }

            if (_tuneDockJamEnabledToggle is not null)
            {
                _tuneDockJamEnabledToggle.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized || evt.previousValue == evt.newValue)
                    {
                        return;
                    }

                    ApplyTuneOverridesFromUi();
                });
            }

            if (_tuneApplyButton is not null)
            {
                _tuneApplyButton.clicked += ApplyTuneOverridesFromUi;
            }

            if (_tuneResetButton is not null)
            {
                _tuneResetButton.clicked += ResetTuneOverridesToLevelDefaults;
            }

            if (_tuneSavePresetButton is not null)
            {
                _tuneSavePresetButton.clicked += SaveTunePresetFromUi;
            }

            if (_tuneLoadPresetButton is not null)
            {
                _tuneLoadPresetButton.clicked += LoadSelectedTunePresetFromUi;
            }

            if (_tunePresetSelector is not null)
            {
                _tunePresetSelector.RegisterValueChangedCallback(evt =>
                {
                    if (_tunePresetNameField is not null && !string.Equals(evt.newValue, evt.previousValue, StringComparison.Ordinal))
                    {
                        _tunePresetNameField.value = evt.newValue;
                    }
                });
            }

            SyncPresetChoices(selectedPreset: null);
            SetActiveTab(_activeTab);
            RefreshTuningUi();
        }

        private void OnLevelLoadedForTuning()
        {
            RefreshTuningUi();
        }

        private void RefreshTuningUi()
        {
            LevelJson? baseLevel = TryGetBaseLevelForTuning();
            if (baseLevel is null)
            {
                return;
            }

            if (_tuneWaterRiseIntervalField is not null)
            {
                _tuneWaterRiseIntervalField.SetValueWithoutNotify(_tuningOverrides.WaterRiseInterval ?? baseLevel.Water.RiseInterval);
            }

            if (_tuneInitialFloodedRowsField is not null)
            {
                _tuneInitialFloodedRowsField.SetValueWithoutNotify(_tuningOverrides.InitialFloodedRows ?? baseLevel.InitialFloodedRows);
            }

            if (_tuneAssistanceChanceField is not null)
            {
                _tuneAssistanceChanceField.SetValueWithoutNotify((float)(_tuningOverrides.AssistanceChance ?? baseLevel.Assistance.Chance));
            }

            if (_tuneForceEmergencyField is not null)
            {
                _tuneForceEmergencyField.SetValueWithoutNotify(_tuningOverrides.ForceEmergencyAssistance switch
                {
                    true => "On",
                    false => "Off",
                    _ => "Auto",
                });
            }

            if (_tuneDockJamEnabledToggle is not null)
            {
                _tuneDockJamEnabledToggle.SetValueWithoutNotify(_tuningOverrides.DockJamEnabled ?? baseLevel.Dock.JamEnabled);
            }

            if (_tuneDockSizeField is not null)
            {
                _tuneDockSizeField.SetValueWithoutNotify(_tuningOverrides.DockSize ?? baseLevel.Dock.Size);
            }

            if (_tuneDefaultCrateHpField is not null)
            {
                _tuneDefaultCrateHpField.SetValueWithoutNotify(_tuningOverrides.DefaultCrateHp ?? 1);
            }

            if (_tuneVineGrowthThresholdField is not null)
            {
                _tuneVineGrowthThresholdField.SetValueWithoutNotify(_tuningOverrides.VineGrowthThreshold ?? baseLevel.Vine.GrowthThreshold);
            }

            if (_tuneSummaryValue is not null)
            {
                _tuneSummaryValue.text = _tuningOverrides.HasValues
                    ? $"Tune overrides active: {DescribeTuneOverrides(_tuningOverrides)}"
                    : "Tune overrides active: none (level-authored defaults)";
            }

            SyncPresetChoices(_tunePresetSelector?.value);
            SetActiveTab(_activeTab);
        }

        private void ConfigureDelayedTuneField(IntegerField? field)
        {
            if (field is null)
            {
                return;
            }

            field.isDelayed = true;
            field.RegisterValueChangedCallback(evt =>
            {
                if (!_initialized || evt.previousValue == evt.newValue)
                {
                    return;
                }

                ApplyTuneOverridesFromUi();
            });
        }

        private void ApplyTuneOverridesFromUi()
        {
            _tuningOverrides = BuildTuneOverridesFromUi();
            ReloadCurrentLevelInternal(emitTuneTelemetry: true, changeSource: "ui_apply", presetName: null);
        }

        private void ResetTuneOverridesToLevelDefaults()
        {
            _tuningOverrides = LevelTuningOverrides.None;
            ReloadCurrentLevelInternal(emitTuneTelemetry: true, changeSource: "ui_reset", presetName: null);
        }

        private LevelTuningOverrides BuildTuneOverridesFromUi()
        {
            LevelJson baseLevel = GetBaseLevelForTuning();

            int waterRiseInterval = _tuneWaterRiseIntervalField?.value ?? baseLevel.Water.RiseInterval;
            int initialFloodedRows = _tuneInitialFloodedRowsField?.value ?? baseLevel.InitialFloodedRows;
            double assistanceChance = _tuneAssistanceChanceField?.value ?? baseLevel.Assistance.Chance;
            bool? forceEmergency = _tuneForceEmergencyField?.value switch
            {
                "On" => true,
                "Off" => false,
                _ => null,
            };
            bool dockJamEnabled = _tuneDockJamEnabledToggle?.value ?? baseLevel.Dock.JamEnabled;
            int dockSize = _tuneDockSizeField?.value ?? baseLevel.Dock.Size;
            int defaultCrateHp = _tuneDefaultCrateHpField?.value ?? 1;
            int vineGrowthThreshold = _tuneVineGrowthThresholdField?.value ?? baseLevel.Vine.GrowthThreshold;

            return new LevelTuningOverrides(
                WaterRiseInterval: waterRiseInterval == baseLevel.Water.RiseInterval ? null : waterRiseInterval,
                InitialFloodedRows: initialFloodedRows == baseLevel.InitialFloodedRows ? null : initialFloodedRows,
                AssistanceChance: Math.Abs(assistanceChance - baseLevel.Assistance.Chance) < 0.0001d ? null : assistanceChance,
                ForceEmergencyAssistance: forceEmergency,
                DockJamEnabled: dockJamEnabled == baseLevel.Dock.JamEnabled ? null : dockJamEnabled,
                DockSize: dockSize == baseLevel.Dock.Size ? null : dockSize,
                DefaultCrateHp: defaultCrateHp == 1 ? null : defaultCrateHp,
                VineGrowthThreshold: vineGrowthThreshold == baseLevel.Vine.GrowthThreshold ? null : vineGrowthThreshold);
        }

        private void EmitTuningTelemetry(string changeSource, string? presetName)
        {
            if (_telemetryLogger is null || string.IsNullOrWhiteSpace(_currentLevelId))
            {
                return;
            }

            long timestampMs = (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
            TelemetryHooks.OnTuningChanged(
                _currentLevelId,
                (ulong)(uint)_currentSeed,
                _tuningOverrides,
                changeSource,
                presetName,
                timestampMs,
                _telemetryLogger);
        }

        private LevelJson GetBaseLevelForTuning()
        {
            return TryGetBaseLevelForTuning()
                ?? throw new InvalidOperationException("A level must be loaded before tune overrides can be edited.");
        }

        private LevelJson? TryGetBaseLevelForTuning()
        {
            if (_testLevel is not null)
            {
                return _testLevel;
            }

            if (string.IsNullOrWhiteSpace(_currentLevelId))
            {
                return null;
            }

            return Loader.LoadLevelDefinition(_currentLevelId);
        }

        private void SetActiveTab(string tabId)
        {
            _activeTab = tabId == TuneTabId ? TuneTabId : PlayTabId;

            if (_playTabContent is not null)
            {
                _playTabContent.style.display = _activeTab == PlayTabId ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_tuneTabContent is not null)
            {
                _tuneTabContent.style.display = _activeTab == TuneTabId ? DisplayStyle.Flex : DisplayStyle.None;
            }

            SetTabButtonState(_playTabButton, _activeTab == PlayTabId);
            SetTabButtonState(_tuneTabButton, _activeTab == TuneTabId);
        }

        private static void SetTabButtonState(Button? button, bool isActive)
        {
            if (button is null)
            {
                return;
            }

            if (isActive)
            {
                button.AddToClassList("debug-tab-button-active");
            }
            else
            {
                button.RemoveFromClassList("debug-tab-button-active");
            }
        }

        private VisualElement CreateFallbackWithTuningTabs()
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

            VisualElement headerRow = new VisualElement();
            headerRow.AddToClassList("debug-header-row");
            headerRow.Add(MakeHeader("Rescue Grid Debug"));
            headerRow.Add(MakeButton("Open", "debug-minimize-button", out _minimizeButton));
            root.Add(headerRow);

            VisualElement body = new VisualElement { name = "debug-panel-body" };
            body.style.flexGrow = 1.0f;
            root.Add(body);

            body.Add(MakeRow(out _statusLabel, "status-label", "Ready."));

            VisualElement tabRow = new VisualElement { name = "debug-tab-row" };
            tabRow.AddToClassList("debug-tab-row");
            tabRow.Add(MakeButton("Play", "tab-play-button", out _playTabButton));
            tabRow.Add(MakeButton("Tune", "tab-tune-button", out _tuneTabButton));
            body.Add(tabRow);

            ScrollView playScroll = new ScrollView(ScrollViewMode.Vertical) { name = "play-tab-content" };
            playScroll.style.flexGrow = 1.0f;
            _playTabContent = playScroll;
            body.Add(playScroll);
            BuildPlayFallbackContent(playScroll);

            ScrollView tuneScroll = new ScrollView(ScrollViewMode.Vertical) { name = "tune-tab-content" };
            tuneScroll.style.flexGrow = 1.0f;
            _tuneTabContent = tuneScroll;
            body.Add(tuneScroll);
            BuildTuneFallbackContent(tuneScroll);

            return root;
        }

        private void BuildPlayFallbackContent(ScrollView scroll)
        {
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

            scroll.Add(MakeSection("Action Playback"));
            Toggle playbackEnabled = new Toggle("Playback Enabled") { name = "playback-enabled-toggle" };
            _playbackEnabledToggle = playbackEnabled;
            scroll.Add(playbackEnabled);
            scroll.Add(MakeFieldRow("Playback Speed", out _playbackSpeedSelector, "playback-speed-selector"));
            scroll.Add(MakeRow(out _playbackStepValue, "playback-step-value", "Playback step: Idle"));

            scroll.Add(MakeButton("Debug Undo", "debug-undo-button", out _debugUndoButton));
            scroll.Add(MakeButton("Reset Level", "reset-button", out _resetButton));

            scroll.Add(MakeSection("Replay"));
            scroll.Add(MakeFieldRow("Session", out _replayPathField, "replay-path-field"));
            VisualElement replayRow = new VisualElement();
            replayRow.AddToClassList("button-row");
            replayRow.Add(MakeButton("Load Replay", "load-replay-button", out _loadReplayButton));
            replayRow.Add(MakeButton("Step Replay", "step-replay-button", out _stepReplayButton));
            replayRow.Add(MakeButton("Clear Replay", "clear-replay-button", out _clearReplayButton));
            scroll.Add(replayRow);
            scroll.Add(MakeRow(out _replayStatusValue, "replay-status-value"));

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
        }

        private void BuildTuneFallbackContent(ScrollView scroll)
        {
            scroll.Add(MakeSection("Tune"));
            scroll.Add(MakeFieldRow("Rise Interval", out _tuneWaterRiseIntervalField, "tune-water-rise-interval-field"));
            scroll.Add(MakeFieldRow("Flooded Rows", out _tuneInitialFloodedRowsField, "tune-initial-flooded-rows-field"));
            scroll.Add(MakeFloatFieldRow("Assist Chance", out _tuneAssistanceChanceField, "tune-assistance-chance-field"));
            scroll.Add(MakeFieldRow("Force Emergency", out _tuneForceEmergencyField, "tune-force-emergency-field"));
            Toggle jamToggle = new Toggle("Dock Jam Enabled") { name = "tune-dock-jam-enabled-toggle" };
            _tuneDockJamEnabledToggle = jamToggle;
            scroll.Add(jamToggle);
            scroll.Add(MakeFieldRow("Dock Size", out _tuneDockSizeField, "tune-dock-size-field"));
            scroll.Add(MakeFieldRow("Crate HP", out _tuneDefaultCrateHpField, "tune-default-crate-hp-field"));
            scroll.Add(MakeFieldRow("Vine Threshold", out _tuneVineGrowthThresholdField, "tune-vine-growth-threshold-field"));

            scroll.Add(MakeSection("Presets"));
            scroll.Add(MakeFieldRow("Preset", out _tunePresetSelector, "tune-preset-selector"));
            scroll.Add(MakeFieldRow("Name", out _tunePresetNameField, "tune-preset-name-field"));

            VisualElement presetButtonRow = new VisualElement();
            presetButtonRow.AddToClassList("button-row");
            presetButtonRow.Add(MakeButton("Apply & Reload", "tune-apply-button", out _tuneApplyButton));
            presetButtonRow.Add(MakeButton("Reset", "tune-reset-button", out _tuneResetButton));
            scroll.Add(presetButtonRow);

            VisualElement presetCrudRow = new VisualElement();
            presetCrudRow.AddToClassList("button-row");
            presetCrudRow.Add(MakeButton("Save Preset", "tune-save-preset-button", out _tuneSavePresetButton));
            presetCrudRow.Add(MakeButton("Load Preset", "tune-load-preset-button", out _tuneLoadPresetButton));
            scroll.Add(presetCrudRow);

            scroll.Add(MakeRow(out _tuneSummaryValue, "tune-summary-value", "Tune overrides active: none"));
        }

        private static VisualElement MakeFloatFieldRow(string title, out FloatField field, string name)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("field-row");
            row.Add(new Label(title));
            field = new FloatField { name = name };
            field.style.flexGrow = 1.0f;
            row.Add(field);
            return row;
        }

        private static string DescribeTuneOverrides(LevelTuningOverrides overrides)
        {
            List<string> parts = new List<string>();
            if (overrides.WaterRiseInterval.HasValue)
            {
                parts.Add($"water interval={overrides.WaterRiseInterval.Value}");
            }

            if (overrides.InitialFloodedRows.HasValue)
            {
                parts.Add($"flooded rows={overrides.InitialFloodedRows.Value}");
            }

            if (overrides.AssistanceChance.HasValue)
            {
                parts.Add($"assist={overrides.AssistanceChance.Value:0.##}");
            }

            if (overrides.ForceEmergencyAssistance.HasValue)
            {
                parts.Add($"force emergency={(overrides.ForceEmergencyAssistance.Value ? "on" : "off")}");
            }

            if (overrides.DockJamEnabled.HasValue)
            {
                parts.Add($"dock jam={overrides.DockJamEnabled.Value}");
            }

            if (overrides.DockSize.HasValue)
            {
                parts.Add($"dock size={overrides.DockSize.Value}");
            }

            if (overrides.DefaultCrateHp.HasValue)
            {
                parts.Add($"crate hp={overrides.DefaultCrateHp.Value}");
            }

            if (overrides.VineGrowthThreshold.HasValue)
            {
                parts.Add($"vine threshold={overrides.VineGrowthThreshold.Value}");
            }

            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        private void SaveTunePresetFromUi()
        {
#if UNITY_EDITOR
            string presetName = string.IsNullOrWhiteSpace(_tunePresetNameField?.value)
                ? "TunePreset"
                : _tunePresetNameField!.value;
            SaveTunePresetInternal(presetName);
#else
            SetStatus("Preset saving is only available in the editor.");
            RefreshUi();
#endif
        }

        private void LoadSelectedTunePresetFromUi()
        {
#if UNITY_EDITOR
            string presetName = string.IsNullOrWhiteSpace(_tunePresetSelector?.value)
                ? _tunePresetNameField?.value ?? string.Empty
                : _tunePresetSelector!.value;
            if (string.IsNullOrWhiteSpace(presetName))
            {
                SetStatus("Choose a preset to load.");
                RefreshUi();
                return;
            }

            ApplyLoadedPreset(LoadPresetAssetRequired(presetName));
#else
            SetStatus("Preset loading is only available in the editor.");
            RefreshUi();
#endif
        }

#if UNITY_EDITOR
        private string SaveTunePresetInternal(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
            {
                throw new ArgumentException("Preset name is required.", nameof(presetName));
            }

            EnsurePresetDirectoryExists();

            string assetPath = GetPresetAssetPath(presetName);
            TuningPresetAsset? asset = AssetDatabase.LoadAssetAtPath<TuningPresetAsset>(assetPath);
            if (asset is null)
            {
                asset = ScriptableObject.CreateInstance<TuningPresetAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            asset.Apply(_tuningOverrides);
            asset.PresetName = presetName;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (_tunePresetNameField is not null)
            {
                _tunePresetNameField.value = presetName;
            }

            SyncPresetChoices(presetName);
            EmitTuningTelemetry("preset_saved", presetName);
            SetStatus($"Saved tune preset '{presetName}'.");
            RefreshUi();
            return assetPath;
        }

        private void ApplyLoadedPreset(TuningPresetAsset asset)
        {
            string presetName = string.IsNullOrWhiteSpace(asset.PresetName) ? asset.name : asset.PresetName;
            _tuningOverrides = asset.ToOverrides();
            if (_tunePresetNameField is not null)
            {
                _tunePresetNameField.value = presetName;
            }

            SyncPresetChoices(presetName);
            ReloadCurrentLevelInternal(emitTuneTelemetry: true, changeSource: "preset_loaded", presetName: presetName);
        }

        private TuningPresetAsset LoadPresetAssetRequired(string presetName)
        {
            TuningPresetAsset? asset = FindPresetAssetByName(presetName);
            if (asset is not null)
            {
                return asset;
            }

            throw new InvalidOperationException($"Tune preset '{presetName}' was not found.");
        }

        private TuningPresetAsset? FindPresetAssetByName(string presetName)
        {
            List<TuningPresetAsset> assets = LoadPresetAssets();
            for (int i = 0; i < assets.Count; i++)
            {
                string candidateName = string.IsNullOrWhiteSpace(assets[i].PresetName) ? assets[i].name : assets[i].PresetName;
                if (string.Equals(candidateName, presetName, StringComparison.Ordinal))
                {
                    return assets[i];
                }
            }

            return null;
        }

        private void SyncPresetChoices(string? selectedPreset)
        {
            if (_tunePresetSelector is null)
            {
                return;
            }

            List<TuningPresetAsset> assets = LoadPresetAssets();
            List<string> choices = new List<string>(assets.Count);
            for (int i = 0; i < assets.Count; i++)
            {
                string presetName = string.IsNullOrWhiteSpace(assets[i].PresetName) ? assets[i].name : assets[i].PresetName;
                choices.Add(presetName);
            }

            _tunePresetSelector.choices = choices;
            if (!string.IsNullOrWhiteSpace(selectedPreset) && choices.Contains(selectedPreset))
            {
                _tunePresetSelector.SetValueWithoutNotify(selectedPreset);
            }
            else if (choices.Count > 0)
            {
                _tunePresetSelector.SetValueWithoutNotify(choices[0]);
            }
        }

        private static List<TuningPresetAsset> LoadPresetAssets()
        {
            if (!AssetDatabase.IsValidFolder(TunePresetDirectory))
            {
                return new List<TuningPresetAsset>();
            }

            string[] guids = AssetDatabase.FindAssets("t:TuningPresetAsset", new[] { TunePresetDirectory });
            List<TuningPresetAsset> assets = new List<TuningPresetAsset>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TuningPresetAsset? asset = AssetDatabase.LoadAssetAtPath<TuningPresetAsset>(path);
                if (asset is not null)
                {
                    assets.Add(asset);
                }
            }

            assets.Sort((left, right) =>
            {
                string leftName = string.IsNullOrWhiteSpace(left.PresetName) ? left.name : left.PresetName;
                string rightName = string.IsNullOrWhiteSpace(right.PresetName) ? right.name : right.PresetName;
                return string.Compare(leftName, rightName, StringComparison.Ordinal);
            });
            return assets;
        }

        private static void EnsurePresetDirectoryExists()
        {
            string absoluteDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Editor", "TuningPresets");
            if (!Directory.Exists(absoluteDirectory))
            {
                Directory.CreateDirectory(absoluteDirectory);
            }

            AssetDatabase.Refresh();
        }

        private static string GetPresetAssetPath(string presetName)
        {
            return $"{TunePresetDirectory}/{SanitizeAssetFileName(presetName)}.asset";
        }

        private static string SanitizeAssetFileName(string name)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] buffer = name.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (Array.IndexOf(invalidChars, buffer[i]) >= 0)
                {
                    buffer[i] = '_';
                }
            }

            string sanitized = new string(buffer).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "TunePreset" : sanitized;
        }
#else
        private void SyncPresetChoices(string? selectedPreset)
        {
            if (_tunePresetSelector is not null)
            {
                _tunePresetSelector.choices = new List<string>();
                _tunePresetSelector.SetValueWithoutNotify(string.Empty);
            }
        }
#endif
    }
}
#endif
