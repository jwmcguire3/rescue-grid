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

        [SerializeField] private PlayableLevelSession? session;
        [SerializeField] private AudioSettingsController? audioSettings;

        private UIDocument? document;
        private VisualElement? panel;
        private Button? toggleButton;
        private Button? restartButton;
        private DropdownField? levelDropdown;
        private Slider? musicSlider;
        private Slider? fxSlider;
        private bool isOpen;

        public bool IsOpen => isOpen;

        public IReadOnlyList<string> LevelChoices => levelDropdown?.choices ?? (IReadOnlyList<string>)Array.Empty<string>();

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
            anchor.style.width = 260f;
            anchor.style.alignItems = Align.FlexEnd;
            anchor.pickingMode = PickingMode.Position;

            toggleButton = new Button(Toggle) { name = "settings-toggle-button", text = "Settings" };
            StyleButton(toggleButton);
            toggleButton.style.width = 112f;
            toggleButton.style.height = 42f;

            panel = new VisualElement { name = "settings-panel" };
            panel.style.display = DisplayStyle.None;
            panel.style.marginTop = 8f;
            panel.style.width = 260f;
            panel.style.paddingTop = 12f;
            panel.style.paddingRight = 12f;
            panel.style.paddingBottom = 12f;
            panel.style.paddingLeft = 12f;
            panel.style.backgroundColor = new Color(0.08f, 0.11f, 0.13f, 0.92f);
            panel.style.borderTopLeftRadius = 8f;
            panel.style.borderTopRightRadius = 8f;
            panel.style.borderBottomRightRadius = 8f;
            panel.style.borderBottomLeftRadius = 8f;

            restartButton = new Button(RequestRestart) { name = "settings-restart-button", text = "Restart Level" };
            StyleButton(restartButton);
            restartButton.style.height = 38f;
            restartButton.style.marginBottom = 10f;

            levelDropdown = new DropdownField("Level")
            {
                name = "settings-level-dropdown",
                choices = BuildLevelChoices(),
            };
            levelDropdown.RegisterValueChangedCallback(evt => SelectLevel(evt.newValue));
            StyleField(levelDropdown);

            musicSlider = CreateSlider("settings-music-slider", "Music");
            musicSlider.RegisterValueChangedCallback(evt => ResolveAudioSettings()?.SetMusicVolume(evt.newValue));

            fxSlider = CreateSlider("settings-fx-slider", "FX");
            fxSlider.RegisterValueChangedCallback(evt => ResolveAudioSettings()?.SetFxVolume(evt.newValue));

            panel.Add(restartButton);
            panel.Add(levelDropdown);
            panel.Add(musicSlider);
            panel.Add(fxSlider);
            anchor.Add(toggleButton);
            anchor.Add(panel);
            root.Add(anchor);
            RefreshValues();
        }

        private static void StyleButton(Button button)
        {
            button.style.backgroundColor = new Color(0.93f, 0.73f, 0.26f, 0.95f);
            button.style.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = 15;
            button.style.borderTopWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.borderLeftWidth = 0f;
        }

        private static void StyleField(VisualElement element)
        {
            element.style.marginBottom = 10f;
            element.style.color = Color.white;
            element.style.fontSize = 13;
        }

        private static Slider CreateSlider(string name, string label)
        {
            Slider slider = new Slider(label, 0f, 1f)
            {
                name = name,
                showInputField = true,
            };
            StyleField(slider);
            return slider;
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

            AudioSettingsController? settings = ResolveAudioSettings();
            if (settings is not null)
            {
                musicSlider?.SetValueWithoutNotify(settings.MusicVolume);
                fxSlider?.SetValueWithoutNotify(settings.FxVolume);
            }
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
    }
}
