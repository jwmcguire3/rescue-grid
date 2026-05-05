namespace Rescue.Unity.Haptics
{
    public interface IHapticPlatformAdapter
    {
        bool SupportsAdvancedPatterns { get; }

        void Play(HapticPattern pattern);
    }
}
