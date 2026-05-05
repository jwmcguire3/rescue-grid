using UnityEngine;

namespace Rescue.Unity.Haptics
{
    public sealed class AndroidHapticPlatformAdapter : IHapticPlatformAdapter
    {
        private readonly IHapticPlatformAdapter fallback;

        public AndroidHapticPlatformAdapter(IHapticPlatformAdapter fallback)
        {
            this.fallback = fallback;
        }

        public bool SupportsAdvancedPatterns => Application.platform == RuntimePlatform.Android;

        public void Play(HapticPattern pattern)
        {
            if (Application.platform != RuntimePlatform.Android || !TryPlayAndroid(pattern))
            {
                fallback.Play(pattern);
            }
        }

        private static bool TryPlayAndroid(HapticPattern pattern)
        {
            try
            {
                using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator is null)
                {
                    return false;
                }

                int amplitude = Mathf.Clamp(Mathf.RoundToInt(pattern.Intensity * 255f), 1, 255);
                if (pattern.HasSecondPulse)
                {
                    long[] timings = { 0L, pattern.DurationMs, pattern.SecondPulseDelayMs, pattern.SecondPulseDurationMs };
                    int[] amplitudes = { 0, amplitude, 0, Mathf.Clamp(Mathf.RoundToInt(pattern.SecondPulseIntensity * 255f), 1, 255) };
                    using AndroidJavaClass vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
                    using AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1);
                    vibrator.Call("vibrate", effect);
                    return true;
                }

                using AndroidJavaClass effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                using AndroidJavaObject effectObject = effectClass.CallStatic<AndroidJavaObject>(
                    "createOneShot",
                    (long)Mathf.Max(1, pattern.DurationMs),
                    amplitude);
                vibrator.Call("vibrate", effectObject);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
