using System.Linq;
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
                        new[] { ".", "C1", "." },
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
                        new[] { ".", "C1", "." },
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
                        new[] { "C2", "C2", "C2" },
                        new[] { "C2", "T0", "C2" },
                        new[] { "C2", ".", "C2" },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "heuristic.unreachableTarget"), Is.True);
        }
    }
}
