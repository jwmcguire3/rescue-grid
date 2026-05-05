using System.Runtime.InteropServices;
using UnityEngine;

namespace Rescue.Unity.Haptics
{
    public sealed class IOSHapticPlatformAdapter : IHapticPlatformAdapter
    {
        private readonly IHapticPlatformAdapter fallback;

        public IOSHapticPlatformAdapter(IHapticPlatformAdapter fallback)
        {
            this.fallback = fallback;
        }

        public bool SupportsAdvancedPatterns => true;

        public void Play(HapticPattern pattern)
        {
#if UNITY_IOS && !UNITY_EDITOR
            RescueGrid_PlayHapticPattern(
                (int)pattern.Style,
                Mathf.Clamp01(pattern.Intensity),
                Mathf.Max(0, pattern.DurationMs),
                Mathf.Clamp01(pattern.SecondPulseIntensity),
                Mathf.Max(0, pattern.SecondPulseDelayMs),
                Mathf.Max(0, pattern.SecondPulseDurationMs));
#else
            fallback.Play(pattern);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RescueGrid_PlayHapticPattern(
            int style,
            float intensity,
            int durationMs,
            float secondPulseIntensity,
            int secondPulseDelayMs,
            int secondPulseDurationMs);
#endif
    }
}
