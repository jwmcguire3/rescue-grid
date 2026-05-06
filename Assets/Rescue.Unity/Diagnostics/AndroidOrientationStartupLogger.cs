#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;

namespace Rescue.Unity.Diagnostics
{
    public static class AndroidOrientationStartupLogger
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LogStartupOrientation()
        {
            Debug.Log(
                "[AndroidOrientationStartup] " +
                $"platform={Application.platform}, " +
                $"orientation={Screen.orientation}, " +
                $"autorotateToLandscapeLeft={Screen.autorotateToLandscapeLeft}, " +
                $"autorotateToLandscapeRight={Screen.autorotateToLandscapeRight}, " +
                $"autorotateToPortrait={Screen.autorotateToPortrait}, " +
                $"autorotateToPortraitUpsideDown={Screen.autorotateToPortraitUpsideDown}");
        }
    }
}
#endif
