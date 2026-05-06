using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Rescue.Core.State;

namespace Rescue.Content
{
    public static class LevelDesignReportBuilder
    {
        private const double HighAssistanceChance = 0.50d;
        private static readonly string[] MissingLevelRisks = { "Level JSON is missing." };

        public static LevelDesignReport Build(string levelPath, string briefPath)
        {
            return Build(levelPath, briefPath, solvePath: null, goldenPath: null, repoRoot: null);
        }

        public static LevelDesignReport Build(
            string levelPath,
            string briefPath,
            string? solvePath,
            string? goldenPath,
            string? repoRoot)
        {
            if (!File.Exists(levelPath))
            {
                return MissingLevelReport(levelPath, briefPath);
            }

            string levelJson = File.ReadAllText(levelPath);
            ValidationResult coreResult = Validator.Validate(levelJson);
            LevelJson? level = null;
            if (!coreResult.HasErrors)
            {
                level = ContentJson.DeserializeLevel(levelJson);
            }

            string levelId = level?.Id ?? Path.GetFileNameWithoutExtension(levelPath);
            string levelName = level?.Name ?? "<unavailable>";
            ValidationResult phase1Result = ValidationResult.Success();
            ValidationResult briefReadResult = ValidationResult.Success();
            ValidationResult briefConformanceResult = ValidationResult.Success();
            ValidationResult readabilityResult = ValidationResult.Success();
            LevelReadabilityMetrics? metrics = null;
            LevelBrief? brief = null;

            if (level is not null)
            {
                phase1Result = Phase1PolicyValidator.Validate(level, Phase1PolicyValidator.LoadDefaultManifest());
                BriefReadResult briefRead = ReadBrief(briefPath, level.Id);
                briefReadResult = briefRead.Result;
                brief = briefRead.Brief;
                if (brief is not null)
                {
                    briefConformanceResult = BriefConformanceValidator.Validate(level, brief);
                }

                metrics = LevelReadabilityAnalyzer.Analyze(level);
                if (brief is not null)
                {
                    readabilityResult = ReadabilityPolicyValidator.Validate(level, brief, metrics);
                }
            }

            string? resolvedRepoRoot = repoRoot ?? SolveArtifactVerifier.FindRepoRoot(levelPath, briefPath, Directory.GetCurrentDirectory());
            string resolvedSolvePath = solvePath ?? ResolveArtifactPath(resolvedRepoRoot, levelId, ".solve.json");
            string resolvedGoldenPath = goldenPath ?? ResolveArtifactPath(resolvedRepoRoot, levelId, ".golden.json");

            SolveArtifactVerificationResult? solveResult = level is null
                ? null
                : SolveArtifactVerifier.VerifySolvePath(resolvedSolvePath, resolvedRepoRoot);
            SolveArtifactVerificationResult? goldenResult = level is null
                ? null
                : SolveArtifactVerifier.VerifyGoldenPath(resolvedGoldenPath, resolvedRepoRoot);

            List<string> risks = BuildRisks(
                level,
                phase1Result,
                briefConformanceResult,
                readabilityResult,
                metrics,
                goldenResult);

            StringBuilder builder = new StringBuilder();
            AppendHeader(builder, levelId, levelName);
            AppendIdentity(builder, levelPath, briefPath, resolvedSolvePath, resolvedGoldenPath);
            AppendValidation(builder, "Core validation", coreResult);
            AppendValidation(builder, "Phase 1 policy", phase1Result);
            AppendValidation(builder, "Brief conformance", Combine(briefReadResult, briefConformanceResult));
            AppendValidation(builder, "Readability/density", Combine(briefReadResult, readabilityResult));
            AppendBoardMetrics(builder, level, metrics);
            AppendSystems(builder, level);
            AppendArtifactStatus(builder, "Solve", solveResult);
            AppendArtifactStatus(builder, "Golden", goldenResult);
            AppendRisks(builder, risks);

            bool hasErrors = coreResult.HasErrors
                || phase1Result.HasErrors
                || briefReadResult.HasErrors
                || briefConformanceResult.HasErrors
                || readabilityResult.HasErrors
                || (solveResult?.Failed ?? false)
                || (goldenResult?.Failed ?? false);

            return new LevelDesignReport(levelId, builder.ToString(), hasErrors);
        }

        public static LevelDesignReportBatch BuildAll(string levelsDir, string briefsDir)
        {
            Dictionary<string, string> levelPathsById = GetLevelPathsById(levelsDir);
            Dictionary<string, string> briefPathsById = GetBriefPathsById(briefsDir);

            SortedSet<string> ids = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string id, string _) in levelPathsById)
            {
                ids.Add(id);
            }

            foreach ((string id, string _) in briefPathsById)
            {
                ids.Add(id);
            }

            List<LevelDesignReport> reports = new List<LevelDesignReport>();
            StringBuilder text = new StringBuilder();
            string? repoRoot = SolveArtifactVerifier.FindRepoRoot(levelsDir, briefsDir, Directory.GetCurrentDirectory());
            foreach (string id in ids)
            {
                bool hasLevel = levelPathsById.TryGetValue(id, out string? levelPath);
                bool hasBrief = briefPathsById.TryGetValue(id, out string? briefPath);
                LevelDesignReport report;
                if (!hasLevel)
                {
                    report = MissingLevelReport(Path.Combine(levelsDir, id + ".json"), briefPath ?? id);
                }
                else
                {
                    string matchedLevelPath = levelPath ?? throw new InvalidOperationException($"Level path was not resolved for '{id}'.");
                    string matchedBriefPath = hasBrief
                        ? briefPath ?? throw new InvalidOperationException($"Brief path was not resolved for '{id}'.")
                        : Path.Combine(briefsDir, id + ".brief.json");
                    report = Build(matchedLevelPath, matchedBriefPath, solvePath: null, goldenPath: null, repoRoot);
                }

                reports.Add(report);
                if (text.Length > 0)
                {
                    text.AppendLine();
                }

                text.Append(report.Text);
            }

            bool hasErrors = false;
            for (int i = 0; i < reports.Count; i++)
            {
                hasErrors |= reports[i].HasErrors;
            }

            return new LevelDesignReportBatch(reports, text.ToString(), hasErrors);
        }

        private static LevelDesignReport MissingLevelReport(string levelPath, string briefPath)
        {
            string id = Path.GetFileNameWithoutExtension(levelPath);
            StringBuilder builder = new StringBuilder();
            AppendHeader(builder, id, "<missing>");
            AppendIdentity(builder, levelPath, briefPath, solvePath: "<not checked>", goldenPath: "<not checked>");
            AppendValidation(builder, "Core validation", ValidationResult.FromErrors(new[]
            {
                new ValidationError(
                    ValidationSeverity.Error,
                    "designReport.level.missing",
                    $"Level JSON was not found at '{levelPath}'.",
                    "$"),
            }));
            AppendRisks(builder, MissingLevelRisks);
            return new LevelDesignReport(id, builder.ToString(), HasErrors: true);
        }

        private static BriefReadResult ReadBrief(string briefPath, string levelId)
        {
            if (!File.Exists(briefPath))
            {
                return new BriefReadResult(
                    Brief: null,
                    ValidationResult.FromErrors(new[]
                    {
                        new ValidationError(
                            ValidationSeverity.Warning,
                            "designReport.brief.missing",
                            $"Level brief was not found at '{briefPath}'.",
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
                        new ValidationError(ValidationSeverity.Warning, "designReport.brief.parse", ex.Message, path),
                    }));
            }
        }

        private static List<string> BuildRisks(
            LevelJson? level,
            ValidationResult phase1Result,
            ValidationResult briefConformanceResult,
            ValidationResult readabilityResult,
            LevelReadabilityMetrics? metrics,
            SolveArtifactVerificationResult? goldenResult)
        {
            List<string> risks = new List<string>();
            if (goldenResult is null || goldenResult.Missing || goldenResult.Failed)
            {
                risks.Add("No golden path found.");
            }

            if (HasCode(readabilityResult, "readability.density.belowTarget")
                || HasCode(readabilityResult, "readability.density.belowTarget.noted"))
            {
                risks.Add("Density below target.");
            }

            if (HasCode(phase1Result, "phase1.waterIntervalBelow6"))
            {
                risks.Add("Water interval below Phase 1 minimum.");
            }

            if (metrics is not null && metrics.RouteAffectingStartingGroupCount == 0)
            {
                risks.Add("No legal starting group near a target route.");
            }

            if (HasCode(briefConformanceResult, "brief.mechanic.forbidden"))
            {
                risks.Add("Brief forbids a mechanic used by this level.");
            }

            if (level is not null && !string.IsNullOrWhiteSpace(level.Meta.ExpectedFailMode))
            {
                risks.Add("Expected fail mode is not executable yet.");
            }

            if (level is not null && level.Assistance.Chance > HighAssistanceChance)
            {
                risks.Add("Assistance chance high; compare no-assistance solve before accepting.");
            }

            return risks;
        }

        private static void AppendHeader(StringBuilder builder, string levelId, string levelName)
        {
            builder.AppendLine($"Design Report: {levelId} - {levelName}");
            builder.AppendLine(new string('=', 18 + levelId.Length + levelName.Length));
        }

        private static void AppendIdentity(StringBuilder builder, string levelPath, string briefPath, string solvePath, string goldenPath)
        {
            builder.AppendLine("Identity:");
            builder.AppendLine($"  Level file: {levelPath}");
            builder.AppendLine($"  Brief file: {briefPath}");
            builder.AppendLine($"  Solve file: {solvePath}");
            builder.AppendLine($"  Golden file: {goldenPath}");
        }

        private static void AppendValidation(StringBuilder builder, string label, ValidationResult result)
        {
            builder.AppendLine(label + ": " + Status(result));
            if (result.Errors.Count == 0)
            {
                builder.AppendLine("  OK");
                return;
            }

            for (int i = 0; i < result.Errors.Count; i++)
            {
                ValidationError error = result.Errors[i];
                builder.AppendLine($"  {error.Severity}: {error.Code} at {error.Path}");
                builder.AppendLine($"    {error.Message}");
            }
        }

        private static void AppendBoardMetrics(StringBuilder builder, LevelJson? level, LevelReadabilityMetrics? metrics)
        {
            builder.AppendLine("Board Metrics:");
            if (level is null || metrics is null)
            {
                builder.AppendLine("  Unavailable because level JSON did not pass core validation.");
                return;
            }

            builder.AppendLine($"  visual occupancy: {metrics.OccupiedVisualCells}/{metrics.TotalCells} ({FormatPercent(metrics.VisualOccupancyRatio)})");
            builder.AppendLine($"  legal starting groups: {metrics.LegalStartingGroupCount}");
            builder.AppendLine($"  route-affecting starting groups: {metrics.RouteAffectingStartingGroupCount}");
            builder.AppendLine($"  targets: {metrics.TargetCount}");
            builder.AppendLine($"  target readiness approximation: trapped={metrics.TrappedTargetCount}, progressing={metrics.ProgressingTargetCount}, one-clear-away={metrics.OneClearAwayTargetCount}");
            builder.AppendLine($"  blockers by type: crate={GetCount(metrics, BlockerType.Crate)}, ice={GetCount(metrics, BlockerType.Ice)}, vine={GetCount(metrics, BlockerType.Vine)}");
            builder.AppendLine($"  debris pool: {string.Join(", ", level.DebrisTypePool)}");
            builder.AppendLine($"  debris counts: A={GetCount(metrics, DebrisType.A)}, B={GetCount(metrics, DebrisType.B)}, C={GetCount(metrics, DebrisType.C)}, D={GetCount(metrics, DebrisType.D)}, E={GetCount(metrics, DebrisType.E)}, F={GetCount(metrics, DebrisType.F)}");
        }

        private static void AppendSystems(StringBuilder builder, LevelJson? level)
        {
            builder.AppendLine("Systems:");
            if (level is null)
            {
                builder.AppendLine("  Unavailable because level JSON did not pass core validation.");
                return;
            }

            builder.AppendLine($"  water interval: {level.Water.RiseInterval}");
            builder.AppendLine($"  initial flooded rows: {level.InitialFloodedRows}");
            builder.AppendLine($"  dock jam enabled: {level.Dock.JamEnabled}");
            builder.AppendLine($"  assistance chance: {level.Assistance.Chance.ToString("0.###", CultureInfo.InvariantCulture)}");
            builder.AppendLine($"  spawn integrity: allowExactTripleSpawns={level.Assistance.SpawnIntegrity.AllowExactTripleSpawns}, allowOversizedSpawnGroups={level.Assistance.SpawnIntegrity.AllowOversizedSpawnGroups}");
        }

        private static void AppendArtifactStatus(StringBuilder builder, string label, SolveArtifactVerificationResult? result)
        {
            builder.AppendLine(label + " Status:");
            if (result is null)
            {
                builder.AppendLine("  Not checked because level JSON did not pass core validation.");
                return;
            }

            if (result.Missing)
            {
                builder.AppendLine("  Warning: " + result.Failure);
                return;
            }

            builder.AppendLine($"  expected {result.ExpectedOutcome}, got {result.ActualOutcome} -> {(result.Passed ? "PASS" : "FAIL")}{FormatFailure(result.Failure)}");
        }

        private static void AppendRisks(StringBuilder builder, IReadOnlyList<string> risks)
        {
            builder.AppendLine("Top Design Risks:");
            if (risks.Count == 0)
            {
                builder.AppendLine("  None flagged.");
                return;
            }

            for (int i = 0; i < risks.Count; i++)
            {
                builder.AppendLine("  - " + risks[i]);
            }
        }

        private static string ResolveArtifactPath(string? repoRoot, string levelId, string suffix)
        {
            if (repoRoot is not null)
            {
                return Path.Combine(repoRoot, "Assets", "Resources", "Levels", levelId + suffix);
            }

            return Path.Combine("Assets", "Resources", "Levels", levelId + suffix);
        }

        private static Dictionary<string, string> GetLevelPathsById(string levelsDir)
        {
            string[] files = Directory.Exists(levelsDir)
                ? Directory.GetFiles(levelsDir, "*.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> pathsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                string id = Path.GetFileNameWithoutExtension(files[i]);
                pathsById[id] = files[i];
            }

            return pathsById;
        }

        private static Dictionary<string, string> GetBriefPathsById(string briefsDir)
        {
            string[] files = Directory.Exists(briefsDir)
                ? Directory.GetFiles(briefsDir, "*.brief.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> pathsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                string id = fileName.EndsWith(".brief.json", StringComparison.OrdinalIgnoreCase)
                    ? fileName[..^".brief.json".Length]
                    : Path.GetFileNameWithoutExtension(files[i]);
                pathsById[id] = files[i];
            }

            return pathsById;
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

        private static bool HasCode(ValidationResult result, string code)
        {
            for (int i = 0; i < result.Errors.Count; i++)
            {
                if (string.Equals(result.Errors[i].Code, code, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Status(ValidationResult result)
        {
            if (result.HasErrors)
            {
                return "FAIL";
            }

            return result.HasWarnings ? "WARN" : "PASS";
        }

        private static string FormatPercent(double ratio)
        {
            return (ratio * 100.0d).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        private static string FormatFailure(string? failure)
        {
            return failure is null ? string.Empty : " (" + failure + ")";
        }

        private static int GetCount(LevelReadabilityMetrics metrics, BlockerType type)
        {
            metrics.BlockerCountByType.TryGetValue(type, out int count);
            return count;
        }

        private static int GetCount(LevelReadabilityMetrics metrics, DebrisType type)
        {
            metrics.DebrisCountByType.TryGetValue(type, out int count);
            return count;
        }

        private sealed record BriefReadResult(LevelBrief? Brief, ValidationResult Result);
    }

    public sealed record LevelDesignReport(string LevelId, string Text, bool HasErrors);

    public sealed record LevelDesignReportBatch(IReadOnlyList<LevelDesignReport> Reports, string Text, bool HasErrors);
}
