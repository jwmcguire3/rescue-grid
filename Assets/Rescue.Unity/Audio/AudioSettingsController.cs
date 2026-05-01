using System;
using UnityEngine;

namespace Rescue.Unity.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioSettingsController : MonoBehaviour
    {
        public const string MusicVolumePrefsKey = "rescue.settings.musicVolume";
        public const string FxVolumePrefsKey = "rescue.settings.fxVolume";

        private const float DefaultVolume = 1.0f;

        [SerializeField] [Range(0f, 1f)] private float musicVolume = DefaultVolume;
        [SerializeField] [Range(0f, 1f)] private float fxVolume = DefaultVolume;

        public event Action? SettingsChanged;

        public float MusicVolume => Mathf.Clamp01(musicVolume);

        public float FxVolume => Mathf.Clamp01(fxVolume);

        public static AudioSettingsController EnsureInstance()
        {
            AudioSettingsController? existing = FindAnyObjectByType<AudioSettingsController>();
            if (existing is not null)
            {
                existing.Load();
                return existing;
            }

            GameObject host = new GameObject("AudioSettingsController");
            AudioSettingsController controller = host.AddComponent<AudioSettingsController>();
            controller.Load();
            return controller;
        }

        public void Load()
        {
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefsKey, DefaultVolume));
            fxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(FxVolumePrefsKey, DefaultVolume));
            SettingsChanged?.Invoke();
        }

        public void SetMusicVolume(float value)
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(musicVolume, clamped) && PlayerPrefs.HasKey(MusicVolumePrefsKey))
            {
                return;
            }

            musicVolume = clamped;
            PlayerPrefs.SetFloat(MusicVolumePrefsKey, musicVolume);
            PlayerPrefs.Save();
            SettingsChanged?.Invoke();
        }

        public void SetFxVolume(float value)
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(fxVolume, clamped) && PlayerPrefs.HasKey(FxVolumePrefsKey))
            {
                return;
            }

            fxVolume = clamped;
            PlayerPrefs.SetFloat(FxVolumePrefsKey, fxVolume);
            PlayerPrefs.Save();
            SettingsChanged?.Invoke();
        }

        private void Awake()
        {
            Load();
        }
    }
}
