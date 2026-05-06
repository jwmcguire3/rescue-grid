using System;
using System.Collections.Generic;
using System.IO;

namespace Rescue.Content
{
    public static class LevelPacketManifestLoader
    {
        public static LevelPacketManifest Load(string path)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            LevelPacketManifest manifest = ContentJson.DeserializeLevelPacketManifest(File.ReadAllText(path));
            ValidationResult result = Validate(manifest);
            if (result.HasErrors)
            {
                throw new InvalidDataException($"Level packet manifest is invalid: {string.Join(", ", ErrorCodes(result.Errors))}");
            }

            return manifest;
        }

        public static ValidationResult Validate(LevelPacketManifest manifest)
        {
            if (manifest is null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            List<ValidationError> errors = new List<ValidationError>();
            if (string.IsNullOrWhiteSpace(manifest.PacketId))
            {
                errors.Add(Error("packet.packetId", "Packet manifest requires a packetId.", "$.packetId"));
            }

            ValidateLevelId(manifest.FirstLevelId, "$.firstLevelId", errors);
            ValidateLevelId(manifest.LastLevelId, "$.lastLevelId", errors);

            string[] expectedLevelIds = manifest.ExpectedLevelIds ?? Array.Empty<string>();
            if (expectedLevelIds.Length == 0)
            {
                errors.Add(Error("packet.expectedLevelIds.empty", "Packet manifest requires at least one expected level id.", "$.expectedLevelIds"));
            }

            HashSet<string> expectedIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < expectedLevelIds.Length; i++)
            {
                string? levelId = expectedLevelIds[i];
                ValidateLevelId(levelId, $"$.expectedLevelIds[{i}]", errors);
                if (IsLevelId(levelId) && !expectedIds.Add(levelId))
                {
                    errors.Add(Error(
                        "packet.expectedLevelIds.duplicate",
                        $"Packet manifest contains duplicate expected level id '{levelId}'.",
                        $"$.expectedLevelIds[{i}]"));
                }
            }

            if (expectedLevelIds.Length > 0)
            {
                if (!string.Equals(manifest.FirstLevelId, expectedLevelIds[0], StringComparison.Ordinal))
                {
                    errors.Add(Error(
                        "packet.firstLevelId.mismatch",
                        "Packet firstLevelId must match the first expectedLevelIds entry.",
                        "$.firstLevelId"));
                }

                if (!string.Equals(manifest.LastLevelId, expectedLevelIds[expectedLevelIds.Length - 1], StringComparison.Ordinal))
                {
                    errors.Add(Error(
                        "packet.lastLevelId.mismatch",
                        "Packet lastLevelId must match the last expectedLevelIds entry.",
                        "$.lastLevelId"));
                }
            }

            ValidatePolicyIds(manifest.RuleTeachLevelIds, expectedIds, "$.ruleTeachLevelIds", "packet.ruleTeachLevelIds", errors);
            ValidatePolicyIds(manifest.DockJamLevelIds, expectedIds, "$.dockJamLevelIds", "packet.dockJamLevelIds", errors);
            ValidatePolicyIds(manifest.StaticVineIntroLevelIds, expectedIds, "$.staticVineIntroLevelIds", "packet.staticVineIntroLevelIds", errors);
            ValidateDebrisPoolBands(manifest.DebrisPoolBands, expectedIds, errors);

            return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.FromErrors(errors);
        }

        private static void ValidatePolicyIds(
            string[]? levelIds,
            HashSet<string> expectedIds,
            string path,
            string codePrefix,
            List<ValidationError> errors)
        {
            string[] ids = levelIds ?? Array.Empty<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                string? levelId = ids[i];
                string itemPath = $"{path}[{i}]";
                ValidateLevelId(levelId, itemPath, errors);
                if (IsLevelId(levelId) && !expectedIds.Contains(levelId))
                {
                    errors.Add(Error(
                        $"{codePrefix}.outsideExpected",
                        $"Packet policy level id '{levelId}' must be included in expectedLevelIds.",
                        itemPath));
                }
            }
        }

        private static void ValidateDebrisPoolBands(
            DebrisPoolBand[]? bands,
            HashSet<string> expectedIds,
            List<ValidationError> errors)
        {
            DebrisPoolBand[] poolBands = bands ?? Array.Empty<DebrisPoolBand>();
            for (int i = 0; i < poolBands.Length; i++)
            {
                DebrisPoolBand? band = poolBands[i];
                if (band is null)
                {
                    errors.Add(Error(
                        "packet.debrisPoolBands.malformed",
                        "Debris pool band must be an object.",
                        $"$.debrisPoolBands[{i}]"));
                    continue;
                }

                string firstPath = $"$.debrisPoolBands[{i}].firstLevelId";
                string lastPath = $"$.debrisPoolBands[{i}].lastLevelId";
                ValidateLevelId(band.FirstLevelId, firstPath, errors);
                ValidateLevelId(band.LastLevelId, lastPath, errors);
                if (IsLevelId(band.FirstLevelId) && !expectedIds.Contains(band.FirstLevelId))
                {
                    errors.Add(Error(
                        "packet.debrisPoolBands.firstLevelId.outsideExpected",
                        $"Debris pool band firstLevelId '{band.FirstLevelId}' must be included in expectedLevelIds.",
                        firstPath));
                }

                if (IsLevelId(band.LastLevelId) && !expectedIds.Contains(band.LastLevelId))
                {
                    errors.Add(Error(
                        "packet.debrisPoolBands.lastLevelId.outsideExpected",
                        $"Debris pool band lastLevelId '{band.LastLevelId}' must be included in expectedLevelIds.",
                        lastPath));
                }
            }
        }

        private static void ValidateLevelId(string? levelId, string path, List<ValidationError> errors)
        {
            if (!IsLevelId(levelId))
            {
                errors.Add(Error(
                    "packet.levelId.malformed",
                    $"Level id '{levelId}' must use the form L00.",
                    path));
            }
        }

        private static bool IsLevelId(string? levelId)
        {
            return levelId is not null
                && levelId.Length == 3
                && levelId[0] == 'L'
                && char.IsDigit(levelId[1])
                && char.IsDigit(levelId[2]);
        }

        private static ValidationError Error(string code, string message, string path)
        {
            return new ValidationError(ValidationSeverity.Error, code, message, path);
        }

        private static IEnumerable<string> ErrorCodes(IReadOnlyList<ValidationError> errors)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                yield return errors[i].Code;
            }
        }
    }
}
