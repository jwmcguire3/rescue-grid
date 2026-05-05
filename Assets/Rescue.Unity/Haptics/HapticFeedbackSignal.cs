namespace Rescue.Unity.Haptics
{
    public readonly record struct HapticFeedbackSignal(
        HapticEventId Id,
        float Intensity,
        int Priority,
        string DebugLabel);
}
