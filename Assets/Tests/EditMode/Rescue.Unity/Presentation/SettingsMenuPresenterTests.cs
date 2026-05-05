using NUnit.Framework;
using Rescue.Unity.Audio;
using Rescue.Unity.Presentation;
using UnityEngine;
using UnityEngine.UIElements;
using UnityObject = UnityEngine.Object;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class SettingsMenuPresenterTests
    {
        private GameObject? presenterObject;
        private GameObject? audioObject;
        private GameObject? terminalObject;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(AudioSettingsController.MusicVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.FxVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsEnabledPrefsKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            if (presenterObject is not null)
            {
                UnityObject.DestroyImmediate(presenterObject);
                presenterObject = null;
            }

            if (audioObject is not null)
            {
                UnityObject.DestroyImmediate(audioObject);
                audioObject = null;
            }

            if (terminalObject is not null)
            {
                UnityObject.DestroyImmediate(terminalObject);
                terminalObject = null;
            }

            PlayerPrefs.DeleteKey(AudioSettingsController.MusicVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.FxVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsEnabledPrefsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void SettingsMenuPresenter_BuildsCleanWideSettingsControls()
        {
            SettingsMenuPresenter presenter = CreatePresenter(out _);

            presenter.SetOpen(true);

            VisualElement root = RootElement();
            VisualElement? panel = root.Q<VisualElement>("settings-panel");
            Slider? musicSlider = root.Q<Slider>("settings-music-slider");
            Slider? fxSlider = root.Q<Slider>("settings-fx-slider");

            Assert.That(root.Q<Button>("settings-toggle-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("settings-resume-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("settings-restart-button"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("settings-level-dropdown"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("settings-mute-music-toggle"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("settings-mute-fx-toggle"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("settings-haptics-toggle"), Is.Not.Null);
            Assert.That(root.Q<Label>("settings-music-slider-value-label"), Is.Not.Null);
            Assert.That(root.Q<Label>("settings-fx-slider-value-label"), Is.Not.Null);

            Assert.That(panel, Is.Not.Null);
            Assert.That(panel!.style.width.value.value, Is.EqualTo(SettingsMenuPresenter.PanelWidth).Within(0.001f));
            Assert.That(musicSlider, Is.Not.Null);
            Assert.That(fxSlider, Is.Not.Null);
            Assert.That(musicSlider!.showInputField, Is.False);
            Assert.That(fxSlider!.showInputField, Is.False);
            Assert.That(musicSlider.style.minWidth.value.value, Is.EqualTo(SettingsMenuPresenter.SliderTrackMinWidth).Within(0.001f));
            Assert.That(fxSlider.style.minWidth.value.value, Is.EqualTo(SettingsMenuPresenter.SliderTrackMinWidth).Within(0.001f));
        }

        [Test]
        public void SettingsMenuPresenter_OpeningRefreshesAudioValues()
        {
            SettingsMenuPresenter presenter = CreatePresenter(out AudioSettingsController audioSettings);
            audioSettings.SetMusicVolume(0.37f);
            audioSettings.SetFxVolume(0.62f);

            presenter.SetOpen(true);

            VisualElement root = RootElement();
            Assert.That(root.Q<Slider>("settings-music-slider")!.value, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(root.Q<Slider>("settings-fx-slider")!.value, Is.EqualTo(0.62f).Within(0.001f));
            Assert.That(root.Q<Label>("settings-music-slider-value-label")!.text, Is.EqualTo("37%"));
            Assert.That(root.Q<Label>("settings-fx-slider-value-label")!.text, Is.EqualTo("62%"));
            Assert.That(root.Q<Toggle>("settings-mute-music-toggle")!.value, Is.False);
            Assert.That(root.Q<Toggle>("settings-mute-fx-toggle")!.value, Is.False);
            Assert.That(root.Q<Toggle>("settings-haptics-toggle")!.value, Is.True);
        }

        [Test]
        public void SettingsMenuPresenter_HapticsTogglePersistsSeparatelyFromAudio()
        {
            SettingsMenuPresenter presenter = CreatePresenter(out AudioSettingsController audioSettings);
            audioSettings.SetMusicVolume(0.37f);
            audioSettings.SetFxVolume(0.62f);

            presenter.SetOpen(true);
            presenter.SetHapticsEnabled(false);

            VisualElement root = RootElement();
            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0.62f).Within(0.001f));
            Assert.That(audioSettings.HapticsEnabled, Is.False);
            Assert.That(root.Q<Toggle>("settings-haptics-toggle")!.value, Is.False);

            presenter.SetHapticsEnabled(true);
            Assert.That(audioSettings.HapticsEnabled, Is.True);
            Assert.That(root.Q<Toggle>("settings-haptics-toggle")!.value, Is.True);
        }

        [Test]
        public void SettingsMenuPresenter_MuteTogglesKeepMusicAndFxSeparateAndRestore()
        {
            SettingsMenuPresenter presenter = CreatePresenter(out AudioSettingsController audioSettings);
            audioSettings.SetMusicVolume(0.37f);
            audioSettings.SetFxVolume(0.62f);

            presenter.SetOpen(true);
            presenter.SetMusicMuted(true);

            VisualElement root = RootElement();
            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0.62f).Within(0.001f));
            Assert.That(root.Q<Toggle>("settings-mute-music-toggle")!.value, Is.True);
            Assert.That(root.Q<Toggle>("settings-mute-fx-toggle")!.value, Is.False);
            Assert.That(root.Q<Label>("settings-music-slider-value-label")!.text, Is.EqualTo("0%"));

            presenter.SetMusicMuted(false);
            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0.62f).Within(0.001f));

            presenter.SetFxMuted(true);
            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0f).Within(0.001f));
            Assert.That(root.Q<Toggle>("settings-mute-music-toggle")!.value, Is.False);
            Assert.That(root.Q<Toggle>("settings-mute-fx-toggle")!.value, Is.True);

            presenter.SetFxMuted(false);
            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0.62f).Within(0.001f));
        }

        [Test]
        public void SettingsMenuPresenter_IgnoresControlsWhileTerminalScreenIsVisible()
        {
            SettingsMenuPresenter presenter = CreatePresenter(out AudioSettingsController audioSettings);
            audioSettings.SetMusicVolume(0.37f);
            terminalObject = new GameObject("VisibleVictoryScreen");
            terminalObject.AddComponent<UIDocument>();
            terminalObject.AddComponent<VictoryScreenPresenter>().Show();

            presenter.SetOpen(true);
            presenter.SetMusicMuted(true);

            Assert.That(presenter.IsOpen, Is.False);
            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.37f).Within(0.001f));
        }

        private SettingsMenuPresenter CreatePresenter(out AudioSettingsController audioSettings)
        {
            audioObject = new GameObject("AudioSettingsControllerTests");
            audioSettings = audioObject.AddComponent<AudioSettingsController>();

            presenterObject = new GameObject("SettingsMenuPresenterTests");
            presenterObject.AddComponent<UIDocument>();
            return presenterObject.AddComponent<SettingsMenuPresenter>();
        }

        private VisualElement RootElement()
        {
            Assert.That(presenterObject, Is.Not.Null);
            UIDocument document = presenterObject!.GetComponent<UIDocument>();
            return document.rootVisualElement;
        }
    }
}
