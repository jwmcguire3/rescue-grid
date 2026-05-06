using System.Linq;
using NUnit.Framework;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    public sealed class ReadabilityPolicyTests
    {
        [Test]
        public void Analyze_OccupancyIncludesSupportedVisualCellsAndFloodedRows()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = new BoardJson
                {
                    Width = 3,
                    Height = 3,
                    Tiles = new[]
                    {
                        new[] { "A", "CR", "T0" },
                        new[] { "IA", "V", "." },
                        new[] { ".", ".", "." },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 0, Col = 2 } },
                InitialFloodedRows = 1,
            };

            LevelReadabilityMetrics metrics = LevelReadabilityAnalyzer.Analyze(level);

            Assert.That(metrics.TotalCells, Is.EqualTo(9));
            Assert.That(metrics.OccupiedVisualCells, Is.EqualTo(8));
            Assert.That(metrics.EmptyCells, Is.EqualTo(1));
            Assert.That(metrics.FloodedVisualCells, Is.EqualTo(3));
            Assert.That(metrics.TargetCount, Is.EqualTo(1));
            Assert.That(metrics.BlockerCountByType[BlockerType.Crate], Is.EqualTo(1));
            Assert.That(metrics.BlockerCountByType[BlockerType.Ice], Is.EqualTo(1));
            Assert.That(metrics.BlockerCountByType[BlockerType.Vine], Is.EqualTo(1));
            Assert.That(metrics.DebrisCountByType[DebrisType.A], Is.EqualTo(1));
            Assert.That(metrics.VisualOccupancyRatio, Is.EqualTo(8.0d / 9.0d).Within(0.0001d));
        }

        [Test]
        public void TryParseDensityTarget_ParsesPercentRange()
        {
            bool parsed = ReadabilityPolicyValidator.TryParseDensityTarget("70-80%", out DensityTarget target);

            Assert.That(parsed, Is.True);
            Assert.That(target.Min, Is.EqualTo(0.70d).Within(0.0001d));
            Assert.That(target.Max, Is.EqualTo(0.80d).Within(0.0001d));
        }

        [Test]
        public void Validate_BelowDensityTarget_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = new BoardJson
                {
                    Width = 4,
                    Height = 4,
                    Tiles = new[]
                    {
                        new[] { "A", "A", ".", "." },
                        new[] { ".", "T0", ".", "." },
                        new[] { ".", ".", ".", "." },
                        new[] { ".", ".", ".", "." },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
            };

            ValidationResult result = ReadabilityPolicyValidator.Validate(level, Brief("70-80%"));

            Assert.That(result.Errors.Any(error => error.Code == "readability.density.belowTarget"), Is.True);
        }

        [Test]
        public void Validate_AboveDensityTarget_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = new BoardJson
                {
                    Width = 4,
                    Height = 4,
                    Tiles = new[]
                    {
                        new[] { "A", "A", "B", "B" },
                        new[] { "C", "T0", "D", "E" },
                        new[] { "F", "A", "B", "C" },
                        new[] { "D", "E", "F", "A" },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
            };

            ValidationResult result = ReadabilityPolicyValidator.Validate(level, Brief("70-80%"));

            Assert.That(result.Errors.Any(error => error.Code == "readability.density.aboveTarget"), Is.True);
        }

        [Test]
        public void Validate_NoLegalStartingGroup_Warns()
        {
            LevelJson level = SingletonHeavyLevel();

            ValidationResult result = ReadabilityPolicyValidator.Validate(level, Brief("70-100%"));

            Assert.That(result.Errors.Any(error => error.Code == "readability.start.noLegalGroups"), Is.True);
        }

        [Test]
        public void Analyze_StartingGroupNearTargetRoute_IsRouteAffecting()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = new BoardJson
                {
                    Width = 3,
                    Height = 3,
                    Tiles = new[]
                    {
                        new[] { "A", "A", "." },
                        new[] { ".", "T0", "." },
                        new[] { ".", ".", "." },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
            };

            LevelReadabilityMetrics metrics = LevelReadabilityAnalyzer.Analyze(level);

            Assert.That(metrics.LegalStartingGroupCount, Is.EqualTo(1));
            Assert.That(metrics.RouteAffectingStartingGroupCount, Is.EqualTo(1));
        }

        [Test]
        public void Validate_SingletonHeavy_Warns()
        {
            LevelJson level = SingletonHeavyLevel();

            ValidationResult result = ReadabilityPolicyValidator.Validate(level, Brief("70-100%"));

            Assert.That(result.Errors.Any(error => error.Code == "readability.debris.singletonHeavy"), Is.True);
        }

        private static LevelJson SingletonHeavyLevel()
        {
            return TestLevels.MinimalLevel() with
            {
                Board = new BoardJson
                {
                    Width = 5,
                    Height = 5,
                    Tiles = new[]
                    {
                        new[] { "A", "B", "C", "D", "E" },
                        new[] { "B", "C", "D", "E", "F" },
                        new[] { "C", "D", "E", "F", "A" },
                        new[] { "D", "E", "F", "A", "B" },
                        new[] { "E", "F", "A", "B", "T0" },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 4, Col = 4 } },
            };
        }

        private static LevelBrief Brief(string densityTarget, string role = "practice")
        {
            return new LevelBrief
            {
                Id = "LTest",
                Role = role,
                DensityTarget = densityTarget,
                DesignNotes = "Test brief.",
            };
        }
    }
}
