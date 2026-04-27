using Rescue.Core.State;

namespace Rescue.Content
{
    public sealed record LevelTuningOverrides(
        int? WaterRiseInterval = null,
        int? InitialFloodedRows = null,
        double? AssistanceChance = null,
        bool? ForceEmergencyAssistance = null,
        bool? DockJamEnabled = null,
        int? DockSize = null,
        int? DefaultCrateHp = null,
        int? VineGrowthThreshold = null,
        WaterContactMode? WaterContactMode = null)
    {
        public bool HasValues =>
            WaterRiseInterval.HasValue
            || InitialFloodedRows.HasValue
            || AssistanceChance.HasValue
            || ForceEmergencyAssistance.HasValue
            || DockJamEnabled.HasValue
            || DockSize.HasValue
            || DefaultCrateHp.HasValue
            || VineGrowthThreshold.HasValue
            || WaterContactMode.HasValue;

        public static LevelTuningOverrides None { get; } = new();
    }

    internal readonly record struct EffectiveLevelTuning(
        int WaterRiseInterval,
        int InitialFloodedRows,
        double AssistanceChance,
        bool? ForceEmergencyAssistance,
        bool DockJamEnabled,
        int DockSize,
        int DefaultCrateHp,
        int VineGrowthThreshold,
        WaterContactMode WaterContactMode);
}
