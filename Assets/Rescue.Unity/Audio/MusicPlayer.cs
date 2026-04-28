using System.Collections;
using UnityEngine;

namespace Rescue.Unity.Audio
{
    [DisallowMultipleComponent]
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private MusicPlaylist? playlist;
        [SerializeField] private AudioSource? audioSource;
        [SerializeField] private bool autoplay = true;

        private Coroutine? fadeRoutine;
        private int previousTrackIndex = -1;
        private int sequentialIndex;
        private bool stopping;

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

        public bool Autoplay
        {
            get => autoplay;
            set => autoplay = value;
        }

        private void Start()
        {
            if (autoplay)
            {
                PlayNext();
            }
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

            float targetVolume = selection.Volume;
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
