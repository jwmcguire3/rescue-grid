using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    public sealed class LevelPacketDesignReportBuilderTests
    {
        private string _tempDir = string.Empty;
        private string _levelsDir = string.Empty;
        private string _briefsDir = string.Empty;
        private string _manifestPath = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "LevelPacketDesignReportTests_" + Guid.NewGuid().ToString("N"));
            _levelsDir = Path.Combine(_tempDir, "levels");
            _briefsDir = Path.Combine(_tempDir, "briefs");
            _manifestPath = Path.Combine(_tempDir, "phase1.packet.json");
            Directory.CreateDirectory(_levelsDir);
            Directory.CreateDirectory(_briefsDir);
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
        public void Build_MissingLevel_DetectsPacketWarningAndFailsReport()
        {
            WriteManifest("L00", "L01");
            WriteLevel(Level("L00"));
            WriteBrief("L00");
            WriteBrief("L01");

            LevelPacketDesignReport report = Build();

            Assert.That(report.HasErrors, Is.True);
            AssertHasCode(report, "packet.level.missing");
            Assert.That(report.Text, Does.Contain("level ids missing: L01"));
        }

        [Test]
        public void Build_MissingBrief_DetectsPacketWarningAndFailsReport()
        {
            WriteManifest("L00", "L01");
            WriteLevel(Level("L00"));
            WriteLevel(Level("L01"));
            WriteBrief("L00");

            LevelPacketDesignReport report = Build();

            Assert.That(report.HasErrors, Is.True);
            AssertHasCode(report, "packet.brief.missing");
            Assert.That(report.Text, Does.Contain("brief ids missing: L01"));
        }

        [Test]
        public void Build_MechanicAppearsBeforeBriefIntroduction_WarnsWithoutFailingReport()
        {
            WriteManifest("L00", "L01");
            WriteLevel(Level("L00") with
            {
                Board = Board(new[]
                {
                    new[] { "A", "A", "." },
                    new[] { ".", "V", "." },
                    new[] { ".", "T0", "." },
                }),
            });
            WriteLevel(Level("L01"));
            WriteBrief("L00", allowedMechanics: "[\"debris\", \"dock\", \"water\", \"target_states\"]");
            WriteBrief("L01", allowedMechanics: "[\"debris\", \"dock\", \"water\", \"vine\", \"target_states\"]");

            LevelPacketDesignReport report = Build();

            Assert.That(report.HasErrors, Is.False, report.Text);
            AssertHasCode(report, "packet.mechanic.beforeBriefIntroduction");
            Assert.That(report.Text, Does.Contain("uses mechanic 'vine' before its brief introduction (L01)"));
        }

        [Test]
        public void Build_WaterIntervalSharpDrop_WarnsWithoutFailingReport()
        {
            WriteManifest("L00", "L01");
            WriteLevel(Level("L00") with { Water = new WaterJson { RiseInterval = 10 } });
            WriteLevel(Level("L01") with { Water = new WaterJson { RiseInterval = 7 } });
            WriteBrief("L00");
            WriteBrief("L01");

            LevelPacketDesignReport report = Build();

            Assert.That(report.HasErrors, Is.False, report.Text);
            AssertHasCode(report, "packet.waterInterval.sharpDrop");
        }

        [Test]
        public void Build_RepeatedPrimarySkill_WarnsWithoutFailingReport()
        {
            WriteManifest("L00", "L01", "L02");
            WriteLevel(Level("L00"));
            WriteLevel(Level("L01"));
            WriteLevel(Level("L02"));
            WriteBrief("L00", primarySkill: "route_reading");
            WriteBrief("L01", primarySkill: "route_reading");
            WriteBrief("L02", primarySkill: "route_reading");

            LevelPacketDesignReport report = Build();

            Assert.That(report.HasErrors, Is.False, report.Text);
            AssertHasCode(report, "packet.primarySkill.repeatedRun");
        }

        [Test]
        public void Build_DensityJump_WarnsWithoutFailingReport()
        {
            WriteManifest("L00", "L01");
            WriteLevel(Level("L00"));
            WriteLevel(Level("L01") with
            {
                Board = Board(new[]
                {
                    new[] { "A", "A", "B" },
                    new[] { "B", "C", "C" },
                    new[] { "D", "T0", "D" },
                }),
            });
            WriteBrief("L00");
            WriteBrief("L01");

            LevelPacketDesignReport report = Build();

            Assert.That(report.HasErrors, Is.False, report.Text);
            AssertHasCode(report, "packet.density.sharpJump");
        }

        private LevelPacketDesignReport Build()
        {
            return LevelPacketDesignReportBuilder.Build(_manifestPath, _levelsDir, _briefsDir);
        }

        private void WriteManifest(params string[] ids)
        {
            File.WriteAllText(_manifestPath, @"{
  ""packetId"": ""test-packet"",
  ""displayName"": ""Test Packet"",
  ""firstLevelId"": """ + ids[0] + @""",
  ""lastLevelId"": """ + ids[^1] + @""",
  ""expectedLevelIds"": [" + string.Join(", ", ids.Select(id => "\"" + id + "\"")) + @"],
  ""ruleTeachLevelIds"": [],
  ""dockJamLevelIds"": [],
  ""staticVineIntroLevelIds"": [],
  ""debrisPoolBands"": [],
  ""waterIntervalMinimum"": 6,
  ""notes"": ""Test manifest.""
}");
        }

        private void WriteLevel(LevelJson level)
        {
            File.WriteAllText(Path.Combine(_levelsDir, level.Id + ".json"), ContentJson.SerializeLevel(level));
        }

        private void WriteBrief(
            string id,
            string role = "practice",
            string primarySkill = "route_reading",
            string campaignBand = "production_onboarding",
            string allowedMechanics = "[\"debris\", \"dock\", \"water\", \"crate\", \"target_states\"]")
        {
            File.WriteAllText(Path.Combine(_briefsDir, id + ".brief.json"), @"{
  ""id"": """ + id + @""",
  ""title"": """ + id + @" Brief"",
  ""campaignBand"": """ + campaignBand + @""",
  ""role"": """ + role + @""",
  ""primarySkill"": """ + primarySkill + @""",
  ""secondarySkill"": ""test_secondary"",
  ""allowedMechanics"": " + allowedMechanics + @",
  ""forbiddenMechanics"": [],
  ""boardSize"": { ""width"": 3, ""height"": 3 },
  ""targetCount"": 1,
  ""densityTarget"": ""40-100%"",
  ""targetFirstAttemptWinRate"": ""70-85%"",
  ""intendedTensionBeat"": ""Test tension beat."",
  ""intendedReleaseBeat"": ""Test release beat."",
  ""expectedPath"": ""Clear the route to the target."",
  ""expectedFailMode"": ""Ignore the route."",
  ""designNotes"": ""Test design notes.""
}");
        }

        private static LevelJson Level(string id)
        {
            return TestLevels.MinimalLevel() with
            {
                Id = id,
                Name = id + " Test Level",
                Meta = TestLevels.MinimalLevel().Meta with
                {
                    Notes = null,
                },
            };
        }

        private static BoardJson Board(string[][] tiles)
        {
            return new BoardJson
            {
                Width = tiles[0].Length,
                Height = tiles.Length,
                Tiles = tiles,
            };
        }

        private static void AssertHasCode(LevelPacketDesignReport report, string code)
        {
            Assert.That(report.Findings.Any(finding => string.Equals(finding.Code, code, StringComparison.Ordinal)), Is.True, report.Text);
        }
    }
}
