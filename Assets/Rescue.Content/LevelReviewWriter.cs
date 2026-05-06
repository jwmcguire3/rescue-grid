using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Rescue.Core.State;

namespace Rescue.Content
{
    public static class LevelReviewWriter
    {
        public const string AutoStartMarker = "<!-- AUTO:START -->";
        public const string AutoEndMarker = "<!-- AUTO:END -->";

        private const int FirstRecommendedFailPathLevel = 3;

        public static LevelReviewWriteResult Write(string levelPath, string briefPath, string outputPath)
        {
            LevelReview review = Build(levelPath, briefPath);
            string nextText = MergeWithExisting(outputPath, review);
            string? outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            File.WriteAllText(outputPath, nextText, Encoding.UTF8);
            return new LevelReviewWriteResult(review.LevelId, outputPath, review.HasErrors);
        }

        public static LevelReviewWriteBatchResult WriteAll(string levelsDir, string briefsDir, string reviewsDir)
        {
            Directory.CreateDirectory(reviewsDir);
            string[] levelPaths = Directory.GetFiles(levelsDir, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(levelPaths, StringComparer.OrdinalIgnoreCase);

            List<LevelReviewWriteResult> results = new List<LevelReviewWriteResult>();
            for (int i = 0; i < levelPaths.Length; i++)
            {
                string levelPath = levelPaths[i];
                string levelId = Path.GetFileNameWithoutExtension(levelPath);
                string briefPath = Path.Combine(briefsDir, levelId + ".brief.json");
                string outputPath = Path.Combine(reviewsDir, levelId + ".review.md");
                results.Add(Write(levelPath, briefPath, outputPath));
            }

            EnsureGitkeepIfEmpty(reviewsDir);

            bool hasErrors = false;
            for (int i = 0; i < results.Count; i++)
            {
                hasErrors |= results[i].HasErrors;
            }

            return new LevelReviewWriteBatchResult(results, hasErrors);
        }

        private static LevelReview Build(string levelPath, string briefPath)
        {
            string levelJson = File.ReadAllText(levelPath);
            ValidationResult coreResult = Validator.Validate(levelJson);
            LevelJson? level = null;
            if (!coreResult.HasErrors)
            {
                level = ContentJson.DeserializeLevel(levelJson);
            }

            string levelId = level?.Id ?? Path.GetFileNameWithoutExtension(levelPath);
            string levelName = level?.Name ?? "<unavailable>";
            LevelBrief? brief = null;
            ValidationResult briefReadResult = ValidationResult.Success();
            ValidationResult briefConformanceResult = ValidationResult.Success();
            ValidationResult phase1Result = ValidationResult.Success();
            ValidationResult readabilityResult = ValidationResult.Success();
            LevelReadabilityMetrics? metrics = null;
            AssistanceComparisonResult? assistanceComparison = null;
            string? assistanceFailure = null;

            if (level is not null)
            {
                phase1Result = Phase1PolicyValidator.Validate(level, Phase1PolicyValidator.LoadDefaultManifest());
                BriefReadResult briefRead = ReadBrief(briefPath);
                brief = briefRead.Brief;
                briefReadResult = briefRead.Result;
                if (brief is not null)
                {
                    briefConformanceResult = BriefConformanceValidator.Validate(level, brief);
                }

                metrics = LevelReadabilityAnalyzer.Analyze(level);
                if (brief is not null)
                {
                    readabilityResult = ReadabilityPolicyValidator.Validate(level, brief, metrics);
                }

                try
                {
                    assistanceComparison = LevelAssistanceComparisonAnalyzer.Compare(
                        level,
                        new AssistanceComparisonOptions(
                            Seed: LevelAssistanceComparisonAnalyzer.DefaultFirstSeed,
                            MaxDepth: LevelAssistanceComparisonAnalyzer.DefaultMaxDepth,
                            FirstSeed: LevelAssistanceComparisonAnalyzer.DefaultFirstSeed,
                            LastSeed: LevelAssistanceComparisonAnalyzer.DefaultFirstSeed));
                }
                catch (Exception ex)
                {
                    assistanceFailure = ex.Message;
                }
            }

            string? repoRoot = SolveArtifactVerifier.FindRepoRoot(levelPath, briefPath, Directory.GetCurrentDirectory());
            string solvePath = ResolveArtifactPath(repoRoot, levelId, ".solve.json");
            string goldenPath = ResolveArtifactPath(repoRoot, levelId, ".golden.json");
            string failPath = ResolveArtifactPath(repoRoot, levelId, ".fail.json");
            SolveArtifactVerificationResult? solveResult = level is null ? null : SolveArtifactVerifier.VerifySolvePath(solvePath, repoRoot);
            SolveArtifactVerificationResult? goldenResult = level is null ? null : SolveArtifactVerifier.VerifyGoldenPath(goldenPath, repoRoot);
            SolveArtifactVerificationResult? failPathResult = level is null ? null : SolveArtifactVerifier.VerifyFailPath(failPath, repoRoot);

            StringBuilder auto = new StringBuilder();
            AppendBriefSummary(auto, brief, level);
            AppendAutomatedStatus(
                auto,
                coreResult,
                phase1Result,
                Combine(briefReadResult, briefConformanceResult),
                Combine(briefReadResult, readabilityResult),
                solveResult,
                goldenResult,
                failPathResult,
                assistanceComparison,
                assistanceFailure,
                ShouldRecommendFailPath(level, brief));
            AppendMetrics(auto, level, metrics);
            AppendDesignerChecklist(auto);

            bool hasErrors = coreResult.HasErrors
                || phase1Result.HasErrors
                || briefReadResult.HasErrors
                || briefConformanceResult.HasErrors
                || readabilityResult.HasErrors
                || (solveResult?.Failed ?? false)
                || (goldenResult?.Failed ?? false)
                || (failPathResult?.Failed ?? false);

            return new LevelReview(levelId, levelName, auto.ToString(), hasErrors);
        }

        private static string MergeWithExisting(string outputPath, LevelReview review)
        {
            string autoBlock = AutoStartMarker + Environment.NewLine
                + review.AutoMarkdown.TrimEnd() + Environment.NewLine
                + AutoEndMarker;
            if (!File.Exists(outputPath))
            {
                return NewReviewText(review, autoBlock);
            }

            string existing = File.ReadAllText(outputPath);
            int start = existing.IndexOf(AutoStartMarker, StringComparison.Ordinal);
            int end = existing.IndexOf(AutoEndMarker, StringComparison.Ordinal);
            string merged;
            if (start >= 0 && end >= start)
            {
                int endAfterMarker = end + AutoEndMarker.Length;
                merged = existing[..start].TrimEnd() + Environment.NewLine + Environment.NewLine
                    + autoBlock
                    + Environment.NewLine + Environment.NewLine + existing[endAfterMarker..].TrimStart();
            }
            else
            {
                merged = existing.TrimEnd() + Environment.NewLine + Environment.NewLine + autoBlock + Environment.NewLine;
            }

            return EnsureManualSections(EnsureTitle(merged, review));
        }

        private static string NewReviewText(LevelReview review, string autoBlock)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# " + review.LevelId + " - " + review.LevelName);
            builder.AppendLine();
            builder.AppendLine(autoBlock);
            builder.AppendLine();
            AppendDefaultDecision(builder);
            builder.AppendLine();
            AppendDefaultNotes(builder);
            return builder.ToString();
        }

        private static string EnsureTitle(string text, LevelReview review)
        {
            string trimmedStart = text.TrimStart();
            if (trimmedStart.StartsWith("# ", StringComparison.Ordinal))
            {
                return text;
            }

            return "# " + review.LevelId + " - " + review.LevelName + Environment.NewLine + Environment.NewLine + text.TrimStart();
        }

        private static string EnsureManualSections(string text)
        {
            StringBuilder builder = new StringBuilder(text.TrimEnd());
            if (text.IndexOf("## Decision", StringComparison.OrdinalIgnoreCase) < 0)
            {
                builder.AppendLine();
                builder.AppendLine();
                AppendDefaultDecision(builder);
            }

            if (text.IndexOf("## Notes", StringComparison.OrdinalIgnoreCase) < 0)
            {
                builder.AppendLine();
                builder.AppendLine();
                AppendDefaultNotes(builder);
            }

            return builder.ToString();
        }

        private static void AppendDefaultDecision(StringBuilder builder)
        {
            builder.AppendLine("## Decision");
            builder.AppendLine();
            builder.AppendLine("- [ ] Accepted");
            builder.AppendLine("- [ ] Needs revision");
            builder.AppendLine("- [ ] Cut");
        }

        private static void AppendDefaultNotes(StringBuilder builder)
        {
            builder.AppendLine("## Notes");
            builder.AppendLine();
        }

        private static void AppendBriefSummary(StringBuilder builder, LevelBrief? brief, LevelJson? level)
        {
            builder.AppendLine("## Brief Summary");
            if (brief is null)
            {
                builder.AppendLine();
                builder.AppendLine("- Warning: Level brief could not be loaded.");
                builder.AppendLine("- Level intent: " + ValueOrMissing(level?.Meta.Intent));
                builder.AppendLine("- Expected path: " + ValueOrMissing(level?.Meta.ExpectedPath));
                builder.AppendLine("- Expected fail mode: " + ValueOrMissing(level?.Meta.ExpectedFailMode));
                builder.AppendLine();
                return;
            }

            builder.AppendLine();
            builder.AppendLine("- Title: " + ValueOrMissing(brief.Title));
            builder.AppendLine("- Role: " + ValueOrMissing(brief.Role));
            builder.AppendLine("- Primary skill: " + ValueOrMissing(brief.PrimarySkill));
            builder.AppendLine("- Secondary skill: " + ValueOrMissing(brief.SecondarySkill));
            builder.AppendLine("- Intended tension: " + ValueOrMissing(brief.IntendedTensionBeat));
            builder.AppendLine("- Intended release: " + ValueOrMissing(brief.IntendedReleaseBeat));
            builder.AppendLine("- Expected path: " + ValueOrMissing(brief.ExpectedPath));
            builder.AppendLine("- Expected fail mode: " + ValueOrMissing(brief.ExpectedFailMode));
            builder.AppendLine("- Design notes: " + ValueOrMissing(brief.DesignNotes));
            builder.AppendLine();
        }

        private static void AppendAutomatedStatus(
            StringBuilder builder,
            ValidationResult coreResult,
            ValidationResult phase1Result,
            ValidationResult briefResult,
            ValidationResult readabilityResult,
            SolveArtifactVerificationResult? solveResult,
            SolveArtifactVerificationResult? goldenResult,
            SolveArtifactVerificationResult? failPathResult,
            AssistanceComparisonResult? assistanceComparison,
            string? assistanceFailure,
            bool failPathRecommended)
        {
            builder.AppendLine("## Automated Status");
            builder.AppendLine();
            AppendValidationStatus(builder, "Core validation", coreResult);
            AppendValidationStatus(builder, "Phase 1 policy", phase1Result);
            AppendValidationStatus(builder, "Brief conformance", briefResult);
            AppendValidationStatus(builder, "Readability/density", readabilityResult);
            AppendArtifactStatus(builder, "Solve path", solveResult, missingIsWarning: true);
            AppendArtifactStatus(builder, "Golden path", goldenResult, missingIsWarning: true);
            AppendArtifactStatus(builder, "Fail path", failPathResult, missingIsWarning: failPathRecommended);
            AppendAssistanceStatus(builder, assistanceComparison, assistanceFailure);
            builder.AppendLine();
        }

        private static void AppendValidationStatus(StringBuilder builder, string label, ValidationResult result)
        {
            builder.AppendLine("- " + label + ": " + Status(result));
            for (int i = 0; i < result.Errors.Count; i++)
            {
                ValidationError error = result.Errors[i];
                builder.AppendLine("  - " + error.Severity + ": " + error.Code + " at " + error.Path + " - " + error.Message);
            }
        }

        private static void AppendArtifactStatus(StringBuilder builder, string label, SolveArtifactVerificationResult? result, bool missingIsWarning)
        {
            if (result is null)
            {
                builder.AppendLine("- " + label + ": NOT CHECKED");
                return;
            }

            if (result.Missing)
            {
                builder.AppendLine("- " + label + ": " + (missingIsWarning ? "WARN" : "MISSING OPTIONAL") + " - " + result.Failure);
                return;
            }

            builder.AppendLine("- " + label + ": " + (result.Passed ? "PASS" : "FAIL")
                + " - expected " + result.ExpectedOutcome
                + ", got " + result.ActualOutcome
                + FormatFailure(result.Failure));
        }

        private static void AppendAssistanceStatus(StringBuilder builder, AssistanceComparisonResult? comparison, string? failure)
        {
            if (comparison is null)
            {
                builder.AppendLine("- Assistance comparison: WARN - " + (string.IsNullOrWhiteSpace(failure) ? "Not checked." : failure));
                return;
            }

            builder.AppendLine("- Assistance comparison: " + (comparison.NoAssistanceFailsAuthoredSucceeds ? "WARN" : "PASS"));
            for (int i = 0; i < comparison.Modes.Length; i++)
            {
                AssistanceModeResult mode = comparison.Modes[i];
                builder.AppendLine("  - " + FormatMode(mode.Mode)
                    + ": winFound=" + mode.WinFound
                    + ", outcome=" + mode.Outcome
                    + ", seed=" + mode.SeedUsed.ToString(CultureInfo.InvariantCulture)
                    + ", actions=" + mode.ActionCount.ToString(CultureInfo.InvariantCulture));
            }

            if (comparison.NoAssistanceFailsAuthoredSucceeds)
            {
                builder.AppendLine("  - Warning: " + LevelAssistanceComparisonAnalyzer.DependencyWarning);
            }

            if (comparison.AuthoredEmergencyOnlyWin)
            {
                builder.AppendLine("  - Warning: Authored solve may depend on emergency-only assistance.");
            }
        }

        private static void AppendMetrics(StringBuilder builder, LevelJson? level, LevelReadabilityMetrics? metrics)
        {
            builder.AppendLine("## Key Metrics");
            builder.AppendLine();
            if (level is null || metrics is null)
            {
                builder.AppendLine("- Density: unavailable");
                builder.AppendLine("- Target count: unavailable");
                builder.AppendLine("- Water interval: unavailable");
                builder.AppendLine("- Assistance chance: unavailable");
                builder.AppendLine("- Legal starting groups: unavailable");
                builder.AppendLine();
                return;
            }

            builder.AppendLine("- Density: " + metrics.OccupiedVisualCells.ToString(CultureInfo.InvariantCulture)
                + "/" + metrics.TotalCells.ToString(CultureInfo.InvariantCulture)
                + " (" + FormatPercent(metrics.VisualOccupancyRatio) + ")");
            builder.AppendLine("- Target count: " + metrics.TargetCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- Water interval: " + level.Water.RiseInterval.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- Assistance chance: " + level.Assistance.Chance.ToString("0.###", CultureInfo.InvariantCulture));
            builder.AppendLine("- Legal starting groups: " + metrics.LegalStartingGroupCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine();
        }

        private static void AppendDesignerChecklist(StringBuilder builder)
        {
            builder.AppendLine("## Designer Checklist");
            builder.AppendLine();
            builder.AppendLine("- [ ] First move readable?");
            builder.AppendLine("- [ ] Wrong move tempting?");
            builder.AppendLine("- [ ] Rescue order central?");
            builder.AppendLine("- [ ] Dock pressure self-authored?");
            builder.AppendLine("- [ ] Water fair?");
            builder.AppendLine("- [ ] Target states visible?");
            builder.AppendLine("- [ ] Level ends emotionally on rescue?");
        }

        private static BriefReadResult ReadBrief(string briefPath)
        {
            if (!File.Exists(briefPath))
            {
                return new BriefReadResult(
                    null,
                    ValidationResult.FromErrors(new[]
                    {
                        new ValidationError(
                            ValidationSeverity.Warning,
                            "review.brief.missing",
                            "Level brief was not found at '" + briefPath + "'.",
                            "$"),
                    }));
            }

            string briefJson = File.ReadAllText(briefPath);
            ValidationResult schema = LevelBriefLoader.ValidateJson(briefJson);
            if (schema.HasErrors)
            {
                return new BriefReadResult(null, schema);
            }

            try
            {
                return new BriefReadResult(ContentJson.DeserializeLevelBrief(briefJson), schema);
            }
            catch (ContentJsonException ex)
            {
                string path = string.IsNullOrWhiteSpace(ex.Path) ? "$" : ex.Path!;
                return new BriefReadResult(
                    null,
                    ValidationResult.FromErrors(new[]
                    {
                        new ValidationError(ValidationSeverity.Warning, "review.brief.parse", ex.Message, path),
                    }));
            }
        }

        private static ValidationResult Combine(params ValidationResult[] results)
        {
            List<ValidationError> errors = new List<ValidationError>();
            for (int i = 0; i < results.Length; i++)
            {
                for (int e = 0; e < results[i].Errors.Count; e++)
                {
                    errors.Add(results[i].Errors[e]);
                }
            }

            return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.FromErrors(errors);
        }

        private static bool ShouldRecommendFailPath(LevelJson? level, LevelBrief? brief)
        {
            if (level is null || !IsPacketLevelAtOrAfter(level.Id, FirstRecommendedFailPathLevel))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(level.Meta.ExpectedFailMode)
                || !string.IsNullOrWhiteSpace(brief?.ExpectedFailMode);
        }

        private static bool IsPacketLevelAtOrAfter(string levelId, int firstRecommendedLevel)
        {
            if (levelId.Length != 3 || levelId[0] != 'L')
            {
                return false;
            }

            return int.TryParse(levelId.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                && index >= firstRecommendedLevel;
        }

        private static string ResolveArtifactPath(string? repoRoot, string levelId, string suffix)
        {
            if (repoRoot is not null)
            {
                return Path.Combine(repoRoot, "Assets", "Resources", "Levels", levelId + suffix);
            }

            return Path.Combine("Assets", "Resources", "Levels", levelId + suffix);
        }

        private static void EnsureGitkeepIfEmpty(string reviewsDir)
        {
            string[] reviewFiles = Directory.GetFiles(reviewsDir, "*.review.md", SearchOption.TopDirectoryOnly);
            if (reviewFiles.Length > 0)
            {
                return;
            }

            File.WriteAllText(Path.Combine(reviewsDir, ".gitkeep"), string.Empty, Encoding.UTF8);
        }

        private static string Status(ValidationResult result)
        {
            if (result.HasErrors)
            {
                return "FAIL";
            }

            return result.HasWarnings ? "WARN" : "PASS";
        }

        private static string FormatFailure(string? failure)
        {
            return string.IsNullOrWhiteSpace(failure) ? string.Empty : " (" + failure + ")";
        }

        private static string FormatPercent(double ratio)
        {
            return (ratio * 100.0d).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        private static string FormatMode(AssistanceComparisonMode mode)
        {
            return mode switch
            {
                AssistanceComparisonMode.Authored => "authored",
                AssistanceComparisonMode.NoAssistance => "no-assistance",
                AssistanceComparisonMode.MaximumEmergency => "maximum-emergency",
                AssistanceComparisonMode.AuthoredNoEmergency => "authored-no-emergency",
                _ => mode.ToString(),
            };
        }

        private static string ValueOrMissing(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<missing>" : value;
        }

        private sealed record BriefReadResult(LevelBrief? Brief, ValidationResult Result);

        private sealed record LevelReview(string LevelId, string LevelName, string AutoMarkdown, bool HasErrors);
    }

    public sealed record LevelReviewWriteResult(string LevelId, string OutputPath, bool HasErrors);

    public sealed record LevelReviewWriteBatchResult(IReadOnlyList<LevelReviewWriteResult> Results, bool HasErrors);
}
