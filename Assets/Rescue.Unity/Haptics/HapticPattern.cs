using UnityEngine;

namespace Rescue.Unity.Haptics
{
    public readonly record struct HapticPattern(
        HapticPatternStyle Style,
        float Intensity,
        int DurationMs,
        float SecondPulseIntensity = 0f,
        int SecondPulseDelayMs = 0,
        int SecondPulseDurationMs = 0)
    {
        public HapticPattern Clamp(float strengthMultiplier)
        {
            return new HapticPattern(
                Style,
                Mathf.Clamp01(Intensity * Mathf.Clamp01(strengthMultiplier)),
                Mathf.Max(0, DurationMs),
                Mathf.Clamp01(SecondPulseIntensity * Mathf.Clamp01(strengthMultiplier)),
                Mathf.Max(0, SecondPulseDelayMs),
                Mathf.Max(0, SecondPulseDurationMs));
        }

        public bool HasSecondPulse => SecondPulseIntensity > 0f && SecondPulseDelayMs > 0 && SecondPulseDurationMs > 0;
    }
}
