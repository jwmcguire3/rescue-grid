using System;
using System.Collections.Generic;
using System.IO;

namespace Rescue.Content
{
    public static class LevelBriefLoader
    {
        public static LevelBrief Load(string path)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            LevelBrief brief = ContentJson.DeserializeLevelBrief(File.ReadAllText(path));
            ValidationResult result = Validate(brief);
            if (result.HasErrors)
            {
                throw new InvalidDataException($"Level brief is invalid: {string.Join(", ", ErrorCodes(result.Errors))}");
            }

            return brief;
        }

        public static IReadOnlyList<LevelBrief> LoadDirectory(string directory)
        {
            if (directory is null)
            {
                throw new ArgumentNullException(nameof(directory));
            }

            string[] paths = Directory.GetFiles(directory, "*.brief.json", SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.Ordinal);

            LevelBrief[] briefs = new LevelBrief[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                briefs[i] = Load(paths[i]);
            }

            return briefs;
        }

        public static ValidationResult ValidateJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return ValidationResult.FromErrors(new[]
                {
                    Error("json.empty", "Level brief JSON must not be empty.", "$"),
                });
            }

            LevelBrief brief;
            try
            {
                brief = ContentJson.DeserializeLevelBrief(json);
            }
            catch (ContentJsonException ex)
            {
                string path = string.IsNullOrWhiteSpace(ex.Path) ? "$" : ex.Path!;
                return ValidationResult.FromErrors(new[]
                {
                    Error("json.parse", ex.Message, path),
                });
            }

            return Validate(brief);
        }

        public static ValidationResult Validate(LevelBrief brief)
        {
            if (brief is null)
            {
                throw new ArgumentNullException(nameof(brief));
            }

            List<ValidationError> errors = new List<ValidationError>();
            AddRequiredTextError(brief.Id, "$.id", "id", errors);
            AddRequiredTextError(brief.Title, "$.title", "title", errors);
            AddRequiredTextError(brief.CampaignBand, "$.campaignBand", "campaignBand", errors);
            AddRequiredTextError(brief.Role, "$.role", "role", errors);
            AddRequiredTextError(brief.PrimarySkill, "$.primarySkill", "primarySkill", errors);
            AddRequiredTextError(brief.SecondarySkill, "$.secondarySkill", "secondarySkill", errors);
            AddRequiredTextError(brief.DensityTarget, "$.densityTarget", "densityTarget", errors);
            AddRequiredTextError(brief.TargetFirstAttemptWinRate, "$.targetFirstAttemptWinRate", "targetFirstAttemptWinRate", errors);
            AddRequiredTextError(brief.IntendedTensionBeat, "$.intendedTensionBeat", "intendedTensionBeat", errors);
            AddRequiredTextError(brief.IntendedReleaseBeat, "$.intendedReleaseBeat", "intendedReleaseBeat", errors);
            AddRequiredTextError(brief.ExpectedPath, "$.expectedPath", "expectedPath", errors);
            AddRequiredTextError(brief.ExpectedFailMode, "$.expectedFailMode", "expectedFailMode", errors);
            AddRequiredTextError(brief.DesignNotes, "$.designNotes", "designNotes", errors);

            if (!string.IsNullOrWhiteSpace(brief.Id) && !IsLevelId(brief.Id))
            {
                errors.Add(Error("brief.id.malformed", $"Level brief id '{brief.Id}' must use the form L00.", "$.id"));
            }

            ValidateMechanics(brief, errors);
            ValidateBoardSize(brief.BoardSize, errors);

            if (brief.TargetCount <= 0)
            {
                errors.Add(Error("brief.targetCount", "targetCount must be positive.", "$.targetCount"));
            }

            return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.FromErrors(errors);
        }

        private static void ValidateMechanics(LevelBrief brief, List<ValidationError> errors)
        {
            string[]? allowedMechanics = brief.AllowedMechanics;
            string[]? forbiddenMechanics = brief.ForbiddenMechanics;
            if (allowedMechanics is null)
            {
                errors.Add(Error("brief.mechanics.required", "allowedMechanics must be an array.", "$.allowedMechanics"));
                allowedMechanics = Array.Empty<string>();
            }

            if (forbiddenMechanics is null)
            {
                errors.Add(Error("brief.mechanics.required", "forbiddenMechanics must be an array.", "$.forbiddenMechanics"));
                forbiddenMechanics = Array.Empty<string>();
            }

            HashSet<string> allowed = new HashSet<string>(StringComparer.Ordinal);
            ValidateMechanicEntries(allowedMechanics, "$.allowedMechanics", allowed, errors);

            for (int i = 0; i < forbiddenMechanics.Length; i++)
            {
                string? mechanic = forbiddenMechanics[i];
                string path = $"$.forbiddenMechanics[{i}]";
                if (string.IsNullOrWhiteSpace(mechanic))
                {
                    errors.Add(Error("brief.mechanics.entry.required", "Mechanic entries must be nonblank.", path));
                    continue;
                }

                if (allowed.Contains(mechanic))
                {
                    errors.Add(Error(
                        "brief.mechanics.overlap",
                        $"Mechanic '{mechanic}' cannot be both allowed and forbidden.",
                        path));
                }
            }
        }

        private static void ValidateMechanicEntries(
            string[] mechanics,
            string basePath,
            HashSet<string> validMechanics,
            List<ValidationError> errors)
        {
            for (int i = 0; i < mechanics.Length; i++)
            {
                string? mechanic = mechanics[i];
                if (string.IsNullOrWhiteSpace(mechanic))
                {
                    errors.Add(Error("brief.mechanics.entry.required", "Mechanic entries must be nonblank.", $"{basePath}[{i}]"));
                    continue;
                }

                validMechanics.Add(mechanic);
            }
        }

        private static void ValidateBoardSize(LevelBriefBoardSize? boardSize, List<ValidationError> errors)
        {
            if (boardSize is null)
            {
                errors.Add(Error("brief.boardSize.required", "boardSize is required.", "$.boardSize"));
                return;
            }

            if (boardSize.Width <= 0)
            {
                errors.Add(Error("brief.boardSize.width", "boardSize.width must be positive.", "$.boardSize.width"));
            }

            if (boardSize.Height <= 0)
            {
                errors.Add(Error("brief.boardSize.height", "boardSize.height must be positive.", "$.boardSize.height"));
            }
        }

        private static void AddRequiredTextError(string value, string path, string fieldName, List<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(Error("brief.field.required", $"{fieldName} is required.", path));
            }
        }

        private static bool IsLevelId(string levelId)
        {
            return levelId.Length == 3
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
