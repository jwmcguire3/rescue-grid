using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.Audio;
using UnityEngine;

namespace Rescue.Unity.Haptics
{
    [DisallowMultipleComponent]
    public class HapticEventRouter : MonoBehaviour
    {
        [SerializeField] private AudioSettingsController? settingsController;

        public AudioSettingsController? SettingsController
        {
            get => settingsController;
            set => settingsController = value;
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

        public void RouteManual(HapticEventId id)
        {
            Route(HapticEventClassifier.CreateManual(id));
        }

        public void Route(HapticFeedbackSignal signal)
        {
            if (signal.Intensity <= 0f || !ResolveHapticsEnabled())
            {
                return;
            }

            PlayHaptic(signal);
        }

        protected virtual void PlayHaptic(HapticFeedbackSignal signal)
        {
            _ = signal;
            if (Application.isMobilePlatform)
            {
                Handheld.Vibrate();
            }
        }

        private bool ResolveHapticsEnabled()
        {
            return ResolveSettingsController()?.HapticsEnabled ?? true;
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
