using UnityEngine;

namespace Rescue.Unity.Audio
{
    public readonly struct MusicTrackSelection
    {
        public MusicTrackSelection(AudioClip clip, int trackIndex, int nextSequentialIndex, float volume)
        {
            Clip = clip;
            TrackIndex = trackIndex;
            NextSequentialIndex = nextSequentialIndex;
            Volume = Mathf.Clamp01(volume);
        }

        public AudioClip Clip { get; }

        public int TrackIndex { get; }

        public int NextSequentialIndex { get; }

        public float Volume { get; }
    }
}
