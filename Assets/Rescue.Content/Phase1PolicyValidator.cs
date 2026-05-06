using System;
using System.Collections.Generic;
using System.IO;

namespace Rescue.Content
{
    public static class Phase1PolicyValidator
    {
        private const string DefaultManifestRelativePath = "docs/level-packets/phase1.packet.json";

        public static ValidationResult Validate(LevelJson level)
        {
            return Validate(level, LoadDefaultManifest());
        }

        public static ValidationResult Validate(LevelJson level, LevelPacketManifest manifest)
        {
            if (level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (manifest is null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            List<ValidationError> warnings = new List<ValidationError>();
            AddWarnings(level, manifest, warnings);

            return warnings.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.FromErrors(warnings);
        }

        public static LevelPacketManifest LoadDefaultManifest()
        {
            return LevelPacketManifestLoader.Load(ResolveDefaultManifestPath());
        }

        public static IReadOnlyList<ValidationError> GetWarnings(LevelJson level)
        {
            return Validate(level).Errors;
        }

        public static IReadOnlyList<ValidationError> GetWarnings(LevelJson level, LevelPacketManifest manifest)
        {
            return Validate(level, manifest).Errors;
        }

        private static void AddWarnings(LevelJson level, LevelPacketManifest manifest, List<ValidationError> warnings)
        {
            if (!Contains(manifest.ExpectedLevelIds, level.Id))
            {
                warnings.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.packet.levelNotInManifest",
                    $"Level '{level.Id}' is not in packet manifest '{manifest.PacketId}'; Phase 1 packet policy checks were skipped.",
                    "$.id"));
                return;
            }

            bool expectedDockJam = Contains(manifest.DockJamLevelIds, level.Id);
            if (level.Dock.JamEnabled != expectedDockJam)
            {
                warnings.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.dockJamLevel",
                    $"Dock Jam should be enabled only on configured packet levels ({FormatIds(manifest.DockJamLevelIds)}) for Phase 1.",
                    "$.dock.jamEnabled"));
            }

            DebrisPoolBand? debrisPoolBand = FindDebrisPoolBand(level.Id, manifest);
            if (debrisPoolBand is not null
                && level.DebrisTypePool.Length != debrisPoolBand.DebrisTypePoolSize)
            {
                warnings.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.debrisPoolSize",
                    $"{level.Id} should use a debrisTypePool size of {debrisPoolBand.DebrisTypePoolSize} for Phase 1.",
                    "$.debrisTypePool"));
            }

            if (Contains(manifest.StaticVineIntroLevelIds, level.Id)
                && level.Vine.GrowthPriority.Length > 0
                && level.Vine.GrowthThreshold < 999)
            {
                warnings.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.l07VineGrowth",
                    "Configured static vine introduction levels should have vine growth disabled.",
                    "$.vine"));
            }

            if (!IsRuleTeach(level, manifest)
                && level.Water.RiseInterval > 0
                && manifest.WaterIntervalMinimum > 0
                && level.Water.RiseInterval < manifest.WaterIntervalMinimum)
            {
                warnings.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.waterIntervalBelow6",
                    $"Phase 1 water.riseInterval should not be below {manifest.WaterIntervalMinimum} outside configured rule-teach special cases.",
                    "$.water.riseInterval"));
            }

            if (ContainsReinforcedCrate(level))
            {
                warnings.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.reinforcedCrate",
                    "Reinforced crates are off by default in Phase 1 and should only appear after explicit late-packet tuning approval.",
                    "$.board.tiles"));
            }

            if (level.Assistance.SpawnIntegrity.AllowExactTripleSpawns
                && string.IsNullOrWhiteSpace(level.Meta.Notes))
            {
                warnings.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.spawnIntegrity.exactTripleException",
                    "Phase 1 exact triple spawn exceptions require meta.notes explaining the teaching, coaching, or relief reason.",
                    "$.assistance.spawnIntegrity.allowExactTripleSpawns"));
            }

            if (level.Assistance.SpawnIntegrity.AllowOversizedSpawnGroups
                && string.IsNullOrWhiteSpace(level.Meta.Notes))
            {
                warnings.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.spawnIntegrity.oversizedException",
                    "Phase 1 oversized spawn group exceptions require meta.notes explaining the teaching, coaching, or relief reason.",
                    "$.assistance.spawnIntegrity.allowOversizedSpawnGroups"));
            }
        }

        private static DebrisPoolBand? FindDebrisPoolBand(string levelId, LevelPacketManifest manifest)
        {
            DebrisPoolBand[] bands = manifest.DebrisPoolBands ?? Array.Empty<DebrisPoolBand>();
            for (int i = 0; i < bands.Length; i++)
            {
                DebrisPoolBand band = bands[i];
                if (IsLevelInRange(levelId, band.FirstLevelId, band.LastLevelId, manifest.ExpectedLevelIds))
                {
                    return band;
                }
            }

            return null;
        }

        private static bool IsLevelInRange(string levelId, string firstLevelId, string lastLevelId, string[] expectedLevelIds)
        {
            int levelIndex = IndexOf(expectedLevelIds, levelId);
            int firstIndex = IndexOf(expectedLevelIds, firstLevelId);
            int lastIndex = IndexOf(expectedLevelIds, lastLevelId);
            return levelIndex >= 0
                && firstIndex >= 0
                && lastIndex >= firstIndex
                && levelIndex >= firstIndex
                && levelIndex <= lastIndex;
        }

        private static bool IsRuleTeach(LevelJson level, LevelPacketManifest manifest)
        {
            return level.Meta.IsRuleTeach || Contains(manifest.RuleTeachLevelIds, level.Id);
        }

        private static bool Contains(string[]? ids, string id)
        {
            return IndexOf(ids, id) >= 0;
        }

        private static int IndexOf(string[]? ids, string id)
        {
            string[] values = ids ?? Array.Empty<string>();
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string FormatIds(string[]? ids)
        {
            string[] values = ids ?? Array.Empty<string>();
            return values.Length == 0 ? "none" : string.Join(", ", values);
        }

        private static string ResolveDefaultManifestPath()
        {
            string? currentDirectoryMatch = FindManifestFromDirectory(Directory.GetCurrentDirectory());
            if (currentDirectoryMatch is not null)
            {
                return currentDirectoryMatch;
            }

            string? baseDirectoryMatch = FindManifestFromDirectory(AppContext.BaseDirectory);
            if (baseDirectoryMatch is not null)
            {
                return baseDirectoryMatch;
            }

            throw new FileNotFoundException($"Could not locate default Phase 1 packet manifest at '{DefaultManifestRelativePath}'.");
        }

        private static string? FindManifestFromDirectory(string startDirectory)
        {
            DirectoryInfo? directory = new DirectoryInfo(startDirectory);
            while (directory is not null)
            {
                string candidate = Path.Combine(directory.FullName, DefaultManifestRelativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private static bool ContainsReinforcedCrate(LevelJson level)
        {
            for (int row = 0; row < level.Board.Tiles.Length; row++)
            {
                string[] tileRow = level.Board.Tiles[row];
                for (int col = 0; col < tileRow.Length; col++)
                {
                    if (string.Equals(tileRow[col], "CX", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
