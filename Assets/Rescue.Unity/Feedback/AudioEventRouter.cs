using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Presentation;
using UnityEngine;

namespace Rescue.Unity.Feedback
{
    [DisallowMultipleComponent]
    public class AudioEventRouter : MonoBehaviour
    {
        [SerializeField] private AudioFeedbackRegistry? registry;
        [SerializeField] private AudioSource? audioSource;
        [SerializeField] private BoardGridViewPresenter? boardGrid;

        public AudioFeedbackRegistry? Registry
        {
            get => registry;
            set => registry = value;
        }

        public AudioSource? AudioSource
        {
            get => audioSource;
            set => audioSource = value;
        }

        public BoardGridViewPresenter? BoardGrid
        {
            get => boardGrid;
            set => boardGrid = value;
        }

        public void Route(FeedbackEvent feedbackEvent)
        {
            Dictionary<FeedbackEventId, int> playCounts = new Dictionary<FeedbackEventId, int>();
            TryPlay(feedbackEvent, playCounts);
        }

        public void Route(ActionEvent actionEvent)
        {
            if (FeedbackEventClassifier.TryClassify(actionEvent, out FeedbackEvent feedbackEvent))
            {
                Route(feedbackEvent);
            }
        }

        public void RoutePlaybackBeat(
            GameState previousState,
            ActionInput input,
            GameState resultState,
            ActionPlaybackStep step)
        {
            _ = previousState;
            _ = input;
            _ = resultState;

            if (!FeedbackEventClassifier.TryClassify(step, out FeedbackEvent feedbackEvent))
            {
                return;
            }

            Dictionary<FeedbackEventId, int> playCounts = new Dictionary<FeedbackEventId, int>();
            TryPlay(feedbackEvent, playCounts);
        }

        public void RouteResultSignals(GameState previousState, ActionInput input, ActionResult result)
        {
            _ = previousState;
            _ = input;

            if (result is null)
            {
                return;
            }

            ImmutableArray<FeedbackEvent> feedbackEvents = FeedbackEventClassifier.Classify(result);
            Dictionary<FeedbackEventId, int> playCounts = new Dictionary<FeedbackEventId, int>();

            for (int i = 0; i < feedbackEvents.Length; i++)
            {
                if (!IsResultSignal(feedbackEvents[i].Id))
                {
                    continue;
                }

                TryPlay(feedbackEvents[i], playCounts);
            }
        }

        protected virtual void PlayClip(AudioClip clip, AudioFeedbackEntry entry, Vector3 worldPosition)
        {
            _ = worldPosition;

            AudioSource? resolvedSource = ResolveAudioSource();
            if (resolvedSource is null || clip is null)
            {
                return;
            }

            float previousPitch = resolvedSource.pitch;
            resolvedSource.pitch = ResolvePitch(entry);
            resolvedSource.PlayOneShot(clip, entry.Volume);
            resolvedSource.pitch = previousPitch;
        }

        private bool TryPlay(FeedbackEvent feedbackEvent, Dictionary<FeedbackEventId, int> playCounts)
        {
            if (registry is null ||
                !registry.TryGetEntry(feedbackEvent.Id, out AudioFeedbackEntry? entry) ||
                entry is null ||
                !entry.TryGetClip(out AudioClip? clip) ||
                clip is null)
            {
                return false;
            }

            playCounts.TryGetValue(feedbackEvent.Id, out int count);
            if (count >= entry.MaxPlaysPerRoute)
            {
                return false;
            }

            playCounts[feedbackEvent.Id] = count + 1;
            PlayClip(clip, entry, ResolveWorldPosition(feedbackEvent));
            return true;
        }

        private static bool IsResultSignal(FeedbackEventId eventId)
        {
            return eventId == FeedbackEventId.InvalidTap
                || eventId == FeedbackEventId.WaterWarning
                || eventId == FeedbackEventId.TargetOneClearAway
                || eventId == FeedbackEventId.VinePreview
                || eventId == FeedbackEventId.VineGrow
                || eventId == FeedbackEventId.Win
                || eventId == FeedbackEventId.LossDockOverflow
                || eventId == FeedbackEventId.LossWaterOnTarget;
        }

        private AudioSource? ResolveAudioSource()
        {
            if (audioSource is not null)
            {
                return audioSource;
            }

            if (TryGetComponent(out AudioSource resolvedSource))
            {
                audioSource = resolvedSource;
                return resolvedSource;
            }

            return null;
        }

        private float ResolvePitch(AudioFeedbackEntry entry)
        {
            if (entry.PitchVariance <= 0f)
            {
                return 1f;
            }

            return Random.Range(1f - entry.PitchVariance, 1f + entry.PitchVariance);
        }

        private Vector3 ResolveWorldPosition(FeedbackEvent feedbackEvent)
        {
            if (!feedbackEvent.Location.HasValue)
            {
                return transform.position;
            }

            BoardGridViewPresenter? resolvedBoardGrid = ResolveBoardGrid();
            if (resolvedBoardGrid is not null &&
                resolvedBoardGrid.TryGetCellWorldPosition(feedbackEvent.Location.Value, out Vector3 worldPosition))
            {
                return worldPosition;
            }

            return transform.position;
        }

        private BoardGridViewPresenter? ResolveBoardGrid()
        {
            if (boardGrid is not null)
            {
                return boardGrid;
            }

            boardGrid = GetComponent<BoardGridViewPresenter>();
            return boardGrid;
        }
    }
}
