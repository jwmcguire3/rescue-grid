using System;
using System.Collections.Generic;
using System.IO;
using Rescue.Content;

namespace Rescue.LevelTelemetryTool
{
    internal static class Program
    {
        private const int DefaultSamples = 200;
        private const int DefaultMaxActions = 30;
        private static readonly string DefaultOutputDirectory = Path.Combine("Reports", "LevelTelemetry");

        private static int Main(string[] args)
        {
            try
            {
                TelemetryOptions options = ParseOptions(args);
                IReadOnlyList<string> levelIds = ResolveLevelIds(options);
                string outputPath = Path.GetFullPath(options.OutputDirectory);

                int loadCount = 0;
                for (int levelIndex = 0; levelIndex < levelIds.Count; levelIndex++)
                {
                    string levelId = levelIds[levelIndex];
                    for (int seed = 1; seed <= options.Samples; seed++)
                    {
                        Loader.LoadLevel(levelId, seed);
                        loadCount++;
                    }
                }

                Console.WriteLine("LevelTelemetry load probe complete.");
                Console.WriteLine($"Levels: {string.Join(", ", levelIds)}");
                Console.WriteLine($"Samples per level: {options.Samples}");
                Console.WriteLine($"Max actions: {options.MaxActions}");
                Console.WriteLine($"Output path: {outputPath}");
                Console.WriteLine($"Loaded states: {loadCount}");

                return 0;
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                PrintUsage();
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static TelemetryOptions ParseOptions(string[] args)
        {
            string? levelId = null;
            string? range = null;
            int samples = DefaultSamples;
            int maxActions = DefaultMaxActions;
            string outputDirectory = DefaultOutputDirectory;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "--level", StringComparison.Ordinal))
                {
                    levelId = ReadValue(args, ref i, "--level");
                    continue;
                }

                if (string.Equals(arg, "--range", StringComparison.Ordinal))
                {
                    range = ReadValue(args, ref i, "--range");
                    continue;
                }

                if (string.Equals(arg, "--samples", StringComparison.Ordinal))
                {
                    samples = ParsePositiveInt(ReadValue(args, ref i, "--samples"), "--samples");
                    continue;
                }

                if (string.Equals(arg, "--max-actions", StringComparison.Ordinal))
                {
                    maxActions = ParsePositiveInt(ReadValue(args, ref i, "--max-actions"), "--max-actions");
                    continue;
                }

                if (string.Equals(arg, "--output", StringComparison.Ordinal))
                {
                    outputDirectory = ReadValue(args, ref i, "--output");
                    if (string.IsNullOrWhiteSpace(outputDirectory))
                    {
                        throw new ArgumentException("--output must not be empty.");
                    }

                    continue;
                }

                throw new ArgumentException($"Unknown argument '{arg}'.");
            }

            if (levelId is null && range is null)
            {
                throw new ArgumentException("Specify exactly one of --level or --range.");
            }

            if (levelId is not null && range is not null)
            {
                throw new ArgumentException("Specify only one of --level or --range, not both.");
            }

            if (levelId is not null)
            {
                ValidateLevelId(levelId, "--level");
            }

            if (range is not null)
            {
                ValidateRange(range);
            }

            return new TelemetryOptions(levelId, range, samples, maxActions, outputDirectory);
        }

        private static IReadOnlyList<string> ResolveLevelIds(TelemetryOptions options)
        {
            if (options.LevelId is not null)
            {
                return new[] { options.LevelId };
            }

            if (options.Range is null)
            {
                throw new ArgumentException("A level or range is required.");
            }

            string[] parts = options.Range.Split('-');
            int start = ParseLevelNumber(parts[0], "--range");
            int end = ParseLevelNumber(parts[1], "--range");
            if (end < start)
            {
                throw new ArgumentException("--range end must be greater than or equal to the start.");
            }

            List<string> ids = new List<string>(end - start + 1);
            for (int value = start; value <= end; value++)
            {
                ids.Add("L" + value.ToString("00"));
            }

            return ids;
        }

        private static string ReadValue(string[] args, ref int index, string name)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"{name} requires a value.");
            }

            index++;
            string value = args[index];
            if (value.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"{name} requires a value.");
            }

            return value;
        }

        private static int ParsePositiveInt(string value, string name)
        {
            if (!int.TryParse(value, out int parsed) || parsed <= 0)
            {
                throw new ArgumentException($"{name} must be a positive integer.");
            }

            return parsed;
        }

        private static void ValidateRange(string range)
        {
            string[] parts = range.Split('-');
            if (parts.Length != 2)
            {
                throw new ArgumentException("--range must use the form L00-L15.");
            }

            ValidateLevelId(parts[0], "--range start");
            ValidateLevelId(parts[1], "--range end");
        }

        private static void ValidateLevelId(string levelId, string name)
        {
            _ = ParseLevelNumber(levelId, name);
        }

        private static int ParseLevelNumber(string levelId, string name)
        {
            if (levelId.Length != 3 || levelId[0] != 'L')
            {
                throw new ArgumentException($"{name} must use the form L00.");
            }

            if (!char.IsDigit(levelId[1]) || !char.IsDigit(levelId[2]))
            {
                throw new ArgumentException($"{name} must use the form L00.");
            }

            return ((levelId[1] - '0') * 10) + (levelId[2] - '0');
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- --level L01 [--samples 200] [--max-actions 30] [--output Reports/LevelTelemetry]");
            Console.Error.WriteLine("  dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- --range L00-L15 [--samples 200] [--max-actions 30] [--output Reports/LevelTelemetry]");
        }

        private sealed record TelemetryOptions(
            string? LevelId,
            string? Range,
            int Samples,
            int MaxActions,
            string OutputDirectory);
    }
}
