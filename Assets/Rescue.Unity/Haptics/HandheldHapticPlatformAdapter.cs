using UnityEngine;

namespace Rescue.Unity.Haptics
{
    public sealed class HandheldHapticPlatformAdapter : IHapticPlatformAdapter
    {
        public bool SupportsAdvancedPatterns => false;

        public void Play(HapticPattern pattern)
        {
            _ = pattern;
            if (Application.isMobilePlatform)
            {
                Handheld.Vibrate();
            }
        }
    }
}
