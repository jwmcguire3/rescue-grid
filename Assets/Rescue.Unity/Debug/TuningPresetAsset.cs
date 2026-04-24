using Rescue.Content;
using UnityEngine;

namespace Rescue.Unity.Debugging
{
    public sealed class TuningPresetAsset : ScriptableObject
    {
        [SerializeField] private string presetName = string.Empty;
        [SerializeField] private int waterRiseInterval;
        [SerializeField] private bool overrideWaterRiseInterval;
        [SerializeField] private int initialFloodedRows;
        [SerializeField] private bool overrideInitialFloodedRows;
        [SerializeField] private float assistanceChance;
        [SerializeField] private bool overrideAssistanceChance;
        [SerializeField] private EmergencyOverrideMode forceEmergencyMode = EmergencyOverrideMode.Auto;
        [SerializeField] private bool dockJamEnabled;
        [SerializeField] private bool overrideDockJamEnabled;
        [SerializeField] private int dockSize;
        [SerializeField] private bool overrideDockSize;
        [SerializeField] private int defaultCrateHp;
        [SerializeField] private bool overrideDefaultCrateHp;
        [SerializeField] private int vineGrowthThreshold;
        [SerializeField] private bool overrideVineGrowthThreshold;

        public string PresetName
        {
            get => presetName;
            set => presetName = value ?? string.Empty;
        }

        public LevelTuningOverrides ToOverrides()
        {
            return new LevelTuningOverrides(
                WaterRiseInterval: overrideWaterRiseInterval ? waterRiseInterval : null,
                InitialFloodedRows: overrideInitialFloodedRows ? initialFloodedRows : null,
                AssistanceChance: overrideAssistanceChance ? assistanceChance : null,
                ForceEmergencyAssistance: forceEmergencyMode.ToNullableBool(),
                DockJamEnabled: overrideDockJamEnabled ? dockJamEnabled : null,
                DockSize: overrideDockSize ? dockSize : null,
                DefaultCrateHp: overrideDefaultCrateHp ? defaultCrateHp : null,
                VineGrowthThreshold: overrideVineGrowthThreshold ? vineGrowthThreshold : null);
        }

        public void Apply(LevelTuningOverrides overrides)
        {
            PresetName = string.IsNullOrWhiteSpace(PresetName) ? "Preset" : PresetName;
            overrideWaterRiseInterval = overrides.WaterRiseInterval.HasValue;
            waterRiseInterval = overrides.WaterRiseInterval.GetValueOrDefault();
            overrideInitialFloodedRows = overrides.InitialFloodedRows.HasValue;
            initialFloodedRows = overrides.InitialFloodedRows.GetValueOrDefault();
            overrideAssistanceChance = overrides.AssistanceChance.HasValue;
            assistanceChance = (float)overrides.AssistanceChance.GetValueOrDefault();
            forceEmergencyMode = EmergencyOverrideModeExtensions.FromNullableBool(overrides.ForceEmergencyAssistance);
            overrideDockJamEnabled = overrides.DockJamEnabled.HasValue;
            dockJamEnabled = overrides.DockJamEnabled.GetValueOrDefault();
            overrideDockSize = overrides.DockSize.HasValue;
            dockSize = overrides.DockSize.GetValueOrDefault();
            overrideDefaultCrateHp = overrides.DefaultCrateHp.HasValue;
            defaultCrateHp = overrides.DefaultCrateHp.GetValueOrDefault();
            overrideVineGrowthThreshold = overrides.VineGrowthThreshold.HasValue;
            vineGrowthThreshold = overrides.VineGrowthThreshold.GetValueOrDefault();
        }
    }

    public enum EmergencyOverrideMode
    {
        Auto,
        On,
        Off,
    }

    public static class EmergencyOverrideModeExtensions
    {
        public static bool? ToNullableBool(this EmergencyOverrideMode mode)
        {
            return mode switch
            {
                EmergencyOverrideMode.On => true,
                EmergencyOverrideMode.Off => false,
                _ => null,
            };
        }

        public static EmergencyOverrideMode FromNullableBool(bool? value)
        {
            return value switch
            {
                true => EmergencyOverrideMode.On,
                false => EmergencyOverrideMode.Off,
                _ => EmergencyOverrideMode.Auto,
            };
        }
    }
}
