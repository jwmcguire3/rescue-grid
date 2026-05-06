using System;
using System.Collections.Generic;
using System.IO;
using Rescue.Core.State;
using Rescue.Content;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            PrintUsage();
            return 2;
        }

        try
        {
            return args[0] switch
            {
                "validate" => ValidateSingle(args[1]),
                "validate-all" => ValidateAll(args[1]),
                "validate-phase1" => ValidatePhase1Single(args[1]),
                "validate-phase1-all" => ValidatePhase1All(args[1]),
                "preview" => Preview(args[1]),
                "readability" => args.Length >= 3 ? ReadabilitySingle(args[1], args[2]) : MissingCommandArguments("readability"),
                "readability-all" => args.Length >= 3 ? ReadabilityAll(args[1], args[2]) : MissingCommandArguments("readability-all"),
                _ => UnknownCommand(args[0]),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int ValidateSingle(string path)
    {
        string json = File.ReadAllText(path);
        ValidationResult result = Validator.Validate(json);
        WriteResult(path, result);
        return result.HasErrors ? 1 : 0;
    }

    private static int ValidateAll(string levelsDir)
    {
        string[] files = Directory.GetFiles(levelsDir, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        bool hasErrors = false;
        for (int i = 0; i < files.Length; i++)
        {
            string json = File.ReadAllText(files[i]);
            ValidationResult result = Validator.Validate(json);
            WriteResult(files[i], result);
            hasErrors |= result.HasErrors;
        }

        return hasErrors ? 1 : 0;
    }

    private static int ValidatePhase1Single(string path)
    {
        ValidationResult result = ValidatePhase1(path);
        WriteResult(path, result);
        return result.HasErrors ? 1 : 0;
    }

    private static int ValidatePhase1All(string levelsDir)
    {
        string[] files = Directory.GetFiles(levelsDir, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        bool hasErrors = false;
        for (int i = 0; i < files.Length; i++)
        {
            ValidationResult result = ValidatePhase1(files[i]);
            WriteResult(files[i], result);
            hasErrors |= result.HasErrors;
        }

        return hasErrors ? 1 : 0;
    }

    private static ValidationResult ValidatePhase1(string path)
    {
        string json = File.ReadAllText(path);
        ValidationResult coreResult = Validator.Validate(json);
        if (coreResult.HasErrors)
        {
            return coreResult;
        }

        LevelJson level = ContentJson.DeserializeLevel(json);
        ValidationResult policyResult = Phase1PolicyValidator.Validate(level);
        if (policyResult.Errors.Count == 0)
        {
            return coreResult;
        }

        ValidationError[] combined = new ValidationError[coreResult.Errors.Count + policyResult.Errors.Count];
        for (int i = 0; i < coreResult.Errors.Count; i++)
        {
            combined[i] = coreResult.Errors[i];
        }

        for (int i = 0; i < policyResult.Errors.Count; i++)
        {
            combined[coreResult.Errors.Count + i] = policyResult.Errors[i];
        }

        return ValidationResult.FromErrors(combined);
    }

    private static int Preview(string path)
    {
        string json = File.ReadAllText(path);
        ValidationResult result = Validator.Validate(json);
        WriteResult(path, result);

        LevelJson level;
        try
        {
            level = ContentJson.DeserializeLevel(json);
        }
        catch (ContentJsonException)
        {
            Console.WriteLine("Preview unavailable: JSON did not deserialize.");
            return 1;
        }

        Console.Write(AsciiPreview.Render(level));
        return result.HasErrors ? 1 : 0;
    }

    private static int ReadabilitySingle(string levelPath, string briefPath)
    {
        ReadabilityRunResult result = RunReadability(levelPath, briefPath);
        WriteResult(levelPath, result.Validation);
        if (result.Metrics is not null)
        {
            WriteMetrics(result.Metrics);
        }

        return result.Validation.HasErrors ? 1 : 0;
    }

    private static int ReadabilityAll(string levelsDir, string briefsDir)
    {
        string[] files = Directory.GetFiles(levelsDir, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        bool hasErrors = false;
        for (int i = 0; i < files.Length; i++)
        {
            string levelPath = files[i];
            string levelId = Path.GetFileNameWithoutExtension(levelPath);
            string briefPath = Path.Combine(briefsDir, $"{levelId}.brief.json");
            ReadabilityRunResult result = RunReadability(levelPath, briefPath);
            WriteResult(levelPath, result.Validation);
            if (result.Metrics is not null)
            {
                WriteMetrics(result.Metrics);
            }

            hasErrors |= result.Validation.HasErrors;
        }

        return hasErrors ? 1 : 0;
    }

    private static ReadabilityRunResult RunReadability(string levelPath, string briefPath)
    {
        string json = File.ReadAllText(levelPath);
        ValidationResult coreResult = Validator.Validate(json);
        if (coreResult.HasErrors)
        {
            return new ReadabilityRunResult(coreResult, Metrics: null);
        }

        LevelJson level = ContentJson.DeserializeLevel(json);
        LevelReadabilityMetrics metrics = LevelReadabilityAnalyzer.Analyze(level);
        BriefReadResult brief = ReadBriefForReadability(briefPath, level.Id);
        ValidationResult readabilityResult = ReadabilityPolicyValidator.Validate(level, brief.Brief, metrics);
        return new ReadabilityRunResult(
            Combine(coreResult, brief.Warnings, readabilityResult),
            metrics);
    }

    private static BriefReadResult ReadBriefForReadability(string briefPath, string levelId)
    {
        if (!File.Exists(briefPath))
        {
            return new BriefReadResult(
                new LevelBrief { Id = levelId },
                ValidationResult.FromErrors(new[]
                {
                    new ValidationError(
                        ValidationSeverity.Warning,
                        "readability.brief.missing",
                        $"Level brief was not found at '{briefPath}'.",
                        "$"),
                }));
        }

        try
        {
            return new BriefReadResult(ContentJson.DeserializeLevelBrief(File.ReadAllText(briefPath)), ValidationResult.Success());
        }
        catch (ContentJsonException ex)
        {
            string path = string.IsNullOrWhiteSpace(ex.Path) ? "$" : ex.Path!;
            return new BriefReadResult(
                new LevelBrief { Id = levelId },
                ValidationResult.FromErrors(new[]
                {
                    new ValidationError(
                        ValidationSeverity.Warning,
                        "readability.brief.parse",
                        ex.Message,
                        path),
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

    private static void WriteMetrics(LevelReadabilityMetrics metrics)
    {
        Console.WriteLine("  Metrics:");
        Console.WriteLine($"    totalCells: {metrics.TotalCells}");
        Console.WriteLine($"    occupiedVisualCells: {metrics.OccupiedVisualCells}");
        Console.WriteLine($"    emptyCells: {metrics.EmptyCells}");
        Console.WriteLine($"    floodedVisualCells: {metrics.FloodedVisualCells}");
        Console.WriteLine($"    targetCount: {metrics.TargetCount}");
        Console.WriteLine($"    visualOccupancyRatio: {metrics.VisualOccupancyRatio:0.###}");
        Console.WriteLine($"    legalStartingGroups: {metrics.LegalStartingGroupCount}");
        Console.WriteLine($"    routeAffectingStartingGroups: {metrics.RouteAffectingStartingGroupCount}");
        Console.WriteLine($"    targetReadiness: trapped={metrics.TrappedTargetCount}, progressing={metrics.ProgressingTargetCount}, oneClearAway={metrics.OneClearAwayTargetCount}");
        Console.WriteLine($"    immediateExactTriples: {metrics.ImmediateExactTripleCount}");
        Console.WriteLine($"    groupsLargerThan5: {metrics.OversizedGroupCount}");
        Console.WriteLine($"    singletonDebrisTiles: {metrics.SingletonDebrisTileCount}");
        Console.WriteLine($"    blockerCounts: {FormatBlockerCounts(metrics)}");
        Console.WriteLine($"    debrisCounts: {FormatDebrisCounts(metrics)}");
    }

    private static string FormatBlockerCounts(LevelReadabilityMetrics metrics)
    {
        return $"crate={GetCount(metrics, BlockerType.Crate)}, ice={GetCount(metrics, BlockerType.Ice)}, vine={GetCount(metrics, BlockerType.Vine)}";
    }

    private static string FormatDebrisCounts(LevelReadabilityMetrics metrics)
    {
        return $"A={GetCount(metrics, DebrisType.A)}, B={GetCount(metrics, DebrisType.B)}, C={GetCount(metrics, DebrisType.C)}, D={GetCount(metrics, DebrisType.D)}, E={GetCount(metrics, DebrisType.E)}, F={GetCount(metrics, DebrisType.F)}";
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

    private static void WriteResult(string path, ValidationResult result)
    {
        Console.WriteLine(path);
        if (result.Errors.Count == 0)
        {
            Console.WriteLine("  OK");
            return;
        }

        for (int i = 0; i < result.Errors.Count; i++)
        {
            ValidationError error = result.Errors[i];
            Console.WriteLine($"  {error.Severity}: {error.Code} at {error.Path}");
            Console.WriteLine($"    {error.Message}");
        }
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 2;
    }

    private static int MissingCommandArguments(string command)
    {
        Console.Error.WriteLine($"Command '{command}' is missing required arguments.");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  validate <path>");
        Console.WriteLine("  validate-all <levels-dir>");
        Console.WriteLine("  validate-phase1 <path>");
        Console.WriteLine("  validate-phase1-all <levels-dir>");
        Console.WriteLine("  preview <path>");
        Console.WriteLine("  readability <level-json-path> <brief-json-path>");
        Console.WriteLine("  readability-all <levels-dir> <briefs-dir>");
    }

    private sealed record ReadabilityRunResult(ValidationResult Validation, LevelReadabilityMetrics? Metrics);

    private sealed record BriefReadResult(LevelBrief Brief, ValidationResult Warnings);
}
