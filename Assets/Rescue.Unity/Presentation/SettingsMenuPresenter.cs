using System;
using System.Collections.Generic;
using System.Reflection;
using Rescue.Content;
using Rescue.Unity.Audio;
using Rescue.Unity.Feedback;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Rescue.Unity.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class SettingsMenuPresenter : MonoBehaviour
    {
        private const string GameSceneName = "Game";
        private const string RuntimeThemeResourcePath = "Rescue.Unity/Debug/UnityDefaultRuntimeTheme";
        private const int PanelSortingOrder = 1000;
        public const float PanelWidth = 400f;
        public const float SliderTrackMinWidth = 220f;

        [SerializeField] private PlayableLevelSession? session;
        [SerializeField] private AudioSettingsController? audioSettings;

        private UIDocument? document;
        private VisualElement? panel;
        private Button? toggleButton;
        private Button? resumeButton;
        private Button? restartButton;
        private DropdownField? levelDropdown;
        private Slider? musicSlider;
        private Slider? fxSlider;
        private Toggle? muteMusicToggle;
        private Toggle? muteFxToggle;
        private Label? musicValueLabel;
        private Label? fxValueLabel;
        private float lastNonZeroMusicVolume = 1.0f;
        private float lastNonZeroFxVolume = 1.0f;
        private bool isOpen;

        public bool IsOpen => isOpen;

        public IReadOnlyList<string> LevelChoices => levelDropdown?.choices ?? (IReadOnlyList<string>)Array.Empty<string>();

        public bool IsMusicMuted => ResolveAudioSettings()?.MusicVolume <= 0f;

        public bool IsFxMuted => ResolveAudioSettings()?.FxVolume <= 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        public static SettingsMenuPresenter? EnsureForActiveGameScene()
        {
            return EnsureForScene(SceneManager.GetActiveScene());
        }

        public static SettingsMenuPresenter EnsureInstance()
        {
            SettingsMenuPresenter? existing = FindAnyObjectByType<SettingsMenuPresenter>();
            if (existing is not null)
            {
                existing.EnsureDocument();
                existing.ResolveSceneReferences();
                return existing;
            }

            GameObject host = new GameObject("SettingsMenu");
            host.AddComponent<UIDocument>();
            SettingsMenuPresenter presenter = host.AddComponent<SettingsMenuPresenter>();
            presenter.EnsureDocument();
            presenter.ResolveSceneReferences();
            return presenter;
        }

        public void Toggle()
        {
            SetOpen(!isOpen);
        }

        public void SetOpen(bool open)
        {
            EnsureDocument();
            isOpen = open;
            if (panel is not null)
            {
                panel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (open)
            {
                RefreshValues();
            }
        }

        public void RequestRestart()
        {
            ResolveSceneReferences();
            session?.Retry();
            RefreshValues();
        }

        public void RequestResume()
        {
            SetOpen(false);
        }

        public void SelectLevel(string levelChoice)
        {
            ResolveSceneReferences();
            if (session is null || string.IsNullOrWhiteSpace(levelChoice))
            {
                return;
            }

            string levelId = ParseLevelId(levelChoice);
            session.LoadLevel(levelId, session.Seed);
            RefreshValues();
        }

        public void SetMusicMuted(bool muted)
        {
            AudioSettingsController? settings = ResolveAudioSettings();
            if (settings is null)
            {
                return;
            }

            if (muted)
            {
                RememberNonZeroMusic(settings.MusicVolume);
                settings.SetMusicVolume(0f);
            }
            else
            {
                settings.SetMusicVolume(ResolveRestoreVolume(lastNonZeroMusicVolume));
            }

            RefreshValues();
        }

        public void SetFxMuted(bool muted)
        {
            AudioSettingsController? settings = ResolveAudioSettings();
            if (settings is null)
            {
                return;
            }

            if (muted)
            {
                RememberNonZeroFx(settings.FxVolume);
                settings.SetFxVolume(0f);
            }
            else
            {
                settings.SetFxVolume(ResolveRestoreVolume(lastNonZeroFxVolume));
            }

            RefreshValues();
        }

        private void Awake()
        {
            EnsureDocument();
            ResolveSceneReferences();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (toggleButton is not null)
            {
                toggleButton.clicked -= Toggle;
            }

            if (resumeButton is not null)
            {
                resumeButton.clicked -= RequestResume;
            }

            if (restartButton is not null)
            {
                restartButton.clicked -= RequestRestart;
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static SettingsMenuPresenter? EnsureForScene(Scene scene)
        {
            if (!string.Equals(scene.name, GameSceneName, StringComparison.Ordinal))
            {
                return null;
            }

            return EnsureInstance();
        }

        private void EnsureDocument()
        {
            if (document is null)
            {
                document = GetComponent<UIDocument>();
                document.panelSettings = CreatePanelSettings();
            }

            if (panel is null)
            {
                BuildVisualTree();
            }
        }

        private void ResolveSceneReferences()
        {
            if (session is null)
            {
                session = FindAnyObjectByType<PlayableLevelSession>();
            }

            if (audioSettings is null)
            {
                audioSettings = AudioSettingsController.EnsureInstance();
            }

            MusicPlayer? musicPlayer = FindAnyObjectByType<MusicPlayer>();
            if (musicPlayer is not null)
            {
                musicPlayer.SettingsController = audioSettings;
            }

            AudioEventRouter? audioRouter = FindAnyObjectByType<AudioEventRouter>();
            if (audioRouter is not null)
            {
                audioRouter.SettingsController = audioSettings;
            }
        }

        private PanelSettings CreatePanelSettings()
        {
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(975, 1536);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = PanelSortingOrder;
            settings.clearColor = false;
            settings.colorClearValue = Color.clear;
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

        private void BuildVisualTree()
        {
            if (document is null)
            {
                return;
            }

            VisualElement root = document.rootVisualElement;
            root.Clear();
            root.name = "settings-menu-root";
            root.style.flexGrow = 1.0f;
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;
            root.pickingMode = PickingMode.Ignore;

            VisualElement anchor = new VisualElement { name = "settings-menu-anchor" };
            anchor.style.position = Position.Absolute;
            anchor.style.top = 64f;
            anchor.style.right = 52f;
            anchor.style.width = PanelWidth;
            anchor.style.alignItems = Align.FlexEnd;
            anchor.pickingMode = PickingMode.Position;

            toggleButton = new Button(Toggle) { name = "settings-toggle-button", text = "Settings" };
            StylePrimaryButton(toggleButton);
            toggleButton.style.width = 112f;
            toggleButton.style.height = 42f;

            panel = new VisualElement { name = "settings-panel" };
            panel.style.display = DisplayStyle.None;
            panel.style.marginTop = 8f;
            panel.style.width = PanelWidth;
            panel.style.paddingTop = 14f;
            panel.style.paddingRight = 16f;
            panel.style.paddingBottom = 16f;
            panel.style.paddingLeft = 16f;
            panel.style.backgroundColor = new Color(0.055f, 0.075f, 0.085f, 0.94f);
            panel.style.borderTopLeftRadius = 8f;
            panel.style.borderTopRightRadius = 8f;
            panel.style.borderBottomRightRadius = 8f;
            panel.style.borderBottomLeftRadius = 8f;
            panel.style.borderTopWidth = 1f;
            panel.style.borderRightWidth = 1f;
            panel.style.borderBottomWidth = 1f;
            panel.style.borderLeftWidth = 1f;
            panel.style.borderTopColor = new Color(1f, 1f, 1f, 0.08f);
            panel.style.borderRightColor = new Color(1f, 1f, 1f, 0.08f);
            panel.style.borderBottomColor = new Color(0f, 0f, 0f, 0.35f);
            panel.style.borderLeftColor = new Color(1f, 1f, 1f, 0.08f);

            VisualElement headerRow = new VisualElement { name = "settings-header-row" };
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.marginBottom = 12f;

            Label titleLabel = new Label("Settings") { name = "settings-title-label" };
            titleLabel.style.color = Color.white;
            titleLabel.style.fontSize = 18;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            resumeButton = new Button(RequestResume) { name = "settings-resume-button", text = "Resume" };
            StyleSecondaryButton(resumeButton);
            resumeButton.style.width = 92f;
            resumeButton.style.height = 34f;

            headerRow.Add(titleLabel);
            headerRow.Add(resumeButton);

            restartButton = new Button(RequestRestart) { name = "settings-restart-button", text = "Restart Level" };
            StylePrimaryButton(restartButton);
            restartButton.style.height = 40f;
            restartButton.style.marginTop = 4f;
            restartButton.style.marginBottom = 14f;

            levelDropdown = new DropdownField("Level")
            {
                name = "settings-level-dropdown",
                choices = BuildLevelChoices(),
            };
            levelDropdown.RegisterValueChangedCallback(evt => SelectLevel(evt.newValue));
            StyleDropdown(levelDropdown);

            Label audioSectionLabel = new Label("Audio") { name = "settings-audio-section-label" };
            audioSectionLabel.style.color = new Color(1f, 1f, 1f, 0.84f);
            audioSectionLabel.style.fontSize = 13;
            audioSectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            audioSectionLabel.style.marginTop = 2f;
            audioSectionLabel.style.marginBottom = 8f;

            VisualElement musicRow = CreateSliderRow("settings-music-row", "Music", "settings-music-slider", out musicSlider, out musicValueLabel);
            musicSlider.RegisterValueChangedCallback(evt =>
            {
                RememberNonZeroMusic(evt.newValue);
                ResolveAudioSettings()?.SetMusicVolume(evt.newValue);
                RefreshAudioControls();
            });

            VisualElement fxRow = CreateSliderRow("settings-fx-row", "FX", "settings-fx-slider", out fxSlider, out fxValueLabel);
            fxSlider.RegisterValueChangedCallback(evt =>
            {
                RememberNonZeroFx(evt.newValue);
                ResolveAudioSettings()?.SetFxVolume(evt.newValue);
                RefreshAudioControls();
            });

            VisualElement muteRow = new VisualElement { name = "settings-mute-row" };
            muteRow.style.flexDirection = FlexDirection.Row;
            muteRow.style.marginTop = 4f;
            muteRow.style.marginBottom = 2f;

            muteMusicToggle = CreateMuteToggle("settings-mute-music-toggle", "Mute Music");
            muteMusicToggle.RegisterValueChangedCallback(evt => SetMusicMuted(evt.newValue));

            muteFxToggle = CreateMuteToggle("settings-mute-fx-toggle", "Mute FX");
            muteFxToggle.RegisterValueChangedCallback(evt => SetFxMuted(evt.newValue));
            muteFxToggle.style.marginLeft = 14f;

            muteRow.Add(muteMusicToggle);
            muteRow.Add(muteFxToggle);

            panel.Add(headerRow);
            panel.Add(restartButton);
            panel.Add(levelDropdown);
            panel.Add(audioSectionLabel);
            panel.Add(musicRow);
            panel.Add(fxRow);
            panel.Add(muteRow);
            anchor.Add(toggleButton);
            anchor.Add(panel);
            root.Add(anchor);
            RefreshValues();
        }

        private static void StylePrimaryButton(Button button)
        {
            button.style.backgroundColor = new Color(0.93f, 0.73f, 0.26f, 0.95f);
            button.style.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = 15;
            button.style.borderTopWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.borderLeftWidth = 0f;
            button.style.borderTopLeftRadius = 3f;
            button.style.borderTopRightRadius = 3f;
            button.style.borderBottomRightRadius = 3f;
            button.style.borderBottomLeftRadius = 3f;
        }

        private static void StyleSecondaryButton(Button button)
        {
            button.style.backgroundColor = new Color(1f, 1f, 1f, 0.10f);
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = 13;
            button.style.borderTopWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftWidth = 1f;
            button.style.borderTopColor = new Color(1f, 1f, 1f, 0.12f);
            button.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);
            button.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
            button.style.borderLeftColor = new Color(1f, 1f, 1f, 0.12f);
            button.style.borderTopLeftRadius = 3f;
            button.style.borderTopRightRadius = 3f;
            button.style.borderBottomRightRadius = 3f;
            button.style.borderBottomLeftRadius = 3f;
        }

        private static void StyleField(VisualElement element)
        {
            element.style.color = Color.white;
            element.style.fontSize = 13;
        }

        private static void StyleDropdown(DropdownField dropdown)
        {
            StyleField(dropdown);
            dropdown.style.marginBottom = 14f;
            dropdown.style.height = 40f;
            dropdown.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
            dropdown.style.borderTopWidth = 1f;
            dropdown.style.borderRightWidth = 1f;
            dropdown.style.borderBottomWidth = 1f;
            dropdown.style.borderLeftWidth = 1f;
            dropdown.style.borderTopColor = new Color(1f, 1f, 1f, 0.16f);
            dropdown.style.borderRightColor = new Color(1f, 1f, 1f, 0.16f);
            dropdown.style.borderBottomColor = new Color(1f, 1f, 1f, 0.16f);
            dropdown.style.borderLeftColor = new Color(1f, 1f, 1f, 0.16f);
            dropdown.style.borderTopLeftRadius = 4f;
            dropdown.style.borderTopRightRadius = 4f;
            dropdown.style.borderBottomRightRadius = 4f;
            dropdown.style.borderBottomLeftRadius = 4f;
            dropdown.Query<VisualElement>().ForEach(child => child.style.color = Color.white);

            Label? label = dropdown.Q<Label>(className: "unity-label");
            if (label is not null)
            {
                label.style.width = 56f;
                label.style.color = Color.white;
                label.style.fontSize = 14;
            }

            VisualElement? input = dropdown.Q<VisualElement>(className: "unity-base-field__input");
            if (input is not null)
            {
                input.style.backgroundColor = new Color(1f, 1f, 1f, 0.10f);
                input.style.borderTopWidth = 1f;
                input.style.borderRightWidth = 1f;
                input.style.borderBottomWidth = 1f;
                input.style.borderLeftWidth = 1f;
                input.style.borderTopColor = new Color(1f, 1f, 1f, 0.14f);
                input.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);
                input.style.borderBottomColor = new Color(1f, 1f, 1f, 0.14f);
                input.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);
                input.style.borderTopLeftRadius = 4f;
                input.style.borderTopRightRadius = 4f;
                input.style.borderBottomRightRadius = 4f;
                input.style.borderBottomLeftRadius = 4f;
            }

            TextElement? selectedText = dropdown.Q<TextElement>(className: "unity-dropdown-field__text");
            if (selectedText is not null)
            {
                selectedText.style.color = Color.white;
                selectedText.style.fontSize = 13;
            }
        }

        private static VisualElement CreateSliderRow(string rowName, string labelText, string sliderName, out Slider slider, out Label valueLabel)
        {
            VisualElement row = new VisualElement { name = rowName };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 10f;
            row.style.height = 34f;

            Label label = new Label(labelText) { name = $"{rowName}-label" };
            label.style.color = Color.white;
            label.style.fontSize = 14;
            label.style.width = 56f;

            slider = new Slider(0f, 1f)
            {
                name = sliderName,
                showInputField = false,
            };
            slider.style.flexGrow = 1f;
            slider.style.minWidth = SliderTrackMinWidth;
            slider.style.height = 24f;
            slider.style.marginLeft = 10f;
            slider.style.marginRight = 10f;

            valueLabel = new Label("100%") { name = $"{sliderName}-value-label" };
            valueLabel.style.color = new Color(1f, 1f, 1f, 0.84f);
            valueLabel.style.fontSize = 13;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            valueLabel.style.width = 44f;

            row.Add(label);
            row.Add(slider);
            row.Add(valueLabel);
            return row;
        }

        private static Toggle CreateMuteToggle(string name, string label)
        {
            Toggle toggle = new Toggle(label) { name = name };
            toggle.style.flexGrow = 1f;
            toggle.style.minHeight = 30f;
            toggle.style.color = new Color(1f, 1f, 1f, 0.92f);
            toggle.style.fontSize = 13;
            return toggle;
        }

        private void RefreshAudioControls()
        {
            AudioSettingsController? settings = ResolveAudioSettings();
            if (settings is null)
            {
                return;
            }

            RememberNonZeroMusic(settings.MusicVolume);
            RememberNonZeroFx(settings.FxVolume);

            musicSlider?.SetValueWithoutNotify(settings.MusicVolume);
            fxSlider?.SetValueWithoutNotify(settings.FxVolume);
            muteMusicToggle?.SetValueWithoutNotify(settings.MusicVolume <= 0f);
            muteFxToggle?.SetValueWithoutNotify(settings.FxVolume <= 0f);
            UpdateValueLabel(musicValueLabel, settings.MusicVolume);
            UpdateValueLabel(fxValueLabel, settings.FxVolume);
        }

        private void RefreshValues()
        {
            ResolveSceneReferences();

            if (levelDropdown is not null && session is not null)
            {
                string choice = ToLevelChoice(session.CurrentLevelId);
                if (!levelDropdown.choices.Contains(choice))
                {
                    levelDropdown.choices = BuildLevelChoices();
                }

                levelDropdown.SetValueWithoutNotify(choice);
            }

            RefreshAudioControls();
        }

        private AudioSettingsController? ResolveAudioSettings()
        {
            if (audioSettings is null)
            {
                audioSettings = AudioSettingsController.EnsureInstance();
            }

            return audioSettings;
        }

        private static List<string> BuildLevelChoices()
        {
            List<string> choices = new List<string>(PlayableLevelSession.LevelIds.Count);
            for (int i = 0; i < PlayableLevelSession.LevelIds.Count; i++)
            {
                choices.Add(ToLevelChoice(PlayableLevelSession.LevelIds[i]));
            }

            return choices;
        }

        private static string ToLevelChoice(string levelId)
        {
            try
            {
                LevelJson level = Loader.LoadLevelDefinition(levelId);
                if (!string.IsNullOrWhiteSpace(level.Name))
                {
                    return $"{level.Id} - {level.Name}";
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Settings menu could not read level metadata for '{levelId}': {exception.Message}");
            }

            return levelId;
        }

        private static string ParseLevelId(string choice)
        {
            int separator = choice.IndexOf(" - ", StringComparison.Ordinal);
            return separator > 0 ? choice.Substring(0, separator) : choice;
        }

        private static void UpdateValueLabel(Label? label, float value)
        {
            if (label is not null)
            {
                label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
            }
        }

        private static float ResolveRestoreVolume(float value)
        {
            return Mathf.Clamp01(value) > 0f ? Mathf.Clamp01(value) : 1.0f;
        }

        private void RememberNonZeroMusic(float value)
        {
            if (value > 0.001f)
            {
                lastNonZeroMusicVolume = Mathf.Clamp01(value);
            }
        }

        private void RememberNonZeroFx(float value)
        {
            if (value > 0.001f)
            {
                lastNonZeroFxVolume = Mathf.Clamp01(value);
            }
        }
    }
}
