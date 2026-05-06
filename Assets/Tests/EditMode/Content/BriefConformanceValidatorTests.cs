using System;
using System.Linq;
using NUnit.Framework;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    public sealed class BriefConformanceValidatorTests
    {
        [Test]
        public void Validate_MatchingLevelAndBrief_Passes()
        {
            ValidationResult result = BriefConformanceValidator.Validate(MatchingLevel(), MatchingBrief());

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Validate_IdMismatch_Fails()
        {
            LevelBrief brief = MatchingBrief() with { Id = "L02" };

            ValidationResult result = BriefConformanceValidator.Validate(MatchingLevel(), brief);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Select(error => error.Code), Has.Member("brief.idMismatch"));
        }

        [Test]
        public void Validate_BoardSizeMismatch_Fails()
        {
            LevelBrief brief = MatchingBrief() with
            {
                BoardSize = new LevelBriefBoardSize { Width = 4, Height = 5 },
            };

            ValidationResult result = BriefConformanceValidator.Validate(MatchingLevel(), brief);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Select(error => error.Code), Has.Member("brief.board.widthMismatch"));
            Assert.That(result.Errors.Select(error => error.Code), Has.Member("brief.board.heightMismatch"));
        }

        [Test]
        public void Validate_TargetCountMismatch_Fails()
        {
            LevelBrief brief = MatchingBrief() with { TargetCount = 2 };

            ValidationResult result = BriefConformanceValidator.Validate(MatchingLevel(), brief);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Select(error => error.Code), Has.Member("brief.targetCountMismatch"));
        }

        [Test]
        public void Validate_ForbiddenMechanic_Fails()
        {
            LevelBrief brief = MatchingBrief() with
            {
                ForbiddenMechanics = new[] { "crate" },
            };

            ValidationResult result = BriefConformanceValidator.Validate(MatchingLevel(), brief);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Select(error => error.Code), Has.Member("brief.mechanic.forbidden"));
        }

        [Test]
        public void Validate_UnlistedMechanic_Warns()
        {
            LevelJson level = MatchingLevel() with
            {
                Board = MatchingLevel().Board with
                {
                    Tiles = new[]
                    {
                        new[] { "A", "A", "." },
                        new[] { ".", "T0", "IB" },
                        new[] { "CR", ".", "." },
                    },
                },
            };

            LevelBrief brief = MatchingBrief() with
            {
                ForbiddenMechanics = Array.Empty<string>(),
            };

            ValidationResult result = BriefConformanceValidator.Validate(level, brief);

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Select(error => error.Code), Has.Member("brief.mechanic.unlisted"));
        }

        [Test]
        public void Validate_GenericExpectedPath_Warns()
        {
            LevelJson level = MatchingLevel() with
            {
                Meta = MatchingLevel().Meta with
                {
                    ExpectedPath = "Win fast.",
                },
            };

            ValidationResult result = BriefConformanceValidator.Validate(level, MatchingBrief());

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Select(error => error.Code), Has.Member("brief.meta.expectedPath.generic"));
        }

        private static LevelJson MatchingLevel()
        {
            return new LevelJson
            {
                Id = "L01",
                Name = "First Rescue",
                Board = new BoardJson
                {
                    Width = 3,
                    Height = 3,
                    Tiles = new[]
                    {
                        new[] { "A", "A", "." },
                        new[] { ".", "T0", "." },
                        new[] { "CR", ".", "." },
                    },
                },
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E },
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
                InitialFloodedRows = 0,
                Water = new WaterJson { RiseInterval = 8 },
                Vine = new VineJson { GrowthThreshold = 4, GrowthPriority = Array.Empty<TileCoordJson>() },
                Dock = new DockJson { Size = 7, JamEnabled = false },
                Assistance = new AssistanceJson { Chance = 0.7d, ConsecutiveEmergencyCap = 2 },
                Meta = new MetaJson
                {
                    Intent = "Teach the first rescue route with action-based water pressure.",
                    ExpectedPath = "Clear the framed debris pair, open the crate route, and rescue the puppy before water pressure matters.",
                    ExpectedFailMode = "The player ignores the rescue route, clears broad debris, and loses tempo to dock pressure.",
                    WhatItProves = "The first rescue board can connect dock cost, route clearing, and puppy extraction.",
                },
            };
        }

        private static LevelBrief MatchingBrief()
        {
            return new LevelBrief
            {
                Id = "L01",
                Title = "First Rescue",
                AllowedMechanics = new[] { "debris", "dock", "water", "crate", "target_states" },
                ForbiddenMechanics = new[] { "ice", "vine", "reinforced_crate" },
                BoardSize = new LevelBriefBoardSize { Width = 3, Height = 3 },
                TargetCount = 1,
                ExpectedPath = "Clear the framed debris pair, open the crate route, and rescue the puppy before water pressure matters.",
                ExpectedFailMode = "The player ignores the rescue route, clears broad debris, and loses tempo to dock pressure.",
            };
        }
    }
}
