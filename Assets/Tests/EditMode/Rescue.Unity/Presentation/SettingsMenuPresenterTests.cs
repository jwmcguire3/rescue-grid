using System;
using System.IO;
using System.Reflection;
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
        private GameObject? sessionObject;
        private GameObject? terminalObject;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(AudioSettingsController.MusicVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.FxVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsEnabledPrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsStrengthPrefsKey);
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

            if (sessionObject is not null)
            {
                UnityObject.DestroyImmediate(sessionObject);
                sessionObject = null;
            }

            DestroyAny<PlayableLevelSession>();
            DestroyAny<VictoryScreenPresenter>();
            DestroyAny<LossScreenPresenter>();
            DestroyAny<L00IntroImagePresenter>();

            PlayerPrefs.DeleteKey(AudioSettingsController.MusicVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.FxVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsEnabledPrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsStrengthPrefsKey);
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
            Slider? hapticsStrengthSlider = root.Q<Slider>("settings-haptics-strength-slider");
            Toggle? hapticsToggle = root.Q<Toggle>("settings-haptics-toggle");
            Label? hapticsStrengthLabel = root.Q<Label>("settings-haptics-strength-row-label");

            Assert.That(root.Q<Button>("settings-toggle-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("settings-resume-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("restart-level-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("settings-restart-button"), Is.Null);
            Assert.That(root.Q<Button>("settings-show-tutorial-button"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("settings-level-dropdown"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("settings-mute-music-toggle"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("settings-mute-fx-toggle"), Is.Not.Null);
            Assert.That(hapticsToggle, Is.Not.Null);
            Assert.That(hapticsToggle!.label, Is.EqualTo("Vibrations"));
            Assert.That(hapticsToggle.value, Is.True);
            Assert.That(hapticsStrengthSlider, Is.Not.Null);
            Assert.That(hapticsStrengthLabel, Is.Not.Null);
            Assert.That(hapticsStrengthLabel!.text, Is.EqualTo("Strength"));
            Assert.That(root.Q<Label>("settings-music-slider-value-label"), Is.Not.Null);
            Assert.That(root.Q<Label>("settings-fx-slider-value-label"), Is.Not.Null);
            Assert.That(root.Q<Label>("settings-haptics-strength-slider-value-label"), Is.Not.Null);

            Assert.That(panel, Is.Not.Null);
            Assert.That(panel!.style.width.value.value, Is.EqualTo(SettingsMenuPresenter.PanelWidth).Within(0.001f));
            Assert.That(musicSlider, Is.Not.Null);
            Assert.That(fxSlider, Is.Not.Null);
            Assert.That(musicSlider!.showInputField, Is.False);
            Assert.That(fxSlider!.showInputField, Is.False);
            Assert.That(hapticsStrengthSlider!.showInputField, Is.False);
            Assert.That(musicSlider.style.minWidth.value.value, Is.EqualTo(SettingsMenuPresenter.SliderTrackMinWidth).Within(0.001f));
            Assert.That(fxSlider.style.minWidth.value.value, Is.EqualTo(SettingsMenuPresenter.SliderTrackMinWidth).Within(0.001f));
            Assert.That(hapticsStrengthSlider.style.minWidth.value.value, Is.EqualTo(SettingsMenuPresenter.SliderTrackMinWidth).Within(0.001f));
        }

        [Test]
        public void SettingsMenuPresenter_LevelChoicesExposeFullPhase1Packet()
        {
            SettingsMenuPresenter presenter = CreatePresenter(out _);

            presenter.SetOpen(true);

            string[] expectedLevelIds = LoadExpectedLevelIdsFromManifest();

            CollectionAssert.AreEqual(expectedLevelIds, PlayableLevelSession.LevelIds);
            Assert.That(presenter.LevelChoices, Has.Count.EqualTo(expectedLevelIds.Length));
            for (int i = 0; i < expectedLevelIds.Length; i++)
            {
                Assert.That(presenter.LevelChoices[i], Does.StartWith(expectedLevelIds[i]));
            }
        }

        [Test]
        public void SettingsMenuPresenter_OpeningRefreshesAudioValues()
        {
            SettingsMenuPresenter presenter = CreatePresenter(out AudioSettingsController audioSettings);
            audioSettings.SetMusicVolume(0.37f);
            audioSettings.SetFxVolume(0.62f);
            audioSettings.SetHapticsStrength(0.48f);

            presenter.SetOpen(true);

            VisualElement root = RootElement();
            Assert.That(root.Q<Slider>("settings-music-slider")!.value, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(root.Q<Slider>("settings-fx-slider")!.value, Is.EqualTo(0.62f).Within(0.001f));
            Assert.That(root.Q<Slider>("settings-haptics-strength-slider")!.value, Is.EqualTo(0.48f).Within(0.001f));
            Assert.That(root.Q<Label>("settings-music-slider-value-label")!.text, Is.EqualTo("37%"));
            Assert.That(root.Q<Label>("settings-fx-slider-value-label")!.text, Is.EqualTo("62%"));
            Assert.That(root.Q<Label>("settings-haptics-strength-slider-value-label")!.text, Is.EqualTo("48%"));
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
            audioSettings.SetHapticsStrength(0.48f);

            presenter.SetOpen(true);
            presenter.SetHapticsEnabled(false);
            VisualElement root = RootElement();
            Slider hapticsStrengthSlider = root.Q<Slider>("settings-haptics-strength-slider")!;
            VisualElement hapticsStrengthRow = root.Q<VisualElement>("settings-haptics-strength-row")!;

            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0.62f).Within(0.001f));
            Assert.That(audioSettings.HapticsEnabled, Is.False);
            Assert.That(PlayerPrefs.GetInt(AudioSettingsController.HapticsEnabledPrefsKey), Is.EqualTo(0));
            Assert.That(audioSettings.HapticsStrength, Is.EqualTo(0.48f).Within(0.001f));
            Assert.That(root.Q<Toggle>("settings-haptics-toggle")!.value, Is.False);
            Assert.That(root.Q<Toggle>("settings-haptics-toggle")!.label, Is.EqualTo("Vibrations"));
            Assert.That(hapticsStrengthSlider.enabledSelf, Is.False);
            Assert.That(hapticsStrengthRow.style.opacity.value, Is.EqualTo(0.45f).Within(0.001f));

            presenter.SetHapticsEnabled(true);
            Assert.That(audioSettings.HapticsEnabled, Is.True);
            Assert.That(PlayerPrefs.GetInt(AudioSettingsController.HapticsEnabledPrefsKey), Is.EqualTo(1));
            Assert.That(root.Q<Toggle>("settings-haptics-toggle")!.value, Is.True);
            Assert.That(hapticsStrengthSlider.enabledSelf, Is.True);
            Assert.That(hapticsStrengthRow.style.opacity.value, Is.EqualTo(1.0f).Within(0.001f));
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

        [Test]
        public void SettingsMenuPresenter_RestartClosesSettingsMenu()
        {
            SettingsMenuPresenter presenter = CreatePresenter(out _);

            presenter.SetOpen(true);
            presenter.RequestRestart();

            Assert.That(presenter.IsOpen, Is.False);
        }

        [Test]
        public void SettingsMenuPresenter_LevelSelectionClosesSettingsMenu()
        {
            SettingsMenuPresenter presenter = CreatePresenter(out _);
            PlayableLevelSession session = CreateSession();
            SetPrivateField(presenter, "session", session);

            presenter.SetOpen(true);
            presenter.SelectLevel("L01 - First Steps");

            Assert.That(presenter.IsOpen, Is.False);
            Assert.That(session.CurrentLevelId, Is.EqualTo("L01"));
        }

        private SettingsMenuPresenter CreatePresenter(out AudioSettingsController audioSettings)
        {
            audioObject = new GameObject("AudioSettingsControllerTests");
            audioSettings = audioObject.AddComponent<AudioSettingsController>();

            presenterObject = new GameObject("SettingsMenuPresenterTests");
            presenterObject.AddComponent<UIDocument>();
            return presenterObject.AddComponent<SettingsMenuPresenter>();
        }

        private PlayableLevelSession CreateSession()
        {
            sessionObject = new GameObject("PlayableLevelSessionTests");
            return sessionObject.AddComponent<PlayableLevelSession>();
        }

        private VisualElement RootElement()
        {
            Assert.That(presenterObject, Is.Not.Null);
            UIDocument document = presenterObject!.GetComponent<UIDocument>();
            return document.rootVisualElement;
        }

        private static void SetPrivateField(object target, string fieldName, object? value)
        {
            System.Reflection.FieldInfo? field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}'.");
            field!.SetValue(target, value);
        }

        private static string[] LoadExpectedLevelIdsFromManifest()
        {
            Assembly jsonAssembly = Assembly.Load(new AssemblyName("System.Text.Json"));
            Type serializerType = jsonAssembly.GetType("System.Text.Json.JsonSerializer", throwOnError: true)
                ?? throw new InvalidOperationException("JsonSerializer type was not found.");
            Type optionsType = jsonAssembly.GetType("System.Text.Json.JsonSerializerOptions", throwOnError: true)
                ?? throw new InvalidOperationException("JsonSerializerOptions type was not found.");
            MethodInfo deserializeMethod = serializerType.GetMethod(
                "Deserialize",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(Type), optionsType },
                modifiers: null)
                ?? throw new MissingMethodException("JsonSerializer.Deserialize(string, Type, JsonSerializerOptions) was not found.");

            object? value = deserializeMethod.Invoke(null, new object?[] { File.ReadAllText(GetRepoManifestPath()), typeof(PacketManifestSubset), null });
            PacketManifestSubset manifest = value as PacketManifestSubset
                ?? throw new InvalidOperationException("Could not deserialize packet manifest.");
            return manifest.expectedLevelIds;
        }

        private static string GetRepoManifestPath()
        {
            return Path.Combine(GetProjectRoot(), "docs", "level-packets", "phase1.packet.json");
        }

        private static string GetProjectRoot()
        {
            string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? throw new IOException("Could not resolve test assembly directory.");
            return Path.GetFullPath(Path.Combine(root, "..", ".."));
        }

        private static void DestroyAny<T>()
            where T : UnityObject
        {
            T[] objects = UnityObject.FindObjectsByType<T>(FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] is not null)
                {
                    UnityObject.DestroyImmediate(objects[i]);
                }
            }
        }

        private sealed class PacketManifestSubset
        {
            public string[] expectedLevelIds { get; set; } = Array.Empty<string>();
        }
    }
}
