using System.Linq;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using Rescue.Content;

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
        public void Validate_AuthoredL00Level_Passes()
        {
            string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? throw new IOException("Could not resolve test assembly directory.");
            string projectRoot = Path.GetFullPath(Path.Combine(root, "..", ".."));
            string l00Path = Path.Combine(projectRoot, "Assets", "StreamingAssets", "Levels", "L00.json");

            string json = File.ReadAllText(l00Path);
            ValidationResult result = Validator.Validate(json);

            Assert.That(result.HasErrors, Is.False, string.Join(", ", result.Errors.Select(error => error.Code)));
        }
    }
}
