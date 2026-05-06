#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !CAPTURE_BUILD
using Rescue.Replay;
using UnityEngine.UIElements;

namespace Rescue.Unity.Debugging
{
    internal static class DebugPanelReplayStatus
    {
        public static void UpdateReplayStatus(Label? label, ReplayResult? replay, int frameIndex)
        {
            SetText(label, FormatReplayStatus(replay, frameIndex));
        }

        public static void UpdatePlaybackStep(Label? label, string currentStepName)
        {
            SetText(label, $"Playback step: {currentStepName}");
        }

        public static void UpdatePlaybackUnavailable(Label? label)
        {
            SetText(label, "Playback step: unavailable");
        }

        private static string FormatReplayStatus(ReplayResult? replay, int frameIndex)
        {
            return replay is null
                ? "Replay: inactive"
                : $"Replay: frame {frameIndex}/{replay.Frames.Length - 1}; verified: {replay.Verified}";
        }

        private static void SetText(Label? label, string text)
        {
            if (label is not null)
            {
                label.text = text;
            }
        }
    }
}
#endif
