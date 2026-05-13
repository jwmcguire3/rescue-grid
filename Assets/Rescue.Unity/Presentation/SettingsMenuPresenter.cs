using System;
using System.Collections.Generic;
using Rescue.Content;
using Rescue.Unity.Audio;
using Rescue.Unity.Feedback;
using Rescue.Unity.Haptics;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rescue.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class SettingsMenuPresenter : MonoBehaviour
    {
        private const string GameSceneName = "Game";
        private const string SettingsMenuPrefabResourcePath = "Rescue.Unity/UI/SettingsMenu";
        public const float PanelWidth = 420f;
        public const float SliderTrackMinWidth = 220f;

        [SerializeField] private PlayableLevelSession? session;
        [SerializeField] private AudioSettingsController? audioSettings;
        [SerializeField] private SettingsMenuView? view;

        private readonly List<string> levelChoices = new List<string>();
        private float lastNonZeroMusicVolume = 1.0f;
        private float lastNonZeroFxVolume = 1.0f;
        private bool isOpen;
        private bool isWired;

        public bool IsOpen => isOpen;

        public SettingsMenuView View
        {
            get
            {
                EnsureView();
                return view ?? throw new InvalidOperationException($"{nameof(SettingsMenuView)} could not be resolved.");
            }
        }

        public IReadOnlyList<string> LevelChoices
        {
            get
            {
                EnsureLevelChoices();
                return levelChoices;
            }
        }

        public bool IsMusicMuted => ResolveAudioSettings()?.MusicVolume <= 0f;

        public bool IsFxMuted => ResolveAudioSettings()?.FxVolume <= 0f;

        public bool HapticsEnabled => ResolveAudioSettings()?.HapticsEnabled ?? true;

        public float HapticsStrength => ResolveAudioSettings()?.HapticsStrength ?? 1.0f;

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
                existing.EnsureView();
                existing.ResolveSceneReferences();
                return existing;
            }

            GameObject? prefab = Resources.Load<GameObject>(SettingsMenuPrefabResourcePath);
            if (prefab is not null)
            {
                GameObject instance = Instantiate(prefab);
                instance.name = "SettingsMenu";
                SettingsMenuPresenter? prefabPresenter = instance.GetComponentInChildren<SettingsMenuPresenter>();
                if (prefabPresenter is not null)
                {
                    prefabPresenter.EnsureView();
                    prefabPresenter.ResolveSceneReferences();
                    return prefabPresenter;
                }
            }

            GameObject host = new GameObject("SettingsMenu");
            SettingsMenuPresenter presenter = host.AddComponent<SettingsMenuPresenter>();
            presenter.EnsureView();
            presenter.ResolveSceneReferences();
            return presenter;
        }

        public void Toggle()
        {
            if (IsTerminalScreenVisible())
            {
                SetOpen(false);
                return;
            }

            SetOpen(!isOpen);
        }

        public void SetOpen(bool open)
        {
            if (open && IsTerminalScreenVisible())
            {
                open = false;
            }

            EnsureView();
            isOpen = open;
            view?.SetOpen(open);

            if (open)
            {
                RefreshValues();
            }
        }

        public void RequestRestart()
        {
            if (IsTerminalScreenVisible())
            {
                return;
            }

            ResolveSceneReferences();
            SetOpen(false);
            session?.Retry();
            RefreshValues();
        }

        public void RequestShowTutorial()
        {
            if (IsTerminalScreenVisible())
            {
                return;
            }

            ResolveSceneReferences();
            SetOpen(false);
            session?.ShowTutorialImage();
        }

        public void RequestResume()
        {
            if (IsTerminalScreenVisible())
            {
                return;
            }

            SetOpen(false);
        }

        public void SelectLevel(string levelChoice)
        {
            if (IsTerminalScreenVisible())
            {
                return;
            }

            ResolveSceneReferences();
            if (session is null || string.IsNullOrWhiteSpace(levelChoice))
            {
                return;
            }

            string levelId = ParseLevelId(levelChoice);
            session.LoadLevel(levelId, session.Seed);
            SetOpen(false);
            RefreshValues();
        }

        public void SetMusicMuted(bool muted)
        {
            if (IsTerminalScreenVisible())
            {
                return;
            }

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
            if (IsTerminalScreenVisible())
            {
                return;
            }

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

        public void SetHapticsEnabled(bool enabled)
        {
            if (IsTerminalScreenVisible())
            {
                RefreshAudioControls();
                return;
            }

            ResolveAudioSettings()?.SetHapticsEnabled(enabled);
            RefreshAudioControls();
        }

        private void Awake()
        {
            EnsureView();
            ResolveSceneReferences();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            UnwireView();
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

        private void EnsureView()
        {
            if (view is null)
            {
                view = GetComponentInChildren<SettingsMenuView>(includeInactive: true);
            }

            if (view is null)
            {
                view = SettingsMenuView.CreateRuntime(this);
            }

            view.EnsureBuilt();
            WireView();
        }

        private void WireView()
        {
            if (view is null || isWired)
            {
                return;
            }

            view.RestartButton.onClick.AddListener(RequestRestart);
            view.SettingsButton.onClick.AddListener(Toggle);
            view.ResumeButton.onClick.AddListener(RequestResume);
            view.ShowTutorialButton.onClick.AddListener(RequestShowTutorial);
            view.LevelDropdown.onValueChanged.AddListener(HandleLevelDropdownChanged);
            view.MusicSlider.onValueChanged.AddListener(HandleMusicSliderChanged);
            view.FxSlider.onValueChanged.AddListener(HandleFxSliderChanged);
            view.HapticsStrengthSlider.onValueChanged.AddListener(HandleHapticsStrengthSliderChanged);
            view.MuteMusicToggle.onValueChanged.AddListener(SetMusicMuted);
            view.MuteFxToggle.onValueChanged.AddListener(SetFxMuted);
            view.HapticsToggle.onValueChanged.AddListener(SetHapticsEnabled);
            isWired = true;
        }

        private void UnwireView()
        {
            if (view is null || !isWired)
            {
                return;
            }

            view.RestartButton.onClick.RemoveListener(RequestRestart);
            view.SettingsButton.onClick.RemoveListener(Toggle);
            view.ResumeButton.onClick.RemoveListener(RequestResume);
            view.ShowTutorialButton.onClick.RemoveListener(RequestShowTutorial);
            view.LevelDropdown.onValueChanged.RemoveListener(HandleLevelDropdownChanged);
            view.MusicSlider.onValueChanged.RemoveListener(HandleMusicSliderChanged);
            view.FxSlider.onValueChanged.RemoveListener(HandleFxSliderChanged);
            view.HapticsStrengthSlider.onValueChanged.RemoveListener(HandleHapticsStrengthSliderChanged);
            view.MuteMusicToggle.onValueChanged.RemoveListener(SetMusicMuted);
            view.MuteFxToggle.onValueChanged.RemoveListener(SetFxMuted);
            view.HapticsToggle.onValueChanged.RemoveListener(SetHapticsEnabled);
            isWired = false;
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

            HapticEventRouter? hapticRouter = FindAnyObjectByType<HapticEventRouter>();
            if (hapticRouter is not null)
            {
                hapticRouter.SettingsController = audioSettings;
            }
        }

        private void HandleLevelDropdownChanged(int index)
        {
            if (view is null)
            {
                return;
            }

            SelectLevel(view.GetSelectedLevelChoice());
        }

        private void HandleMusicSliderChanged(float value)
        {
            if (IsTerminalScreenVisible())
            {
                RefreshAudioControls();
                return;
            }

            RememberNonZeroMusic(value);
            ResolveAudioSettings()?.SetMusicVolume(value);
            RefreshAudioControls();
        }

        private void HandleFxSliderChanged(float value)
        {
            if (IsTerminalScreenVisible())
            {
                RefreshAudioControls();
                return;
            }

            RememberNonZeroFx(value);
            ResolveAudioSettings()?.SetFxVolume(value);
            RefreshAudioControls();
        }

        private void HandleHapticsStrengthSliderChanged(float value)
        {
            if (IsTerminalScreenVisible())
            {
                RefreshAudioControls();
                return;
            }

            ResolveAudioSettings()?.SetHapticsStrength(value);
            RefreshAudioControls();
        }

        private void RefreshAudioControls()
        {
            EnsureView();
            AudioSettingsController? settings = ResolveAudioSettings();
            if (settings is null || view is null)
            {
                return;
            }

            RememberNonZeroMusic(settings.MusicVolume);
            RememberNonZeroFx(settings.FxVolume);

            view.SetMusicValue(settings.MusicVolume, FormatPercent(settings.MusicVolume));
            view.SetFxValue(settings.FxVolume, FormatPercent(settings.FxVolume));
            view.SetHapticsStrengthValue(settings.HapticsStrength, FormatPercent(settings.HapticsStrength));
            view.SetToggleValues(settings.MusicVolume <= 0f, settings.FxVolume <= 0f, settings.HapticsEnabled);
        }

        private void RefreshValues()
        {
            EnsureView();
            ResolveSceneReferences();
            EnsureLevelChoices();

            if (view is not null && session is not null)
            {
                view.SetLevelChoices(levelChoices, ToLevelChoice(session.CurrentLevelId));
            }

            RefreshAudioControls();
        }

        private void EnsureLevelChoices()
        {
            if (levelChoices.Count > 0)
            {
                return;
            }

            levelChoices.AddRange(BuildLevelChoices());
        }

        private AudioSettingsController? ResolveAudioSettings()
        {
            if (audioSettings is null)
            {
                audioSettings = AudioSettingsController.EnsureInstance();
            }

            return audioSettings;
        }

        private static bool IsTerminalScreenVisible()
        {
            VictoryScreenPresenter? victoryScreen = FindAnyObjectByType<VictoryScreenPresenter>();
            if (victoryScreen is not null && victoryScreen.IsVisible)
            {
                return true;
            }

            LossScreenPresenter? lossScreen = FindAnyObjectByType<LossScreenPresenter>();
            return lossScreen is not null && lossScreen.IsVisible;
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

        private static string FormatPercent(float value)
        {
            return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
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
