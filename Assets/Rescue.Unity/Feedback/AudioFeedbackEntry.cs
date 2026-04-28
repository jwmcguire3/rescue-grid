using System;
using UnityEngine;

namespace Rescue.Unity.Feedback
{
    [Serializable]
    public sealed class AudioFeedbackEntry
    {
        [SerializeField] private FeedbackEventId eventId;
        [SerializeField] private AudioClip?[] clips = Array.Empty<AudioClip?>();
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;
        [SerializeField] [Min(0f)] private float pitchVariance = 0f;
        [SerializeField] [Min(1)] private int maxPlaysPerRoute = 1;

        public AudioFeedbackEntry()
        {
        }

        public AudioFeedbackEntry(
            FeedbackEventId eventId,
            AudioClip?[]? clips,
            float volume = 1f,
            float pitchVariance = 0f,
            int maxPlaysPerRoute = 1)
        {
            this.eventId = eventId;
            this.clips = clips ?? Array.Empty<AudioClip?>();
            this.volume = Mathf.Clamp01(volume);
            this.pitchVariance = Mathf.Max(0f, pitchVariance);
            this.maxPlaysPerRoute = Mathf.Max(1, maxPlaysPerRoute);
        }

        public FeedbackEventId EventId => eventId;

        public float Volume => Mathf.Clamp01(volume);

        public float PitchVariance => Mathf.Max(0f, pitchVariance);

        public int MaxPlaysPerRoute => Mathf.Max(1, maxPlaysPerRoute);

        public bool TryGetClip(out AudioClip? clip)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] is not null)
                {
                    clip = clips[i];
                    return true;
                }
            }

            clip = null;
            return false;
        }
    }
}
