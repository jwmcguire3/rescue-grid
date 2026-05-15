#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !CAPTURE_BUILD
using System;
using System.Collections.Generic;
using Rescue.Core.State;
using Rescue.Unity.Presentation;
using Rescue.Unity.Presentation.Targets;
using UnityEngine;
using UnityEngine.UIElements;
using static Rescue.Unity.Debugging.DebugPanelFallbackUi;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rescue.Unity.Debugging
{
    public sealed partial class DebugPanel
    {
        private const string PuppyAnimationCatalogResourcePath = "PuppyAnimationCatalog";
        private const string DaisyFbxAssetPath = "Assets/Rescue.Unity/Art/Models/Targets/daisy_final.fbx";

        private DropdownField? _puppyTargetSelector;
        private DropdownField? _puppyAnimationSelector;
        private Toggle? _puppyRepeatToggle;
        private Button? _puppyAddButton;
        private Button? _puppyClearButton;
        private Button? _puppyPlayButton;
        private Button? _puppyStopButton;
        private Button? _puppyLookAtPlayerButton;
        private Label? _puppySequenceValue;
        private Label? _puppyCatalogValue;

        private readonly List<AnimationClip> _puppyCatalogClips = new List<AnimationClip>();
        private readonly Dictionary<string, AnimationClip> _puppyClipByLabel = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        private readonly List<AnimationClip> _puppySequence = new List<AnimationClip>();
        private bool _puppyCatalogLoaded;

        private void BindPuppyDebugUi(VisualElement panel)
        {
            _puppyTargetSelector = panel.Q<DropdownField>("puppy-target-selector");
            _puppyAnimationSelector = panel.Q<DropdownField>("puppy-animation-selector");
            _puppyRepeatToggle = panel.Q<Toggle>("puppy-repeat-toggle");
            _puppyAddButton = panel.Q<Button>("puppy-add-button");
            _puppyClearButton = panel.Q<Button>("puppy-clear-button");
            _puppyPlayButton = panel.Q<Button>("puppy-play-button");
            _puppyStopButton = panel.Q<Button>("puppy-stop-button");
            _puppyLookAtPlayerButton = panel.Q<Button>("puppy-look-at-player-button");
            _puppySequenceValue = panel.Q<Label>("puppy-sequence-value");
            _puppyCatalogValue = panel.Q<Label>("puppy-catalog-value");

            if (_puppyAddButton is not null) _puppyAddButton.clicked += AddSelectedPuppyAnimation;
            if (_puppyClearButton is not null) _puppyClearButton.clicked += ClearPuppyAnimationSequence;
            if (_puppyPlayButton is not null) _puppyPlayButton.clicked += PlayPuppyAnimationSequence;
            if (_puppyStopButton is not null) _puppyStopButton.clicked += StopSelectedPuppyAnimation;
            if (_puppyLookAtPlayerButton is not null) _puppyLookAtPlayerButton.clicked += MakeSelectedPuppyLookAtPlayer;

            RefreshPuppyDebugUi();
        }

        private void RefreshPuppyDebugUi()
        {
            EnsurePuppyCatalogLoaded();
            RefreshPuppyTargetChoices();
            RefreshPuppyAnimationChoices();
            RefreshPuppySequenceLabel();
        }

        private void BuildPuppyFallbackContent(ScrollView scroll)
        {
            scroll.Add(MakeSection("Puppy Animation"));
            scroll.Add(MakeFieldRow("Target", out _puppyTargetSelector, "puppy-target-selector"));
            scroll.Add(MakeFieldRow("Animation", out _puppyAnimationSelector, "puppy-animation-selector"));

            VisualElement buildRow = new VisualElement();
            buildRow.AddToClassList("button-row");
            buildRow.Add(MakeButton("Add", "puppy-add-button", out _puppyAddButton));
            buildRow.Add(MakeButton("Clear", "puppy-clear-button", out _puppyClearButton));
            scroll.Add(buildRow);

            _puppyRepeatToggle = new Toggle("Repeat") { name = "puppy-repeat-toggle" };
            scroll.Add(_puppyRepeatToggle);

            VisualElement playRow = new VisualElement();
            playRow.AddToClassList("button-row");
            playRow.Add(MakeButton("Play", "puppy-play-button", out _puppyPlayButton));
            playRow.Add(MakeButton("Stop", "puppy-stop-button", out _puppyStopButton));
            scroll.Add(playRow);

            scroll.Add(MakeButton("Look At Player", "puppy-look-at-player-button", out _puppyLookAtPlayerButton));
            scroll.Add(MakeRow(out _puppySequenceValue, "puppy-sequence-value", "Sequence: <empty>"));
            scroll.Add(MakeRow(out _puppyCatalogValue, "puppy-catalog-value", "Catalog: 0 clips"));
        }

        private void RefreshPuppyTargetChoices()
        {
            if (_puppyTargetSelector is null || _currentState is null)
            {
                return;
            }

            string? previous = _puppyTargetSelector.value;
            List<string> choices = new List<string>();
            GameStateViewPresenter? presenter = ResolveGameStateViewPresenter();
            for (int i = 0; i < _currentState.Targets.Length; i++)
            {
                TargetState target = _currentState.Targets[i];
                if (target.Extracted)
                {
                    continue;
                }

                if (presenter is not null &&
                    !presenter.TryGetTargetInstance(target.TargetId, out GameObject? targetObject) &&
                    targetObject == null)
                {
                    continue;
                }

                choices.Add(target.TargetId);
            }

            _puppyTargetSelector.choices = choices;
            if (!string.IsNullOrWhiteSpace(previous) && choices.Contains(previous))
            {
                _puppyTargetSelector.SetValueWithoutNotify(previous);
            }
            else
            {
                _puppyTargetSelector.SetValueWithoutNotify(choices.Count > 0 ? choices[0] : string.Empty);
            }
        }

        private void RefreshPuppyAnimationChoices()
        {
            if (_puppyAnimationSelector is null)
            {
                return;
            }

            string? previous = _puppyAnimationSelector.value;
            List<string> labels = new List<string>(_puppyClipByLabel.Keys);
            labels.Sort(StringComparer.Ordinal);
            _puppyAnimationSelector.choices = labels;
            if (!string.IsNullOrWhiteSpace(previous) && labels.Contains(previous))
            {
                _puppyAnimationSelector.SetValueWithoutNotify(previous);
            }
            else
            {
                _puppyAnimationSelector.SetValueWithoutNotify(labels.Count > 0 ? labels[0] : string.Empty);
            }

            if (_puppyCatalogValue is not null)
            {
                _puppyCatalogValue.text = $"Catalog: {_puppyCatalogClips.Count} clips";
            }
        }

        private void AddSelectedPuppyAnimation()
        {
            AnimationClip? clip = GetSelectedPuppyClip();
            if (clip == null)
            {
                SetStatus("Choose a puppy animation first.");
                RefreshUi();
                return;
            }

            _puppySequence.Add(clip);
            SetStatus($"Added puppy animation {NormalizePuppyClipName(clip.name)}.");
            RefreshPuppySequenceLabel();
        }

        private void ClearPuppyAnimationSequence()
        {
            _puppySequence.Clear();
            SetStatus("Cleared puppy animation sequence.");
            RefreshPuppySequenceLabel();
        }

        private void PlayPuppyAnimationSequence()
        {
            if (!TryResolveSelectedPuppyAnimator(out TargetPuppyAnimator? puppyAnimator))
            {
                SetStatus("No live target puppy selected.");
                RefreshUi();
                return;
            }

            List<AnimationClip> sequence = _puppySequence.Count > 0
                ? new List<AnimationClip>(_puppySequence)
                : BuildSingleSelectedPuppyClipSequence();
            if (sequence.Count == 0)
            {
                SetStatus("Choose a puppy animation first.");
                RefreshUi();
                return;
            }

            bool repeat = _puppyRepeatToggle is not null && _puppyRepeatToggle.value;
            if (!puppyAnimator!.PlayDebugAnimationSequence(sequence, repeat))
            {
                SetStatus("Selected puppy could not play the debug animation.");
                RefreshUi();
                return;
            }

            SetStatus(repeat
                ? $"Playing puppy sequence ({sequence.Count}) on repeat."
                : $"Playing puppy sequence ({sequence.Count}).");
            RefreshUi();
        }

        private void StopSelectedPuppyAnimation()
        {
            if (!TryResolveSelectedPuppyAnimator(out TargetPuppyAnimator? puppyAnimator))
            {
                SetStatus("No live target puppy selected.");
                RefreshUi();
                return;
            }

            puppyAnimator!.StopDebugAnimation();
            SetStatus("Stopped puppy debug animation.");
            RefreshUi();
        }

        private void MakeSelectedPuppyLookAtPlayer()
        {
            if (!TryResolveSelectedPuppyObject(out GameObject? targetObject))
            {
                SetStatus("No live target puppy selected.");
                RefreshUi();
                return;
            }

            TargetPuppyLookAt? lookAt = targetObject!.GetComponentInChildren<TargetPuppyLookAt>(includeInactive: true);
            if (lookAt == null)
            {
                SetStatus("Selected puppy has no look-at component.");
                RefreshUi();
                return;
            }

            if (!lookAt.ForceLookAtPlayer(Camera.main))
            {
                SetStatus("Selected puppy could not look at the player.");
                RefreshUi();
                return;
            }

            SetStatus("Selected puppy is looking at the player.");
            RefreshUi();
        }

        private List<AnimationClip> BuildSingleSelectedPuppyClipSequence()
        {
            AnimationClip? clip = GetSelectedPuppyClip();
            return clip == null
                ? new List<AnimationClip>()
                : new List<AnimationClip> { clip };
        }

        private AnimationClip? GetSelectedPuppyClip()
        {
            string? selected = _puppyAnimationSelector?.value;
            if (string.IsNullOrWhiteSpace(selected))
            {
                return null;
            }

            return _puppyClipByLabel.TryGetValue(selected, out AnimationClip clip) ? clip : null;
        }

        private bool TryResolveSelectedPuppyAnimator(out TargetPuppyAnimator? puppyAnimator)
        {
            puppyAnimator = null;
            if (!TryResolveSelectedPuppyObject(out GameObject? targetObject) || targetObject == null)
            {
                return false;
            }

            puppyAnimator = targetObject.GetComponentInChildren<TargetPuppyAnimator>(includeInactive: true);
            return puppyAnimator != null;
        }

        private bool TryResolveSelectedPuppyObject(out GameObject? targetObject)
        {
            targetObject = null;
            string? targetId = _puppyTargetSelector?.value;
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            GameStateViewPresenter? presenter = ResolveGameStateViewPresenter();
            return presenter is not null &&
                presenter.TryGetTargetInstance(targetId, out targetObject) &&
                targetObject != null;
        }

        private void RefreshPuppySequenceLabel()
        {
            if (_puppySequenceValue is null)
            {
                return;
            }

            if (_puppySequence.Count == 0)
            {
                _puppySequenceValue.text = "Sequence: <selected animation only>";
                return;
            }

            List<string> labels = new List<string>(_puppySequence.Count);
            for (int i = 0; i < _puppySequence.Count; i++)
            {
                labels.Add(NormalizePuppyClipName(_puppySequence[i].name));
            }

            _puppySequenceValue.text = $"Sequence: {string.Join(" -> ", labels)}";
        }

        private void EnsurePuppyCatalogLoaded()
        {
            if (_puppyCatalogLoaded)
            {
                return;
            }

            _puppyCatalogLoaded = true;
            _puppyCatalogClips.Clear();
            _puppyClipByLabel.Clear();

            TargetPuppyAnimationCatalog? catalog = Resources.Load<TargetPuppyAnimationCatalog>(PuppyAnimationCatalogResourcePath);
            if (catalog is not null)
            {
                AddPuppyClips(catalog.Clips);
            }

#if UNITY_EDITOR
            if (_puppyCatalogClips.Count == 0)
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(DaisyFbxAssetPath);
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is AnimationClip clip)
                    {
                        AddPuppyClip(clip);
                    }
                }
            }
#endif
        }

        private void AddPuppyClips(IReadOnlyList<AnimationClip> clips)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                AddPuppyClip(clips[i]);
            }
        }

        private void AddPuppyClip(AnimationClip? clip)
        {
            if (clip == null || _puppyCatalogClips.Contains(clip))
            {
                return;
            }

            _puppyCatalogClips.Add(clip);
            string label = NormalizePuppyClipName(clip.name);
            string uniqueLabel = label;
            int suffix = 2;
            while (_puppyClipByLabel.ContainsKey(uniqueLabel))
            {
                uniqueLabel = $"{label} ({suffix})";
                suffix++;
            }

            _puppyClipByLabel.Add(uniqueLabel, clip);
        }

        private static string NormalizePuppyClipName(string clipName)
        {
            const string doublePrefix = "Arm_Labrador|Arm_Labrador|";
            const string singlePrefix = "Arm_Labrador|";
            if (clipName.StartsWith(doublePrefix, StringComparison.Ordinal))
            {
                return clipName.Substring(doublePrefix.Length);
            }

            return clipName.StartsWith(singlePrefix, StringComparison.Ordinal)
                ? clipName.Substring(singlePrefix.Length)
                : clipName;
        }
    }
}
#endif
