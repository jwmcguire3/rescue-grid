using System;
using System.Collections.Generic;
using System.IO;
using Rescue.Content;

namespace Rescue.SolveAuthoringTool
{
    internal static class AcceptanceVerifier
    {
        private static readonly string DefaultManifestPath = Path.Combine("docs", "level-packets", "phase1.packet.json");
        private static readonly string DefaultLevelsDirectory = Path.Combine("Assets", "StreamingAssets", "Levels");
        private static readonly string DefaultBriefsDirectory = Path.Combine("docs", "level-briefs");
        private static readonly string DefaultResourcesDirectory = Path.Combine("Assets", "Resources", "Levels");

        public static int Run(string[] args)
        {
            if (!TryParseOptions(args, out AcceptanceOptions options, out string? error))
            {
                Console.Error.WriteLine(error);
                PrintUsage();
                return 2;
            }

            if (!File.Exists(options.ManifestPath))
            {
                Console.Error.WriteLine($"Packet manifest was not found at '{options.ManifestPath}'.");
                return 2;
            }

            LevelPacketManifest manifest;
            try
            {
                manifest = LevelPacketManifestLoader.Load(options.ManifestPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Packet manifest could not be loaded: {ex.Message}");
                return 2;
            }

            string? repoRoot = SolveArtifactVerifier.FindRepoRoot(
                options.LevelsDirectory,
                options.ResourcesDirectory,
                options.ManifestPath,
                Directory.GetCurrentDirectory());

            Console.WriteLine($"Acceptance: {manifest.PacketId}");
            Console.WriteLine($"Manifest: {options.ManifestPath}");

            List<string> failedLevelIds = new List<string>();
            string[] expectedLevelIds = manifest.ExpectedLevelIds ?? Array.Empty<string>();
            for (int i = 0; i < expectedLevelIds.Length; i++)
            {
                string levelId = expectedLevelIds[i];
                bool passed = VerifyLevel(levelId, manifest, options, repoRoot);
                if (!passed)
                {
                    failedLevelIds.Add(levelId);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Acceptance summary: {expectedLevelIds.Length - failedLevelIds.Count}/{expectedLevelIds.Length} level(s) passed.");
            if (failedLevelIds.Count > 0)
            {
                Console.WriteLine("Failed level ids: " + string.Join(", ", failedLevelIds));
                return 1;
            }

            Console.WriteLine("All packet levels accepted.");
            return 0;
        }

        private static bool VerifyLevel(
            string levelId,
            LevelPacketManifest manifest,
            AcceptanceOptions options,
            string? repoRoot)
        {
            string levelPath = Path.Combine(options.LevelsDirectory, levelId + ".json");
            string briefPath = Path.Combine(options.BriefsDirectory, levelId + ".brief.json");
            string solvePath = Path.Combine(options.ResourcesDirectory, levelId + ".solve.json");
            string goldenPath = Path.Combine(options.ResourcesDirectory, levelId + ".golden.json");

            List<string> failures = new List<string>();
            Console.WriteLine();
            Console.WriteLine($"{levelId}:");
            RequireFile("Playable level JSON", levelPath, failures);
            RequireFile("Brief JSON", briefPath, failures);
            RequireFile("Solve JSON", solvePath, failures);
            RequireFile("Golden JSON", goldenPath, failures);

            LevelJson? level = null;
            LevelBrief? brief = null;

            if (File.Exists(levelPath))
            {
                string levelJson = File.ReadAllText(levelPath);
                ValidationResult coreResult = Validator.Validate(levelJson);
                WriteValidation("Core validation", coreResult);
                if (coreResult.HasErrors)
                {
                    failures.Add("core validation");
                }
                else
                {
                    level = ContentJson.DeserializeLevel(levelJson);
                }
            }
            else
            {
                Console.WriteLine("  Core validation: FAIL");
                Console.WriteLine("    Error: acceptance.level.missing at $");
                Console.WriteLine($"      Level JSON was not found at '{levelPath}'.");
            }

            if (level is not null)
            {
                ValidationResult phase1Result = Phase1PolicyValidator.Validate(level, manifest);
                WriteValidation("Phase 1 policy warnings", phase1Result);
                if (phase1Result.HasErrors)
                {
                    failures.Add("Phase 1 policy");
                }

                if (File.Exists(briefPath))
                {
                    string briefJson = File.ReadAllText(briefPath);
                    ValidationResult briefSchemaResult = LevelBriefLoader.ValidateJson(briefJson);
                    WriteValidation("Brief JSON", briefSchemaResult);
                    if (briefSchemaResult.HasErrors)
                    {
                        failures.Add("brief JSON");
                    }
                    else
                    {
                        brief = ContentJson.DeserializeLevelBrief(briefJson);
                        ValidationResult conformanceResult = BriefConformanceValidator.Validate(level, brief);
                        WriteValidation("Brief conformance", conformanceResult);
                        if (conformanceResult.HasErrors)
                        {
                            failures.Add("brief conformance");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("  Brief JSON: FAIL");
                    Console.WriteLine("    Error: acceptance.brief.missing at $");
                    Console.WriteLine($"      Brief JSON was not found at '{briefPath}'.");
                }

                if (brief is not null)
                {
                    ValidationResult readabilityResult = ReadabilityPolicyValidator.Validate(level, brief);
                    WriteValidation("Readability warnings", readabilityResult);
                    if (readabilityResult.HasErrors)
                    {
                        failures.Add("readability");
                    }
                }
                else
                {
                    Console.WriteLine("  Readability warnings: SKIP");
                    Console.WriteLine("    Skipped because brief JSON did not pass required checks.");
                }
            }
            else
            {
                Console.WriteLine("  Phase 1 policy warnings: SKIP");
                Console.WriteLine("    Skipped because core validation did not pass.");
                Console.WriteLine("  Brief conformance: SKIP");
                Console.WriteLine("    Skipped because core validation did not pass.");
                Console.WriteLine("  Readability warnings: SKIP");
                Console.WriteLine("    Skipped because core validation did not pass.");
            }

            VerifyArtifact("Solve", solvePath, repoRoot, isGolden: false, failures);
            VerifyArtifact("Golden", goldenPath, repoRoot, isGolden: true, failures);

            bool passed = failures.Count == 0;
            Console.WriteLine($"  Result: {(passed ? "PASS" : "FAIL")}");
            return passed;
        }

        private static void RequireFile(string label, string path, List<string> failures)
        {
            if (File.Exists(path))
            {
                Console.WriteLine($"  {label}: PASS ({path})");
                return;
            }

            Console.WriteLine($"  {label}: FAIL ({path})");
            failures.Add(label);
        }

        private static void VerifyArtifact(
            string label,
            string path,
            string? repoRoot,
            bool isGolden,
            List<string> failures)
        {
            SolveArtifactVerificationResult result = isGolden
                ? SolveArtifactVerifier.VerifyGoldenPath(path, repoRoot)
                : SolveArtifactVerifier.VerifySolvePath(path, repoRoot);
            Console.WriteLine($"  {label}: expected {result.ExpectedOutcome}, got {result.ActualOutcome} -> {(result.Passed ? "PASS" : "FAIL")}{FormatFailure(result.Failure)}");
            if (!result.Passed)
            {
                failures.Add(label);
            }
        }

        private static void WriteValidation(string label, ValidationResult result)
        {
            Console.WriteLine($"  {label}: {Status(result)}");
            if (result.Errors.Count == 0)
            {
                Console.WriteLine("    OK");
                return;
            }

            for (int i = 0; i < result.Errors.Count; i++)
            {
                ValidationError error = result.Errors[i];
                Console.WriteLine($"    {error.Severity}: {error.Code} at {error.Path}");
                Console.WriteLine($"      {error.Message}");
            }
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
            return failure is null ? string.Empty : $" ({failure})";
        }

        private static bool TryParseOptions(string[] args, out AcceptanceOptions options, out string? error)
        {
            string manifestPath = DefaultManifestPath;
            string levelsDirectory = DefaultLevelsDirectory;
            string briefsDirectory = DefaultBriefsDirectory;
            string resourcesDirectory = DefaultResourcesDirectory;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "--manifest", StringComparison.Ordinal))
                {
                    if (!TryReadValue(args, ref i, out manifestPath, out error))
                    {
                        options = default;
                        return false;
                    }
                }
                else if (string.Equals(arg, "--levels-dir", StringComparison.Ordinal))
                {
                    if (!TryReadValue(args, ref i, out levelsDirectory, out error))
                    {
                        options = default;
                        return false;
                    }
                }
                else if (string.Equals(arg, "--briefs-dir", StringComparison.Ordinal))
                {
                    if (!TryReadValue(args, ref i, out briefsDirectory, out error))
                    {
                        options = default;
                        return false;
                    }
                }
                else if (string.Equals(arg, "--resources-dir", StringComparison.Ordinal))
                {
                    if (!TryReadValue(args, ref i, out resourcesDirectory, out error))
                    {
                        options = default;
                        return false;
                    }
                }
                else
                {
                    options = default;
                    error = $"Unknown --verify-acceptance argument '{arg}'.";
                    return false;
                }
            }

            options = new AcceptanceOptions(manifestPath, levelsDirectory, briefsDirectory, resourcesDirectory);
            error = null;
            return true;
        }

        private static bool TryReadValue(string[] args, ref int index, out string value, out string? error)
        {
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = string.Empty;
                error = $"Argument '{args[index]}' requires a value.";
                return false;
            }

            index++;
            value = args[index];
            error = null;
            return true;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: --verify-acceptance [--manifest <path>] [--levels-dir <dir>] [--briefs-dir <dir>] [--resources-dir <dir>]");
        }

        private readonly record struct AcceptanceOptions(
            string ManifestPath,
            string LevelsDirectory,
            string BriefsDirectory,
            string ResourcesDirectory);
    }
}
