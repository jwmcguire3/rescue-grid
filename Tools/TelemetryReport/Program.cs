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
                sb.AppendLine($"| Vine growth events | {lvl.VineGrowthCount} |");

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

                    case WaterRiseEvent:
                        lvl.WaterRiseCount++;
                        break;

                    case VineGrowthEvent:
                        lvl.VineGrowthCount++;
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
            IEnumerable<string> parts = reasons
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}: {kv.Value}");
            return string.Join(", ", parts);
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
        public List<int> WinActionCounts { get; } = new List<int>();
        public List<int> LossActionCounts { get; } = new List<int>();
        public List<long> IdleTimes { get; } = new List<long>();
        public List<long> FirstActionTimes { get; } = new List<long>();
        public Dictionary<string, int> LossReasons { get; } = new Dictionary<string, int>();
    }
}
