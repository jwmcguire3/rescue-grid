using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rescue.Content;

namespace Rescue.Content.Tests
{
    public sealed class LevelBriefLoaderTests
    {
        [Test]
        public void Validate_ValidBrief_Passes()
        {
            ValidationResult result = LevelBriefLoader.Validate(ValidBrief());

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void Load_ValidRepoBrief_Passes()
        {
            LevelBrief brief = LevelBriefLoader.Load(Path.Combine(GetBriefsDirectory(), "L00.brief.json"));

            Assert.That(brief.Id, Is.EqualTo("L00"));
            Assert.That(brief.PrimarySkill, Is.EqualTo("action_hazard_literacy"));
        }

        [Test]
        public void Validate_MissingId_Fails()
        {
            LevelBrief brief = ValidBrief() with
            {
                Id = string.Empty,
            };

            ValidationResult result = LevelBriefLoader.Validate(brief);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "brief.field.required" && error.Path == "$.id"), Is.True);
        }

        [Test]
        public void Validate_MalformedId_Fails()
        {
            LevelBrief brief = ValidBrief() with
            {
                Id = "Level01",
            };

            ValidationResult result = LevelBriefLoader.Validate(brief);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "brief.id.malformed"), Is.True);
        }

        [Test]
        public void Validate_MissingPrimarySkill_Fails()
        {
            LevelBrief brief = ValidBrief() with
            {
                PrimarySkill = string.Empty,
            };

            ValidationResult result = LevelBriefLoader.Validate(brief);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "brief.field.required" && error.Path == "$.primarySkill"), Is.True);
        }

        [Test]
        public void Validate_AllowedForbiddenOverlap_Fails()
        {
            LevelBrief brief = ValidBrief() with
            {
                AllowedMechanics = new[] { "debris", "dock", "water" },
                ForbiddenMechanics = new[] { "vine", "water" },
            };

            ValidationResult result = LevelBriefLoader.Validate(brief);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "brief.mechanics.overlap"), Is.True);
        }

        [Test]
        public void Validate_InvalidBoardSize_Fails()
        {
            LevelBrief brief = ValidBrief() with
            {
                BoardSize = new LevelBriefBoardSize
                {
                    Width = 0,
                    Height = -1,
                },
            };

            ValidationResult result = LevelBriefLoader.Validate(brief);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "brief.boardSize.width"), Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "brief.boardSize.height"), Is.True);
        }

        [Test]
        public void LoadDirectory_CurrentRepoBriefs_LoadsInOrder()
        {
            IReadOnlyList<LevelBrief> briefs = LevelBriefLoader.LoadDirectory(GetBriefsDirectory());
            string[] expectedIds = Directory.GetFiles(GetBriefsDirectory(), "*.brief.json", SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetFileName(path).Replace(".brief.json", string.Empty))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            Assert.That(briefs.Select(brief => brief.Id), Is.EqualTo(expectedIds));
        }

        [Test]
        public void Validate_CurrentRepoBriefs_Pass()
        {
            string[] paths = Directory.GetFiles(GetBriefsDirectory(), "*.brief.json", SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.Ordinal);

            foreach (string path in paths)
            {
                ValidationResult result = LevelBriefLoader.ValidateJson(File.ReadAllText(path));

                Assert.That(
                    result.HasErrors,
                    Is.False,
                    $"{Path.GetFileName(path)}: {string.Join(", ", result.Errors.Select(error => error.Code))}");
            }
        }

        private static LevelBrief ValidBrief()
        {
            return new LevelBrief
            {
                Id = "L00",
                Title = "Rule Teach",
                CampaignBand = "production_onboarding",
                Role = "rule_teach",
                PrimarySkill = "action_hazard_literacy",
                SecondarySkill = "valid_group_clearing",
                AllowedMechanics = new[] { "debris", "dock", "water", "crate", "target_states" },
                ForbiddenMechanics = new[] { "ice", "vine", "reinforced_crate" },
                BoardSize = new LevelBriefBoardSize
                {
                    Width = 6,
                    Height = 7,
                },
                TargetCount = 1,
                DensityTarget = "60-75%",
                TargetFirstAttemptWinRate = "90-98%",
                IntendedTensionBeat = "The first valid action visibly advances water.",
                IntendedReleaseBeat = "The puppy is rescued quickly.",
                ExpectedPath = "Take the readable route.",
                ExpectedFailMode = "The board is too unclear.",
                DesignNotes = "Keep this as a real board.",
            };
        }

        private static string GetBriefsDirectory()
        {
            return Path.Combine(GetProjectRoot(), "docs", "level-briefs");
        }

        private static string GetProjectRoot()
        {
            string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? throw new IOException("Could not resolve test assembly directory.");
            return Path.GetFullPath(Path.Combine(root, "..", ".."));
        }
    }
}
