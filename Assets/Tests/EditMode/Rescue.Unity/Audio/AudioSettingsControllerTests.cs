using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Rescue.Unity.Audio.Tests
{
    public sealed class AudioSettingsControllerTests
    {
        private GameObject? controllerObject;

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
            if (controllerObject is not null)
            {
                UnityObject.DestroyImmediate(controllerObject);
                controllerObject = null;
            }

            PlayerPrefs.DeleteKey(AudioSettingsController.MusicVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.FxVolumePrefsKey);
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsEnabledPrefsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void AudioSettingsController_DefaultsToFullVolume()
        {
            AudioSettingsController controller = CreateController();

            controller.Load();

            Assert.That(controller.MusicVolume, Is.EqualTo(1.0f));
            Assert.That(controller.FxVolume, Is.EqualTo(1.0f));
            Assert.That(controller.HapticsEnabled, Is.True);
        }

        [Test]
        public void AudioSettingsController_ClampsAndPersistsValues()
        {
            AudioSettingsController controller = CreateController();

            controller.SetMusicVolume(1.5f);
            controller.SetFxVolume(-0.25f);
            controller.SetHapticsEnabled(false);

            Assert.That(controller.MusicVolume, Is.EqualTo(1.0f));
            Assert.That(controller.FxVolume, Is.EqualTo(0.0f));
            Assert.That(controller.HapticsEnabled, Is.False);
            Assert.That(PlayerPrefs.GetFloat(AudioSettingsController.MusicVolumePrefsKey), Is.EqualTo(1.0f));
            Assert.That(PlayerPrefs.GetFloat(AudioSettingsController.FxVolumePrefsKey), Is.EqualTo(0.0f));
            Assert.That(PlayerPrefs.GetInt(AudioSettingsController.HapticsEnabledPrefsKey), Is.EqualTo(0));
        }

        [Test]
        public void AudioSettingsController_LoadReadsPersistedValues()
        {
            PlayerPrefs.SetFloat(AudioSettingsController.MusicVolumePrefsKey, 0.35f);
            PlayerPrefs.SetFloat(AudioSettingsController.FxVolumePrefsKey, 0.65f);
            PlayerPrefs.SetInt(AudioSettingsController.HapticsEnabledPrefsKey, 0);
            PlayerPrefs.Save();
            AudioSettingsController controller = CreateController();

            controller.Load();

            Assert.That(controller.MusicVolume, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(controller.FxVolume, Is.EqualTo(0.65f).Within(0.001f));
            Assert.That(controller.HapticsEnabled, Is.False);
        }

        private AudioSettingsController CreateController()
        {
            controllerObject = new GameObject("AudioSettingsControllerTests");
            return controllerObject.AddComponent<AudioSettingsController>();
        }
    }
}
