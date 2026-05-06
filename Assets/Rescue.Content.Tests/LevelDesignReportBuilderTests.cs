using System;
using System.IO;
using NUnit.Framework;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    public sealed class LevelDesignReportBuilderTests
    {
        private string _repoRoot = string.Empty;
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _repoRoot = FindRepoRoot();
            _tempDir = Path.Combine(Path.GetTempPath(), "LevelDesignReportTests_" + Guid.NewGuid().ToString("N"));
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
        public void Build_ValidLevel_ProducesReportSections()
        {
            LevelDesignReport report = LevelDesignReportBuilder.Build(
                Path.Combine(_repoRoot, "Assets", "StreamingAssets", "Levels", "L03.json"),
                Path.Combine(_repoRoot, "docs", "level-briefs", "L03.brief.json"));

            Assert.That(report.Text, Does.Contain("Design Report: L03 - Rescue Order Arrives"));
            Assert.That(report.Text, Does.Contain("Identity:"));
            Assert.That(report.Text, Does.Contain("Core validation:"));
            Assert.That(report.Text, Does.Contain("Phase 1 policy:"));
            Assert.That(report.Text, Does.Contain("Brief conformance:"));
            Assert.That(report.Text, Does.Contain("Readability/density:"));
            Assert.That(report.Text, Does.Contain("Board Metrics:"));
            Assert.That(report.Text, Does.Contain("Systems:"));
            Assert.That(report.Text, Does.Contain("Solve Status:"));
            Assert.That(report.Text, Does.Contain("Golden Status:"));
            Assert.That(report.Text, Does.Contain("Top Design Risks:"));
        }

        [Test]
        public void Build_MissingSolveAndGolden_ReportWarningsNotCrash()
        {
            string missingSolve = Path.Combine(_tempDir, "missing.solve.json");
            string missingGolden = Path.Combine(_tempDir, "missing.golden.json");

            LevelDesignReport report = LevelDesignReportBuilder.Build(
                Path.Combine(_repoRoot, "Assets", "StreamingAssets", "Levels", "L03.json"),
                Path.Combine(_repoRoot, "docs", "level-briefs", "L03.brief.json"),
                missingSolve,
                missingGolden,
                _repoRoot);

            Assert.That(report.Text, Does.Contain("Warning: Solve file was not found"));
            Assert.That(report.Text, Does.Contain("Warning: Golden path file was not found"));
            Assert.That(report.Text, Does.Contain("No golden path found."));
        }

        [Test]
        public void Build_RuleBasedRisks_UsesValidatorSignals()
        {
            string levelPath = WriteLevel("L90", RiskLevel());
            string briefPath = WriteBrief("L90", "Risk Level", forbiddenMechanics: "[\"debris\"]", densityTarget: "90-100%");

            LevelDesignReport report = LevelDesignReportBuilder.Build(
                levelPath,
                briefPath,
                Path.Combine(_tempDir, "L90.solve.json"),
                Path.Combine(_tempDir, "L90.golden.json"),
                _repoRoot);

            Assert.That(report.Text, Does.Contain("Density below target."));
            Assert.That(report.Text, Does.Contain("No legal starting group near a target route."));
            Assert.That(report.Text, Does.Contain("Brief forbids a mechanic used by this level."));
            Assert.That(report.Text, Does.Contain("Expected fail mode is not executable yet."));
            Assert.That(report.Text, Does.Contain("Assistance chance high; compare no-assistance solve before accepting."));
        }

        [Test]
        public void BuildAll_MissingBrief_DoesNotCrash()
        {
            string levelsDir = Path.Combine(_tempDir, "levels");
            string briefsDir = Path.Combine(_tempDir, "briefs");
            Directory.CreateDirectory(levelsDir);
            Directory.CreateDirectory(briefsDir);
            File.WriteAllText(Path.Combine(levelsDir, "LOnlyLevel.json"), ContentJson.SerializeLevel(TestLevels.MinimalLevel() with
            {
                Id = "LOnlyLevel",
                Name = "Only Level",
            }));

            LevelDesignReportBatch batch = LevelDesignReportBuilder.BuildAll(levelsDir, briefsDir);

            Assert.That(batch.Text, Does.Contain("designReport.brief.missing"));
            Assert.That(batch.Text, Does.Contain("Level brief was not found"));
        }

        [Test]
        public void BuildAll_MissingLevel_DoesNotCrash()
        {
            string levelsDir = Path.Combine(_tempDir, "levels");
            string briefsDir = Path.Combine(_tempDir, "briefs");
            Directory.CreateDirectory(levelsDir);
            Directory.CreateDirectory(briefsDir);
            File.WriteAllText(Path.Combine(briefsDir, "LOnlyBrief.brief.json"), BriefJson("LOnlyBrief", "Only Brief", "[]", "70-80%"));

            LevelDesignReportBatch batch = LevelDesignReportBuilder.BuildAll(levelsDir, briefsDir);

            Assert.That(batch.HasErrors, Is.True);
            Assert.That(batch.Text, Does.Contain("designReport.level.missing"));
            Assert.That(batch.Text, Does.Contain("Level JSON was not found"));
        }

        private string WriteLevel(string id, LevelJson level)
        {
            string path = Path.Combine(_tempDir, id + ".json");
            File.WriteAllText(path, ContentJson.SerializeLevel(level));
            return path;
        }

        private string WriteBrief(string id, string title, string forbiddenMechanics, string densityTarget)
        {
            string path = Path.Combine(_tempDir, id + ".brief.json");
            File.WriteAllText(path, BriefJson(id, title, forbiddenMechanics, densityTarget));
            return path;
        }

        private static string BriefJson(string id, string title, string forbiddenMechanics, string densityTarget)
        {
            return @"{
  ""id"": """ + id + @""",
  ""title"": """ + title + @""",
  ""campaignBand"": ""test"",
  ""role"": ""practice"",
  ""primarySkill"": ""route_readability"",
  ""secondarySkill"": ""water_timing"",
  ""allowedMechanics"": [""dock"", ""water"", ""target_states""],
  ""forbiddenMechanics"": " + forbiddenMechanics + @",
  ""boardSize"": { ""width"": 4, ""height"": 4 },
  ""targetCount"": 1,
  ""densityTarget"": """ + densityTarget + @""",
  ""targetFirstAttemptWinRate"": ""70-85%"",
  ""intendedTensionBeat"": ""Test tension."",
  ""intendedReleaseBeat"": ""Test release."",
  ""expectedPath"": ""Clear the remote route."",
  ""expectedFailMode"": ""Ignore the route."",
  ""designNotes"": ""Test brief.""
}";
        }

        private static LevelJson RiskLevel()
        {
            return TestLevels.MinimalLevel() with
            {
                Id = "L90",
                Name = "Risk Level",
                Board = new BoardJson
                {
                    Width = 4,
                    Height = 4,
                    Tiles = new[]
                    {
                        new[] { "A", "A", ".", "." },
                        new[] { ".", ".", ".", "." },
                        new[] { ".", ".", ".", "B" },
                        new[] { ".", ".", "C", "T0" },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 3, Col = 3 } },
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E },
                Assistance = new AssistanceJson
                {
                    Chance = 0.75d,
                    ConsecutiveEmergencyCap = 2,
                    SpawnIntegrity = new SpawnIntegrityJson(),
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
