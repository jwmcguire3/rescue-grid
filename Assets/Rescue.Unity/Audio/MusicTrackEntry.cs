using System;
using UnityEngine;

namespace Rescue.Unity.Audio
{
    [Serializable]
    public sealed class MusicTrackEntry
    {
        [SerializeField] private AudioClip? clip;
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private bool overrideDefaultVolume;

        public MusicTrackEntry()
        {
        }

        public MusicTrackEntry(AudioClip? clip, float volume = 1f, bool overrideDefaultVolume = false)
        {
            this.clip = clip;
            this.volume = Mathf.Clamp01(volume);
            this.overrideDefaultVolume = overrideDefaultVolume;
        }

        public AudioClip? Clip => clip;

        public float Volume => Mathf.Clamp01(volume);

        public bool OverrideDefaultVolume => overrideDefaultVolume;

        public float ResolveVolume(float defaultVolume)
        {
            return overrideDefaultVolume ? Volume : Mathf.Clamp01(defaultVolume);
        }
    }
}
