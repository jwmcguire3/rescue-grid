namespace Rescue.Unity.Haptics
{
    public readonly record struct HapticFeedbackSignal(
        HapticEventId Id,
        HapticPattern Pattern,
        int Priority,
        HapticCooldownKey CooldownKey,
        float CooldownSeconds,
        string DebugLabel);
}
