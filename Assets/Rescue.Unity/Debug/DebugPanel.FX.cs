#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !CAPTURE_BUILD
using System;
using System.Collections.Generic;
using Rescue.Unity.FX;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rescue.Unity.Debugging
{
    public sealed partial class DebugPanel
    {
        private DropdownField? _fxHookSelector;
        private DropdownField? _fxPrefabSelector;
        private Label? _fxFrameLabel;
        private Button? _fxPlayButton;
        private Button? _fxStopButton;
        private Button? _fxPreviousFrameButton;
        private Button? _fxNextFrameButton;
        private Button? _fxRestartButton;
        private Button? _fxSpawnSelectedButton;
        private Toggle? _fxDebugDiagnosticsToggle;
        private FloatField? _fxSurfaceOffsetField;
        private Vector3Field? _fxPlaneRotationOffsetField;
        private readonly List<FxDebugCandidate> _fxCandidates = new List<FxDebugCandidate>();
        private GameObject? _manualFxInstance;
        private SpriteSequenceFxPlayer? _manualFxPlayer;

        private void BindFxDebugUi(VisualElement panel)
        {
            _fxHookSelector = panel.Q<DropdownField>("fx-hook-selector");
            _fxPrefabSelector = panel.Q<DropdownField>("fx-prefab-selector");
            _fxFrameLabel = panel.Q<Label>("fx-frame-label");
            _fxPlayButton = panel.Q<Button>("fx-play-button");
            _fxStopButton = panel.Q<Button>("fx-stop-button");
            _fxPreviousFrameButton = panel.Q<Button>("fx-previous-frame-button");
            _fxNextFrameButton = panel.Q<Button>("fx-next-frame-button");
            _fxRestartButton = panel.Q<Button>("fx-restart-button");
            _fxSpawnSelectedButton = panel.Q<Button>("fx-spawn-selected-button");
            _fxDebugDiagnosticsToggle = panel.Q<Toggle>("fx-debug-diagnostics-toggle");
            _fxSurfaceOffsetField = panel.Q<FloatField>("fx-surface-offset-field");
            _fxPlaneRotationOffsetField = panel.Q<Vector3Field>("fx-plane-rotation-offset-field");

            if (_fxHookSelector is not null)
            {
                _fxHookSelector.choices = BuildFxHookChoices();
                _fxHookSelector.SetValueWithoutNotify(_fxHookSelector.choices.Count > 0 ? _fxHookSelector.choices[0] : string.Empty);
                _fxHookSelector.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized || string.Equals(evt.previousValue, evt.newValue, StringComparison.Ordinal))
                    {
                        return;
                    }

                    RebuildFxPrefabChoices(resetSelection: true);
                });
            }

            if (_fxPrefabSelector is not null)
            {
                _fxPrefabSelector.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized || string.Equals(evt.previousValue, evt.newValue, StringComparison.Ordinal))
                    {
                        return;
                    }

                    UpdateFxFrameLabel();
                });
            }

            if (_fxPlayButton is not null) _fxPlayButton.clicked += PlayManualFx;
            if (_fxStopButton is not null) _fxStopButton.clicked += StopManualFx;
            if (_fxPreviousFrameButton is not null) _fxPreviousFrameButton.clicked += PreviousManualFxFrame;
            if (_fxNextFrameButton is not null) _fxNextFrameButton.clicked += NextManualFxFrame;
            if (_fxRestartButton is not null) _fxRestartButton.clicked += RestartManualFx;
            if (_fxSpawnSelectedButton is not null) _fxSpawnSelectedButton.clicked += SpawnSelectedFx;

            if (_fxDebugDiagnosticsToggle is not null)
            {
                _fxDebugDiagnosticsToggle.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized || evt.previousValue == evt.newValue)
                    {
                        return;
                    }

                    FxEventRouter? router = ResolveFxEventRouter();
                    if (router is not null)
                    {
                        router.DiagnosticsEnabled = evt.newValue;
                    }
                });
            }

            if (_fxSurfaceOffsetField is not null)
            {
                _fxSurfaceOffsetField.isDelayed = true;
                _fxSurfaceOffsetField.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized || Mathf.Approximately(evt.previousValue, evt.newValue))
                    {
                        return;
                    }

                    FxEventRouter? router = ResolveFxEventRouter();
                    if (router is not null)
                    {
                        router.SpawnedFxSurfaceOffset = evt.newValue;
                    }
                });
            }

            if (_fxPlaneRotationOffsetField is not null)
            {
                _fxPlaneRotationOffsetField.RegisterValueChangedCallback(evt =>
                {
                    if (!_initialized || evt.previousValue == evt.newValue)
                    {
                        return;
                    }

                    FxEventRouter? router = ResolveFxEventRouter();
                    if (router is not null)
                    {
                        router.SpawnedFxPlaneEulerOffset = evt.newValue;
                    }
                });
            }

            RebuildFxPrefabChoices(resetSelection: true);
            RefreshFxDebugUi();
        }

        private void RefreshFxDebugUi()
        {
            FxEventRouter? router = ResolveFxEventRouter();
            if (_fxDebugDiagnosticsToggle is not null)
            {
                _fxDebugDiagnosticsToggle.SetValueWithoutNotify(router is not null && router.DiagnosticsEnabled);
            }

            if (_fxSurfaceOffsetField is not null && router is not null)
            {
                _fxSurfaceOffsetField.SetValueWithoutNotify(router.SpawnedFxSurfaceOffset);
            }

            if (_fxPlaneRotationOffsetField is not null && router is not null)
            {
                _fxPlaneRotationOffsetField.SetValueWithoutNotify(router.SpawnedFxPlaneEulerOffset);
            }

            if (router is not null)
            {
                RefreshSpeedSlider(_fxPlaybackSpeedSlider, _fxPlaybackSpeedValue, router.FxPlaybackSpeedMultiplier);
            }

            UpdateFxFrameLabel();
        }

        private void BuildFxFallbackContent(ScrollView scroll)
        {
            scroll.Add(MakeSection("FX"));
            scroll.Add(MakeFieldRow("Action/Hook", out _fxHookSelector, "fx-hook-selector"));
            scroll.Add(MakeFieldRow("FX Prefab", out _fxPrefabSelector, "fx-prefab-selector"));
            scroll.Add(MakeRow(out _fxFrameLabel, "fx-frame-label", "Frame 0 / 0"));

            VisualElement playRow = new VisualElement();
            playRow.AddToClassList("button-row");
            playRow.Add(MakeButton("Play", "fx-play-button", out _fxPlayButton));
            playRow.Add(MakeButton("Stop", "fx-stop-button", out _fxStopButton));
            scroll.Add(playRow);

            VisualElement stepRow = new VisualElement();
            stepRow.AddToClassList("button-row");
            stepRow.Add(MakeButton("Previous Frame", "fx-previous-frame-button", out _fxPreviousFrameButton));
            stepRow.Add(MakeButton("Next Frame", "fx-next-frame-button", out _fxNextFrameButton));
            scroll.Add(stepRow);

            VisualElement spawnRow = new VisualElement();
            spawnRow.AddToClassList("button-row");
            spawnRow.Add(MakeButton("Restart", "fx-restart-button", out _fxRestartButton));
            spawnRow.Add(MakeButton("Spawn Selected", "fx-spawn-selected-button", out _fxSpawnSelectedButton));
            scroll.Add(spawnRow);

            _fxDebugDiagnosticsToggle = new Toggle("Diagnostics Enabled") { name = "fx-debug-diagnostics-toggle" };
            scroll.Add(_fxDebugDiagnosticsToggle);
            scroll.Add(MakeFloatFieldRow("Surface Offset", out _fxSurfaceOffsetField, "fx-surface-offset-field"));
            scroll.Add(MakeVector3FieldRow("Plane Rotation", out _fxPlaneRotationOffsetField, "fx-plane-rotation-offset-field"));
        }

        private static VisualElement MakeVector3FieldRow(string title, out Vector3Field field, string name)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("field-row");
            row.Add(new Label(title));
            field = new Vector3Field { name = name };
            field.style.flexGrow = 1.0f;
            row.Add(field);
            return row;
        }

        private static List<string> BuildFxHookChoices()
        {
            List<string> choices = new List<string> { FxDebugCatalog.UnhookedGroupLabel };
            Array hooks = Enum.GetValues(typeof(FxEventHook));
            for (int i = 0; i < hooks.Length; i++)
            {
                object? value = hooks.GetValue(i);
                if (value is FxEventHook hook)
                {
                    choices.Add(hook.ToString());
                }
            }

            return choices;
        }

        private void RebuildFxPrefabChoices(bool resetSelection)
        {
            if (_fxPrefabSelector is null)
            {
                return;
            }

            FxEventHook? hook = ParseSelectedFxHook();
            _fxCandidates.Clear();
            _fxCandidates.AddRange(FxDebugCatalog.GetCandidates(ResolveFxEventRouter(), hook));

            List<string> labels = new List<string>(_fxCandidates.Count);
            for (int i = 0; i < _fxCandidates.Count; i++)
            {
                labels.Add(_fxCandidates[i].Label);
            }

            string? previousValue = _fxPrefabSelector.value;
            _fxPrefabSelector.choices = labels;
            if (labels.Count == 0)
            {
                _fxPrefabSelector.SetValueWithoutNotify(string.Empty);
            }
            else if (!resetSelection && previousValue is not null && labels.Contains(previousValue))
            {
                _fxPrefabSelector.SetValueWithoutNotify(previousValue);
            }
            else
            {
                _fxPrefabSelector.SetValueWithoutNotify(labels[0]);
            }

            UpdateFxFrameLabel();
        }

        private FxEventHook? ParseSelectedFxHook()
        {
            string? selected = _fxHookSelector?.value;
            if (string.IsNullOrWhiteSpace(selected) || string.Equals(selected, FxDebugCatalog.UnhookedGroupLabel, StringComparison.Ordinal))
            {
                return null;
            }

            return Enum.TryParse(selected, out FxEventHook hook) ? hook : null;
        }

        private FxDebugCandidate? GetSelectedFxCandidate()
        {
            string? selected = _fxPrefabSelector?.value;
            if (string.IsNullOrWhiteSpace(selected))
            {
                return null;
            }

            for (int i = 0; i < _fxCandidates.Count; i++)
            {
                if (string.Equals(_fxCandidates[i].Label, selected, StringComparison.Ordinal))
                {
                    return _fxCandidates[i];
                }
            }

            return null;
        }

        private void SpawnSelectedFx()
        {
            FxEventRouter? router = ResolveFxEventRouter();
            FxDebugCandidate? selected = GetSelectedFxCandidate();
            if (router is null || !selected.HasValue)
            {
                SetStatus("No FX router or prefab selected.");
                return;
            }

            if (_manualFxInstance is not null)
            {
                Destroy(_manualFxInstance);
            }

            Vector3 position = ResolveFxDiagnosticPosition(router);
            FxEventHook hook = selected.Value.Hook ?? ParseSelectedFxHook() ?? FxEventHook.GroupClear;
            _manualFxInstance = router.SpawnManualDebugFx(selected.Value.Prefab, selected.Value.Prefab.name + "_Manual", hook, position);
            _manualFxPlayer = _manualFxInstance is null
                ? null
                : _manualFxInstance.GetComponentInChildren<SpriteSequenceFxPlayer>(includeInactive: true);
            if (_manualFxPlayer is null)
            {
                SetStatus(selected.Value.HasInspectableRenderer
                    ? $"Spawned {selected.Value.Prefab.name}; no frame player could be attached."
                    : $"Spawned {selected.Value.Prefab.name}; no SpriteRenderer found for frame inspection.");
            }
            else
            {
                string source = selected.Value.HasFramePlayer ? "prefab frame player" : "debug inspection player";
                SetStatus(_manualFxPlayer.FrameCount == 0
                    ? $"Spawned {selected.Value.Prefab.name} with {source}; no sprite frames found."
                    : $"Spawned {selected.Value.Prefab.name} with {source} at frame {_manualFxPlayer.CurrentFrameIndex + 1}/{_manualFxPlayer.FrameCount}.");
            }

            UpdateFxFrameLabel();
        }

        private void PlayManualFx()
        {
            if (!EnsureManualFxPlayer())
            {
                return;
            }

            SpriteSequenceFxPlayer? player = _manualFxPlayer;
            if (player is null)
            {
                return;
            }

            player.PlayFromCurrentFrame();
            UpdateFxFrameLabel();
        }

        private void StopManualFx()
        {
            if (!EnsureManualFxPlayer())
            {
                return;
            }

            SpriteSequenceFxPlayer? player = _manualFxPlayer;
            if (player is null)
            {
                return;
            }

            player.StopPlayback();
            UpdateFxFrameLabel();
        }

        private void PreviousManualFxFrame()
        {
            if (!EnsureManualFxPlayer())
            {
                return;
            }

            SpriteSequenceFxPlayer? player = _manualFxPlayer;
            if (player is null)
            {
                return;
            }

            player.PreviousFrame();
            UpdateFxFrameLabel();
        }

        private void NextManualFxFrame()
        {
            if (!EnsureManualFxPlayer())
            {
                return;
            }

            SpriteSequenceFxPlayer? player = _manualFxPlayer;
            if (player is null)
            {
                return;
            }

            player.NextFrame();
            UpdateFxFrameLabel();
        }

        private void RestartManualFx()
        {
            if (!EnsureManualFxPlayer())
            {
                return;
            }

            SpriteSequenceFxPlayer? player = _manualFxPlayer;
            if (player is null)
            {
                return;
            }

            player.RestartPlayback();
            UpdateFxFrameLabel();
        }

        private bool EnsureManualFxPlayer()
        {
            if (_manualFxInstance is null)
            {
                SpawnSelectedFx();
            }

            if (_manualFxPlayer is not null)
            {
                return true;
            }

            FxDebugCandidate? selected = GetSelectedFxCandidate();
            SetStatus(selected.HasValue && !selected.Value.HasInspectableRenderer
                ? "Selected FX has no SpriteRenderer for frame inspection."
                : "Selected FX has no frame player.");
            UpdateFxFrameLabel();
            return false;
        }

        private void UpdateFxFrameLabel()
        {
            if (_fxFrameLabel is null)
            {
                return;
            }

            if (_manualFxPlayer is null)
            {
                FxDebugCandidate? selected = GetSelectedFxCandidate();
                if (selected.HasValue && !selected.Value.HasFramePlayer)
                {
                    _fxFrameLabel.text = selected.Value.HasInspectableRenderer
                        ? "Frame 0 / 0 (no frame player; spawn to attach debug player)"
                        : "Frame 0 / 0 (no SpriteRenderer to inspect)";
                    return;
                }

                _fxFrameLabel.text = "Frame 0 / 0";
                return;
            }

            _fxFrameLabel.text = _manualFxPlayer.FrameCount == 0
                ? "Frame 0 / 0"
                : $"Frame {_manualFxPlayer.CurrentFrameIndex + 1} / {_manualFxPlayer.FrameCount}";
        }
    }
}
#endif
