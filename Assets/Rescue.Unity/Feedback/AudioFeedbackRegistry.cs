using System;
using UnityEngine;

namespace Rescue.Unity.Feedback
{
    [CreateAssetMenu(fileName = "AudioFeedbackRegistry", menuName = "Rescue Grid/Feedback/Audio Feedback Registry")]
    public sealed class AudioFeedbackRegistry : ScriptableObject
    {
        [SerializeField] private AudioFeedbackEntry?[] entries = Array.Empty<AudioFeedbackEntry?>();

        public ReadOnlySpan<AudioFeedbackEntry?> Entries => entries;

        public bool TryGetEntry(FeedbackEventId eventId, out AudioFeedbackEntry? entry)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                AudioFeedbackEntry? candidate = entries[i];
                if (candidate is not null && candidate.EventId == eventId)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public void SetEntries(params AudioFeedbackEntry[] newEntries)
        {
            entries = newEntries ?? Array.Empty<AudioFeedbackEntry>();
        }
    }
}
