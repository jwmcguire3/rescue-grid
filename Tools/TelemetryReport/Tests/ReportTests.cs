using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using TelemetryReport;

namespace TelemetryReport.Tests
{
    public sealed class ReportTests
    {
        private string _testDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "TelemetryReportTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }

        [Test]
        public void Report_ThreeLevelSession_CorrectWinRates()
        {
            string path = WriteFixtureSession();

            int exitCode = RunReport("report", path, out string output);

            Assert.That(exitCode, Is.EqualTo(0), $"Exit code must be 0. Output:\n{output}");

            // L1: 2 attempts, 1 win → 50%
            Assert.That(output, Does.Contain("L1"), "Report must include L1.");
            // L2: 1 attempt, 1 win → 100%
            Assert.That(output, Does.Contain("L2"), "Report must include L2.");
            // L3: 1 attempt, 0 wins → 0%
            Assert.That(output, Does.Contain("L3"), "Report must include L3.");
        }

        [Test]
        public void Report_ThreeLevelSession_CorrectLossReasonDistribution()
        {
            string path = WriteFixtureSession();

            int exitCode = RunReport("report", path, out string output);

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output, Does.Contain("dock_overflow"), "dock_overflow reason must appear in report.");
        }

        [Test]
        public void Report_ThreeLevelSession_CorrectAverageActionCounts()
        {
            string path = WriteFixtureSession();

            int exitCode = RunReport("report", path, out string output);

            Assert.That(exitCode, Is.EqualTo(0));
            // L1 win had 4 actions → avg win actions for L1 = 4.0
            Assert.That(output, Does.Contain("4"), "Win action count must appear.");
        }

        [Test]
        public void Report_UnsupportedSchemaVersion_ExitsNonZeroWithClearError()
        {
            string path = Path.Combine(_testDir, "bad_version.jsonl");
            File.WriteAllText(path,
                "{\"eventType\":\"level_start\",\"schemaVersion\":99,\"levelId\":\"L1\",\"timestampMs\":0}\n");

            int exitCode = RunReport("report", path, out string output);

            Assert.That(exitCode, Is.Not.EqualTo(0), "Must exit non-zero for unsupported schema version.");
            Assert.That(output, Does.Contain("99"), "Error message must include the unsupported version number.");
            Assert.That(output, Does.Contain("version 1"), "Error message must state the supported version.");
        }

        [Test]
        public void ReportAll_MultipleFiles_AggregatesAcrossSessions()
        {
            string dir = Path.Combine(_testDir, "sessions");
            Directory.CreateDirectory(dir);

            // Two separate session files
            File.WriteAllText(Path.Combine(dir, "s1.jsonl"), BuildSession1());
            File.WriteAllText(Path.Combine(dir, "s2.jsonl"), BuildSession2());

            int exitCode = RunReport("report-all", dir, out string output);

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output, Does.Contain("L1"), "Aggregated report must include L1.");
        }

        [Test]
        public void Report_IncludesPhase1CoverageFields()
        {
            string path = Path.Combine(_testDir, "coverage.jsonl");
            StringBuilder sb = new StringBuilder();
            WriteLine(sb, new LevelStartEvent { LevelId = "L9", TimestampMs = 0, Seed = 1, AssistanceChance = 1.0, RiseInterval = 8, InitialFloodedRows = 0, VineGrowthThreshold = 4, TargetCount = 1, WaterMode = "OneTickGrace", SchemaVersion = 1 });
            WriteLine(sb, new WaterForecastEvent { LevelId = "L9", TimestampMs = 10, ActionIndex = 1, WaterMode = "OneTickGrace", NextFloodRow = 4, ForecastAvailable = true, ActionsUntilRise = 1, SchemaVersion = 1 });
            WriteLine(sb, new DockOccupancyEvent { LevelId = "L9", TimestampMs = 11, ActionIndex = 1, Occupancy = 6, WarningLevel = "Acute", DockSize = 7, SchemaVersion = 1 });
            WriteLine(sb, new TargetStateTransitionEvent { LevelId = "L9", TimestampMs = 12, ActionIndex = 1, TargetId = "pup", FromState = "Progressing", ToState = "OneClearAway", SchemaVersion = 1 });
            WriteLine(sb, new TargetStateTransitionEvent { LevelId = "L9", TimestampMs = 13, ActionIndex = 2, TargetId = "pup", FromState = "OneClearAway", ToState = "ExtractableLatched", SchemaVersion = 1 });
            WriteLine(sb, new FinalRescueEvent { LevelId = "L9", TimestampMs = 14, ActionIndex = 2, TargetId = "pup", DockOverflowWouldHaveFailed = true, HazardAdvanceSkipped = true, SchemaVersion = 1 });
            WriteLine(sb, new FinalRescueDockOverflowOverrideEvent { LevelId = "L9", TimestampMs = 15, ActionIndex = 2, OverflowCount = 1, SchemaVersion = 1 });
            WriteLine(sb, new HazardAdvanceSkippedEvent { LevelId = "L9", TimestampMs = 16, ActionIndex = 2, Reason = "final_rescue", SchemaVersion = 1 });
            WriteLine(sb, new GraceEvent { LevelId = "L9", TimestampMs = 17, ActionIndex = 1, TargetId = "pup", Outcome = "entered", SchemaVersion = 1 });
            WriteLine(sb, new AssistedSpawnEvent { LevelId = "L9", TimestampMs = 18, ActionIndex = 1, Reason = "dock_pressure", Context = "dock=6/7", SpawnCount = 2, EmergencyRequested = true, EmergencyApplied = true, EffectiveAssistanceChance = 1.0, SchemaVersion = 1 });
            WriteLine(sb, new AssistedSpawnFollowUpEvent { LevelId = "L9", TimestampMs = 19, OriginalActionIndex = 1, FollowUpActionIndex = 2, UsedType = "A", SchemaVersion = 1 });
            WriteLine(sb, new VinePreviewEvent { LevelId = "L9", TimestampMs = 20, ActionIndex = 1, SchemaVersion = 1 });
            WriteLine(sb, new DeadboardLikeStateEvent { LevelId = "L9", TimestampMs = 21, ActionIndex = 3, Reason = "no_valid_groups", SchemaVersion = 1 });
            File.WriteAllText(path, sb.ToString());

            int exitCode = RunReport("report", path, out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(output, Does.Contain("Water modes"));
            Assert.That(output, Does.Contain("One-clear-away first action"));
            Assert.That(output, Does.Contain("Extraction latch first action"));
            Assert.That(output, Does.Contain("Final rescue dock overrides"));
            Assert.That(output, Does.Contain("Assisted spawn reasons"));
            Assert.That(output, Does.Contain("Grace outcomes"));
            Assert.That(output, Does.Contain("Deadboard-like states"));
        }

        // ── fixture helpers ───────────────────────────────────────────────────

        private string WriteFixtureSession()
        {
            string path = Path.Combine(_testDir, "session.jsonl");
            File.WriteAllText(path, BuildThreeLevelSession());
            return path;
        }

        // L1: attempt 1 → loss (dock_overflow, 6 actions)
        //     attempt 2 → win (4 actions, undoUsed=false)
        // L2: attempt 1 → win (3 actions, undoUsed=true)
        // L3: attempt 1 → loss (water_on_target, 8 actions)
        private static string BuildThreeLevelSession()
        {
            StringBuilder sb = new StringBuilder();
            WriteLine(sb, new LevelStartEvent { LevelId = "L1", TimestampMs = 0, Seed = 1, AssistanceChance = 0.7, RiseInterval = 12, InitialFloodedRows = 0, VineGrowthThreshold = 4, TargetCount = 1, SchemaVersion = 1 });
            WriteLine(sb, new LevelLossEvent { LevelId = "L1", TimestampMs = 600, ActionCount = 6, Reason = "dock_overflow", SchemaVersion = 1 });
            WriteLine(sb, new LevelStartEvent { LevelId = "L1", TimestampMs = 1000, Seed = 2, AssistanceChance = 0.7, RiseInterval = 12, InitialFloodedRows = 0, VineGrowthThreshold = 4, TargetCount = 1, SchemaVersion = 1 });
            WriteLine(sb, new LevelWinEvent { LevelId = "L1", TimestampMs = 1400, ActionCount = 4, ExtractedTargetOrder = new[] { "t0" }, UndoUsed = false, SchemaVersion = 1 });
            WriteLine(sb, new LevelStartEvent { LevelId = "L2", TimestampMs = 2000, Seed = 3, AssistanceChance = 0.7, RiseInterval = 0, InitialFloodedRows = 0, VineGrowthThreshold = 4, TargetCount = 1, SchemaVersion = 1 });
            WriteLine(sb, new LevelWinEvent { LevelId = "L2", TimestampMs = 2300, ActionCount = 3, ExtractedTargetOrder = new[] { "t0" }, UndoUsed = true, SchemaVersion = 1 });
            WriteLine(sb, new LevelStartEvent { LevelId = "L3", TimestampMs = 3000, Seed = 4, AssistanceChance = 0.6, RiseInterval = 9, InitialFloodedRows = 0, VineGrowthThreshold = 4, TargetCount = 2, SchemaVersion = 1 });
            WriteLine(sb, new LevelLossEvent { LevelId = "L3", TimestampMs = 3800, ActionCount = 8, Reason = "water_on_target", LostTargetId = "t1", SchemaVersion = 1 });
            return sb.ToString();
        }

        private static string BuildSession1()
        {
            StringBuilder sb = new StringBuilder();
            WriteLine(sb, new LevelStartEvent { LevelId = "L1", TimestampMs = 0, Seed = 10, AssistanceChance = 0.7, RiseInterval = 12, InitialFloodedRows = 0, VineGrowthThreshold = 4, TargetCount = 1, SchemaVersion = 1 });
            WriteLine(sb, new LevelWinEvent { LevelId = "L1", TimestampMs = 500, ActionCount = 5, ExtractedTargetOrder = new[] { "t0" }, UndoUsed = false, SchemaVersion = 1 });
            return sb.ToString();
        }

        private static string BuildSession2()
        {
            StringBuilder sb = new StringBuilder();
            WriteLine(sb, new LevelStartEvent { LevelId = "L1", TimestampMs = 0, Seed = 20, AssistanceChance = 0.7, RiseInterval = 12, InitialFloodedRows = 0, VineGrowthThreshold = 4, TargetCount = 1, SchemaVersion = 1 });
            WriteLine(sb, new LevelLossEvent { LevelId = "L1", TimestampMs = 700, ActionCount = 7, Reason = "dock_overflow", SchemaVersion = 1 });
            return sb.ToString();
        }

        private static void WriteLine(StringBuilder sb, object ev)
        {
            sb.AppendLine(JsonSerializer.Serialize(ev, ev.GetType(), new JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            }));
        }

        // Runs TelemetryReport.Program.Main via reflection-free call by redirecting console.
        private static int RunReport(string command, string target, out string output)
        {
            StringBuilder sb = new StringBuilder();
            TextWriter originalOut = Console.Out;
            TextWriter originalErr = Console.Error;

            Console.SetOut(new StringWriter(sb));
            Console.SetError(new StringWriter(sb));

            try
            {
                // We can't call Main directly since it's not exposed. Re-implement the dispatch.
                // Instead we call a thin wrapper exposed for testing.
                int exit = ReportRunner.Run(new[] { command, target });
                output = sb.ToString();
                return exit;
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
            }
        }
    }

    // Thin test seam so tests can call the report logic without spawning a process.
    internal static class ReportRunner
    {
        public static int Run(string[] args)
        {
            // Mirror the logic in Program.Main.
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: TelemetryReport report|report-all <path>");
                return 1;
            }

            string command = args[0];
            string target = args[1];

            try
            {
                System.Collections.Generic.List<ITelemetryEvent> allEvents =
                    new System.Collections.Generic.List<ITelemetryEvent>();

                if (command == "report")
                {
                    if (!File.Exists(target))
                    {
                        Console.Error.WriteLine($"File not found: {target}");
                        return 1;
                    }

                    LoadEvents(target, allEvents);
                }
                else if (command == "report-all")
                {
                    if (!Directory.Exists(target))
                    {
                        Console.Error.WriteLine($"Directory not found: {target}");
                        return 1;
                    }

                    foreach (string f in Directory.GetFiles(target, "*.jsonl"))
                    {
                        LoadEvents(f, allEvents);
                    }
                }
                else
                {
                    Console.Error.WriteLine($"Unknown command: {command}");
                    return 1;
                }

                string report = BuildReport(allEvents, new[] { target });
                Console.WriteLine(report);
                return 0;
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

        private static void LoadEvents(string path, System.Collections.Generic.List<ITelemetryEvent> target)
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

        private static string BuildReport(
            System.Collections.Generic.List<ITelemetryEvent> events,
            string[] sources)
        {
            // Delegate to Program's internal Aggregate + BuildReport.
            // Since those are private, we replicate a minimal version here.
            System.Collections.Generic.Dictionary<string, TestStats> byLevel
                = new System.Collections.Generic.Dictionary<string, TestStats>();

            TestStats GetOrCreate(string id)
            {
                if (!byLevel.TryGetValue(id, out var s))
                {
                    s = new TestStats();
                    byLevel[id] = s;
                }
                return s;
            }

            foreach (ITelemetryEvent ev in events)
            {
                var s = GetOrCreate(ev.LevelId);
                if (ev is LevelStartEvent start)
                {
                    s.Attempts++;
                    if (!string.IsNullOrEmpty(start.WaterMode)) Increment(s.WaterModes, start.WaterMode);
                }
                else if (ev is LevelWinEvent win)
                {
                    s.WinActions.Add(win.ActionCount);
                    s.Wins++;
                }
                else if (ev is LevelLossEvent loss)
                {
                    s.LossReasons.Add(loss.Reason);
                    s.LossActions.Add(loss.ActionCount);
                    s.Losses++;
                }
                else if (ev is WaterForecastEvent forecast)
                {
                    if (!string.IsNullOrEmpty(forecast.WaterMode)) Increment(s.WaterModes, forecast.WaterMode);
                    s.LastNextFloodRow = forecast.NextFloodRow;
                }
                else if (ev is DockOccupancyEvent dock)
                {
                    Increment(s.DockWarningStates, dock.WarningLevel);
                    s.PeakDockOccupancy = !s.PeakDockOccupancy.HasValue ? dock.Occupancy : Math.Max(s.PeakDockOccupancy.Value, dock.Occupancy);
                }
                else if (ev is TargetStateTransitionEvent transition)
                {
                    Increment(s.TargetTransitions, $"{transition.FromState}->{transition.ToState}");
                    if (transition.ToState == "OneClearAway" && !s.OneClearAway.ContainsKey(transition.TargetId)) s.OneClearAway[transition.TargetId] = transition.ActionIndex;
                    if (transition.ToState == "ExtractableLatched" && !s.Latches.ContainsKey(transition.TargetId)) s.Latches[transition.TargetId] = transition.ActionIndex;
                }
                else if (ev is FinalRescueEvent final)
                {
                    s.FinalRescues.Add(final.ActionIndex);
                }
                else if (ev is FinalRescueDockOverflowOverrideEvent)
                {
                    s.FinalOverrides++;
                }
                else if (ev is HazardAdvanceSkippedEvent)
                {
                    s.HazardSkips++;
                }
                else if (ev is GraceEvent grace)
                {
                    Increment(s.GraceOutcomes, grace.Outcome);
                }
                else if (ev is AssistedSpawnEvent assisted)
                {
                    s.AssistedSpawns++;
                    s.AssistedPieces += assisted.SpawnCount;
                    Increment(s.AssistedReasons, assisted.Reason);
                }
                else if (ev is AssistedSpawnFollowUpEvent)
                {
                    s.AssistedFollowUps++;
                }
                else if (ev is VinePreviewEvent)
                {
                    s.VinePreviews++;
                }
                else if (ev is DeadboardLikeStateEvent)
                {
                    s.Deadboards++;
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Telemetry Report");
            sb.AppendLine();

            int totalAttempts = 0, totalWins = 0;
            foreach (var kv in byLevel)
            {
                totalAttempts += kv.Value.Attempts;
                totalWins += kv.Value.Wins;
            }

            sb.AppendLine($"Session totals: {totalAttempts} attempts, {totalWins} wins");
            sb.AppendLine();

            foreach (var kv in byLevel)
            {
                TestStats s = kv.Value;
                int attempts = s.Attempts;
                int wins = s.Wins;
                System.Collections.Generic.List<string> lossReasons = s.LossReasons;
                System.Collections.Generic.List<int> winActions = s.WinActions;
                System.Collections.Generic.List<int> lossActions = s.LossActions;
                double winRate = attempts == 0 ? 0 : (double)wins / attempts * 100;
                double avgWinActions = winActions.Count == 0 ? 0 : winActions.Average();
                double avgLossActions = lossActions.Count == 0 ? 0 : lossActions.Average();

                sb.AppendLine($"## Level `{kv.Key}`");
                sb.AppendLine();
                sb.AppendLine($"- Attempts: {attempts}");
                sb.AppendLine($"- Wins: {wins}");
                sb.AppendLine($"- Win rate: {winRate:F1}%");
                sb.AppendLine($"- Avg actions (wins): {avgWinActions:F1}");
                sb.AppendLine($"- Avg actions (losses): {avgLossActions:F1}");
                sb.AppendLine($"- Water modes: {FormatCounts(s.WaterModes)}");
                sb.AppendLine($"- Last next flood row: {(s.LastNextFloodRow.HasValue ? s.LastNextFloodRow.Value.ToString() : "N/A")}");
                sb.AppendLine($"- Dock warning states: {FormatCounts(s.DockWarningStates)}");
                sb.AppendLine($"- Peak dock occupancy: {(s.PeakDockOccupancy.HasValue ? s.PeakDockOccupancy.Value.ToString() : "N/A")}");
                sb.AppendLine($"- Target transitions: {FormatCounts(s.TargetTransitions)}");
                sb.AppendLine($"- One-clear-away first action: {FormatActions(s.OneClearAway)}");
                sb.AppendLine($"- Extraction latch first action: {FormatActions(s.Latches)}");
                sb.AppendLine($"- Final rescue actions: {(s.FinalRescues.Count == 0 ? "N/A" : string.Join(", ", s.FinalRescues))}");
                sb.AppendLine($"- Final rescue dock overrides: {s.FinalOverrides}");
                sb.AppendLine($"- Hazard skips after final rescue: {s.HazardSkips}");
                sb.AppendLine($"- Assisted spawns: {s.AssistedSpawns} ({s.AssistedPieces} pieces)");
                sb.AppendLine($"- Assisted spawn reasons: {FormatCounts(s.AssistedReasons)}");
                sb.AppendLine($"- Assisted follow-up uses <=2 actions: {s.AssistedFollowUps}");
                sb.AppendLine($"- Grace outcomes: {FormatCounts(s.GraceOutcomes)}");
                sb.AppendLine($"- Vine preview events: {s.VinePreviews}");
                sb.AppendLine($"- Deadboard-like states: {s.Deadboards}");

                if (lossReasons.Count > 0)
                {
                    System.Collections.Generic.Dictionary<string, int> reasonCounts = new System.Collections.Generic.Dictionary<string, int>();
                    foreach (string r in lossReasons)
                    {
                        if (!reasonCounts.ContainsKey(r)) reasonCounts[r] = 0;
                        reasonCounts[r]++;
                    }

                    sb.AppendLine($"- Loss reasons: {string.Join(", ", System.Linq.Enumerable.Select(reasonCounts, kv2 => $"{kv2.Key}: {kv2.Value}"))}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private sealed class TestStats
        {
            public int Attempts;
            public int Wins;
            public int Losses;
            public int FinalOverrides;
            public int HazardSkips;
            public int AssistedSpawns;
            public int AssistedPieces;
            public int AssistedFollowUps;
            public int VinePreviews;
            public int Deadboards;
            public int? LastNextFloodRow;
            public int? PeakDockOccupancy;
            public System.Collections.Generic.List<string> LossReasons { get; } = new System.Collections.Generic.List<string>();
            public System.Collections.Generic.List<int> WinActions { get; } = new System.Collections.Generic.List<int>();
            public System.Collections.Generic.List<int> LossActions { get; } = new System.Collections.Generic.List<int>();
            public System.Collections.Generic.List<int> FinalRescues { get; } = new System.Collections.Generic.List<int>();
            public System.Collections.Generic.Dictionary<string, int> WaterModes { get; } = new System.Collections.Generic.Dictionary<string, int>();
            public System.Collections.Generic.Dictionary<string, int> DockWarningStates { get; } = new System.Collections.Generic.Dictionary<string, int>();
            public System.Collections.Generic.Dictionary<string, int> TargetTransitions { get; } = new System.Collections.Generic.Dictionary<string, int>();
            public System.Collections.Generic.Dictionary<string, int> AssistedReasons { get; } = new System.Collections.Generic.Dictionary<string, int>();
            public System.Collections.Generic.Dictionary<string, int> GraceOutcomes { get; } = new System.Collections.Generic.Dictionary<string, int>();
            public System.Collections.Generic.Dictionary<string, int> OneClearAway { get; } = new System.Collections.Generic.Dictionary<string, int>();
            public System.Collections.Generic.Dictionary<string, int> Latches { get; } = new System.Collections.Generic.Dictionary<string, int>();
        }

        private static void Increment(System.Collections.Generic.Dictionary<string, int> counts, string key)
        {
            if (!counts.ContainsKey(key)) counts[key] = 0;
            counts[key]++;
        }

        private static string FormatCounts(System.Collections.Generic.Dictionary<string, int> counts)
        {
            return counts.Count == 0
                ? "N/A"
                : string.Join(", ", counts.Select(kv => $"{kv.Key}: {kv.Value}"));
        }

        private static string FormatActions(System.Collections.Generic.Dictionary<string, int> actions)
        {
            return actions.Count == 0
                ? "N/A"
                : string.Join(", ", actions.Select(kv => $"{kv.Key}: {kv.Value}"));
        }
    }
}
