using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.Audio;
using Rescue.Unity.Presentation;
using UnityEngine;

namespace Rescue.Unity.Haptics
{
    [DisallowMultipleComponent]
    public class HapticEventRouter : MonoBehaviour
    {
        [SerializeField] private AudioSettingsController? settingsController;
        [SerializeField] private bool diagnosticsEnabled;

        private readonly Dictionary<HapticCooldownKey, float> lastPlayedByCooldownKey = new Dictionary<HapticCooldownKey, float>();
        private IHapticPlatformAdapter? platformAdapter;
        private HapticFeedbackSignal? activeHeadlineSignal;

        public AudioSettingsController? SettingsController
        {
            get => settingsController;
            set => settingsController = value;
        }

        public bool DiagnosticsEnabled
        {
            get => diagnosticsEnabled;
            set => diagnosticsEnabled = value;
        }

        public void RouteResultSignals(GameState previousState, ActionInput input, ActionResult result)
        {
            _ = previousState;
            _ = input;

            if (HapticEventClassifier.TryClassify(result, out HapticFeedbackSignal signal))
            {
                Route(signal);
            }
        }

        public void BeginActionRoute(ActionResult result)
        {
            activeHeadlineSignal = HapticEventClassifier.TryClassify(result, out HapticFeedbackSignal signal)
                ? signal
                : null;
        }

        public void EndActionRoute()
        {
            activeHeadlineSignal = null;
        }

        public void RoutePlaybackBeat(
            GameState previousState,
            ActionInput input,
            GameState resultState,
            ActionPlaybackStep playbackStep)
        {
            _ = previousState;
            _ = input;
            _ = resultState;

            if (!TryClassifyPlaybackStep(playbackStep, out HapticFeedbackSignal signal) || ShouldSuppressPlaybackSignal(signal))
            {
                return;
            }

            Route(signal);
        }

        private static bool TryClassifyPlaybackStep(ActionPlaybackStep playbackStep, out HapticFeedbackSignal signal)
        {
            bool hasSignal = false;
            HapticFeedbackSignal bestSignal = default;
            ImmutableArray<ActionEvent> events = playbackStep.Events;
            if (events.IsDefaultOrEmpty)
            {
                signal = default;
                return false;
            }

            for (int i = 0; i < events.Length; i++)
            {
                if (!HapticEventClassifier.TryClassify(events[i], out HapticFeedbackSignal candidate))
                {
                    continue;
                }

                if (!hasSignal || candidate.Priority > bestSignal.Priority)
                {
                    bestSignal = candidate;
                    hasSignal = true;
                }
            }

            signal = bestSignal;
            return hasSignal;
        }

        public void RouteManual(HapticEventId id)
        {
            Route(HapticEventClassifier.CreateManual(id));
        }

        public void Route(HapticFeedbackSignal signal)
        {
            HapticPattern pattern = signal.Pattern.Clamp(ResolveHapticsStrength());
            if (pattern.Intensity <= 0f || !ResolveHapticsEnabled() || IsCoolingDown(signal))
            {
                return;
            }

            MarkCooldown(signal);
            PlayHaptic(signal with { Pattern = pattern });
        }

        protected virtual void PlayHaptic(HapticFeedbackSignal signal)
        {
            if (diagnosticsEnabled)
            {
                Debug.Log(
                    $"[Haptics] id={signal.Id} style={signal.Pattern.Style} intensity={signal.Pattern.Intensity:0.00} durationMs={signal.Pattern.DurationMs} source={signal.DebugLabel}",
                    this);
            }

            IHapticPlatformAdapter adapter = ResolvePlatformAdapter();
            adapter.Play(signal.Pattern);
            if (!adapter.SupportsAdvancedPatterns && signal.Pattern.HasSecondPulse && Application.isPlaying && isActiveAndEnabled)
            {
                StartCoroutine(PlaySecondPulse(signal.Pattern));
            }
        }

        protected virtual IHapticPlatformAdapter CreatePlatformAdapter()
        {
            IHapticPlatformAdapter handheldFallback = new HandheldHapticPlatformAdapter();
            return Application.platform switch
            {
                RuntimePlatform.Android => new AndroidHapticPlatformAdapter(handheldFallback),
                RuntimePlatform.IPhonePlayer => new IOSHapticPlatformAdapter(handheldFallback),
                _ => handheldFallback,
            };
        }

        private IEnumerator PlaySecondPulse(HapticPattern pattern)
        {
            yield return new WaitForSeconds(pattern.SecondPulseDelayMs / 1000f);
            ResolvePlatformAdapter().Play(new HapticPattern(
                pattern.Style,
                pattern.SecondPulseIntensity,
                pattern.SecondPulseDurationMs));
        }

        private bool ShouldSuppressPlaybackSignal(HapticFeedbackSignal signal)
        {
            if (!activeHeadlineSignal.HasValue)
            {
                return false;
            }

            HapticFeedbackSignal headline = activeHeadlineSignal.Value;
            if (headline.Id == HapticEventId.Win && signal.Id == HapticEventId.TargetExtract)
            {
                return true;
            }

            if ((headline.Id == HapticEventId.DockOverflow || headline.Id == HapticEventId.WaterLoss) &&
                signal.Priority < 70)
            {
                return true;
            }

            return false;
        }

        private bool IsCoolingDown(HapticFeedbackSignal signal)
        {
            if (signal.CooldownKey == HapticCooldownKey.None || signal.CooldownSeconds <= 0f)
            {
                return false;
            }

            float now = Time.realtimeSinceStartup;
            return lastPlayedByCooldownKey.TryGetValue(signal.CooldownKey, out float lastPlayed) &&
                now - lastPlayed < signal.CooldownSeconds;
        }

        private void MarkCooldown(HapticFeedbackSignal signal)
        {
            if (signal.CooldownKey != HapticCooldownKey.None && signal.CooldownSeconds > 0f)
            {
                lastPlayedByCooldownKey[signal.CooldownKey] = Time.realtimeSinceStartup;
            }
        }

        private IHapticPlatformAdapter ResolvePlatformAdapter()
        {
            platformAdapter ??= CreatePlatformAdapter();
            return platformAdapter;
        }

        private bool ResolveHapticsEnabled()
        {
            return ResolveSettingsController()?.HapticsEnabled ?? true;
        }

        private float ResolveHapticsStrength()
        {
            return ResolveSettingsController()?.HapticsStrength ?? 1f;
        }

        private AudioSettingsController? ResolveSettingsController()
        {
            if (settingsController is not null)
            {
                return settingsController;
            }

            settingsController = FindAnyObjectByType<AudioSettingsController>();
            return settingsController;
        }
    }
}
