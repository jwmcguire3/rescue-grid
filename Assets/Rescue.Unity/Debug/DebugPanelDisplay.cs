#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !CAPTURE_BUILD
using System.Collections.Immutable;
using Rescue.Core.Pipeline;
using Rescue.Core.Rules;
using Rescue.Core.State;
using Rescue.Unity.Telemetry;
using UnityEngine;

namespace Rescue.Unity.Debugging
{
    internal static class DebugPanelDisplay
    {
        public static string FormatDockContents(Dock dock)
        {
            string[] contents = new string[dock.Slots.Length];
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                contents[i] = dock.Slots[i]?.ToString() ?? "-";
            }

            return string.Join(" ", contents);
        }

        public static string SerializeRngState(Rescue.Core.Rng.RngState rngState)
        {
            return $"{rngState.S0}:{rngState.S1}";
        }

        public static string GetNextFloodRowLabel(GameState state)
        {
            int? nextFloodRow = WaterHelpers.GetNextFloodRow(state.Board, state.Water);
            if (!nextFloodRow.HasValue)
            {
                return "none";
            }

            return nextFloodRow.Value.ToString();
        }

        public static string GetWaterForecastSummary(GameState state)
        {
            int? nextFloodRow = WaterHelpers.GetNextFloodRow(state.Board, state.Water);
            if (!nextFloodRow.HasValue)
            {
                return "board fully flooded";
            }

            if (state.Water.PauseUntilFirstAction)
            {
                return $"row {nextFloodRow.Value} will flood on the first valid action";
            }

            if (state.Water.RiseInterval <= 0)
            {
                return $"row {nextFloodRow.Value} queued, water disabled";
            }

            string actionLabel = state.Water.ActionsUntilRise == 1 ? "action" : "actions";
            return $"row {nextFloodRow.Value} in {state.Water.ActionsUntilRise} {actionLabel}";
        }

        public static string GetNearRescueTargetsSummary(GameState state)
        {
            ImmutableArray<string>.Builder targetIds = ImmutableArray.CreateBuilder<string>();
            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                if (!target.Extracted && target.OneClearAway)
                {
                    targetIds.Add(target.TargetId);
                }
            }

            return targetIds.Count == 0 ? "none" : string.Join(", ", targetIds);
        }

        public static string FormatCoord(TileCoord? coord)
        {
            return coord.HasValue ? $"({coord.Value.Row}, {coord.Value.Col})" : "none";
        }

        public static string FormatCoord(TileCoord coord)
        {
            return $"({coord.Row}, {coord.Col})";
        }

        public static DebugEventLogLine BuildEventLogLine(ActionEvent actionEvent)
        {
            return new DebugEventLogLine(
                actionEvent.GetType().Name,
                DescribeActionEvent(actionEvent),
                DetermineEventColor(actionEvent),
                TelemetryEventClassifier.IsDevOnly(actionEvent));
        }

        private static string DescribeActionEvent(ActionEvent actionEvent)
        {
            return actionEvent switch
            {
                InvalidInput invalid => $"Invalid input at {FormatCoord(invalid.TappedCoord)} ({invalid.Reason})",
                GroupRemoved removed => $"Removed {removed.Type} group of {removed.Coords.Length}",
                DockInserted inserted => $"Dock inserted {inserted.Pieces.Length}; occupancy {inserted.OccupancyAfterInsert}",
                DockCleared cleared => $"Dock cleared {cleared.SetsCleared}x {cleared.Type}",
                DockJamTriggered triggered => $"Dock jam triggered ({triggered.OverflowCount} overflow)",
                WaterWarning warning => $"Water warning: {warning.ActionsUntilRise} action left; row {warning.NextFloodRow}",
                WaterRose rose => $"Water rose into row {rose.FloodedRow}",
                VinePreviewChanged preview => $"Vine preview -> {FormatCoord(preview.PendingTile)}",
                VineGrown grown => $"Vine grew at {FormatCoord(grown.Coord)}",
                TargetExtracted extracted => $"Target {extracted.TargetId} extracted",
                TargetOneClearAway almost => $"Target {almost.TargetId} is one clear away",
                DebugSpawnOverrideApplied applied => $"Spawn override active (requested={applied.EmergencyRequested}, applied={applied.EmergencyApplied}, chance={applied.EffectiveAssistanceChance:0.##})",
                Lost lost => $"Loss: {lost.Outcome}",
                Won won => $"Win after {won.TotalActions} actions",
                Spawned spawned => $"Spawned {spawned.Pieces.Length} pieces",
                _ => actionEvent.ToString() ?? actionEvent.GetType().Name,
            };
        }

        private static Color DetermineEventColor(ActionEvent actionEvent)
        {
            return actionEvent switch
            {
                Lost => new Color(0.90f, 0.33f, 0.29f),
                Won => new Color(0.43f, 0.78f, 0.47f),
                WaterWarning or DockWarningChanged or VinePreviewChanged => new Color(0.98f, 0.79f, 0.31f),
                WaterRose or VineGrown or DockJamTriggered => new Color(0.45f, 0.73f, 0.95f),
                DebugSpawnOverrideApplied => new Color(0.88f, 0.55f, 0.97f),
                _ => new Color(0.88f, 0.92f, 0.96f),
            };
        }
    }
}
#endif
