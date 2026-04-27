using System.Linq;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    public sealed class ValidatorTests
    {
        [Test]
        public void Validate_ValidLevel_Passes()
        {
            string json = TestLevels.Serialize(TestLevels.MinimalLevel());

            ValidationResult result = Validator.Validate(json);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void Validate_OutOfBoundsTarget_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Targets = new[] { new TargetJson { Id = "0", Row = 4, Col = 1 } },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "target.bounds"), Is.True);
        }

        [Test]
        public void Validate_DuplicateTargetIds_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Targets = new[]
                {
                    new TargetJson { Id = "0", Row = 2, Col = 1 },
                    new TargetJson { Id = "0", Row = 0, Col = 2 },
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "target.duplicateId"), Is.True);
        }

        [Test]
        public void Validate_WrongSizeTilesArray_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = TestLevels.MinimalLevel().Board with
                {
                    Tiles = new[]
                    {
                        new[] { ".", ".", "." },
                        new[] { ".", "CR", "." },
                    },
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "board.tiles.height"), Is.True);
        }

        [Test]
        public void Validate_UnknownTileCode_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = TestLevels.MinimalLevel().Board with
                {
                    Tiles = new[]
                    {
                        new[] { "Z", "A", "." },
                        new[] { ".", "CR", "." },
                        new[] { ".", "T0", "." },
                    },
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "tile.unknown"), Is.True);
        }

        [Test]
        public void Validate_ImpossibleStart_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                InitialFloodedRows = 1,
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
                Board = TestLevels.MinimalLevel().Board with
                {
                    Tiles = new[]
                    {
                        new[] { ".", ".", "." },
                        new[] { ".", "T0", "." },
                        new[] { ".", ".", "." },
                    },
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "heuristic.impossibleStart"), Is.True);
        }

        [Test]
        public void Validate_UnreachableTarget_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = TestLevels.MinimalLevel().Board with
                {
                    Tiles = new[]
                    {
                        new[] { "CX", "CX", "CX" },
                        new[] { "CX", "T0", "CX" },
                        new[] { "CX", ".", "CX" },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "heuristic.unreachableTarget"), Is.True);
        }

        [Test]
        public void Validate_RuleTeachLevelWithoutPositiveRiseInterval_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Meta = TestLevels.MinimalLevel().Meta with
                {
                    IsRuleTeach = true,
                },
                Water = new WaterJson
                {
                    RiseInterval = 0,
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "water.ruleTeachRiseInterval"), Is.True);
        }

        [Test]
        public void Validate_DockJamOutsideTeachingLevels_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L03",
                Dock = TestLevels.MinimalLevel().Dock with
                {
                    JamEnabled = true,
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.dockJamLevel"), Is.True);
        }

        [Test]
        public void Validate_WrongDebrisPoolSizeForLevelBand_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L05",
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.debrisPoolSize"), Is.True);
        }

        [Test]
        public void Validate_NonRuleTeachWaterIntervalBelowSix_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L13",
                Water = new WaterJson
                {
                    RiseInterval = 5,
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.waterIntervalBelow6"), Is.True);
        }

        [Test]
        public void Validate_L07ActiveVineGrowth_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L07",
                Vine = new VineJson
                {
                    GrowthThreshold = 4,
                    GrowthPriority = new[] { new TileCoordJson { Row = 0, Col = 0 } },
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.l07VineGrowth"), Is.True);
        }

        [Test]
        public void Validate_ReinforcedCrate_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = TestLevels.MinimalLevel().Board with
                {
                    Tiles = new[]
                    {
                        new[] { "CX", ".", "." },
                        new[] { ".", ".", "." },
                        new[] { ".", "T0", "." },
                    },
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.reinforcedCrate"), Is.True);
        }

        [Test]
        public void Validate_AuthoredL00Level_Passes()
        {
            string json = File.ReadAllText(GetAuthoredL00Path());
            ValidationResult result = Validator.Validate(json);

            Assert.That(result.HasErrors, Is.False, string.Join(", ", result.Errors.Select(error => error.Code)));
        }

        [Test]
        public void AuthoredL00Level_ExpectedPath_WinsAfterFirstWaterRise()
        {
            LevelJson level = ContentJson.DeserializeLevel(File.ReadAllText(GetAuthoredL00Path()));
            GameState state = Loader.LoadLevel(level, seed: 0);

            ActionResult first = Pipeline.RunAction(state, new ActionInput(new TileCoord(4, 0)), new RunOptions(RecordSnapshot: false));

            Assert.That(first.Outcome, Is.EqualTo(ActionOutcome.Ok));
            Assert.That(first.Events, Has.Some.EqualTo(new WaterRose(6)));

            ActionResult second = Pipeline.RunAction(first.State, new ActionInput(new TileCoord(1, 2)), new RunOptions(RecordSnapshot: false));

            Assert.That(second.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(second.Events, Has.Some.EqualTo(new TargetExtracted("0", new TileCoord(2, 0))));
            Assert.That(second.Events, Has.Some.TypeOf<Won>());
        }

        private static string GetAuthoredL00Path()
        {
            string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? throw new IOException("Could not resolve test assembly directory.");
            string projectRoot = Path.GetFullPath(Path.Combine(root, "..", ".."));
            return Path.Combine(projectRoot, "Assets", "StreamingAssets", "Levels", "L00.json");
        }
    }
}
