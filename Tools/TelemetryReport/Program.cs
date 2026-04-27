using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace TelemetryReport
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine("  TelemetryReport report <session-jsonl-path>");
                Console.Error.WriteLine("  TelemetryReport report-all <telemetry-dir>");
                return 1;
            }

            string command = args[0];
            string target = args[1];

            try
            {
                switch (command)
                {
                    case "report":
                        return RunReport(new[] { target });

                    case "report-all":
                        if (!Directory.Exists(target))
                        {
                            Console.Error.WriteLine($"Directory not found: {target}");
                            return 1;
                        }

                        string[] files = Directory.GetFiles(target, "*.jsonl");
                        if (files.Length == 0)
                        {
                            Console.Error.WriteLine($"No .jsonl files found in: {target}");
                            return 1;
                        }

                        return RunReport(files);

                    default:
                        Console.Error.WriteLine($"Unknown command: {command}");
                        return 1;
                }
            }
            catch (UnsupportedSchemaVersionException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static int RunReport(string[] paths)
        {
            List<ITelemetryEvent> allEvents = new List<ITelemetryEvent>();

            foreach (string path in paths)
            {
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine($"File not found: {path}");
                    return 1;
                }

                LoadEvents(path, allEvents);
            }

            string report = BuildReport(allEvents, paths);
            Console.WriteLine(report);
            return 0;
        }

        private static void LoadEvents(string path, List<ITelemetryEvent> target)
        {
            string[] lines = File.ReadAllLines(path);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                ITelemetryEvent? ev = JsonSerializer.Deserialize<ITelemetryEvent>(
                    line, TelemetryEventConverter.OuterOptions);
                if (ev is not null) target.Add(ev);
            }
        }

        private static string BuildReport(List<ITelemetryEvent> events, string[] sourcePaths)
        {
            SessionStats session = Aggregate(events);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Telemetry Report");
            sb.AppendLine();

            if (sourcePaths.Length == 1)
            {
                sb.AppendLine($"**Source:** `{sourcePaths[0]}`");
            }
            else
            {
                sb.AppendLine($"**Sources:** {sourcePaths.Length} session files");
            }

            sb.AppendLine();

            // Session totals
            sb.AppendLine("## Session Totals");
            sb.AppendLine();
            sb.AppendLine($"- Total levels attempted: {session.TotalAttempts}");
            sb.AppendLine($"- Total wins: {session.TotalWins}");
            sb.AppendLine($"- Total losses: {session.TotalLosses}");
            sb.AppendLine($"- Session win rate: {Pct(session.TotalWins, session.TotalAttempts)}");
            sb.AppendLine();

            // Per-level breakdown
            sb.AppendLine("## Per-Level Summary");
            sb.AppendLine();

            foreach ((string levelId, LevelStats lvl) in session.ByLevel.OrderBy(kv => kv.Key))
            {
                sb.AppendLine($"### Level `{levelId}`");
                sb.AppendLine();
                sb.AppendLine($"| Metric | Value |");
                sb.AppendLine($"|--------|-------|");
                sb.AppendLine($"| Attempts | {lvl.Attempts} |");
                sb.AppendLine($"| Wins | {lvl.Wins} |");
                sb.AppendLine($"| Win rate | {Pct(lvl.Wins, lvl.Attempts)} |");
                sb.AppendLine($"| Avg actions (wins) | {Avg(lvl.WinActionCounts)} |");
                sb.AppendLine($"| Avg actions (losses) | {Avg(lvl.LossActionCounts)} |");
                sb.AppendLine($"| Undo usage rate | {Pct(lvl.UndoUsedCount, lvl.Wins)} |");
                sb.AppendLine($"| Invalid taps | {lvl.InvalidTapCount} |");
                sb.AppendLine($"| Invalid tap rate | {RatePer(lvl.InvalidTapCount, lvl.TotalActions)} /action |");
                sb.AppendLine($"| Water rises | {lvl.WaterRiseCount} |");
                sb.AppendLine($"| Water modes | {FormatReasons(lvl.WaterModes)} |");
                sb.AppendLine($"| Last next flood row | {FormatNullableInt(lvl.LastNextFloodRow)} |");
                sb.AppendLine($"| Water forecast timings | {FormatReasons(lvl.WaterForecastTimings)} |");
                sb.AppendLine($"| Last pre-action flood row | {FormatNullableInt(lvl.LastPreActionNextFloodRow)} |");
                sb.AppendLine($"| Last post-action flood row | {FormatNullableInt(lvl.LastPostActionNextFloodRow)} |");
                sb.AppendLine($"| Vine growth events | {lvl.VineGrowthCount} |");
                sb.AppendLine($"| Vine preview events | {lvl.VinePreviewCount} |");
                sb.AppendLine($"| Target transitions | {FormatReasons(lvl.TargetTransitions)} |");
                sb.AppendLine($"| One-clear-away first action | {FormatTargetActions(lvl.OneClearAwayActionByTarget)} |");
                sb.AppendLine($"| Extraction latch first action | {FormatTargetActions(lvl.ExtractionLatchActionByTarget)} |");
                sb.AppendLine($"| Final rescue actions | {FormatIntList(lvl.FinalRescueActions)} |");
                sb.AppendLine($"| Final rescue dock overrides | {lvl.FinalRescueDockOverrideCount} |");
                sb.AppendLine($"| Hazard skips after final rescue | {lvl.HazardAdvanceSkippedCount} |");
                sb.AppendLine($"| Assisted spawns | {lvl.AssistedSpawnCount} ({lvl.AssistedSpawnPieces} pieces) |");
                sb.AppendLine($"| Assisted spawn reasons | {FormatReasons(lvl.AssistedSpawnReasons)} |");
                sb.AppendLine($"| Assisted follow-up uses <=2 actions | {lvl.AssistedSpawnFollowUpCount} |");
                sb.AppendLine($"| Grace outcomes | {FormatReasons(lvl.GraceOutcomes)} |");
                sb.AppendLine($"| Dock warning states | {FormatReasons(lvl.DockWarningStates)} |");
                sb.AppendLine($"| Peak dock occupancy | {FormatNullableInt(lvl.PeakDockOccupancy)} |");
                sb.AppendLine($"| Deadboard-like states | {lvl.DeadboardLikeCount} |");
                sb.AppendLine($"| Deadboard reasons | {FormatReasons(lvl.DeadboardReasons)} |");

                if (lvl.IdleTimes.Count > 0)
                {
                    sb.AppendLine($"| Avg idle time/action | {(long)lvl.IdleTimes.Average()} ms |");
                }

                if (lvl.FirstActionTimes.Count > 0)
                {
                    List<long> sorted = new List<long>(lvl.FirstActionTimes);
                    sorted.Sort();
                    sb.AppendLine($"| Time-to-first-action (min) | {sorted[0]} ms |");
                    sb.AppendLine($"| Time-to-first-action (median) | {Median(sorted)} ms |");
                    sb.AppendLine($"| Time-to-first-action (max) | {sorted[sorted.Count - 1]} ms |");
                }

                if (lvl.LossReasons.Count > 0)
                {
                    sb.AppendLine($"| Loss reasons | {FormatReasons(lvl.LossReasons)} |");
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        private static SessionStats Aggregate(List<ITelemetryEvent> events)
        {
            SessionStats session = new SessionStats();

            foreach (ITelemetryEvent ev in events)
            {
                LevelStats lvl = session.GetOrCreate(ev.LevelId);

                switch (ev)
                {
                    case LevelStartEvent:
                        lvl.Attempts++;
                        session.TotalAttempts++;
                        LevelStartEvent start = (LevelStartEvent)ev;
                        if (!string.IsNullOrEmpty(start.WaterMode))
                        {
                            Increment(lvl.WaterModes, start.WaterMode);
                        }
                        break;

                    case LevelWinEvent win:
                        lvl.Wins++;
                        session.TotalWins++;
                        lvl.WinActionCounts.Add(win.ActionCount);
                        if (win.UndoUsed) lvl.UndoUsedCount++;
                        break;

                    case LevelLossEvent loss:
                        session.TotalLosses++;
                        lvl.LossActionCounts.Add(loss.ActionCount);
                        if (!lvl.LossReasons.TryGetValue(loss.Reason, out _))
                            lvl.LossReasons[loss.Reason] = 0;
                        lvl.LossReasons[loss.Reason]++;
                        break;

                    case IdleTimeEvent idle:
                        lvl.IdleTimes.Add(idle.IdleMs);
                        lvl.TotalActions++;
                        break;

                    case TimeToFirstActionEvent first:
                        lvl.FirstActionTimes.Add(first.FirstActionMs);
                        break;

                    case InvalidTapEvent:
                        lvl.InvalidTapCount++;
                        break;

                    case DockOccupancyEvent dock:
                        Increment(lvl.DockWarningStates, dock.WarningLevel);
                        lvl.PeakDockOccupancy = !lvl.PeakDockOccupancy.HasValue
                            ? dock.Occupancy
                            : Math.Max(lvl.PeakDockOccupancy.Value, dock.Occupancy);
                        break;

                    case WaterForecastEvent forecast:
                        if (!string.IsNullOrEmpty(forecast.WaterMode))
                        {
                            Increment(lvl.WaterModes, forecast.WaterMode);
                        }

                        lvl.LastNextFloodRow = forecast.NextFloodRow;
                        Increment(lvl.WaterForecastTimings, forecast.Timing);
                        if (string.Equals(forecast.Timing, "PreAction", StringComparison.Ordinal))
                        {
                            lvl.LastPreActionNextFloodRow = forecast.NextFloodRow;
                        }
                        else if (string.Equals(forecast.Timing, "PostAction", StringComparison.Ordinal))
                        {
                            lvl.LastPostActionNextFloodRow = forecast.NextFloodRow;
                        }
                        break;

                    case WaterRiseEvent:
                        lvl.WaterRiseCount++;
                        break;

                    case VineGrowthEvent:
                        lvl.VineGrowthCount++;
                        break;

                    case VinePreviewEvent:
                        lvl.VinePreviewCount++;
                        break;

                    case TargetStateTransitionEvent transition:
                        string transitionKey = $"{transition.FromState}->{transition.ToState}";
                        Increment(lvl.TargetTransitions, transitionKey);
                        if (transition.ToState == "OneClearAway"
                            && !lvl.OneClearAwayActionByTarget.ContainsKey(transition.TargetId))
                        {
                            lvl.OneClearAwayActionByTarget[transition.TargetId] = transition.ActionIndex;
                        }

                        if (transition.ToState == "ExtractableLatched"
                            && !lvl.ExtractionLatchActionByTarget.ContainsKey(transition.TargetId))
                        {
                            lvl.ExtractionLatchActionByTarget[transition.TargetId] = transition.ActionIndex;
                        }
                        break;

                    case FinalRescueEvent final:
                        lvl.FinalRescueActions.Add(final.ActionIndex);
                        break;

                    case FinalRescueDockOverflowOverrideEvent:
                        lvl.FinalRescueDockOverrideCount++;
                        break;

                    case HazardAdvanceSkippedEvent skipped when skipped.Reason == "final_rescue":
                        lvl.HazardAdvanceSkippedCount++;
                        break;

                    case GraceEvent grace:
                        Increment(lvl.GraceOutcomes, grace.Outcome);
                        break;

                    case AssistedSpawnEvent assisted:
                        lvl.AssistedSpawnCount++;
                        lvl.AssistedSpawnPieces += assisted.SpawnCount;
                        if (assisted.Pieces is { Length: > 0 })
                        {
                            foreach (AssistedSpawnPieceTelemetry piece in assisted.Pieces)
                            {
                                foreach (string reason in piece.Reasons)
                                {
                                    Increment(lvl.AssistedSpawnReasons, reason);
                                }
                            }
                        }
                        else
                        {
                            Increment(lvl.AssistedSpawnReasons, assisted.Reason);
                        }
                        break;

                    case AssistedSpawnFollowUpEvent:
                        lvl.AssistedSpawnFollowUpCount++;
                        break;

                    case DeadboardLikeStateEvent:
                        lvl.DeadboardLikeCount++;
                        Increment(lvl.DeadboardReasons, ((DeadboardLikeStateEvent)ev).Reason);
                        break;
                }
            }

            return session;
        }

        // ── formatting helpers ────────────────────────────────────────────────

        private static string Pct(int numerator, int denominator) =>
            denominator == 0 ? "N/A" : $"{numerator * 100.0 / denominator:F1}%";

        private static string Avg(List<int> values) =>
            values.Count == 0 ? "N/A" : $"{values.Average():F1}";

        private static string RatePer(int count, int total) =>
            total == 0 ? "N/A" : $"{(double)count / total:F2}";

        private static long Median(List<long> sorted) =>
            sorted.Count % 2 == 0
                ? (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2
                : sorted[sorted.Count / 2];

        private static string FormatReasons(Dictionary<string, int> reasons)
        {
            if (reasons.Count == 0)
            {
                return "N/A";
            }

            IEnumerable<string> parts = reasons
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}: {kv.Value}");
            return string.Join(", ", parts);
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            if (!counts.TryGetValue(key, out _))
            {
                counts[key] = 0;
            }

            counts[key]++;
        }

        private static string FormatNullableInt(int? value) =>
            value.HasValue ? value.Value.ToString() : "N/A";

        private static string FormatIntList(List<int> values) =>
            values.Count == 0 ? "N/A" : string.Join(", ", values);

        private static string FormatTargetActions(Dictionary<string, int> actions)
        {
            if (actions.Count == 0)
            {
                return "N/A";
            }

            return string.Join(", ", actions.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}: {kv.Value}"));
        }
    }

    internal sealed class SessionStats
    {
        public int TotalAttempts;
        public int TotalWins;
        public int TotalLosses;
        public Dictionary<string, LevelStats> ByLevel { get; } = new Dictionary<string, LevelStats>();

        public LevelStats GetOrCreate(string levelId)
        {
            if (!ByLevel.TryGetValue(levelId, out LevelStats? stats))
            {
                stats = new LevelStats();
                ByLevel[levelId] = stats;
            }

            return stats;
        }
    }

    internal sealed class LevelStats
    {
        public int Attempts;
        public int Wins;
        public int UndoUsedCount;
        public int TotalActions;
        public int InvalidTapCount;
        public int WaterRiseCount;
        public int VineGrowthCount;
        public int VinePreviewCount;
        public int FinalRescueDockOverrideCount;
        public int HazardAdvanceSkippedCount;
        public int AssistedSpawnCount;
        public int AssistedSpawnPieces;
        public int AssistedSpawnFollowUpCount;
        public int DeadboardLikeCount;
        public int? LastNextFloodRow;
        public int? LastPreActionNextFloodRow;
        public int? LastPostActionNextFloodRow;
        public int? PeakDockOccupancy;
        public List<int> WinActionCounts { get; } = new List<int>();
        public List<int> LossActionCounts { get; } = new List<int>();
        public List<int> FinalRescueActions { get; } = new List<int>();
        public List<long> IdleTimes { get; } = new List<long>();
        public List<long> FirstActionTimes { get; } = new List<long>();
        public Dictionary<string, int> LossReasons { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> WaterModes { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> WaterForecastTimings { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> TargetTransitions { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> GraceOutcomes { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> AssistedSpawnReasons { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> DockWarningStates { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> DeadboardReasons { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> OneClearAwayActionByTarget { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> ExtractionLatchActionByTarget { get; } = new Dictionary<string, int>();
    }
}
