using System;
using System.Collections.Generic;

namespace Rescue.Content
{
    public static class Phase1PolicyValidator
    {
        public static ValidationResult Validate(LevelJson level)
        {
            if (level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            List<ValidationError> warnings = new List<ValidationError>();
            AddWarnings(level, warnings);

            return warnings.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.FromErrors(warnings);
        }

        public static IReadOnlyList<ValidationError> GetWarnings(LevelJson level)
        {
            return Validate(level).Errors;
        }

        private static void AddWarnings(LevelJson level, List<ValidationError> warnings)
        {
            if (TryGetPhase1LevelNumber(level.Id, out int levelNumber))
            {
                bool expectedDockJam = levelNumber is 1 or 2;
                if (level.Dock.JamEnabled != expectedDockJam)
                {
                    warnings.Add(new ValidationError(
                        ValidationSeverity.Warning,
                        "phase1.dockJamLevel",
                        "Dock Jam should be enabled only on L01 and L02 for Phase 1.",
                        "$.dock.jamEnabled"));
                }

                int expectedPoolSize = levelNumber <= 4 ? 5 : 6;
                if (level.DebrisTypePool.Length != expectedPoolSize)
                {
                    warnings.Add(new ValidationError(
                        ValidationSeverity.Warning,
                        "phase1.debrisPoolSize",
                        $"L{levelNumber:00} should use a debrisTypePool size of {expectedPoolSize} for Phase 1.",
                        "$.debrisTypePool"));
                }

                if (levelNumber == 7
                    && level.Vine.GrowthPriority.Length > 0
                    && level.Vine.GrowthThreshold < 999)
                {
                    warnings.Add(new ValidationError(
                        ValidationSeverity.Warning,
                        "phase1.l07VineGrowth",
                        "L07 is the static vine introduction; vine growth should be disabled.",
                        "$.vine"));
                }
            }

            if (!level.Meta.IsRuleTeach && level.Water.RiseInterval > 0 && level.Water.RiseInterval < 6)
            {
                warnings.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.waterIntervalBelow6",
                    "Phase 1 water.riseInterval should not be below 6 outside the L00 rule-teach special case.",
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

        private static bool TryGetPhase1LevelNumber(string id, out int levelNumber)
        {
            levelNumber = 0;
            if (id.Length != 3 || id[0] != 'L')
            {
                return false;
            }

            return int.TryParse(id[1..], out levelNumber) && levelNumber >= 0 && levelNumber <= 15;
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
