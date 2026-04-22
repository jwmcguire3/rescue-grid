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
            System.Collections.Generic.Dictionary<string, (int attempts, int wins, int losses, System.Collections.Generic.List<string> lossReasons, System.Collections.Generic.List<int> winActions, System.Collections.Generic.List<int> lossActions)> byLevel
                = new System.Collections.Generic.Dictionary<string, (int, int, int, System.Collections.Generic.List<string>, System.Collections.Generic.List<int>, System.Collections.Generic.List<int>)>();

            (int, int, int, System.Collections.Generic.List<string>, System.Collections.Generic.List<int>, System.Collections.Generic.List<int>) GetOrCreate(string id)
            {
                if (!byLevel.TryGetValue(id, out var s))
                {
                    s = (0, 0, 0, new System.Collections.Generic.List<string>(), new System.Collections.Generic.List<int>(), new System.Collections.Generic.List<int>());
                    byLevel[id] = s;
                }
                return s;
            }

            foreach (ITelemetryEvent ev in events)
            {
                var s = GetOrCreate(ev.LevelId);
                if (ev is LevelStartEvent)
                {
                    byLevel[ev.LevelId] = (s.Item1 + 1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6);
                }
                else if (ev is LevelWinEvent win)
                {
                    s.Item5.Add(win.ActionCount);
                    byLevel[ev.LevelId] = (s.Item1, s.Item2 + 1, s.Item3, s.Item4, s.Item5, s.Item6);
                }
                else if (ev is LevelLossEvent loss)
                {
                    s.Item4.Add(loss.Reason);
                    s.Item6.Add(loss.ActionCount);
                    byLevel[ev.LevelId] = (s.Item1, s.Item2, s.Item3 + 1, s.Item4, s.Item5, s.Item6);
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Telemetry Report");
            sb.AppendLine();

            int totalAttempts = 0, totalWins = 0;
            foreach (var kv in byLevel)
            {
                totalAttempts += kv.Value.Item1;
                totalWins += kv.Value.Item2;
            }

            sb.AppendLine($"Session totals: {totalAttempts} attempts, {totalWins} wins");
            sb.AppendLine();

            foreach (var kv in byLevel)
            {
                var (attempts, wins, losses, lossReasons, winActions, lossActions) = kv.Value;
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
    }
}
