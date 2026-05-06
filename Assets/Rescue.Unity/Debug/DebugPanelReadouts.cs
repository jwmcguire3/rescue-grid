#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !CAPTURE_BUILD
using Rescue.Core.Rules;
using Rescue.Core.State;
using UnityEngine.UIElements;

namespace Rescue.Unity.Debugging
{
    internal static class DebugPanelReadouts
    {
        internal static void Update(
            GameState state,
            Label? waterActionsValue,
            Label? waterRiseIntervalValue,
            Label? waterNextFloodRowValue,
            Label? waterForecastValue,
            Label? ruleTeachValue,
            Label? vineActionsValue,
            Label? vineThresholdValue,
            Label? vinePendingValue,
            Label? dockOccupancyValue,
            Label? dockWarningValue,
            Label? dockContentsValue,
            Label? dockJamUsedValue,
            Label? dockJamEnabledValue,
            Label? nearRescueTargetsValue,
            Label? rngStateValue,
            Label? consecutiveEmergencyValue,
            Label? spawnRecoveryValue)
        {
            SetText(waterActionsValue, $"Water actions until rise: {state.Water.ActionsUntilRise}");
            SetText(waterRiseIntervalValue, $"Water rise interval: {state.Water.RiseInterval}");
            SetText(waterNextFloodRowValue, $"Next row to flood: {DebugPanelDisplay.GetNextFloodRowLabel(state)}");
            SetText(waterForecastValue, $"Water forecast: {DebugPanelDisplay.GetWaterForecastSummary(state)}");
            SetText(ruleTeachValue, $"Rule teach active: {state.LevelConfig.IsRuleTeach}; waiting for first action: {state.Water.PauseUntilFirstAction}");
            SetText(vineActionsValue, $"Vine actions since clear: {state.Vine.ActionsSinceLastClear}");
            SetText(vineThresholdValue, $"Vine growth threshold: {state.Vine.GrowthThreshold}");
            SetText(vinePendingValue, $"Pending growth tile: {DebugPanelDisplay.FormatCoord(state.Vine.PendingGrowthTile)}");
            SetText(dockOccupancyValue, $"Dock occupancy: {DockHelpers.Occupancy(state.Dock)}/{state.Dock.Size}");
            SetText(dockWarningValue, $"Dock warning level: {DockHelpers.GetWarningLevel(state.Dock)}");
            SetText(dockContentsValue, $"Dock contents: {DebugPanelDisplay.FormatDockContents(state.Dock)}");
            SetText(dockJamUsedValue, $"Dock jam used: {state.DockJamUsed}");
            SetText(dockJamEnabledValue, $"Dock jam enabled: {state.DockJamEnabled}");
            SetText(nearRescueTargetsValue, $"Near-rescue targets: {DebugPanelDisplay.GetNearRescueTargetsSummary(state)}");
            SetText(rngStateValue, $"RNG state: {DebugPanelDisplay.SerializeRngState(state.RngState)}");
            SetText(consecutiveEmergencyValue, $"Consecutive emergency spawns: {state.ConsecutiveEmergencySpawns}");
            SetText(spawnRecoveryValue, $"Spawn recovery counter: {state.SpawnRecoveryCounter}");
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
