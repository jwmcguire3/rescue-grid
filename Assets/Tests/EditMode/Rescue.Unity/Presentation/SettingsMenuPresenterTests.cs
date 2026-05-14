using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rescue.Unity.Audio;
using Rescue.Unity.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIDocument = UnityEngine.UIElements.UIDocument;
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
        public void SettingsMenuPresenter_BuildsRescueRowSettingsControls()
        {
            SettingsMenuPresenter presenter = CreatePresenter(out _);

            presenter.SetOpen(true);

            SettingsMenuView view = presenter.View;
            Assert.That(view.RestartButton, Is.Not.Null);
            Assert.That(view.SettingsButton, Is.Not.Null);
            Assert.That(view.ResumeButton, Is.Not.Null);
            Assert.That(view.ShowTutorialButton, Is.Not.Null);
            Assert.That(view.LevelDropdown, Is.Not.Null);
            Assert.That(view.MusicSlider, Is.Not.Null);
            Assert.That(view.FxSlider, Is.Not.Null);
            Assert.That(view.HapticsStrengthSlider, Is.Not.Null);
            Assert.That(view.MuteMusicToggle, Is.Not.Null);
            Assert.That(view.MuteFxToggle, Is.Not.Null);
            Assert.That(view.HapticsToggle, Is.Not.Null);
            Assert.That(view.HapticsToggle.isOn, Is.True);
            Assert.That(view.PanelRoot.activeSelf, Is.True);

            Assert.That(view.ReadableLabels, Is.Not.Empty);
            Assert.That(view.ReadableLabels, Has.All.TypeOf<TextMeshProUGUI>());
            Assert.That(view.ReadableLabels, Has.Some.Matches<TextMeshProUGUI>(label => label.text == "Vibrations"));
            Assert.That(view.ReadableLabels, Has.Some.Matches<TextMeshProUGUI>(label => label.text == "Strength"));

            Image restartImage = view.RestartButton.GetComponent<Image>();
            Image settingsImage = view.SettingsButton.GetComponent<Image>();
            Image panelImage = view.PanelRoot.GetComponent<Image>();
            Assert.That(restartImage, Is.Not.Null);
            Assert.That(settingsImage, Is.Not.Null);
            Assert.That(panelImage, Is.Not.Null);
            Assert.That(restartImage.sprite, Is.Not.Null, "Restart should use a Rescue Row plaque sprite.");
            Assert.That(settingsImage.sprite, Is.Not.Null, "Settings should use a Rescue Row plaque sprite.");
            Assert.That(panelImage.sprite, Is.Not.Null, "Settings panel should use the worn panel sprite.");
            Assert.That(restartImage.type, Is.EqualTo(Image.Type.Simple), "Small top plaques should render the full painted sprite instead of collapsed 9-slice borders.");
            Assert.That(settingsImage.type, Is.EqualTo(Image.Type.Simple), "Small top plaques should render the full painted sprite instead of collapsed 9-slice borders.");
            Assert.That(panelImage.type, Is.EqualTo(Image.Type.Sliced));
            LayoutElement restartLayout = view.RestartButton.GetComponent<LayoutElement>();
            LayoutElement settingsLayout = view.SettingsButton.GetComponent<LayoutElement>();
            Assert.That(restartLayout.preferredWidth, Is.EqualTo(208f));
            Assert.That(restartLayout.preferredHeight, Is.EqualTo(60f));
            Assert.That(settingsLayout.preferredHeight, Is.EqualTo(80f));
            Assert.That(settingsLayout.preferredHeight * 651f / 1024f, Is.EqualTo(restartLayout.preferredHeight * 872f / 1024f).Within(0.5f));
            RectTransform topRowRect = FindRectTransform(view, "SettingsTopButtonRow");
            Assert.That(topRowRect.sizeDelta.x, Is.EqualTo(420f));
            Assert.That(topRowRect.sizeDelta.y, Is.EqualTo(92f));

            AssertContainedInPanel(view, "SettingsTitle");
            AssertContainedInPanel(view, "ResumeButton");
            AssertContainedInPanel(view, "ShowTutorialButton");
            AssertContainedInPanel(view, "LevelDropdownRow");
            AssertContainedInPanel(view, "MusicSliderRow");
            AssertContainedInPanel(view, "FXSliderRow");
            AssertContainedInPanel(view, "MuteRow");
            AssertContainedInPanel(view, "VibrationsToggle");
            AssertContainedInPanel(view, "HapticsStrengthRow");
            AssertBottomPaddingInPanel(view, "HapticsStrengthRow", 80f);
            AssertHorizontalPaddingInPanel(view, "SettingsTitle", 56f, 160f);
            AssertHorizontalPaddingInPanel(view, "ResumeButton", 280f, 56f);
            AssertHorizontalPaddingInPanel(view, "ShowTutorialButton", 56f, 56f);
            AssertHorizontalPaddingInPanel(view, "LevelDropdownRow", 56f, 56f);
            AssertHorizontalPaddingInPanel(view, "MusicSliderRow", 56f, 56f);
            AssertHorizontalPaddingInPanel(view, "FXSliderRow", 56f, 56f);
            AssertHorizontalPaddingInPanel(view, "MuteRow", 56f, 56f);
            AssertHorizontalPaddingInPanel(view, "HapticsStrengthRow", 56f, 56f);
            AssertPreferredHeightAtLeast(view, "LevelDropdownRow", 52f);
            AssertPreferredHeightAtLeast(view, "MusicSliderRow", 46f);
            AssertPreferredHeightAtLeast(view, "FXSliderRow", 46f);
            AssertPreferredHeightAtLeast(view, "MuteRow", 48f);
            AssertPreferredHeightAtLeast(view, "HapticsStrengthRow", 46f);

            AssertRusticSlider(view.MusicSlider);
            AssertRusticSlider(view.FxSlider);
            AssertRusticSlider(view.HapticsStrengthSlider);
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

            SettingsMenuView view = presenter.View;
            Assert.That(view.MusicSlider.value, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(view.FxSlider.value, Is.EqualTo(0.62f).Within(0.001f));
            Assert.That(view.HapticsStrengthSlider.value, Is.EqualTo(0.48f).Within(0.001f));
            Assert.That(FindLabel(view, "MusicValue").text, Is.EqualTo("37%"));
            Assert.That(FindLabel(view, "FXValue").text, Is.EqualTo("62%"));
            Assert.That(FindLabel(view, "HapticsStrengthValue").text, Is.EqualTo("48%"));
            Assert.That(view.MuteMusicToggle.isOn, Is.False);
            Assert.That(view.MuteFxToggle.isOn, Is.False);
            Assert.That(view.HapticsToggle.isOn, Is.True);
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
            SettingsMenuView view = presenter.View;
            Slider hapticsStrengthSlider = view.HapticsStrengthSlider;
            CanvasGroup hapticsStrengthRow = hapticsStrengthSlider.GetComponentInParent<CanvasGroup>()!;

            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0.62f).Within(0.001f));
            Assert.That(audioSettings.HapticsEnabled, Is.False);
            Assert.That(PlayerPrefs.GetInt(AudioSettingsController.HapticsEnabledPrefsKey), Is.EqualTo(0));
            Assert.That(audioSettings.HapticsStrength, Is.EqualTo(0.48f).Within(0.001f));
            Assert.That(view.HapticsToggle.isOn, Is.False);
            Assert.That(hapticsStrengthSlider.interactable, Is.False);
            Assert.That(hapticsStrengthRow.alpha, Is.EqualTo(0.45f).Within(0.001f));

            presenter.SetHapticsEnabled(true);
            Assert.That(audioSettings.HapticsEnabled, Is.True);
            Assert.That(PlayerPrefs.GetInt(AudioSettingsController.HapticsEnabledPrefsKey), Is.EqualTo(1));
            Assert.That(view.HapticsToggle.isOn, Is.True);
            Assert.That(hapticsStrengthSlider.interactable, Is.True);
            Assert.That(hapticsStrengthRow.alpha, Is.EqualTo(1.0f).Within(0.001f));
        }

        [Test]
        public void SettingsMenuPresenter_MuteTogglesKeepMusicAndFxSeparateAndRestore()
        {
            SettingsMenuPresenter presenter = CreatePresenter(out AudioSettingsController audioSettings);
            audioSettings.SetMusicVolume(0.37f);
            audioSettings.SetFxVolume(0.62f);

            presenter.SetOpen(true);
            presenter.SetMusicMuted(true);

            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0.62f).Within(0.001f));
            SettingsMenuView view = presenter.View;
            Assert.That(view.MuteMusicToggle.isOn, Is.True);
            Assert.That(view.MuteFxToggle.isOn, Is.False);
            Assert.That(FindLabel(view, "MusicValue").text, Is.EqualTo("0%"));

            presenter.SetMusicMuted(false);
            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0.62f).Within(0.001f));

            presenter.SetFxMuted(true);
            Assert.That(audioSettings.MusicVolume, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(audioSettings.FxVolume, Is.EqualTo(0f).Within(0.001f));
            Assert.That(view.MuteMusicToggle.isOn, Is.False);
            Assert.That(view.MuteFxToggle.isOn, Is.True);

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
            return presenterObject.AddComponent<SettingsMenuPresenter>();
        }

        private PlayableLevelSession CreateSession()
        {
            sessionObject = new GameObject("PlayableLevelSessionTests");
            return sessionObject.AddComponent<PlayableLevelSession>();
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

        private static TextMeshProUGUI FindLabel(SettingsMenuView view, string name)
        {
            TextMeshProUGUI? label = Array.Find(
                view.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true),
                candidate => candidate.name == name);
            Assert.That(label, Is.Not.Null, $"Expected TMP label '{name}'.");
            return label!;
        }

        private static RectTransform FindRectTransform(SettingsMenuView view, string name)
        {
            RectTransform? rect = Array.Find(
                view.GetComponentsInChildren<RectTransform>(includeInactive: true),
                candidate => candidate.name == name);
            Assert.That(rect, Is.Not.Null, $"Expected rect transform '{name}'.");
            return rect!;
        }

        private static void AssertContainedInPanel(SettingsMenuView view, string childName)
        {
            RectTransform panel = view.PanelRoot.GetComponent<RectTransform>();
            RectTransform? child = Array.Find(
                view.PanelRoot.GetComponentsInChildren<RectTransform>(includeInactive: true),
                candidate => candidate.name == childName);
            Assert.That(child, Is.Not.Null, $"Expected panel child '{childName}'.");

            Vector3[] childWorldCorners = new Vector3[4];
            Vector3[] panelWorldCorners = new Vector3[4];
            child!.GetWorldCorners(childWorldCorners);
            panel.GetWorldCorners(panelWorldCorners);

            const float tolerance = 0.5f;
            Assert.That(childWorldCorners[0].x, Is.GreaterThanOrEqualTo(panelWorldCorners[0].x - tolerance), $"{childName} overflowed panel left.");
            Assert.That(childWorldCorners[0].y, Is.GreaterThanOrEqualTo(panelWorldCorners[0].y - tolerance), $"{childName} overflowed panel bottom.");
            Assert.That(childWorldCorners[2].x, Is.LessThanOrEqualTo(panelWorldCorners[2].x + tolerance), $"{childName} overflowed panel right.");
            Assert.That(childWorldCorners[2].y, Is.LessThanOrEqualTo(panelWorldCorners[2].y + tolerance), $"{childName} overflowed panel top.");
        }

        private static void AssertBottomPaddingInPanel(SettingsMenuView view, string childName, float minimumPadding)
        {
            RectTransform panel = view.PanelRoot.GetComponent<RectTransform>();
            RectTransform child = FindRectTransform(view, childName);

            Vector3[] childWorldCorners = new Vector3[4];
            Vector3[] panelWorldCorners = new Vector3[4];
            child.GetWorldCorners(childWorldCorners);
            panel.GetWorldCorners(panelWorldCorners);

            float bottomPadding = childWorldCorners[0].y - panelWorldCorners[0].y;
            Assert.That(bottomPadding, Is.GreaterThanOrEqualTo(minimumPadding), $"{childName} should sit visibly above the panel bottom.");
        }

        private static void AssertHorizontalPaddingInPanel(SettingsMenuView view, string childName, float minimumLeftPadding, float minimumRightPadding)
        {
            RectTransform panel = view.PanelRoot.GetComponent<RectTransform>();
            RectTransform child = FindRectTransform(view, childName);

            Vector3[] childWorldCorners = new Vector3[4];
            Vector3[] panelWorldCorners = new Vector3[4];
            child.GetWorldCorners(childWorldCorners);
            panel.GetWorldCorners(panelWorldCorners);

            float leftPadding = childWorldCorners[0].x - panelWorldCorners[0].x;
            float rightPadding = panelWorldCorners[2].x - childWorldCorners[2].x;
            Assert.That(leftPadding, Is.GreaterThanOrEqualTo(minimumLeftPadding), $"{childName} should sit visibly inside the panel left edge.");
            Assert.That(rightPadding, Is.GreaterThanOrEqualTo(minimumRightPadding), $"{childName} should sit visibly inside the panel right edge.");
        }

        private static void AssertPreferredHeightAtLeast(SettingsMenuView view, string childName, float minimumHeight)
        {
            RectTransform rect = FindRectTransform(view, childName);
            LayoutElement? layout = rect.GetComponent<LayoutElement>();
            Assert.That(layout, Is.Not.Null, $"{childName} should have an explicit layout height.");
            Assert.That(layout!.preferredHeight, Is.GreaterThanOrEqualTo(minimumHeight), $"{childName} should remain large enough for mobile input.");
        }

        private static void AssertRusticSlider(Slider slider)
        {
            Image? background = Array.Find(
                slider.GetComponentsInChildren<Image>(includeInactive: true),
                candidate => candidate.name == "Background");
            Image? fill = Array.Find(
                slider.GetComponentsInChildren<Image>(includeInactive: true),
                candidate => candidate.name == "Fill");
            Image? handle = Array.Find(
                slider.GetComponentsInChildren<Image>(includeInactive: true),
                candidate => candidate.name == "Handle");

            Assert.That(background, Is.Not.Null);
            Assert.That(fill, Is.Not.Null);
            Assert.That(handle, Is.Not.Null);
            Assert.That(background!.sprite, Is.Not.Null, "Slider background should use the rustic bar sprite.");
            Assert.That(fill!.sprite, Is.Not.Null, "Slider fill should use the rustic bar sprite.");
            Assert.That(handle!.sprite, Is.Not.Null, "Slider handle should use the paw handle sprite.");
            Assert.That(background!.type, Is.EqualTo(Image.Type.Simple), "Thin slider bars should render the full painted sprite instead of collapsed 9-slice borders.");
            Assert.That(fill!.type, Is.EqualTo(Image.Type.Simple), "Thin slider bars should render the full painted sprite instead of collapsed 9-slice borders.");
            Assert.That(background.color.a, Is.EqualTo(0f).Within(0.001f), "Slider background should be an invisible full-width hit target.");
            Assert.That(fill.enabled, Is.True, "Slider fill should draw the only visible bar so it ends at the paw handle.");
            Assert.That(background.raycastTarget, Is.True, "Slider track should receive pointer hits for click-to-set volume.");
            Assert.That(fill.raycastTarget, Is.False, "Slider fill should not block track pointer hits.");
            Assert.That(handle.raycastTarget, Is.True, "Paw handle should receive pointer hits for dragging volume.");
            Assert.That(background.rectTransform.sizeDelta.y, Is.GreaterThanOrEqualTo(40f), "Slider bar should be visibly tall enough at mobile scale.");
            Assert.That(fill.rectTransform.sizeDelta.y, Is.GreaterThanOrEqualTo(40f), "Slider fill should remain the visible bar behind the handle.");
            Assert.That(slider.fillRect, Is.SameAs(fill.rectTransform), "Slider fillRect should drive the rustic fill image.");
            Assert.That(slider.handleRect, Is.SameAs(handle.rectTransform), "Slider handleRect should drive the paw handle image.");
            Assert.That(handle.rectTransform.sizeDelta.x, Is.GreaterThanOrEqualTo(38f), "Paw handle should remain large enough to visibly cap the bar.");
            Assert.That(handle.rectTransform.anchorMin.x, Is.EqualTo(1f).Within(0.001f), "A full-value slider should initialize the paw at the bar end.");
            Assert.That(handle.rectTransform.anchorMax.x, Is.EqualTo(1f).Within(0.001f), "A full-value slider should initialize the paw at the bar end.");

            LayoutElement? layout = slider.GetComponent<LayoutElement>();
            Assert.That(layout, Is.Not.Null, "Slider layout should prevent row-driven height collapse.");
            Assert.That(layout!.minHeight, Is.GreaterThanOrEqualTo(44f), "Slider minHeight should keep the rustic bar visible.");
            Assert.That(layout.preferredHeight, Is.GreaterThanOrEqualTo(44f), "Slider preferredHeight should keep the rustic bar visible.");
        }

        private sealed class PacketManifestSubset
        {
            public string[] expectedLevelIds { get; set; } = Array.Empty<string>();
        }
    }
}
