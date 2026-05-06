using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.LevelTelemetryTool
{
    internal static class Program
    {
        private const int DefaultSamples = 200;
        private const int DefaultMaxActions = 30;
        private const string BotName = "random_legal";
        private static readonly string DefaultOutputDirectory = Path.Combine("Reports", "LevelTelemetry");
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        private static int Main(string[] args)
        {
            try
            {
                TelemetryOptions options = ParseOptions(args);
                IReadOnlyList<string> levelIds = ResolveLevelIds(options);
                string outputPath = Path.GetFullPath(options.OutputDirectory);
                Directory.CreateDirectory(outputPath);

                List<RunReport> runs = new List<RunReport>();
                SortedSet<string> terminalReasons = new SortedSet<string>(StringComparer.Ordinal);
                SortedSet<string> outcomeNames = new SortedSet<string>(StringComparer.Ordinal);
                SortedSet<string> eventNames = new SortedSet<string>(StringComparer.Ordinal);
                for (int levelIndex = 0; levelIndex < levelIds.Count; levelIndex++)
                {
                    string levelId = levelIds[levelIndex];
                    for (int seed = 1; seed <= options.Samples; seed++)
                    {
                        RunReport run = RunRandomLegal(levelId, seed, options.MaxActions);
                        runs.Add(run);
                        terminalReasons.Add(run.TerminalReason);
                        outcomeNames.Add(run.Outcome);
                        for (int eventIndex = 0; eventIndex < run.EventTypeNames.Length; eventIndex++)
                        {
                            eventNames.Add(run.EventTypeNames[eventIndex]);
                        }
                    }
                }

                TelemetryReport report = new TelemetryReport(
                    Bot: BotName,
                    Levels: levelIds.ToArray(),
                    SamplesPerLevel: options.Samples,
                    MaxActions: options.MaxActions,
                    Runs: runs.ToArray(),
                    TerminalReasons: terminalReasons.ToArray(),
                    Outcomes: outcomeNames.ToArray(),
                    EventTypeNames: eventNames.ToArray());

                string reportPath = Path.Combine(
                    outputPath,
                    $"random_legal_{BuildReportName(levelIds, options.Samples, options.MaxActions)}.json");
                File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));

                Console.WriteLine("LevelTelemetry random_legal simulation complete.");
                Console.WriteLine($"Bot: {BotName}");
                Console.WriteLine($"Levels: {string.Join(", ", levelIds)}");
                Console.WriteLine($"Samples per level: {options.Samples}");
                Console.WriteLine($"Max actions: {options.MaxActions}");
                Console.WriteLine($"Output path: {reportPath}");
                Console.WriteLine($"Runs: {runs.Count}");
                Console.WriteLine($"Terminal reasons: {string.Join(", ", terminalReasons)}");
                Console.WriteLine($"Outcomes: {string.Join(", ", outcomeNames)}");
                Console.WriteLine($"Event type names: {string.Join(", ", eventNames)}");

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

        private static string BuildReportName(IReadOnlyList<string> levelIds, int samples, int maxActions)
        {
            string levelPart = levelIds.Count == 1
                ? levelIds[0]
                : levelIds[0] + "-" + levelIds[levelIds.Count - 1];
            return $"{levelPart}_samples{samples}_max{maxActions}";
        }

        private static RunReport RunRandomLegal(string levelId, int seed, int maxActions)
        {
            GameState state = Loader.LoadLevel(levelId, seed);
            List<ActionReport> actions = new List<ActionReport>(maxActions);
            SortedSet<string> eventTypeNames = new SortedSet<string>(StringComparer.Ordinal);
            string terminalReason = "MaxActionsReached";
            ActionOutcome outcome = ActionOutcome.Ok;

            for (int actionIndex = 1; actionIndex <= maxActions; actionIndex++)
            {
                ImmutableArray<CandidateAction> candidates = EnumerateCandidateActions(state);
                if (candidates.Length == 0)
                {
                    terminalReason = "StalledNoLegalMoves";
                    break;
                }

                CandidateAction chosen = ChooseRandomLegal(levelId, seed, BotName, actionIndex, candidates);
                ActionResult result = Pipeline.RunAction(
                    state,
                    new ActionInput(chosen.Coord),
                    new RunOptions(RecordSnapshot: false));

                string[] actionEventNames = new string[result.Events.Length];
                for (int eventIndex = 0; eventIndex < result.Events.Length; eventIndex++)
                {
                    string eventName = result.Events[eventIndex].GetType().Name;
                    actionEventNames[eventIndex] = eventName;
                    eventTypeNames.Add(eventName);
                }

                outcome = result.Outcome;
                actions.Add(new ActionReport(
                    ActionIndex: actionIndex,
                    CandidateCount: candidates.Length,
                    Row: chosen.Coord.Row,
                    Col: chosen.Coord.Col,
                    GroupSize: chosen.GroupSize,
                    Outcome: outcome.ToString(),
                    EventTypeNames: actionEventNames));

                state = result.State;

                if (outcome == ActionOutcome.Win)
                {
                    terminalReason = "Win";
                    break;
                }

                if (outcome != ActionOutcome.Ok)
                {
                    terminalReason = outcome.ToString();
                    break;
                }
            }

            return new RunReport(
                LevelId: levelId,
                Seed: seed,
                Bot: BotName,
                ActionsTaken: actions.Count,
                TerminalReason: terminalReason,
                Outcome: outcome.ToString(),
                EventTypeNames: eventTypeNames.ToArray(),
                Actions: actions.ToArray());
        }

        private static ImmutableArray<CandidateAction> EnumerateCandidateActions(GameState state)
        {
            ImmutableArray<CandidateAction>.Builder candidates = ImmutableArray.CreateBuilder<CandidateAction>();
            for (int row = 0; row < state.Board.Height; row++)
            {
                for (int col = 0; col < state.Board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    ImmutableArray<TileCoord>? group = GroupOps.FindGroup(state.Board, coord);
                    if (group is null)
                    {
                        continue;
                    }

                    TileCoord canonical = CanonicalCoord(group.Value);
                    if (canonical != coord)
                    {
                        continue;
                    }

                    candidates.Add(new CandidateAction(coord, group.Value.Length));
                }
            }

            return candidates.ToImmutable();
        }

        private static TileCoord CanonicalCoord(ImmutableArray<TileCoord> group)
        {
            TileCoord best = group[0];
            for (int i = 1; i < group.Length; i++)
            {
                TileCoord current = group[i];
                if (current.Row < best.Row || (current.Row == best.Row && current.Col < best.Col))
                {
                    best = current;
                }
            }

            return best;
        }

        private static CandidateAction ChooseRandomLegal(
            string levelId,
            int seed,
            string botName,
            int actionIndex,
            ImmutableArray<CandidateAction> candidates)
        {
            int randomSeed = CreateDeterministicSeed(levelId, seed, botName, actionIndex);
            Random random = new Random(randomSeed);
            return candidates[random.Next(candidates.Length)];
        }

        private static int CreateDeterministicSeed(string levelId, int seed, string botName, int actionIndex)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = AddString(hash, levelId);
                hash = AddInt(hash, seed);
                hash = AddString(hash, botName);
                hash = AddInt(hash, actionIndex);
                return (int)(hash & 0x7fffffffu);
            }
        }

        private static uint AddString(uint hash, string value)
        {
            unchecked
            {
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static uint AddInt(uint hash, int value)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (byte)(value >> shift);
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private sealed record TelemetryOptions(
            string? LevelId,
            string? Range,
            int Samples,
            int MaxActions,
            string OutputDirectory);

        private sealed record CandidateAction(TileCoord Coord, int GroupSize);

        private sealed record TelemetryReport(
            string Bot,
            string[] Levels,
            int SamplesPerLevel,
            int MaxActions,
            RunReport[] Runs,
            string[] TerminalReasons,
            string[] Outcomes,
            string[] EventTypeNames);

        private sealed record RunReport(
            string LevelId,
            int Seed,
            string Bot,
            int ActionsTaken,
            string TerminalReason,
            string Outcome,
            string[] EventTypeNames,
            ActionReport[] Actions);

        private sealed record ActionReport(
            int ActionIndex,
            int CandidateCount,
            int Row,
            int Col,
            int GroupSize,
            string Outcome,
            string[] EventTypeNames);
    }
}
