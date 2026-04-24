using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rescue.Core.Rng;
using Rescue.Core.State;

namespace Rescue.Telemetry
{
    public interface ITelemetryEvent
    {
        string EventType { get; }
        int SchemaVersion { get; }
        string LevelId { get; }
        long TimestampMs { get; }
    }

    public static class LossReasons
    {
        public const string DockOverflow = "dock_overflow";
        public const string WaterOnTarget = "water_on_target";
        public const string DockJamUnresolved = "dock_jam_unresolved";
        public const string ManualAbandon = "manual_abandon";
    }

    public static class DockJamResolutions
    {
        public const string TripleCleared = "triple_cleared";
        public const string FailedToClear = "failed_to_clear";
    }

    public static class InvalidTapReasons
    {
        public const string NoGroup = "no_group";
        public const string IsolatedTile = "isolated_tile";
        public const string BlockerTile = "blocker_tile";
        public const string FloodedTile = "flooded_tile";
        public const string TargetTile = "target_tile";
        public const string OutOfBounds = "out_of_bounds";
        public const string Frozen = "frozen";
    }

    public sealed record LevelStartEvent(
        string LevelId,
        long TimestampMs,
        ulong Seed,
        double AssistanceChance,
        int RiseInterval,
        int InitialFloodedRows,
        int VineGrowthThreshold,
        int TargetCount) : ITelemetryEvent
    {
        public string EventType => "level_start";
        public int SchemaVersion => 1;
    }

    public sealed record LevelWinEvent(
        string LevelId,
        long TimestampMs,
        int ActionCount,
        string[] ExtractedTargetOrder,
        bool UndoUsed) : ITelemetryEvent
    {
        public string EventType => "level_win";
        public int SchemaVersion => 1;
    }

    public sealed record LevelLossEvent(
        string LevelId,
        long TimestampMs,
        int ActionCount,
        string Reason,
        string? LostTargetId) : ITelemetryEvent
    {
        public string EventType => "level_loss";
        public int SchemaVersion => 1;
    }

    public sealed record DockOccupancyEvent(
        string LevelId,
        long TimestampMs,
        int ActionIndex,
        int Occupancy,
        DockWarningLevel WarningLevel) : ITelemetryEvent
    {
        public string EventType => "dock_occupancy";
        public int SchemaVersion => 1;
    }

    public sealed record WaterRiseEvent(
        string LevelId,
        long TimestampMs,
        int ActionIndex,
        int NewFloodedRows) : ITelemetryEvent
    {
        public string EventType => "water_rise";
        public int SchemaVersion => 1;
    }

    public sealed record VineGrowthEvent(
        string LevelId,
        long TimestampMs,
        int ActionIndex,
        TileCoord GrownTile) : ITelemetryEvent
    {
        public string EventType => "vine_growth";
        public int SchemaVersion => 1;
    }

    public sealed record UndoUsedEvent(
        string LevelId,
        long TimestampMs,
        int ActionIndexBeforeUndo) : ITelemetryEvent
    {
        public string EventType => "undo_used";
        public int SchemaVersion => 1;
    }

    public sealed record TargetExtractedEvent(
        string LevelId,
        long TimestampMs,
        int ActionIndex,
        string TargetId) : ITelemetryEvent
    {
        public string EventType => "target_extracted";
        public int SchemaVersion => 1;
    }

    public sealed record TargetLostEvent(
        string LevelId,
        long TimestampMs,
        int ActionIndex,
        string TargetId) : ITelemetryEvent
    {
        public string EventType => "target_lost";
        public int SchemaVersion => 1;
    }

    public sealed record InvalidTapEvent(
        string LevelId,
        long TimestampMs,
        TileCoord TappedCoord,
        string Reason) : ITelemetryEvent
    {
        public string EventType => "invalid_tap";
        public int SchemaVersion => 1;
    }

    public sealed record IdleTimeEvent(
        string LevelId,
        long TimestampMs,
        int ActionIndex,
        long IdleMs) : ITelemetryEvent
    {
        public string EventType => "idle_time";
        public int SchemaVersion => 1;
    }

    public sealed record TimeToFirstActionEvent(
        string LevelId,
        long TimestampMs,
        long FirstActionMs) : ITelemetryEvent
    {
        public string EventType => "time_to_first_action";
        public int SchemaVersion => 1;
    }

    public sealed record HazardProximityToTargetEvent(
        string LevelId,
        long TimestampMs,
        int ActionIndex,
        string TargetId,
        int WaterDistanceRows,
        bool VineAdjacency) : ITelemetryEvent
    {
        public string EventType => "hazard_proximity_to_target";
        public int SchemaVersion => 1;
    }

    public sealed record DockJamTriggeredEvent(
        string LevelId,
        long TimestampMs,
        int ActionIndex) : ITelemetryEvent
    {
        public string EventType => "dock_jam_triggered";
        public int SchemaVersion => 1;
    }

    public sealed record DockJamResolvedEvent(
        string LevelId,
        long TimestampMs,
        int ActionIndex,
        string ResolvedBy) : ITelemetryEvent
    {
        public string EventType => "dock_jam_resolved";
        public int SchemaVersion => 1;
    }

    public sealed record CaptureSnapshotEvent(
        string LevelId,
        long TimestampMs,
        int ActionIndex,
        string GameStateJson) : ITelemetryEvent
    {
        public string EventType => "capture_snapshot";
        public int SchemaVersion => 1;
    }

    public sealed record TuningChangedEvent(
        string LevelId,
        long TimestampMs,
        ulong Seed,
        string ChangeSource,
        string? PresetName,
        int? WaterRiseInterval,
        int? InitialFloodedRows,
        double? AssistanceChance,
        bool? ForceEmergencyAssistance,
        bool? DockJamEnabled,
        int? DockSize,
        int? DefaultCrateHp,
        int? VineGrowthThreshold) : ITelemetryEvent
    {
        public string EventType => "tuning_changed";
        public int SchemaVersion => 1;
    }

    public sealed record ActionTakenEvent(
        string LevelId,
        long TimestampMs,
        int ActionIndex,
        TileCoord Input,
        ulong Seed,
        RngState RngStateBefore,
        RngState RngStateAfter,
        bool UndoAvailable,
        SpawnOverride? DebugSpawnOverride) : ITelemetryEvent
    {
        public string EventType => "action_taken";
        public int SchemaVersion => 1;
    }

    public sealed class TelemetryJsonConverter : JsonConverter<ITelemetryEvent>
    {
        private static readonly JsonSerializerOptions InnerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        public static readonly JsonSerializerOptions OuterOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new TelemetryJsonConverter(), new JsonStringEnumConverter() },
        };

        public override ITelemetryEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("eventType", out JsonElement eventTypeEl))
            {
                throw new JsonException("Missing required field: eventType");
            }

            string rawJson = root.GetRawText();
            return eventTypeEl.GetString() switch
            {
                "level_start" => JsonSerializer.Deserialize<LevelStartEvent>(rawJson, InnerOptions),
                "level_win" => JsonSerializer.Deserialize<LevelWinEvent>(rawJson, InnerOptions),
                "level_loss" => JsonSerializer.Deserialize<LevelLossEvent>(rawJson, InnerOptions),
                "dock_occupancy" => JsonSerializer.Deserialize<DockOccupancyEvent>(rawJson, InnerOptions),
                "water_rise" => JsonSerializer.Deserialize<WaterRiseEvent>(rawJson, InnerOptions),
                "vine_growth" => JsonSerializer.Deserialize<VineGrowthEvent>(rawJson, InnerOptions),
                "undo_used" => JsonSerializer.Deserialize<UndoUsedEvent>(rawJson, InnerOptions),
                "target_extracted" => JsonSerializer.Deserialize<TargetExtractedEvent>(rawJson, InnerOptions),
                "target_lost" => JsonSerializer.Deserialize<TargetLostEvent>(rawJson, InnerOptions),
                "invalid_tap" => JsonSerializer.Deserialize<InvalidTapEvent>(rawJson, InnerOptions),
                "idle_time" => JsonSerializer.Deserialize<IdleTimeEvent>(rawJson, InnerOptions),
                "time_to_first_action" => JsonSerializer.Deserialize<TimeToFirstActionEvent>(rawJson, InnerOptions),
                "hazard_proximity_to_target" => JsonSerializer.Deserialize<HazardProximityToTargetEvent>(rawJson, InnerOptions),
                "dock_jam_triggered" => JsonSerializer.Deserialize<DockJamTriggeredEvent>(rawJson, InnerOptions),
                "dock_jam_resolved" => JsonSerializer.Deserialize<DockJamResolvedEvent>(rawJson, InnerOptions),
                "capture_snapshot" => JsonSerializer.Deserialize<CaptureSnapshotEvent>(rawJson, InnerOptions),
                "tuning_changed" => JsonSerializer.Deserialize<TuningChangedEvent>(rawJson, InnerOptions),
                "action_taken" => JsonSerializer.Deserialize<ActionTakenEvent>(rawJson, InnerOptions),
                var unknown => throw new JsonException($"Unknown telemetry event type: {unknown}"),
            };
        }

        public override void Write(Utf8JsonWriter writer, ITelemetryEvent value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), InnerOptions);
        }
    }
}
