using System;
using UnityEngine;

namespace Rescue.Unity.Audio
{
    [CreateAssetMenu(fileName = "MusicPlaylist", menuName = "Rescue Grid/Audio/Music Playlist")]
    public sealed class MusicPlaylist : ScriptableObject
    {
        [SerializeField] private MusicTrackEntry?[] tracks = Array.Empty<MusicTrackEntry?>();
        [SerializeField] private bool shuffle = true;
        [SerializeField] private bool avoidImmediateRepeat = true;
        [SerializeField] private bool loopPlaylist = true;
        [SerializeField] [Min(0f)] private float fadeInDurationSeconds = 0f;
        [SerializeField] [Min(0f)] private float fadeOutDurationSeconds = 0f;
        [SerializeField] [Range(0f, 1f)] private float defaultVolume = 1f;

        public ReadOnlySpan<MusicTrackEntry?> Tracks => tracks;

        public bool Shuffle => shuffle;

        public bool AvoidImmediateRepeat => avoidImmediateRepeat;

        public bool LoopPlaylist => loopPlaylist;

        public float FadeInDurationSeconds => Mathf.Max(0f, fadeInDurationSeconds);

        public float FadeOutDurationSeconds => Mathf.Max(0f, fadeOutDurationSeconds);

        public float DefaultVolume => Mathf.Clamp01(defaultVolume);

        public void ConfigureForTests(
            MusicTrackEntry?[]? newTracks,
            bool newShuffle = true,
            bool newAvoidImmediateRepeat = true,
            bool newLoopPlaylist = true,
            float newFadeInDurationSeconds = 0f,
            float newFadeOutDurationSeconds = 0f,
            float newDefaultVolume = 1f)
        {
            tracks = newTracks ?? Array.Empty<MusicTrackEntry?>();
            shuffle = newShuffle;
            avoidImmediateRepeat = newAvoidImmediateRepeat;
            loopPlaylist = newLoopPlaylist;
            fadeInDurationSeconds = Mathf.Max(0f, newFadeInDurationSeconds);
            fadeOutDurationSeconds = Mathf.Max(0f, newFadeOutDurationSeconds);
            defaultVolume = Mathf.Clamp01(newDefaultVolume);
        }

        public bool TryGetNextTrack(
            int previousTrackIndex,
            int sequentialIndex,
            out MusicTrackSelection selection)
        {
            return TryGetNextTrack(
                previousTrackIndex,
                sequentialIndex,
                UnityEngine.Random.Range,
                out selection);
        }

        public bool TryGetNextTrack(
            int previousTrackIndex,
            int sequentialIndex,
            Func<int, int, int>? randomRange,
            out MusicTrackSelection selection)
        {
            selection = default;

            if (tracks.Length == 0)
            {
                return false;
            }

            return shuffle
                ? TryGetRandomTrack(previousTrackIndex, randomRange, out selection)
                : TryGetSequentialTrack(sequentialIndex, out selection);
        }

        private bool TryGetRandomTrack(
            int previousTrackIndex,
            Func<int, int, int>? randomRange,
            out MusicTrackSelection selection)
        {
            selection = default;

            int validCount = CountValidTracks();
            if (validCount == 0)
            {
                return false;
            }

            bool canSkipPrevious = avoidImmediateRepeat
                && validCount > 1
                && IsValidTrackIndex(previousTrackIndex);

            int randomUpperBound = canSkipPrevious ? validCount - 1 : validCount;
            int chosenOrdinal = InvokeRandomRange(randomRange, 0, randomUpperBound);
            int validOrdinal = 0;

            for (int i = 0; i < tracks.Length; i++)
            {
                if (!IsValidTrackIndex(i) || (canSkipPrevious && i == previousTrackIndex))
                {
                    continue;
                }

                if (validOrdinal == chosenOrdinal)
                {
                    return TryCreateSelection(i, 0, out selection);
                }

                validOrdinal++;
            }

            return false;
        }

        private bool TryGetSequentialTrack(int sequentialIndex, out MusicTrackSelection selection)
        {
            selection = default;

            if (tracks.Length == 0)
            {
                return false;
            }

            int startIndex = Mathf.Clamp(sequentialIndex, 0, tracks.Length);
            for (int i = startIndex; i < tracks.Length; i++)
            {
                if (TryCreateSelection(i, i + 1, out selection))
                {
                    return true;
                }
            }

            if (!loopPlaylist)
            {
                return false;
            }

            for (int i = 0; i < startIndex; i++)
            {
                if (TryCreateSelection(i, i + 1, out selection))
                {
                    return true;
                }
            }

            return false;
        }

        private int CountValidTracks()
        {
            int count = 0;
            for (int i = 0; i < tracks.Length; i++)
            {
                if (IsValidTrackIndex(i))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsValidTrackIndex(int index)
        {
            return index >= 0
                && index < tracks.Length
                && tracks[index]?.Clip is not null;
        }

        private bool TryCreateSelection(int trackIndex, int nextSequentialIndex, out MusicTrackSelection selection)
        {
            MusicTrackEntry? entry = tracks[trackIndex];
            AudioClip? clip = entry?.Clip;
            if (entry is null || clip is null)
            {
                selection = default;
                return false;
            }

            selection = new MusicTrackSelection(
                clip,
                trackIndex,
                nextSequentialIndex,
                entry.ResolveVolume(DefaultVolume));
            return true;
        }

        private static int InvokeRandomRange(Func<int, int, int>? randomRange, int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            int value = randomRange?.Invoke(minInclusive, maxExclusive)
                ?? UnityEngine.Random.Range(minInclusive, maxExclusive);
            return Mathf.Clamp(value, minInclusive, maxExclusive - 1);
        }
    }
}
