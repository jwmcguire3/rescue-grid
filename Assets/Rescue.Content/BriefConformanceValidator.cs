using System;
using System.Collections.Generic;

namespace Rescue.Content
{
    public static class BriefConformanceValidator
    {
        private const int MinimumMeaningfulTokenCount = 4;
        private const int MinimumNonWhitespaceCharacterCount = 24;

        private static readonly string[] StopWords =
        {
            "and",
            "are",
            "but",
            "for",
            "from",
            "into",
            "not",
            "the",
            "then",
            "this",
            "that",
            "use",
            "with",
            "without",
            "you",
            "your",
        };

        public static ValidationResult Validate(LevelJson level, LevelBrief brief)
        {
            if (level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (brief is null)
            {
                throw new ArgumentNullException(nameof(brief));
            }

            List<ValidationError> results = new List<ValidationError>();
            AddIdentityAndShapeResults(level, brief, results);
            AddMechanicResults(level, brief, results);
            AddTargetTileCountResult(level, results);
            AddMetaResults(level, brief, results);

            return results.Count == 0 ? ValidationResult.Success() : ValidationResult.FromErrors(results);
        }

        public static IReadOnlyCollection<string> DetectMechanics(LevelJson level)
        {
            if (level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            HashSet<string> mechanics = new HashSet<string>(StringComparer.Ordinal);
            if (level.DebrisTypePool.Length > 0)
            {
                mechanics.Add("debris");
            }

            if (level.Dock.Size > 0)
            {
                mechanics.Add("dock");
            }

            if (level.InitialFloodedRows > 0 || level.Water.RiseInterval > 0)
            {
                mechanics.Add("water");
            }

            if (level.Vine.GrowthPriority.Length > 0)
            {
                mechanics.Add("vine");
            }

            if (level.Targets.Length > 0)
            {
                mechanics.Add("target_states");
            }

            for (int row = 0; row < level.Board.Tiles.Length; row++)
            {
                string[] tileRow = level.Board.Tiles[row];
                for (int col = 0; col < tileRow.Length; col++)
                {
                    string code = tileRow[col];
                    if (!ContentTileParser.TryParseCell(code, out ContentCellInfo cell))
                    {
                        continue;
                    }

                    switch (cell.Kind)
                    {
                        case ContentCellKind.Debris:
                            mechanics.Add("debris");
                            break;
                        case ContentCellKind.Crate:
                            mechanics.Add("crate");
                            if (cell.Hp > 1)
                            {
                                mechanics.Add("reinforced_crate");
                            }

                            break;
                        case ContentCellKind.Ice:
                            mechanics.Add("ice");
                            break;
                        case ContentCellKind.Vine:
                            mechanics.Add("vine");
                            break;
                        case ContentCellKind.Target:
                            mechanics.Add("target_states");
                            break;
                    }
                }
            }

            string[] sorted = new string[mechanics.Count];
            mechanics.CopyTo(sorted);
            Array.Sort(sorted, StringComparer.Ordinal);
            return sorted;
        }

        private static void AddIdentityAndShapeResults(
            LevelJson level,
            LevelBrief brief,
            List<ValidationError> results)
        {
            if (!string.Equals(level.Id, brief.Id, StringComparison.Ordinal))
            {
                results.Add(Error(
                    "brief.idMismatch",
                    $"Level id '{level.Id}' does not match brief id '{brief.Id}'.",
                    "$.id"));
            }

            if (!string.Equals(level.Name, brief.Title, StringComparison.Ordinal))
            {
                results.Add(Warning(
                    "brief.titleMismatch",
                    $"Level name '{level.Name}' does not match brief title '{brief.Title}'.",
                    "$.name"));
            }

            if (brief.BoardSize is not null)
            {
                if (level.Board.Width != brief.BoardSize.Width)
                {
                    results.Add(Error(
                        "brief.board.widthMismatch",
                        $"Board width {level.Board.Width} does not match brief width {brief.BoardSize.Width}.",
                        "$.board.width"));
                }

                if (level.Board.Height != brief.BoardSize.Height)
                {
                    results.Add(Error(
                        "brief.board.heightMismatch",
                        $"Board height {level.Board.Height} does not match brief height {brief.BoardSize.Height}.",
                        "$.board.height"));
                }
            }

            if (level.Targets.Length != brief.TargetCount)
            {
                results.Add(Error(
                    "brief.targetCountMismatch",
                    $"Level targets count {level.Targets.Length} does not match brief targetCount {brief.TargetCount}.",
                    "$.targets"));
            }
        }

        private static void AddMechanicResults(
            LevelJson level,
            LevelBrief brief,
            List<ValidationError> results)
        {
            IReadOnlyCollection<string> detectedMechanics = DetectMechanics(level);
            HashSet<string> allowed = ToSet(brief.AllowedMechanics);
            HashSet<string> forbidden = ToSet(brief.ForbiddenMechanics);

            foreach (string mechanic in detectedMechanics)
            {
                if (forbidden.Contains(mechanic))
                {
                    results.Add(Error(
                        "brief.mechanic.forbidden",
                        $"Level uses forbidden mechanic '{mechanic}' from the brief.",
                        "$.board.tiles"));
                }

                if (!allowed.Contains(mechanic))
                {
                    results.Add(Warning(
                        "brief.mechanic.unlisted",
                        $"Level uses mechanic '{mechanic}' which is not listed in brief allowedMechanics.",
                        "$.allowedMechanics"));
                }
            }
        }

        private static void AddTargetTileCountResult(LevelJson level, List<ValidationError> results)
        {
            int targetTileCount = CountTargetTiles(level);
            if (targetTileCount != level.Targets.Length)
            {
                results.Add(Error(
                    "brief.targetTileCountMismatch",
                    $"Level has {targetTileCount} target tile(s), but targets[] has {level.Targets.Length} entry/entries.",
                    "$.board.tiles"));
            }
        }

        private static void AddMetaResults(LevelJson level, LevelBrief brief, List<ValidationError> results)
        {
            AddRequiredTextError(level.Meta.Intent, "brief.meta.intent.required", "meta.intent", "$.meta.intent", results);
            bool hasExpectedPath = AddRequiredTextError(
                level.Meta.ExpectedPath,
                "brief.meta.expectedPath.required",
                "meta.expectedPath",
                "$.meta.expectedPath",
                results);
            bool hasExpectedFailMode = AddRequiredTextError(
                level.Meta.ExpectedFailMode,
                "brief.meta.expectedFailMode.required",
                "meta.expectedFailMode",
                "$.meta.expectedFailMode",
                results);
            AddRequiredTextError(
                level.Meta.WhatItProves,
                "brief.meta.whatItProves.required",
                "meta.whatItProves",
                "$.meta.whatItProves",
                results);

            if (hasExpectedPath)
            {
                AddGenericTextWarning(
                    level.Meta.ExpectedPath,
                    "brief.meta.expectedPath.generic",
                    "meta.expectedPath is too short or generic.",
                    "$.meta.expectedPath",
                    results);
                AddUnrelatedTextWarning(
                    brief.ExpectedPath,
                    level.Meta.ExpectedPath,
                    "brief.meta.expectedPath.unrelated",
                    "Brief expectedPath and level meta.expectedPath appear unrelated by keyword overlap.",
                    "$.meta.expectedPath",
                    results);
            }

            if (hasExpectedFailMode)
            {
                AddGenericTextWarning(
                    level.Meta.ExpectedFailMode,
                    "brief.meta.expectedFailMode.generic",
                    "meta.expectedFailMode is too short or generic.",
                    "$.meta.expectedFailMode",
                    results);
                AddUnrelatedTextWarning(
                    brief.ExpectedFailMode,
                    level.Meta.ExpectedFailMode,
                    "brief.meta.expectedFailMode.unrelated",
                    "Brief expectedFailMode and level meta.expectedFailMode appear unrelated by keyword overlap.",
                    "$.meta.expectedFailMode",
                    results);
            }
        }

        private static bool AddRequiredTextError(
            string value,
            string code,
            string fieldName,
            string path,
            List<ValidationError> results)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            results.Add(Error(code, $"{fieldName} is required.", path));
            return false;
        }

        private static void AddGenericTextWarning(
            string text,
            string code,
            string message,
            string path,
            List<ValidationError> results)
        {
            if (CountMeaningfulTokens(text) < MinimumMeaningfulTokenCount
                || CountNonWhitespaceCharacters(text) < MinimumNonWhitespaceCharacterCount)
            {
                results.Add(Warning(code, message, path));
            }
        }

        private static void AddUnrelatedTextWarning(
            string briefText,
            string levelText,
            string code,
            string message,
            string path,
            List<ValidationError> results)
        {
            HashSet<string> briefTokens = Tokenize(briefText);
            HashSet<string> levelTokens = Tokenize(levelText);
            if (briefTokens.Count == 0 || levelTokens.Count == 0)
            {
                return;
            }

            foreach (string token in levelTokens)
            {
                if (briefTokens.Contains(token))
                {
                    return;
                }
            }

            results.Add(Warning(code, message, path));
        }

        private static int CountTargetTiles(LevelJson level)
        {
            int count = 0;
            for (int row = 0; row < level.Board.Tiles.Length; row++)
            {
                string[] tileRow = level.Board.Tiles[row];
                for (int col = 0; col < tileRow.Length; col++)
                {
                    if (ContentTileParser.TryParseCell(tileRow[col], out ContentCellInfo cell)
                        && cell.Kind == ContentCellKind.Target)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static HashSet<string> ToSet(string[]? values)
        {
            HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
            string[] entries = values ?? Array.Empty<string>();
            for (int i = 0; i < entries.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(entries[i]))
                {
                    set.Add(entries[i]);
                }
            }

            return set;
        }

        private static int CountMeaningfulTokens(string text)
        {
            return Tokenize(text).Count;
        }

        private static int CountNonWhitespaceCharacters(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static HashSet<string> Tokenize(string text)
        {
            HashSet<string> tokens = new HashSet<string>(StringComparer.Ordinal);
            char[] buffer = new char[text.Length];
            int length = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                char c = i < text.Length ? char.ToLowerInvariant(text[i]) : ' ';
                if (char.IsLetterOrDigit(c))
                {
                    buffer[length] = c;
                    length++;
                    continue;
                }

                AddToken(buffer, length, tokens);
                length = 0;
            }

            return tokens;
        }

        private static void AddToken(char[] buffer, int length, HashSet<string> tokens)
        {
            if (length < 3)
            {
                return;
            }

            string token = new string(buffer, 0, length);
            if (!IsStopWord(token))
            {
                tokens.Add(token);
            }
        }

        private static bool IsStopWord(string token)
        {
            for (int i = 0; i < StopWords.Length; i++)
            {
                if (string.Equals(token, StopWords[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static ValidationError Error(string code, string message, string path)
        {
            return new ValidationError(ValidationSeverity.Error, code, message, path);
        }

        private static ValidationError Warning(string code, string message, string path)
        {
            return new ValidationError(ValidationSeverity.Warning, code, message, path);
        }
    }
}
