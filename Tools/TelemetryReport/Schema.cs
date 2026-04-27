using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TelemetryReport
{
    internal interface ITelemetryEvent
    {
        string EventType { get; }
        int SchemaVersion { get; }
        string LevelId { get; }
        long TimestampMs { get; }
    }

    internal sealed class RawEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = string.Empty;
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
    }

    internal sealed class LevelStartEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "level_start";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public ulong Seed { get; set; }
        public double AssistanceChance { get; set; }
        public int RiseInterval { get; set; }
        public int InitialFloodedRows { get; set; }
        public int VineGrowthThreshold { get; set; }
        public int TargetCount { get; set; }
        public string WaterMode { get; set; } = string.Empty;
    }

    internal sealed class LevelWinEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "level_win";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionCount { get; set; }
        public string[] ExtractedTargetOrder { get; set; } = Array.Empty<string>();
        public bool UndoUsed { get; set; }
    }

    internal sealed class LevelLossEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "level_loss";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionCount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? LostTargetId { get; set; }
    }

    internal sealed class IdleTimeEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "idle_time";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
        public long IdleMs { get; set; }
    }

    internal sealed class TimeToFirstActionEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "time_to_first_action";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public long FirstActionMs { get; set; }
    }

    internal sealed class InvalidTapEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "invalid_tap";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    internal sealed class WaterRiseEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "water_rise";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
        public int NewFloodedRows { get; set; }
    }

    internal sealed class WaterForecastEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "water_forecast";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
        public string WaterMode { get; set; } = string.Empty;
        public int? NextFloodRow { get; set; }
        public bool ForecastAvailable { get; set; }
        public int ActionsUntilRise { get; set; }
        public string Timing { get; set; } = "PostAction";
    }

    internal sealed class DockOccupancyEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "dock_occupancy";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
        public int Occupancy { get; set; }
        public string WarningLevel { get; set; } = string.Empty;
        public int DockSize { get; set; }
    }

    internal sealed class VineGrowthEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "vine_growth";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
    }

    internal sealed class VinePreviewEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "vine_preview";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
    }

    internal sealed class TargetStateTransitionEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "target_state_transition";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
        public string TargetId { get; set; } = string.Empty;
        public string FromState { get; set; } = string.Empty;
        public string ToState { get; set; } = string.Empty;
    }

    internal sealed class FinalRescueEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "final_rescue";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
        public string? TargetId { get; set; }
        public bool DockOverflowWouldHaveFailed { get; set; }
        public bool HazardAdvanceSkipped { get; set; }
    }

    internal sealed class FinalRescueDockOverflowOverrideEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "final_rescue_dock_overflow_override";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
        public int OverflowCount { get; set; }
    }

    internal sealed class HazardAdvanceSkippedEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "hazard_advance_skipped";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    internal sealed class GraceEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "grace";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
        public string TargetId { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
    }

    internal sealed class AssistedSpawnEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "assisted_spawn";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public int SpawnCount { get; set; }
        public bool EmergencyRequested { get; set; }
        public bool EmergencyApplied { get; set; }
        public double EffectiveAssistanceChance { get; set; }
        public AssistedSpawnPieceTelemetry[]? Pieces { get; set; }
    }

    internal sealed class AssistedSpawnPieceTelemetry
    {
        public int LineageId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string[] Reasons { get; set; } = Array.Empty<string>();
    }

    internal sealed class AssistedSpawnFollowUpEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "assisted_spawn_follow_up";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int OriginalActionIndex { get; set; }
        public int FollowUpActionIndex { get; set; }
        public string UsedType { get; set; } = string.Empty;
        public int SpawnLineageId { get; set; }
    }

    internal sealed class DeadboardLikeStateEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "deadboard_like_state";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndex { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? TargetId { get; set; }
    }

    internal sealed class UndoUsedEvent : ITelemetryEvent
    {
        public string EventType { get; set; } = "undo_used";
        public int SchemaVersion { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public long TimestampMs { get; set; }
        public int ActionIndexBeforeUndo { get; set; }
    }

    // ── deserializer ──────────────────────────────────────────────────────────

    internal sealed class TelemetryEventConverter : JsonConverter<ITelemetryEvent>
    {
        private static readonly JsonSerializerOptions InnerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        public static readonly JsonSerializerOptions OuterOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new TelemetryEventConverter() },
        };

        public override ITelemetryEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("eventType", out JsonElement eventTypeEl)
                && !root.TryGetProperty("EventType", out eventTypeEl))
            {
                throw new JsonException("Missing eventType field.");
            }

            if (!root.TryGetProperty("schemaVersion", out JsonElement versionEl)
                && !root.TryGetProperty("SchemaVersion", out versionEl))
            {
                throw new JsonException("Missing schemaVersion field.");
            }

            int schemaVersion = versionEl.GetInt32();
            if (schemaVersion != 1)
            {
                throw new UnsupportedSchemaVersionException(schemaVersion);
            }

            string rawJson = root.GetRawText();
            return eventTypeEl.GetString() switch
            {
                "level_start" => JsonSerializer.Deserialize<LevelStartEvent>(rawJson, InnerOptions),
                "level_win" => JsonSerializer.Deserialize<LevelWinEvent>(rawJson, InnerOptions),
                "level_loss" => JsonSerializer.Deserialize<LevelLossEvent>(rawJson, InnerOptions),
                "idle_time" => JsonSerializer.Deserialize<IdleTimeEvent>(rawJson, InnerOptions),
                "time_to_first_action" => JsonSerializer.Deserialize<TimeToFirstActionEvent>(rawJson, InnerOptions),
                "invalid_tap" => JsonSerializer.Deserialize<InvalidTapEvent>(rawJson, InnerOptions),
                "dock_occupancy" => JsonSerializer.Deserialize<DockOccupancyEvent>(rawJson, InnerOptions),
                "water_forecast" => JsonSerializer.Deserialize<WaterForecastEvent>(rawJson, InnerOptions),
                "water_rise" => JsonSerializer.Deserialize<WaterRiseEvent>(rawJson, InnerOptions),
                "vine_growth" => JsonSerializer.Deserialize<VineGrowthEvent>(rawJson, InnerOptions),
                "vine_preview" => JsonSerializer.Deserialize<VinePreviewEvent>(rawJson, InnerOptions),
                "target_state_transition" => JsonSerializer.Deserialize<TargetStateTransitionEvent>(rawJson, InnerOptions),
                "final_rescue" => JsonSerializer.Deserialize<FinalRescueEvent>(rawJson, InnerOptions),
                "final_rescue_dock_overflow_override" => JsonSerializer.Deserialize<FinalRescueDockOverflowOverrideEvent>(rawJson, InnerOptions),
                "hazard_advance_skipped" => JsonSerializer.Deserialize<HazardAdvanceSkippedEvent>(rawJson, InnerOptions),
                "grace" => JsonSerializer.Deserialize<GraceEvent>(rawJson, InnerOptions),
                "assisted_spawn" => JsonSerializer.Deserialize<AssistedSpawnEvent>(rawJson, InnerOptions),
                "assisted_spawn_follow_up" => JsonSerializer.Deserialize<AssistedSpawnFollowUpEvent>(rawJson, InnerOptions),
                "deadboard_like_state" => JsonSerializer.Deserialize<DeadboardLikeStateEvent>(rawJson, InnerOptions),
                "undo_used" => JsonSerializer.Deserialize<UndoUsedEvent>(rawJson, InnerOptions),
                _ => JsonSerializer.Deserialize<RawEvent>(rawJson, InnerOptions),
            };
        }

        public override void Write(Utf8JsonWriter writer, ITelemetryEvent value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), InnerOptions);
        }
    }

    internal sealed class UnsupportedSchemaVersionException : Exception
    {
        public int Version { get; }

        public UnsupportedSchemaVersionException(int version)
            : base($"Unsupported telemetry schema version: {version}. This tool supports version 1. Re-export telemetry or update the tool.")
        {
            Version = version;
        }
    }
}
