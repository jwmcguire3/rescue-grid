using System.Collections;
using UnityEngine;

namespace Rescue.Unity.Audio
{
    [DisallowMultipleComponent]
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private MusicPlaylist? playlist;
        [SerializeField] private AudioSource? audioSource;
        [SerializeField] private AudioSettingsController? settingsController;
        [SerializeField] private bool autoplay = true;

        private Coroutine? fadeRoutine;
        private int previousTrackIndex = -1;
        private int sequentialIndex;
        private bool stopping;
        private float currentTrackVolume = 1.0f;

        public MusicPlaylist? Playlist
        {
            get => playlist;
            set => playlist = value;
        }

        public AudioSource? AudioSource
        {
            get => audioSource;
            set => audioSource = value;
        }

        public AudioSettingsController? SettingsController
        {
            get => settingsController;
            set
            {
                UnsubscribeSettings();
                settingsController = value;
                SubscribeSettings();
                ApplySettingsVolume();
            }
        }

        public bool Autoplay
        {
            get => autoplay;
            set => autoplay = value;
        }

        private void Start()
        {
            ResolveSettingsController();
            if (autoplay)
            {
                PlayNext();
            }
        }

        private void OnEnable()
        {
            SubscribeSettings();
            ApplySettingsVolume();
        }

        private void OnDisable()
        {
            UnsubscribeSettings();
        }

        private void Update()
        {
            AudioSource? resolvedSource = ResolveAudioSource(createIfMissing: false);
            if (stopping ||
                resolvedSource is null ||
                playlist is null ||
                !playlist.LoopPlaylist ||
                resolvedSource.clip is null ||
                resolvedSource.isPlaying)
            {
                return;
            }

            PlayNext();
        }

        public bool PlayNext()
        {
            if (playlist is null ||
                !playlist.TryGetNextTrack(previousTrackIndex, sequentialIndex, out MusicTrackSelection selection))
            {
                return false;
            }

            AudioSource? resolvedSource = ResolveAudioSource(createIfMissing: true);
            if (resolvedSource is null)
            {
                return false;
            }

            previousTrackIndex = selection.TrackIndex;
            sequentialIndex = selection.NextSequentialIndex;
            PlaySelection(resolvedSource, selection);
            return true;
        }

        public void Stop()
        {
            Stop(fadeOut: true);
        }

        public void Stop(bool fadeOut)
        {
            AudioSource? resolvedSource = ResolveAudioSource(createIfMissing: false);
            if (resolvedSource is null)
            {
                return;
            }

            stopping = true;
            StopFadeRoutine();

            float fadeOutDuration = fadeOut && playlist is not null
                ? playlist.FadeOutDurationSeconds
                : 0f;

            if (fadeOutDuration <= 0f)
            {
                resolvedSource.Stop();
                resolvedSource.clip = null;
                return;
            }

            fadeRoutine = StartCoroutine(FadeOutAndStop(resolvedSource, fadeOutDuration));
        }

        private void PlaySelection(AudioSource resolvedSource, MusicTrackSelection selection)
        {
            stopping = false;
            StopFadeRoutine();

            resolvedSource.loop = false;
            resolvedSource.clip = selection.Clip;

            currentTrackVolume = selection.Volume;
            float targetVolume = ResolveEffectiveVolume();
            float fadeInDuration = playlist is not null ? playlist.FadeInDurationSeconds : 0f;
            if (fadeInDuration <= 0f)
            {
                resolvedSource.volume = targetVolume;
                resolvedSource.Play();
                return;
            }

            resolvedSource.volume = 0f;
            resolvedSource.Play();
            fadeRoutine = StartCoroutine(FadeVolume(resolvedSource, targetVolume, fadeInDuration));
        }

        private AudioSource? ResolveAudioSource(bool createIfMissing)
        {
            if (audioSource is not null)
            {
                return audioSource;
            }

            if (TryGetComponent(out AudioSource existingSource))
            {
                audioSource = existingSource;
                return existingSource;
            }

            if (!createIfMissing)
            {
                return null;
            }

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            return audioSource;
        }

        private AudioSettingsController? ResolveSettingsController()
        {
            if (settingsController is not null)
            {
                return settingsController;
            }

            settingsController = FindAnyObjectByType<AudioSettingsController>();
            if (settingsController is not null)
            {
                SubscribeSettings();
            }

            return settingsController;
        }

        private void SubscribeSettings()
        {
            if (settingsController is null)
            {
                return;
            }

            settingsController.SettingsChanged -= ApplySettingsVolume;
            settingsController.SettingsChanged += ApplySettingsVolume;
        }

        private void UnsubscribeSettings()
        {
            if (settingsController is not null)
            {
                settingsController.SettingsChanged -= ApplySettingsVolume;
            }
        }

        private void ApplySettingsVolume()
        {
            AudioSource? resolvedSource = ResolveAudioSource(createIfMissing: false);
            if (resolvedSource is not null)
            {
                StopFadeRoutine();
                resolvedSource.volume = ResolveEffectiveVolume();
            }
        }

        private float ResolveEffectiveVolume()
        {
            return Mathf.Clamp01(currentTrackVolume * (ResolveSettingsController()?.MusicVolume ?? 1.0f));
        }

        private void StopFadeRoutine()
        {
            if (fadeRoutine is null)
            {
                return;
            }

            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        private IEnumerator FadeVolume(AudioSource source, float targetVolume, float durationSeconds)
        {
            float startVolume = source.volume;
            float elapsed = 0f;

            while (elapsed < durationSeconds && source is not null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / durationSeconds);
                source.volume = Mathf.Lerp(startVolume, targetVolume, t);
                yield return null;
            }

            if (source is not null)
            {
                source.volume = targetVolume;
            }

            fadeRoutine = null;
        }

        private IEnumerator FadeOutAndStop(AudioSource source, float durationSeconds)
        {
            yield return FadeVolume(source, 0f, durationSeconds);

            if (source is not null)
            {
                source.Stop();
                source.clip = null;
            }
        }
    }
}
