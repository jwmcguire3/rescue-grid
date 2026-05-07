using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.LevelTelemetryTool;

namespace Rescue.LevelTelemetryTool.Tests
{
    public sealed class ReportTests
    {
        private static readonly string[] BotNames =
        {
            "random_legal",
            "greedy_clear",
            "rescue_focused",
            "dock_safe",
        };

        private string _testDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "LevelTelemetryTests_" + Guid.NewGuid().ToString("N"));
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
        public void Report_IncludesRequiredTopLevelFieldsAndAllBotKeys()
        {
            LevelTelemetryRunner.LevelTelemetryReport report = BuildReport("L01", 200, 30, RoleBrief("practice"), targetCount: 1);
            using JsonDocument document = Serialize(report);
            JsonElement root = document.RootElement;

            AssertHasProperties(root, "levelId", "samplesPerBot", "maxActions", "seedStart", "seedEnd", "generatedAtUtc", "bots", "difficultySignals", "notes");
            Assert.That(root.GetProperty("levelId").GetString(), Is.EqualTo("L01"));
            Assert.That(root.GetProperty("samplesPerBot").GetInt32(), Is.EqualTo(200));
            Assert.That(root.GetProperty("maxActions").GetInt32(), Is.EqualTo(30));
            Assert.That(root.GetProperty("bots").EnumerateObject().Select(static p => p.Name), Is.EquivalentTo(BotNames));
        }

        [Test]
        public void BotReport_ContainsRequiredFields()
        {
            LevelTelemetryRunner.LevelTelemetryReport report = BuildReport("L01", 200, 30, RoleBrief("practice"), targetCount: 1);
            using JsonDocument document = Serialize(report);

            foreach (JsonProperty bot in document.RootElement.GetProperty("bots").EnumerateObject())
            {
                AssertHasProperties(
                    bot.Value,
                    "sampleCount",
                    "winCount",
                    "winRate",
                    "lossCount",
                    "stalledCount",
                    "maxActionsReachedCount",
                    "medianActionsToWin",
                    "medianActionsToTerminal",
                    "averageActionsToTerminal",
                    "terminalReasonCounts",
                    "dockOverflowCount",
                    "waterLossCount",
                    "averageTargetsExtracted",
                    "targetExtractionOrderCounts",
                    "eventCounts");
            }
        }

        [Test]
        public void GeneratedAtUtc_IsRoundTrippableUtc()
        {
            LevelTelemetryRunner.LevelTelemetryReport report = BuildReport("L01", 200, 30, RoleBrief("practice"), targetCount: 1);

            Assert.That(report.GeneratedAtUtc, Is.Not.Empty);
            Assert.That(DateTimeOffset.TryParse(report.GeneratedAtUtc, out DateTimeOffset parsed), Is.True);
            Assert.That(parsed.Offset, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void JsonSerialization_PreservesExpectedPropertyNames()
        {
            LevelTelemetryRunner.LevelTelemetryReport report = BuildReport("L01", 200, 30, RoleBrief("practice"), targetCount: 1);
            string json = JsonSerializer.Serialize(report);
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.That(json, Does.Contain("\"levelId\""));
            Assert.That(json, Does.Contain("\"samplesPerBot\""));
            Assert.That(json, Does.Contain("\"medianActionsToWin\""));
            Assert.That(document.RootElement.TryGetProperty("LevelId", out _), Is.False);
            Assert.That(document.RootElement.GetProperty("bots").GetProperty("random_legal").TryGetProperty("SampleCount", out _), Is.False);
        }

        [Test]
        public void Aggregate_CalculatesWinCountAndWinRate()
        {
            LevelTelemetryRunner.BotReport report = LevelTelemetryRunner.AggregateBot(
                Enumerable.Range(0, 6).Select(static _ => Run("Win", 5)).Concat(
                Enumerable.Range(0, 4).Select(static _ => Run("LossDockOverflow", 6))).ToArray());

            Assert.That(report.WinCount, Is.EqualTo(6));
            Assert.That(report.WinRate, Is.EqualTo(0.6d).Within(0.0001d));
        }

        [Test]
        public void Aggregate_LossCountExcludesWinsStallsAndMaxActions()
        {
            LevelTelemetryRunner.BotReport report = LevelTelemetryRunner.AggregateBot(new[]
            {
                Run("Win", 5),
                Run("StalledNoLegalMoves", 2),
                Run("MaxActionsReached", 30),
                Run("LossDockOverflow", 7),
                Run("LossWaterOnTarget", 8),
            });

            Assert.That(report.LossCount, Is.EqualTo(2));
        }

        [Test]
        public void Aggregate_CountsStallsAndMaxActions()
        {
            LevelTelemetryRunner.BotReport report = LevelTelemetryRunner.AggregateBot(new[]
            {
                Run("StalledNoLegalMoves", 2),
                Run("StalledNoLegalMoves", 3),
                Run("MaxActionsReached", 30),
            });

            Assert.That(report.StalledCount, Is.EqualTo(2));
            Assert.That(report.MaxActionsReachedCount, Is.EqualTo(1));
        }

        [Test]
        public void Aggregate_GroupsTerminalReasonsExactly()
        {
            LevelTelemetryRunner.BotReport report = LevelTelemetryRunner.AggregateBot(new[]
            {
                Run("Win", 5),
                Run("Win", 6),
                Run("DockOverflow", 7),
                Run("WaterLoss", 8),
            });

            Assert.That(report.TerminalReasonCounts["Win"], Is.EqualTo(2));
            Assert.That(report.TerminalReasonCounts["DockOverflow"], Is.EqualTo(1));
            Assert.That(report.TerminalReasonCounts["WaterLoss"], Is.EqualTo(1));
        }

        [Test]
        public void Aggregate_RecognizesDockAndWaterTerminalReasons()
        {
            LevelTelemetryRunner.BotReport dock = LevelTelemetryRunner.AggregateBot(new[]
            {
                Run("DockOverflow", 5),
                Run("DockJamFailure", 6),
                Run("WaterLoss", 7),
            });
            LevelTelemetryRunner.BotReport water = LevelTelemetryRunner.AggregateBot(new[]
            {
                Run("WaterLoss", 5),
                Run("WaterReachedTarget", 6),
                Run("DockOverflow", 7),
            });

            Assert.That(dock.DockOverflowCount, Is.EqualTo(2));
            Assert.That(water.WaterLossCount, Is.EqualTo(2));
        }

        [Test]
        public void Aggregate_CalculatesMedians()
        {
            Assert.That(LevelTelemetryRunner.AggregateBot(new[] { Run("LossDockOverflow", 5) }).MedianActionsToWin, Is.Null);
            Assert.That(LevelTelemetryRunner.AggregateBot(new[] { Run("Win", 5), Run("Win", 7), Run("Win", 9) }).MedianActionsToWin, Is.EqualTo(7));
            Assert.That(LevelTelemetryRunner.AggregateBot(new[] { Run("Win", 4), Run("Win", 6), Run("Win", 8), Run("Win", 10) }).MedianActionsToWin, Is.EqualTo(7));
            Assert.That(LevelTelemetryRunner.AggregateBot(new[] { Run("Win", 1), Run("Win", 3), Run("Win", 5) }).MedianActionsToTerminal, Is.EqualTo(3));
            Assert.That(LevelTelemetryRunner.AggregateBot(new[] { Run("Win", 1), Run("Win", 3), Run("Win", 5), Run("Win", 7) }).MedianActionsToTerminal, Is.EqualTo(4));
        }

        [Test]
        public void Aggregate_CalculatesAverageActionsEventsTargetsAndOrderCounts()
        {
            LevelTelemetryRunner.BotReport report = LevelTelemetryRunner.AggregateBot(new[]
            {
                Run("Win", 5, new[] { "TargetExtractedEvent" }, 0, Array.Empty<string>()),
                Run("Win", 10, new[] { "TargetExtractedEvent" }, 1, new[] { "a" }),
                Run("Win", 15, new[] { "DockClearedEvent" }, 2, new[] { "a", "b" }),
                Run("Win", 20, Array.Empty<string>(), 2, new[] { "a", "b" }),
            });

            Assert.That(report.AverageActionsToTerminal, Is.EqualTo(12.5d));
            Assert.That(report.EventCounts["TargetExtractedEvent"], Is.EqualTo(2));
            Assert.That(report.EventCounts["DockClearedEvent"], Is.EqualTo(1));
            Assert.That(report.AverageTargetsExtracted, Is.EqualTo(1.25d));
            Assert.That(report.TargetExtractionOrderCounts["a>b"], Is.EqualTo(2));
        }

        [Test]
        public void Brief_ExistingFieldsAreCopied()
        {
            string briefDir = Path.Combine(_testDir, "briefs");
            Directory.CreateDirectory(briefDir);
            File.WriteAllText(Path.Combine(briefDir, "L01.brief.json"), RoleBrief("practice"));

            LevelTelemetryRunner.LevelTelemetryReport report = BuildReportFromDir("L01", 1, 5, briefDir, targetCount: 1);

            Assert.That(report.BriefRole, Is.EqualTo("practice"));
            Assert.That(report.BriefPrimarySkill, Is.EqualTo("rescue_order"));
            Assert.That(report.BriefTargetFirstAttemptWinRate, Is.EqualTo("75-90%"));
            Assert.That(report.BriefExpectedFailMode, Is.EqualTo("dock pressure"));
        }

        [Test]
        public void Brief_MissingAddsNoteAndMalformedAddsClearNote()
        {
            string briefDir = Path.Combine(_testDir, "briefs");
            Directory.CreateDirectory(briefDir);
            LevelTelemetryRunner.LevelTelemetryReport missing = BuildReportFromDir("L01", 1, 5, briefDir, targetCount: 1);
            File.WriteAllText(Path.Combine(briefDir, "L02.brief.json"), "{ bad json");
            LevelTelemetryRunner.LevelTelemetryReport malformed = BuildReportFromDir("L02", 1, 5, briefDir, targetCount: 1);

            Assert.That(missing.Notes, Does.Contain("No level brief found."));
            Assert.That(malformed.Notes.Any(static note => note.StartsWith("Level brief parse failed:", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void DifficultySignals_CompareBotRates()
        {
            LevelTelemetryRunner.LevelTelemetryReport report = BuildReport(
                "L01",
                100,
                30,
                RoleBrief("practice"),
                targetCount: 1,
                random: Runs(100, 51, "Win", "LossDockOverflow"),
                greedy: Runs(100, 70, "Win", "LossDockOverflow"),
                rescue: Runs(100, 59, "Win", "LossDockOverflow"),
                dock: Runs(100, 80, "Win", "LossDockOverflow"));

            Assert.That(report.DifficultySignals, Does.Contain("greedy_clear_outperforms_rescue_focused"));
            Assert.That(report.DifficultySignals, Does.Contain("rescue_focused_low_win_rate"));
            Assert.That(report.DifficultySignals, Does.Contain("random_legal_high_win_rate"));
            Assert.That(report.DifficultySignals, Does.Contain("dock_safety_may_dominate_rescue"));
        }

        [Test]
        public void DifficultySignals_RespectThresholdEdgesAndRoleExemptions()
        {
            LevelTelemetryRunner.LevelTelemetryReport lowAbsent = BuildReport("L01", 100, 30, RoleBrief("teach"), targetCount: 1, random: Runs(100, 51, "Win", "LossDockOverflow"), rescue: Runs(100, 60, "Win", "LossDockOverflow"), dock: Runs(100, 79, "Win", "LossDockOverflow"));

            Assert.That(lowAbsent.DifficultySignals, Does.Not.Contain("rescue_focused_low_win_rate"));
            Assert.That(lowAbsent.DifficultySignals, Does.Not.Contain("random_legal_high_win_rate"));
            Assert.That(lowAbsent.DifficultySignals, Does.Not.Contain("dock_safety_may_dominate_rescue"));
        }

        [Test]
        public void DifficultySignals_DominantTerminalReasonsAndProgressAndMaxActions()
        {
            LevelTelemetryRunner.LevelTelemetryReport report = BuildReport(
                "L01",
                100,
                30,
                RoleBrief("practice", expectedFailMode: "dock pressure"),
                targetCount: 1,
                rescue: Runs(100, 0, "Win", "LossDockOverflow", eventName: null, maxActionsCount: 26));
            LevelTelemetryRunner.LevelTelemetryReport water = BuildReport(
                "L02",
                10,
                30,
                RoleBrief("teach", expectedFailMode: "dock pressure"),
                targetCount: 1,
                rescue: Runs(10, 0, "Win", "WaterLoss"));
            LevelTelemetryRunner.LevelTelemetryReport maxEdge = BuildReport(
                "L03",
                100,
                30,
                RoleBrief("practice"),
                targetCount: 1,
                rescue: Runs(100, 0, "Win", "LossDockOverflow", eventName: "TargetProgressed", maxActionsCount: 25));

            Assert.That(report.DifficultySignals, Does.Contain("dock_overflow_too_prominent_for_role"));
            Assert.That(report.DifficultySignals, Does.Contain("no_target_progress_events_seen"));
            Assert.That(report.DifficultySignals, Does.Contain("many_runs_reach_max_actions"));
            Assert.That(water.DifficultySignals, Does.Contain("water_loss_too_prominent_for_role"));
            Assert.That(maxEdge.DifficultySignals, Does.Not.Contain("many_runs_reach_max_actions"));
            Assert.That(maxEdge.DifficultySignals, Does.Not.Contain("no_target_progress_events_seen"));
        }

        [Test]
        public void FileOutput_WritesExpectedFilenamesAndMultipleLevels()
        {
            string outputDir = Path.Combine(_testDir, "reports");
            foreach (string levelId in new[] { "L00", "L01", "L02", "L03" })
            {
                LevelTelemetryRunner.WriteReport(outputDir, BuildReport(levelId, 1, 5, RoleBrief("practice"), targetCount: 1));
            }

            Assert.That(File.Exists(Path.Combine(outputDir, "L01.telemetry.json")), Is.True);
            Assert.That(
                Directory.GetFiles(outputDir, "*.json").Select(Path.GetFileName),
                Is.EquivalentTo(
                    new[] { "L00.telemetry.json", "L01.telemetry.json", "L02.telemetry.json", "L03.telemetry.json" }));
        }

        [Test]
        public void Output_DefaultPathResolvesToReportsLevelTelemetry()
        {
            string resolved = LevelTelemetryRunner.ResolveOutputDirectory(Path.Combine("Reports", "LevelTelemetry"));

            Assert.That(resolved, Is.EqualTo(Path.GetFullPath(Path.Combine("Reports", "LevelTelemetry"))));
        }

        [Test]
        public void Output_CustomPathOverridesDefaultAndUsesTempDirectory()
        {
            string outputDir = Path.Combine(_testDir, "Temp", "MyTelemetry");

            int exitCode = RunTelemetry(new[] { "--level", "L01", "--samples", "1", "--max-actions", "1", "--output", outputDir }, out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(File.Exists(Path.Combine(outputDir, "L01.telemetry.json")), Is.True);
            Assert.That(output, Does.Contain(Path.GetFullPath(outputDir)));
        }

        [Test]
        public void Gitignore_IgnoresGeneratedTelemetryReports()
        {
            string gitignore = File.ReadAllText(Path.Combine(FindRepoRoot(), ".gitignore"));

            Assert.That(gitignore, Does.Contain("/Reports/LevelTelemetry/"));
        }

        [Test]
        public void AuthoringDocs_ContainTelemetryUsageSection()
        {
            string authoring = File.ReadAllText(Path.Combine(FindRepoRoot(), "Assets", "Rescue.Content", "AUTHORING.md"));

            Assert.That(authoring, Does.Contain("## Generate difficulty telemetry"));
            Assert.That(authoring, Does.Contain("dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- --level L01"));
            Assert.That(authoring, Does.Contain("dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- --range L00-L20 --samples 200 --max-actions 30"));
            Assert.That(authoring, Does.Contain("dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- summarize-level L03"));
            Assert.That(authoring, Does.Contain("dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- summarize-all"));
            Assert.That(authoring, Does.Contain("Telemetry reports are offline design diagnostics, not runtime analytics."));
            Assert.That(authoring, Does.Contain("Telemetry does not replace human playtest."));
        }

        [Test]
        public void Run_WritesPerLevelReportShape()
        {
            string outputDir = Path.Combine(_testDir, "run");

            int exitCode = RunTelemetry(new[] { "--level", "L01", "--samples", "2", "--max-actions", "5", "--output", outputDir }, out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            string reportPath = Path.Combine(outputDir, "L01.telemetry.json");
            Assert.That(File.Exists(reportPath), Is.True);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(reportPath));
            Assert.That(document.RootElement.GetProperty("levelId").GetString(), Is.EqualTo("L01"));
            Assert.That(document.RootElement.GetProperty("bots").EnumerateObject().Count(), Is.EqualTo(4));
            Assert.That(document.RootElement.GetProperty("bots").EnumerateObject().Select(static property => property.Name), Is.EquivalentTo(BotNames));
        }

        [Test]
        public void Run_Range_WritesOneReportPerLevel()
        {
            string outputDir = Path.Combine(_testDir, "range");

            int exitCode = RunTelemetry(new[] { "--range", "L00-L03", "--samples", "2", "--max-actions", "5", "--output", outputDir }, out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(
                Directory.GetFiles(outputDir, "*.json").Select(Path.GetFileName),
                Is.EquivalentTo(
                    new[] { "L00.telemetry.json", "L01.telemetry.json", "L02.telemetry.json", "L03.telemetry.json" }));
            foreach (string levelId in new[] { "L00", "L01", "L02", "L03" })
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDir, levelId + ".telemetry.json")));
                Assert.That(document.RootElement.GetProperty("levelId").GetString(), Is.EqualTo(levelId));
            }
        }

        [Test]
        public void Run_InvalidArgs_ReturnsUsageExitCode()
        {
            int combinedExitCode = RunTelemetry(new[] { "--level", "L01", "--range", "L00-L03" }, out string combinedOutput);
            int reversedExitCode = RunTelemetry(new[] { "--range", "L03-L00" }, out string reversedOutput);

            Assert.That(combinedExitCode, Is.EqualTo(2), combinedOutput);
            Assert.That(combinedOutput, Does.Contain("Specify only one of --level or --range"));
            Assert.That(reversedExitCode, Is.EqualTo(2), reversedOutput);
            Assert.That(reversedOutput, Does.Contain("--range end must be greater than or equal to the start"));
        }

        [Test]
        public void SummarizeLevel_L03CapturesWinningReplaySummary()
        {
            int exitCode = RunTelemetry(new[] { "summarize-level", "L03" }, out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(output, Does.Contain("LevelTelemetry replay summary: L03"));
            Assert.That(output, Does.Contain("golden path:"));
            Assert.That(output, Does.Contain("solve path:"));
            Assert.That(output, Does.Contain("final outcome: Win"));
            Assert.That(output, Does.Contain("action count: 4"));
            Assert.That(output, Does.Contain("extraction order: 0>1"));
            Assert.That(output, Does.Contain("fail path:"));
            Assert.That(output, Does.Contain("expected outcome: LossDockOverflow"));
            Assert.That(output, Does.Contain("final outcome: LossDockOverflow"));
        }

        [Test]
        public void BuildSummaryFromEvents_CountsReplayEventDetails()
        {
            LevelTelemetryRunner.ReplaySummary summary = LevelTelemetryRunner.BuildSummaryFromEvents(
                "L99",
                LevelTelemetryRunner.ReplayArtifactKind.Golden,
                "L99.golden.json",
                seed: 1,
                expectedOutcome: "Win",
                actionCount: 3,
                finalOutcome: ActionOutcome.Win,
                extractionOrder: ImmutableArray.Create("a", "b"),
                events: new ActionEvent[]
                {
                    new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 0),
                    new WaterRose(FloodedRow: 4),
                    new TargetProgressed("a", new TileCoord(1, 1)),
                    new TargetOneClearAway("a", new TileCoord(1, 1)),
                    new TargetExtractionLatched("a", new TileCoord(1, 1)),
                    new TargetExtracted("a", new TileCoord(1, 1)),
                    new TargetDistressedEntered("b", new TileCoord(2, 2)),
                    new DockJamTriggered(OverflowCount: 1),
                    new VinePreviewChanged(new TileCoord(3, 3)),
                    new VinePreviewChanged(PendingTile: null),
                    new VineGrown(new TileCoord(3, 3)),
                });

            Assert.That(summary.ExtractionOrder, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(summary.DockClears, Is.EqualTo(1));
            Assert.That(summary.WaterRises, Is.EqualTo(1));
            Assert.That(summary.TargetProgressed, Is.EqualTo(1));
            Assert.That(summary.TargetOneClearAway, Is.EqualTo(1));
            Assert.That(summary.TargetLatched, Is.EqualTo(1));
            Assert.That(summary.TargetExtracted, Is.EqualTo(1));
            Assert.That(summary.TargetDistressed, Is.EqualTo(1));
            Assert.That(summary.DockJamEvents, Is.EqualTo(1));
            Assert.That(summary.VinePreviews, Is.EqualTo(1));
            Assert.That(summary.VinePreviewClears, Is.EqualTo(1));
            Assert.That(summary.VineGrowths, Is.EqualTo(1));
            Assert.That(summary.VinePlannedEvents, Is.EqualTo(0));
            Assert.That(summary.VineProgressedEvents, Is.EqualTo(0));
            Assert.That(summary.VineCanceledEvents, Is.EqualTo(0));
        }

        [Test]
        public void BuildSummaryFromEvents_CapturesLossReason()
        {
            LevelTelemetryRunner.ReplaySummary summary = LevelTelemetryRunner.BuildSummaryFromEvents(
                "L99",
                LevelTelemetryRunner.ReplayArtifactKind.Fail,
                "L99.fail.json",
                seed: 1,
                expectedOutcome: "LossWaterOnTarget",
                actionCount: 6,
                finalOutcome: ActionOutcome.LossWaterOnTarget,
                extractionOrder: ImmutableArray<string>.Empty,
                events: new ActionEvent[] { new Lost(ActionOutcome.LossWaterOnTarget) });

            Assert.That(summary.FinalOutcome, Is.EqualTo("LossWaterOnTarget"));
            Assert.That(summary.LossReason, Is.EqualTo("LossWaterOnTarget"));
        }

        [Test]
        public void BuildSummaryFromEvents_DistinguishesAssistedSpawnZeroAndUnavailable()
        {
            LevelTelemetryRunner.ReplaySummary zero = LevelTelemetryRunner.BuildSummaryFromEvents(
                "L99",
                LevelTelemetryRunner.ReplayArtifactKind.Solve,
                "L99.solve.json",
                seed: 1,
                expectedOutcome: "Win",
                actionCount: 1,
                finalOutcome: ActionOutcome.Win,
                extractionOrder: ImmutableArray<string>.Empty,
                events: Array.Empty<ActionEvent>());

            LevelTelemetryRunner.ReplaySummary unavailable = LevelTelemetryRunner.BuildSummaryFromEvents(
                "L99",
                LevelTelemetryRunner.ReplayArtifactKind.Solve,
                "L99.solve.json",
                seed: 1,
                expectedOutcome: "Win",
                actionCount: 1,
                finalOutcome: ActionOutcome.Win,
                extractionOrder: ImmutableArray<string>.Empty,
                events: new ActionEvent[]
                {
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.A))),
                });

            Assert.That(zero.AssistedSpawnDetailAvailable, Is.True);
            Assert.That(zero.AssistedSpawnEvents, Is.EqualTo(0));
            Assert.That(unavailable.AssistedSpawnDetailAvailable, Is.False);
        }

        [Test]
        public void BuildSummaryFromEvents_CapturesAssistedSpawnDetails()
        {
            SpawnedPiece piece = new SpawnedPiece(
                new TileCoord(0, 0),
                DebrisType.A,
                LineageId: 42,
                Reasons: ImmutableArray.Create(SpawnAssistReason.RouteAdjacency, SpawnAssistReason.EmergencyDockPressure),
                TriggerContext: ImmutableArray.Create("dock=6/7"),
                UrgentTargetId: "target",
                UrgentTargetCoord: new TileCoord(2, 2),
                WaterRisesRemaining: 1,
                DockOccupancy: 6,
                RecoveryCounterBefore: 0,
                EmergencyRequested: true,
                EmergencyApplied: true,
                EffectiveAssistanceChance: 0.75d);

            LevelTelemetryRunner.ReplaySummary summary = LevelTelemetryRunner.BuildSummaryFromEvents(
                "L99",
                LevelTelemetryRunner.ReplayArtifactKind.Solve,
                "L99.solve.json",
                seed: 1,
                expectedOutcome: "Win",
                actionCount: 1,
                finalOutcome: ActionOutcome.Win,
                extractionOrder: ImmutableArray<string>.Empty,
                events: new ActionEvent[] { new Spawned(ImmutableArray.Create(piece)) });

            Assert.That(summary.AssistedSpawnDetailAvailable, Is.True);
            Assert.That(summary.AssistedSpawnEvents, Is.EqualTo(1));
            Assert.That(summary.AssistedSpawnPieces, Is.EqualTo(1));
            Assert.That(summary.EmergencyRequestedPieces, Is.EqualTo(1));
            Assert.That(summary.EmergencyAppliedPieces, Is.EqualTo(1));
            Assert.That(summary.MaxEffectiveAssistanceChance, Is.EqualTo(0.75d));
            Assert.That(summary.AssistedSpawnReasons["route_adjacency"], Is.EqualTo(1));
            Assert.That(summary.AssistedSpawnReasons["emergency_dock_pressure"], Is.EqualTo(1));
        }

        [Test]
        public void SummarizeAll_SucceedsWithMissingOptionalFailPaths()
        {
            int exitCode = RunTelemetry(new[] { "summarize-all" }, out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(output, Does.Contain("LevelTelemetry replay summary: L00"));
            Assert.That(output, Does.Contain("missing optional data"));
        }

        private LevelTelemetryRunner.LevelTelemetryReport BuildReport(
            string levelId,
            int samples,
            int maxActions,
            string briefJson,
            int targetCount,
            LevelTelemetryRunner.BotRunResult[]? random = null,
            LevelTelemetryRunner.BotRunResult[]? greedy = null,
            LevelTelemetryRunner.BotRunResult[]? rescue = null,
            LevelTelemetryRunner.BotRunResult[]? dock = null)
        {
            string briefDir = Path.Combine(_testDir, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(briefDir);
            File.WriteAllText(Path.Combine(briefDir, levelId + ".brief.json"), briefJson);
            return BuildReportFromDir(levelId, samples, maxActions, briefDir, targetCount, random, greedy, rescue, dock);
        }

        private static LevelTelemetryRunner.LevelTelemetryReport BuildReportFromDir(
            string levelId,
            int samples,
            int maxActions,
            string briefDir,
            int targetCount,
            LevelTelemetryRunner.BotRunResult[]? random = null,
            LevelTelemetryRunner.BotRunResult[]? greedy = null,
            LevelTelemetryRunner.BotRunResult[]? rescue = null,
            LevelTelemetryRunner.BotRunResult[]? dock = null)
        {
            Dictionary<string, LevelTelemetryRunner.BotRunResult[]> runs = new Dictionary<string, LevelTelemetryRunner.BotRunResult[]>(StringComparer.Ordinal)
            {
                ["random_legal"] = random ?? Runs(samples, samples, "Win", "LossDockOverflow"),
                ["greedy_clear"] = greedy ?? Runs(samples, samples, "Win", "LossDockOverflow"),
                ["rescue_focused"] = rescue ?? Runs(samples, samples, "Win", "LossDockOverflow", eventName: "TargetProgressed"),
                ["dock_safe"] = dock ?? Runs(samples, samples, "Win", "LossDockOverflow"),
            };
            return LevelTelemetryRunner.BuildReport(levelId, samples, maxActions, 1, samples, runs, DateTimeOffset.UtcNow, briefDir, targetCount);
        }

        private static LevelTelemetryRunner.BotRunResult[] Runs(int sampleCount, int winCount, string winReason, string lossReason, string? eventName = "TargetProgressed", int maxActionsCount = 0)
        {
            LevelTelemetryRunner.BotRunResult[] runs = new LevelTelemetryRunner.BotRunResult[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                string reason = i < winCount ? winReason : lossReason;
                if (i >= sampleCount - maxActionsCount)
                {
                    reason = "MaxActionsReached";
                }

                runs[i] = Run(reason, 5 + (i % 3), eventName is null ? Array.Empty<string>() : new[] { eventName });
            }

            return runs;
        }

        private static LevelTelemetryRunner.BotRunResult Run(string terminalReason, int actions, string[]? events = null, int targetsExtracted = 0, string[]? order = null)
        {
            return new LevelTelemetryRunner.BotRunResult(
                "L01",
                1,
                "test",
                actions,
                terminalReason,
                terminalReason,
                events ?? Array.Empty<string>(),
                targetsExtracted,
                order ?? Array.Empty<string>(),
                Array.Empty<LevelTelemetryRunner.ActionReport>());
        }

        private static string RoleBrief(string role, string expectedFailMode = "dock pressure")
        {
            return "{" +
                $"\"role\":\"{role}\"," +
                "\"primarySkill\":\"rescue_order\"," +
                "\"targetFirstAttemptWinRate\":\"75-90%\"," +
                $"\"expectedFailMode\":\"{expectedFailMode}\"" +
                "}";
        }

        private static JsonDocument Serialize(LevelTelemetryRunner.LevelTelemetryReport report)
        {
            return JsonDocument.Parse(JsonSerializer.Serialize(report));
        }

        private static string FindRepoRoot()
        {
            DirectoryInfo? current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, ".gitignore"))
                    && File.Exists(Path.Combine(current.FullName, "Assets", "Rescue.Content", "AUTHORING.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not find repository root.");
        }

        private static void AssertHasProperties(JsonElement element, params string[] names)
        {
            foreach (string name in names)
            {
                Assert.That(element.TryGetProperty(name, out _), Is.True, "Missing property " + name);
            }
        }

        private static int RunTelemetry(string[] args, out string output)
        {
            StringBuilder builder = new StringBuilder();
            TextWriter originalOut = Console.Out;
            TextWriter originalErr = Console.Error;

            Console.SetOut(new StringWriter(builder));
            Console.SetError(new StringWriter(builder));

            try
            {
                int exitCode = LevelTelemetryRunner.Run(args);
                output = builder.ToString();
                return exitCode;
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
            }
        }
    }
}
