using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Rescue.Content;
using Rescue.Core.State;
using Rescue.SolveAuthoringTool;

namespace Rescue.SolveAuthoringTool.Tests
{
    public sealed class AssistanceComparisonTests
    {
        private static readonly string[] CompareAssistanceWarningArgs = { "--compare-assistance", "L99", "1", "2" };
        private static readonly string[] CompareAssistanceMissingArgs = { "--compare-assistance" };

        private string _repoRoot = string.Empty;
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _repoRoot = FindRepoRoot();
            _tempDir = Path.Combine(Path.GetTempPath(), "AssistanceComparisonTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Test]
        public void Compare_NoAssistanceAndAuthoredBothPass_DoesNotWarn()
        {
            LevelJson level = DirectSolveLevel();

            AssistanceComparisonResult result = LevelAssistanceComparisonAnalyzer.Compare(
                level,
                new AssistanceComparisonOptions(Seed: 1, MaxDepth: 2, FirstSeed: 1, LastSeed: 1));

            Assert.That(result.Modes[0].WinFound, Is.True);
            Assert.That(result.Modes[1].WinFound, Is.True);
            Assert.That(result.NoAssistanceFailsAuthoredSucceeds, Is.False);
        }

        [Test]
        public void Compare_AuthoredPassesButNoAssistanceFails_ProducesWarning()
        {
            LevelJson level = AssistanceDependentLevel(forceEmergency: false);

            AssistanceComparisonResult result = LevelAssistanceComparisonAnalyzer.Compare(
                level,
                new AssistanceComparisonOptions(Seed: 1, MaxDepth: 2, FirstSeed: 1, LastSeed: 1));

            Assert.That(result.Modes[0].WinFound, Is.True);
            Assert.That(result.Modes[1].WinFound, Is.False);
            Assert.That(result.NoAssistanceFailsAuthoredSucceeds, Is.True);
            Assert.That(result.AuthoredEmergencyOnlyWin, Is.False);
        }

        [Test]
        public void Compare_AuthoredEmergencyOnlyWin_DetectsEmergencyDependence()
        {
            LevelJson level = AssistanceDependentLevel(forceEmergency: true);

            AssistanceComparisonResult result = LevelAssistanceComparisonAnalyzer.Compare(
                level,
                new AssistanceComparisonOptions(Seed: 1, MaxDepth: 2, FirstSeed: 1, LastSeed: 1));

            Assert.That(result.Modes[0].WinFound, Is.True);
            Assert.That(result.Modes[1].WinFound, Is.False);
            Assert.That(result.NoAssistanceFailsAuthoredSucceeds, Is.True);
            Assert.That(result.AuthoredEmergencyOnlyWin, Is.True);
        }

        [Test]
        public void CompareAssistanceCommand_DesignWarning_ReturnsZero()
        {
            string commandRoot = Path.Combine(_tempDir, "command-root");
            string levelsDir = Path.Combine(commandRoot, "Assets", "StreamingAssets", "Levels");
            Directory.CreateDirectory(levelsDir);
            string levelPath = Path.Combine(levelsDir, "L99.json");
            File.WriteAllText(levelPath, ContentJson.SerializeLevel(AssistanceDependentLevel(forceEmergency: false)));

            int exitCode = RunSolveAuthoring(CompareAssistanceWarningArgs, commandRoot, out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(output, Does.Contain("Assistance comparison: L99"));
            Assert.That(output, Does.Contain("authored: winFound=True"));
            Assert.That(output, Does.Contain("no-assistance: winFound=False"));
            Assert.That(output, Does.Contain(LevelAssistanceComparisonAnalyzer.DependencyWarning));
        }

        [Test]
        public void CompareAssistanceCommand_MissingArguments_ReturnsNonzero()
        {
            int exitCode = RunSolveAuthoring(CompareAssistanceMissingArgs, out string output);

            Assert.That(exitCode, Is.Not.EqualTo(0), output);
            Assert.That(output, Does.Contain("Usage: --compare-assistance <levelId> [seed] [maxDepth]"));
        }

        [Test]
        public void DesignReport_AssistanceDependencyWarning_DoesNotSetHasErrors()
        {
            string reportRoot = Path.Combine(_tempDir, "report-root");
            Directory.CreateDirectory(Path.Combine(reportRoot, "Assets", "StreamingAssets", "Levels"));
            Directory.CreateDirectory(Path.Combine(reportRoot, "Assets", "Resources", "Levels"));

            LevelJson level = AssistanceDependentLevel(forceEmergency: false) with
            {
                Id = "L98",
                Name = "Report Assistance Risk",
            };
            string levelPath = Path.Combine(reportRoot, "Assets", "StreamingAssets", "Levels", "L98.json");
            File.WriteAllText(levelPath, ContentJson.SerializeLevel(level));
            string briefPath = WriteBrief("L98", "Report Assistance Risk");
            string solvePath = Path.Combine(_tempDir, "L98.solve.json");
            string goldenPath = Path.Combine(_tempDir, "L98.golden.json");
            AssistanceComparisonResult comparison = LevelAssistanceComparisonAnalyzer.Compare(
                level,
                new AssistanceComparisonOptions(Seed: 1, MaxDepth: 2, FirstSeed: 1, LastSeed: 1));
            WritePassingArtifacts("L98", solvePath, goldenPath, comparison.Modes[0].Actions);

            LevelDesignReport report = LevelDesignReportBuilder.Build(
                levelPath,
                briefPath,
                solvePath,
                goldenPath,
                reportRoot);

            Assert.That(report.HasErrors, Is.False, report.Text);
            Assert.That(report.Text, Does.Contain(LevelAssistanceComparisonAnalyzer.DependencyWarning));
        }

        private int RunSolveAuthoring(string[] args, out string output)
        {
            return RunSolveAuthoring(args, _repoRoot, out output);
        }

        private static int RunSolveAuthoring(string[] args, string workingDirectory, out string output)
        {
            string originalCurrentDirectory = Directory.GetCurrentDirectory();
            TextWriter originalOut = Console.Out;
            TextWriter originalErr = Console.Error;
            StringBuilder builder = new StringBuilder();

            Directory.SetCurrentDirectory(workingDirectory);
            Console.SetOut(new StringWriter(builder));
            Console.SetError(new StringWriter(builder));

            try
            {
                int exitCode = SolveAuthoringRunner.Run(args);
                output = builder.ToString();
                return exitCode;
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
                Directory.SetCurrentDirectory(originalCurrentDirectory);
            }
        }

        private string WriteBrief(string id, string title)
        {
            string path = Path.Combine(_tempDir, id + ".brief.json");
            File.WriteAllText(path, @"{
  ""id"": """ + id + @""",
  ""title"": """ + title + @""",
  ""campaignBand"": ""test"",
  ""role"": ""practice"",
  ""primarySkill"": ""route_readability"",
  ""secondarySkill"": ""water_timing"",
  ""allowedMechanics"": [""dock"", ""water"", ""target_states""],
  ""forbiddenMechanics"": [],
  ""boardSize"": { ""width"": 3, ""height"": 3 },
  ""targetCount"": 1,
  ""densityTarget"": ""50-100%"",
  ""targetFirstAttemptWinRate"": ""70-85%"",
  ""intendedTensionBeat"": ""Test tension."",
  ""intendedReleaseBeat"": ""Test release."",
  ""expectedPath"": ""Clear the route."",
  ""expectedFailMode"": ""Ignore the route."",
  ""designNotes"": ""Test brief.""
}");
            return path;
        }

        private static void WritePassingArtifacts(
            string levelId,
            string solvePath,
            string goldenPath,
            System.Collections.Immutable.ImmutableArray<TileCoord> actions)
        {
            File.WriteAllText(solvePath, @"{
  ""LevelId"": """ + levelId + @""",
  ""Seed"": 1,
  ""AlternateSeed"": 1001,
  ""ExpectedOutcome"": ""Win"",
  ""ExpectAlternateSeedDivergence"": false,
  ""Actions"": [" + SolveActionsJson(actions) + @"]
}");

            File.WriteAllText(goldenPath, @"{
  ""levelId"": """ + levelId + @""",
  ""seed"": 1,
  ""pathType"": ""golden"",
  ""expectedOutcome"": ""Win"",
  ""maxActions"": " + actions.Length + @",
  ""actions"": [" + GoldenActionsJson(actions) + @"],
  ""expectedEventsInOrder"": [""Won""],
  ""expectedExtractionOrder"": [""0""],
  ""notes"": ""Synthetic test golden.""
}");
        }

        private static string SolveActionsJson(System.Collections.Immutable.ImmutableArray<TileCoord> actions)
        {
            string[] json = new string[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                json[i] = @"{ ""Row"": " + actions[i].Row + @", ""Col"": " + actions[i].Col + @" }";
            }

            return string.Join(", ", json);
        }

        private static string GoldenActionsJson(System.Collections.Immutable.ImmutableArray<TileCoord> actions)
        {
            string[] json = new string[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                json[i] = @"{ ""row"": " + actions[i].Row + @", ""col"": " + actions[i].Col + @", ""intent"": ""Synthetic solve action."" }";
            }

            return string.Join(", ", json);
        }

        private static LevelJson DirectSolveLevel()
        {
            return BaseLevel() with
            {
                Id = "LDirect",
                Name = "Direct Solve",
                Board = new BoardJson
                {
                    Width = 3,
                    Height = 3,
                    Tiles = new[]
                    {
                        new[] { "B", "B", "." },
                        new[] { ".", ".", "." },
                        new[] { ".", "T0", "." },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 2, Col = 1 } },
                Assistance = new AssistanceJson
                {
                    Chance = 0.0d,
                    ConsecutiveEmergencyCap = 2,
                    SpawnIntegrity = new SpawnIntegrityJson(),
                },
            };
        }

        private static LevelJson AssistanceDependentLevel(bool forceEmergency)
        {
            return BaseLevel() with
            {
                Id = forceEmergency ? "LEmergency" : "L99",
                Name = forceEmergency ? "Emergency Dependent" : "Assistance Dependent",
                Board = new BoardJson
                {
                    Width = 3,
                    Height = 3,
                    Tiles = new[]
                    {
                        new[] { "B", "B", "." },
                        new[] { ".", "A", "." },
                        new[] { ".", "T0", "." },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 2, Col = 1 } },
                BaseDistribution = new System.Collections.Generic.Dictionary<DebrisType, double>
                {
                    [DebrisType.A] = 0.0d,
                    [DebrisType.B] = 1.0d,
                    [DebrisType.C] = 0.0d,
                    [DebrisType.D] = 0.0d,
                    [DebrisType.E] = 0.0d,
                },
                Assistance = new AssistanceJson
                {
                    Chance = forceEmergency ? 0.0d : 1.0d,
                    ConsecutiveEmergencyCap = 8,
                    SpawnIntegrity = new SpawnIntegrityJson
                    {
                        AllowExactTripleSpawns = true,
                        AllowOversizedSpawnGroups = true,
                    },
                },
                Dock = new DockJson
                {
                    Size = 7,
                    JamEnabled = false,
                },
            };
        }

        private static LevelJson BaseLevel()
        {
            return new LevelJson
            {
                Id = "LBase",
                Name = "Base",
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E },
                InitialFloodedRows = 0,
                Water = new WaterJson
                {
                    RiseInterval = 10,
                },
                Vine = new VineJson
                {
                    GrowthThreshold = 4,
                    GrowthPriority = Array.Empty<TileCoordJson>(),
                },
                Dock = new DockJson
                {
                    Size = 7,
                    JamEnabled = false,
                },
                Meta = new MetaJson
                {
                    Intent = "Synthetic assistance comparison test.",
                    ExpectedPath = "Clear the target route.",
                    ExpectedFailMode = "Spawn cannot create the route pair.",
                    WhatItProves = "Assistance comparison diagnostics.",
                    IsRuleTeach = false,
                },
            };
        }

        private static string FindRepoRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory is not null)
            {
                string levelsPath = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "Levels");
                if (Directory.Exists(levelsPath))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not find repository root from test directory.");
        }
    }
}
