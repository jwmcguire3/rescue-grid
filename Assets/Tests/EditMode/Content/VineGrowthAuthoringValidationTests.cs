using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    public sealed class VineGrowthAuthoringValidationTests
    {
        private string _testDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "VineGrowthAuthoringValidation_" + Guid.NewGuid().ToString("N"));
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
        public void Validator_DoesNotRequireAuthoredPriorityWhenSystemicPlanIsValid()
        {
            LevelJson level = SystemicVineLevel();

            ValidationResult result = Validator.Validate(level);
            VineGrowthAuthoringInfo vine = VineGrowthAuthoringInspector.Inspect(level);

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.Errors.Select(static error => error.Code), Does.Not.Contain("vine.growthPlan.missing"));
            Assert.That(vine.AuthoredPriorityPresent, Is.False);
            Assert.That(vine.SystemicPlanAvailable, Is.True);
            Assert.That(vine.AuthoredFallbackPossible, Is.False);
        }

        [Test]
        public void Validator_WarnsWhenActiveVineHasNoSystemicPlanOrAuthoredFallback()
        {
            LevelJson level = NoPlanVineLevel();

            ValidationResult result = Validator.Validate(level);

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.Errors.Select(static error => error.Code), Does.Contain("vine.growthPlan.missing"));
        }

        [Test]
        public void Phase1Policy_StaticVineIntroWarningStillAppliesWithoutAuthoredPriority()
        {
            LevelJson level = SystemicVineLevel();

            ValidationResult result = Phase1PolicyValidator.Validate(level, TestManifest(staticVineIntro: true));

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.Errors.Select(static error => error.Code), Does.Contain("phase1.l07VineGrowth"));
        }

        [Test]
        public void Phase1Policy_StaticVineIntroAllowsThreshold999EmptyPriority()
        {
            LevelJson level = SystemicVineLevel() with
            {
                Vine = new VineJson { GrowthThreshold = 999, GrowthPriority = Array.Empty<TileCoordJson>() },
            };

            ValidationResult result = Phase1PolicyValidator.Validate(level, TestManifest(staticVineIntro: true));

            Assert.That(result.Errors.Select(static error => error.Code), Does.Not.Contain("phase1.l07VineGrowth"));
        }

        [Test]
        public void DesignReport_SurfacesSystemicVinePlanInfo()
        {
            string levelPath = Path.Combine(_testDir, "LX.json");
            string briefPath = Path.Combine(_testDir, "LX.brief.json");
            File.WriteAllText(levelPath, ContentJson.SerializeLevel(SystemicVineLevel()));
            File.WriteAllText(briefPath, "{}");

            LevelDesignReport report = LevelDesignReportBuilder.Build(levelPath, briefPath, solvePath: "missing.solve.json", goldenPath: "missing.golden.json", repoRoot: null);

            Assert.That(report.Text, Does.Contain("vine authored priority present: False"));
            Assert.That(report.Text, Does.Contain("vine systemic planning active: True"));
            Assert.That(report.Text, Does.Contain("Info: systemic vine plan available."));
        }

        private static LevelJson SystemicVineLevel()
        {
            return CreateLevel(new[]
            {
                new[] { "V", ".", ".", "." },
                new[] { "A", "A", "C", "T0" },
                new[] { "B", "B", ".", "D" },
            });
        }

        private static LevelJson NoPlanVineLevel()
        {
            return CreateLevel(new[]
            {
                new[] { "CR", "CR", "CR", "CR" },
                new[] { "CR", "V", "CR", "CR" },
                new[] { "A", "CR", "B", "B" },
                new[] { "T0", ".", "C", "C" },
            });
        }

        private static LevelJson CreateLevel(string[][] tiles)
        {
            (int targetRow, int targetCol) = FindTargetTile(tiles);
            return new LevelJson
            {
                Id = "LX",
                Name = "Vine Authoring Fixture",
                Board = new BoardJson
                {
                    Width = tiles[0].Length,
                    Height = tiles.Length,
                    Tiles = tiles,
                },
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E },
                Targets = new[] { new TargetJson { Id = "0", Row = targetRow, Col = targetCol } },
                InitialFloodedRows = 0,
                Water = new WaterJson { RiseInterval = 10 },
                Vine = new VineJson { GrowthThreshold = 4, GrowthPriority = Array.Empty<TileCoordJson>() },
                Dock = new DockJson { Size = 7, JamEnabled = false },
                Assistance = new AssistanceJson { Chance = 0.0d, ConsecutiveEmergencyCap = 2 },
                Meta = new MetaJson
                {
                    Intent = "Exercise vine authoring validation.",
                    ExpectedPath = "Clear groups while vine planning is inspected.",
                    ExpectedFailMode = "Ignoring vine pressure.",
                    WhatItProves = "Authoring tools understand systemic vine planning.",
                },
            };
        }

        private static (int Row, int Col) FindTargetTile(string[][] tiles)
        {
            for (int row = 0; row < tiles.Length; row++)
            {
                for (int col = 0; col < tiles[row].Length; col++)
                {
                    if (tiles[row][col] == "T0")
                    {
                        return (row, col);
                    }
                }
            }

            throw new InvalidOperationException("Test fixture must include T0.");
        }

        private static LevelPacketManifest TestManifest(bool staticVineIntro)
        {
            return new LevelPacketManifest
            {
                PacketId = "phase1-test",
                DisplayName = "Phase 1 Test Packet",
                FirstLevelId = "LX",
                LastLevelId = "LX",
                ExpectedLevelIds = new[] { "LX" },
                RuleTeachLevelIds = Array.Empty<string>(),
                DockJamLevelIds = Array.Empty<string>(),
                StaticVineIntroLevelIds = staticVineIntro ? new[] { "LX" } : Array.Empty<string>(),
                DebrisPoolBands = new[]
                {
                    new DebrisPoolBand
                    {
                        FirstLevelId = "LX",
                        LastLevelId = "LX",
                        DebrisTypePoolSize = 5,
                    },
                },
                WaterIntervalMinimum = 6,
                Notes = "Test manifest.",
            };
        }
    }
}
