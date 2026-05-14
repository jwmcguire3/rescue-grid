using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rescue.Content;

namespace Rescue.Content.Tests
{
    public sealed class LevelPacketManifestTests
    {
        [Test]
        public void Load_ValidRepoManifest_Passes()
        {
            LevelPacketManifest manifest = LevelPacketManifestLoader.Load(GetRepoManifestPath());

            Assert.That(manifest.PacketId, Is.EqualTo("phase1"));
            Assert.That(manifest.FirstLevelId, Is.EqualTo("L00"));
            Assert.That(manifest.LastLevelId, Is.EqualTo("L40"));
            Assert.That(manifest.ExpectedLevelIds, Has.Length.EqualTo(41));
        }

        [Test]
        public void Validate_DuplicateExpectedLevelIds_Fails()
        {
            LevelPacketManifest manifest = ValidManifest() with
            {
                ExpectedLevelIds = new[] { "L00", "L01", "L01" },
                LastLevelId = "L01",
            };

            ValidationResult result = LevelPacketManifestLoader.Validate(manifest);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "packet.expectedLevelIds.duplicate"), Is.True);
        }

        [Test]
        public void Validate_MalformedLevelId_Fails()
        {
            LevelPacketManifest manifest = ValidManifest() with
            {
                ExpectedLevelIds = new[] { "L00", "Level01" },
                LastLevelId = "Level01",
            };

            ValidationResult result = LevelPacketManifestLoader.Validate(manifest);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "packet.levelId.malformed"), Is.True);
        }

        [Test]
        public void Validate_DockJamOutsideExpectedLevelIds_Fails()
        {
            LevelPacketManifest manifest = ValidManifest() with
            {
                DockJamLevelIds = new[] { "L09" },
            };

            ValidationResult result = LevelPacketManifestLoader.Validate(manifest);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "packet.dockJamLevelIds.outsideExpected"), Is.True);
        }

        [Test]
        public void RepoManifest_ExpectedLevelIdsHaveAuthoredLevelFiles()
        {
            LevelPacketManifest manifest = LevelPacketManifestLoader.Load(GetRepoManifestPath());
            string[] authoredLevelIds = Directory.GetFiles(GetAuthoredLevelsDirectory(), "L*.json", SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.IsSubsetOf(manifest.ExpectedLevelIds, authoredLevelIds);
        }

        private static LevelPacketManifest ValidManifest()
        {
            return new LevelPacketManifest
            {
                PacketId = "phase1",
                DisplayName = "Phase 1 Level Packet",
                FirstLevelId = "L00",
                LastLevelId = "L02",
                ExpectedLevelIds = new[] { "L00", "L01", "L02" },
                RuleTeachLevelIds = new[] { "L00" },
                DockJamLevelIds = new[] { "L01", "L02" },
                StaticVineIntroLevelIds = Array.Empty<string>(),
                DebrisPoolBands = new[]
                {
                    new DebrisPoolBand
                    {
                        FirstLevelId = "L00",
                        LastLevelId = "L02",
                        DebrisTypePoolSize = 5,
                    },
                },
                WaterIntervalMinimum = 6,
                Notes = "Test manifest.",
            };
        }

        private static string GetRepoManifestPath()
        {
            return Path.Combine(GetProjectRoot(), "docs", "level-packets", "phase1.packet.json");
        }

        private static string GetAuthoredLevelsDirectory()
        {
            return Path.Combine(GetProjectRoot(), "Assets", "StreamingAssets", "Levels");
        }

        private static string GetProjectRoot()
        {
            string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? throw new IOException("Could not resolve test assembly directory.");
            return Path.GetFullPath(Path.Combine(root, "..", ".."));
        }
    }
}
