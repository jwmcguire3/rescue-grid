using System;
using System.Collections.Generic;
using System.Globalization;

namespace Rescue.Content
{
    public static class ReadabilityPolicyValidator
    {
        private const double SingletonWarningRatio = 0.20d;
        private const int ExactTripleWarningThreshold = 2;

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

            LevelReadabilityMetrics metrics = LevelReadabilityAnalyzer.Analyze(level);
            return Validate(level, brief, metrics);
        }

        public static ValidationResult Validate(LevelJson level, LevelBrief brief, LevelReadabilityMetrics metrics)
        {
            if (level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (brief is null)
            {
                throw new ArgumentNullException(nameof(brief));
            }

            if (metrics is null)
            {
                throw new ArgumentNullException(nameof(metrics));
            }

            List<ValidationError> warnings = new List<ValidationError>();
            AddDensityWarnings(level, brief, metrics, warnings);
            AddReadabilityWarnings(level, brief, metrics, warnings);
            return warnings.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.FromErrors(warnings);
        }

        public static bool TryParseDensityTarget(string? value, out DensityTarget densityTarget)
        {
            densityTarget = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (!trimmed.EndsWith('%'))
            {
                return false;
            }

            string withoutPercent = trimmed[..^1];
            string[] parts = withoutPercent.Split('-');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!double.TryParse(parts[0].Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double minPercent)
                || !double.TryParse(parts[1].Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double maxPercent))
            {
                return false;
            }

            if (minPercent < 0.0d || maxPercent > 100.0d || minPercent > maxPercent)
            {
                return false;
            }

            densityTarget = new DensityTarget(minPercent / 100.0d, maxPercent / 100.0d);
            return true;
        }

        private static void AddDensityWarnings(
            LevelJson level,
            LevelBrief brief,
            LevelReadabilityMetrics metrics,
            List<ValidationError> warnings)
        {
            if (!TryParseDensityTarget(brief.DensityTarget, out DensityTarget densityTarget))
            {
                warnings.Add(Warning(
                    "readability.densityTarget.malformed",
                    $"Brief densityTarget '{brief.DensityTarget}' must use a percent range like 70-80%.",
                    "$.densityTarget"));
                return;
            }

            if (metrics.VisualOccupancyRatio < densityTarget.Min)
            {
                AddDensityOutOfBandWarning(
                    level,
                    warnings,
                    notedCode: "readability.density.belowTarget.noted",
                    plainCode: "readability.density.belowTarget",
                    message: $"Visual occupancy {FormatPercent(metrics.VisualOccupancyRatio)} is below brief target {FormatPercent(densityTarget.Min)}-{FormatPercent(densityTarget.Max)}.");
            }
            else if (metrics.VisualOccupancyRatio > densityTarget.Max)
            {
                AddDensityOutOfBandWarning(
                    level,
                    warnings,
                    notedCode: "readability.density.aboveTarget.noted",
                    plainCode: "readability.density.aboveTarget",
                    message: $"Visual occupancy {FormatPercent(metrics.VisualOccupancyRatio)} is above brief target {FormatPercent(densityTarget.Min)}-{FormatPercent(densityTarget.Max)}.");
            }
        }

        private static void AddReadabilityWarnings(
            LevelJson level,
            LevelBrief brief,
            LevelReadabilityMetrics metrics,
            List<ValidationError> warnings)
        {
            if (metrics.LegalStartingGroupCount == 0)
            {
                warnings.Add(Warning(
                    "readability.start.noLegalGroups",
                    "Level starts with zero legal debris groups.",
                    "$.board.tiles"));
            }

            if (metrics.RouteAffectingStartingGroupCount == 0)
            {
                warnings.Add(Warning(
                    "readability.start.noRouteAffectingGroups",
                    "No legal starting group touches or affects any target required-neighbor route zone.",
                    "$.board.tiles"));
            }

            if (metrics.TotalCells > 0 && metrics.SingletonDebrisTileCount / (double)metrics.TotalCells > SingletonWarningRatio)
            {
                warnings.Add(Warning(
                    "readability.debris.singletonHeavy",
                    $"Singleton debris tiles are {metrics.SingletonDebrisTileCount}/{metrics.TotalCells}, above the {FormatPercent(SingletonWarningRatio)} readability threshold.",
                    "$.board.tiles"));
            }

            if (metrics.ImmediateExactTripleCount > ExactTripleWarningThreshold && string.IsNullOrWhiteSpace(level.Meta.Notes))
            {
                warnings.Add(Warning(
                    "readability.debris.exactTripleHeavy",
                    $"Level starts with {metrics.ImmediateExactTripleCount} immediate exact triples; more than {ExactTripleWarningThreshold} needs a spawn, teaching, or relief note.",
                    "$.board.tiles"));
            }

            if (metrics.OneClearAwayTargetCount > 0
                && !IsOneClearAwayAllowedRole(brief.Role)
                && string.IsNullOrWhiteSpace(level.Meta.Notes))
            {
                warnings.Add(Warning(
                    "readability.target.oneClearAwayStart",
                    $"Level starts with {metrics.OneClearAwayTargetCount} target(s) one-clear-away, which should be reserved for rule_teach/release roles or justified in meta.notes.",
                    "$.targets"));
            }
        }

        private static void AddDensityOutOfBandWarning(
            LevelJson level,
            List<ValidationError> warnings,
            string notedCode,
            string plainCode,
            string message)
        {
            if (string.IsNullOrWhiteSpace(level.Meta.Notes))
            {
                warnings.Add(Warning(plainCode, message, "$.board.tiles"));
                return;
            }

            warnings.Add(Warning(
                notedCode,
                $"{message} Noted exception: {level.Meta.Notes}",
                "$.board.tiles"));
        }

        private static bool IsOneClearAwayAllowedRole(string role)
        {
            return string.Equals(role, "rule_teach", StringComparison.Ordinal)
                || string.Equals(role, "release", StringComparison.Ordinal);
        }

        private static string FormatPercent(double ratio)
        {
            return (ratio * 100.0d).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        private static ValidationError Warning(string code, string message, string path)
        {
            return new ValidationError(ValidationSeverity.Warning, code, message, path);
        }
    }

    public readonly record struct DensityTarget(double Min, double Max);
}
