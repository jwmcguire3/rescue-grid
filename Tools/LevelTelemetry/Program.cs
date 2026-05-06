using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.LevelTelemetryTool
{
#if !LEVEL_TELEMETRY_TESTS
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return LevelTelemetryRunner.Run(args);
        }
    }
#endif

    internal static class LevelTelemetryRunner
    {
        private const int DefaultSamples = 200;
        private const int DefaultMaxActions = 30;
        private static readonly string DefaultOutputDirectory = Path.Combine("Reports", "LevelTelemetry");
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };
        private static readonly JsonSerializerOptions BriefJsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        private static readonly string[] BotNames =
        {
            "random_legal",
            "greedy_clear",
            "rescue_focused",
            "dock_safe",
        };

        internal static string ResolveOutputDirectory(string outputDirectory)
        {
            return Path.GetFullPath(outputDirectory);
        }

        public static int Run(string[] args)
        {
            try
            {
                TelemetryOptions options = ParseOptions(args);
                IReadOnlyList<string> levelIds = ResolveLevelIds(options);
                string outputPath = ResolveOutputDirectory(options.OutputDirectory);
                Directory.CreateDirectory(outputPath);

                SortedSet<string> terminalReasons = new SortedSet<string>(StringComparer.Ordinal);
                SortedSet<string> eventNames = new SortedSet<string>(StringComparer.Ordinal);
                List<LevelTelemetryReport> reports = new List<LevelTelemetryReport>(levelIds.Count);
                for (int levelIndex = 0; levelIndex < levelIds.Count; levelIndex++)
                {
                    string levelId = levelIds[levelIndex];
                    Dictionary<string, BotRunResult[]> botRuns = new Dictionary<string, BotRunResult[]>(StringComparer.Ordinal);
                    for (int botIndex = 0; botIndex < BotNames.Length; botIndex++)
                    {
                        string botName = BotNames[botIndex];
                        BotRunResult[] runs = new BotRunResult[options.Samples];
                        for (int seed = 1; seed <= options.Samples; seed++)
                        {
                            BotRunResult run = RunBot(levelId, seed, botName, options.MaxActions);
                            runs[seed - 1] = run;
                            terminalReasons.Add(run.TerminalReason);
                            for (int eventIndex = 0; eventIndex < run.EventTypeNames.Length; eventIndex++)
                            {
                                eventNames.Add(run.EventTypeNames[eventIndex]);
                            }
                        }

                        botRuns.Add(botName, runs);
                    }

                    LevelJson level = Loader.LoadLevelDefinition(levelId);
                    LevelTelemetryReport report = BuildReport(
                        levelId,
                        options.Samples,
                        options.MaxActions,
                        seedStart: 1,
                        seedEnd: options.Samples,
                        botRuns,
                        DateTimeOffset.UtcNow,
                        Path.Combine("docs", "level-briefs"),
                        level.Targets.Length);
                    reports.Add(report);

                    string reportPath = WriteReport(outputPath, report);
                    Console.WriteLine($"Wrote {reportPath}");
                }

                Console.WriteLine("LevelTelemetry simulation complete.");
                Console.WriteLine($"Levels: {string.Join(", ", levelIds)}");
                Console.WriteLine($"Samples per bot: {options.Samples}");
                Console.WriteLine($"Max actions: {options.MaxActions}");
                Console.WriteLine($"Output directory: {outputPath}");
                Console.WriteLine($"Reports: {reports.Count}");
                Console.WriteLine($"Terminal reasons: {string.Join(", ", terminalReasons)}");
                Console.WriteLine($"Event type names: {string.Join(", ", eventNames)}");
                for (int reportIndex = 0; reportIndex < reports.Count; reportIndex++)
                {
                    LevelTelemetryReport report = reports[reportIndex];
                    Console.WriteLine($"{report.LevelId} difficultySignals: {string.Join(", ", report.DifficultySignals)}");
                }

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

        internal static LevelTelemetryReport BuildReport(
            string levelId,
            int samplesPerBot,
            int maxActions,
            int seedStart,
            int seedEnd,
            IReadOnlyDictionary<string, BotRunResult[]> botRuns,
            DateTimeOffset generatedAtUtc,
            string briefDirectory,
            int targetCount)
        {
            Dictionary<string, BotReport> bots = new Dictionary<string, BotReport>(StringComparer.Ordinal);
            for (int botIndex = 0; botIndex < BotNames.Length; botIndex++)
            {
                string botName = BotNames[botIndex];
                BotRunResult[] runs = botRuns.TryGetValue(botName, out BotRunResult[]? foundRuns)
                    ? foundRuns
                    : Array.Empty<BotRunResult>();
                bots.Add(botName, AggregateBot(runs));
            }

            List<string> notes = new List<string>();
            LevelBrief? brief = ReadBrief(levelId, briefDirectory, notes);
            List<string> difficultySignals = BuildDifficultySignals(bots, brief, targetCount, notes);
            return new LevelTelemetryReport(
                LevelId: levelId,
                SamplesPerBot: samplesPerBot,
                MaxActions: maxActions,
                SeedStart: seedStart,
                SeedEnd: seedEnd,
                GeneratedAtUtc: generatedAtUtc.UtcDateTime.ToString("O"),
                Bots: bots,
                DifficultySignals: difficultySignals,
                Notes: notes,
                BriefRole: brief?.Role,
                BriefPrimarySkill: brief?.PrimarySkill,
                BriefTargetFirstAttemptWinRate: brief?.TargetFirstAttemptWinRate,
                BriefExpectedFailMode: brief?.ExpectedFailMode);
        }

        internal static BotReport AggregateBot(IReadOnlyList<BotRunResult> runs)
        {
            int sampleCount = runs.Count;
            int winCount = 0;
            int stalledCount = 0;
            int maxActionsReachedCount = 0;
            int dockOverflowCount = 0;
            int waterLossCount = 0;
            int targetCountTotal = 0;
            int actionCountTotal = 0;
            List<int> winActionCounts = new List<int>();
            List<int> terminalActionCounts = new List<int>();
            Dictionary<string, int> terminalReasonCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> extractionOrderCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> eventCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < runs.Count; i++)
            {
                BotRunResult run = runs[i];
                Increment(terminalReasonCounts, run.TerminalReason);
                terminalActionCounts.Add(run.ActionsTaken);
                actionCountTotal += run.ActionsTaken;
                targetCountTotal += run.TargetsExtracted;

                if (IsWin(run.TerminalReason))
                {
                    winCount++;
                    winActionCounts.Add(run.ActionsTaken);
                }
                else
                {
                    if (IsDockOverflowReason(run.TerminalReason))
                    {
                        dockOverflowCount++;
                    }

                    if (IsWaterLossReason(run.TerminalReason))
                    {
                        waterLossCount++;
                    }
                }

                if (string.Equals(run.TerminalReason, "StalledNoLegalMoves", StringComparison.Ordinal))
                {
                    stalledCount++;
                }

                if (string.Equals(run.TerminalReason, "MaxActionsReached", StringComparison.Ordinal))
                {
                    maxActionsReachedCount++;
                }

                if (run.TargetExtractionOrder.Length > 0)
                {
                    Increment(extractionOrderCounts, string.Join(">", run.TargetExtractionOrder));
                }

                for (int eventIndex = 0; eventIndex < run.EventTypeNames.Length; eventIndex++)
                {
                    Increment(eventCounts, run.EventTypeNames[eventIndex]);
                }
            }

            int lossCount = sampleCount - winCount - stalledCount - maxActionsReachedCount;
            return new BotReport(
                SampleCount: sampleCount,
                WinCount: winCount,
                WinRate: sampleCount == 0 ? 0.0d : (double)winCount / sampleCount,
                LossCount: lossCount,
                StalledCount: stalledCount,
                MaxActionsReachedCount: maxActionsReachedCount,
                MedianActionsToWin: winActionCounts.Count == 0 ? null : Median(winActionCounts),
                MedianActionsToTerminal: terminalActionCounts.Count == 0 ? 0.0d : Median(terminalActionCounts),
                AverageActionsToTerminal: sampleCount == 0 ? 0.0d : (double)actionCountTotal / sampleCount,
                TerminalReasonCounts: terminalReasonCounts,
                DockOverflowCount: dockOverflowCount,
                WaterLossCount: waterLossCount,
                AverageTargetsExtracted: sampleCount == 0 ? 0.0d : (double)targetCountTotal / sampleCount,
                TargetExtractionOrderCounts: extractionOrderCounts,
                EventCounts: eventCounts);
        }

        internal static string WriteReport(string outputDirectory, LevelTelemetryReport report)
        {
            Directory.CreateDirectory(outputDirectory);
            string reportPath = Path.Combine(outputDirectory, $"{report.LevelId}.telemetry.json");
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));
            return reportPath;
        }

        internal static BotRunResult RunBot(string levelId, int seed, string botName, int maxActions)
        {
            GameState state = Loader.LoadLevel(levelId, seed);
            List<ActionReport> actions = new List<ActionReport>(maxActions);
            List<string> eventTypeNames = new List<string>();
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

                CandidateAction chosen = ChooseCandidate(levelId, seed, botName, actionIndex, state, candidates);
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

            return new BotRunResult(
                LevelId: levelId,
                Seed: seed,
                Bot: botName,
                ActionsTaken: actions.Count,
                TerminalReason: terminalReason,
                Outcome: outcome.ToString(),
                EventTypeNames: eventTypeNames.ToArray(),
                TargetsExtracted: state.ExtractedTargetOrder.Length,
                TargetExtractionOrder: state.ExtractedTargetOrder.ToArray(),
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

                    candidates.Add(new CandidateAction(
                        coord,
                        group.Value,
                        GetDebrisType(state.Board, coord)));
                }
            }

            return candidates.ToImmutable();
        }

        private static DebrisType GetDebrisType(Board board, TileCoord coord)
        {
            if (BoardHelpers.GetTile(board, coord) is DebrisTile debris)
            {
                return debris.Type;
            }

            throw new InvalidOperationException($"Candidate at {coord.Row},{coord.Col} is not debris.");
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

        private static CandidateAction ChooseCandidate(
            string levelId,
            int seed,
            string botName,
            int actionIndex,
            GameState state,
            ImmutableArray<CandidateAction> candidates)
        {
            return botName switch
            {
                "random_legal" => ChooseRandomLegal(levelId, seed, botName, actionIndex, candidates),
                "greedy_clear" => ChooseGreedyClear(candidates),
                "rescue_focused" => ChooseByScore(candidates, candidate => ScoreRescueFocused(state, candidate)),
                "dock_safe" => ChooseByScore(candidates, candidate => ScoreDockSafe(state, candidate)),
                _ => throw new ArgumentException($"Unknown bot '{botName}'."),
            };
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

        private static CandidateAction ChooseGreedyClear(ImmutableArray<CandidateAction> candidates)
        {
            return candidates
                .OrderByDescending(static candidate => candidate.GroupSize)
                .ThenBy(static candidate => DistanceToMultipleOfThree(candidate.GroupSize))
                .ThenByDescending(static candidate => candidate.LowestRow)
                .ThenBy(static candidate => candidate.LowestCol)
                .ThenBy(static candidate => candidate.Coord.Row)
                .ThenBy(static candidate => candidate.Coord.Col)
                .First();
        }

        private static CandidateAction ChooseByScore(
            ImmutableArray<CandidateAction> candidates,
            Func<CandidateAction, int> score)
        {
            return candidates
                .OrderByDescending(score)
                .ThenByDescending(static candidate => candidate.GroupSize)
                .ThenBy(static candidate => candidate.Coord.Row)
                .ThenBy(static candidate => candidate.Coord.Col)
                .First();
        }

        private static int ScoreRescueFocused(GameState state, CandidateAction candidate)
        {
            int score = 0;
            score += CountTargetAdjacentTiles(state, candidate.Group) * 100;
            score += CountUrgentRouteTiles(state, candidate.Group) * 50;
            score += GroupOps.FindAdjacentBlockers(state.Board, candidate.Group).Length * 30;
            if (candidate.GroupSize == 3 || candidate.GroupSize == 6)
            {
                score += 20;
            }

            int remainder = candidate.GroupSize % 3;
            score -= remainder * 20;
            if (DockHelpers.Occupancy(state.Dock) >= 5 && remainder == 2)
            {
                score -= 30;
            }

            return score;
        }

        private static int ScoreDockSafe(GameState state, CandidateAction candidate)
        {
            int score = 0;
            if (candidate.GroupSize == 3 || candidate.GroupSize == 6)
            {
                score += 80;
            }

            int remainder = candidate.GroupSize % 3;
            // Offline policy approximation: count current dock slots by debris type,
            // then add this group's inserted remainder. If that reaches 3, this action
            // is treated as likely to complete a dock triple without simulating rules.
            int dockTypeCount = CountDockType(state.Dock, candidate.DebrisType);
            if ((dockTypeCount == 1 || dockTypeCount == 2) && dockTypeCount + remainder >= 3)
            {
                score += 40;
            }

            score -= remainder * 50;
            if (DockHelpers.Occupancy(state.Dock) >= 5 && remainder == 2)
            {
                score -= 100;
            }

            score += GroupOps.FindAdjacentBlockers(state.Board, candidate.Group).Length * 20;
            score += CountTargetAdjacentTiles(state, candidate.Group) * 10;
            return score;
        }

        private static int DistanceToMultipleOfThree(int groupSize)
        {
            int remainder = groupSize % 3;
            return remainder == 0 ? 0 : Math.Min(remainder, 3 - remainder);
        }

        private static int CountDockType(Dock dock, DebrisType type)
        {
            int count = 0;
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                if (dock.Slots[i] == type)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountTargetAdjacentTiles(GameState state, ImmutableArray<TileCoord> group)
        {
            int count = 0;
            for (int i = 0; i < group.Length; i++)
            {
                if (IsAdjacentToUnextractedTarget(state, group[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsAdjacentToUnextractedTarget(GameState state, TileCoord coord)
        {
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(state.Board, coord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                Tile neighbor = BoardHelpers.GetTile(state.Board, neighbors[i]);
                if (neighbor is not TargetTile targetTile || targetTile.Extracted)
                {
                    continue;
                }

                for (int targetIndex = 0; targetIndex < state.Targets.Length; targetIndex++)
                {
                    TargetState target = state.Targets[targetIndex];
                    if (target.TargetId == targetTile.TargetId && !target.Extracted)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int CountUrgentRouteTiles(GameState state, ImmutableArray<TileCoord> group)
        {
            UrgentRoute? route = SpawnOps.FindUrgentRoute(state);
            if (!route.HasValue)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < group.Length; i++)
            {
                TileCoord coord = group[i];
                if (ContainsCoord(route.Value.HardRouteCells, coord)
                    || ContainsCoord(route.Value.SoftRouteCells, coord)
                    || IsAdjacentToRouteCell(state.Board, coord, route.Value))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsAdjacentToRouteCell(Board board, TileCoord coord, UrgentRoute route)
        {
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, coord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (ContainsCoord(route.HardRouteCells, neighbors[i])
                    || ContainsCoord(route.SoftRouteCells, neighbors[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsCoord(ImmutableArray<TileCoord> coords, TileCoord candidate)
        {
            for (int i = 0; i < coords.Length; i++)
            {
                if (coords[i] == candidate)
                {
                    return true;
                }
            }

            return false;
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

        private static LevelBrief? ReadBrief(string levelId, string briefDirectory, List<string> notes)
        {
            string briefPath = Path.Combine(briefDirectory, $"{levelId}.brief.json");
            if (!File.Exists(briefPath))
            {
                notes.Add("No level brief found.");
                return null;
            }

            try
            {
                LevelBrief? brief = JsonSerializer.Deserialize<LevelBrief>(File.ReadAllText(briefPath), BriefJsonOptions);
                if (brief is null)
                {
                    notes.Add("Level brief could not be parsed.");
                    return null;
                }

                return brief;
            }
            catch (JsonException ex)
            {
                notes.Add($"Level brief parse failed: {ex.Message}");
                return null;
            }
        }

        private static List<string> BuildDifficultySignals(
            Dictionary<string, BotReport> bots,
            LevelBrief? brief,
            int targetCount,
            List<string> notes)
        {
            List<string> signals = new List<string>();
            BotReport random = bots["random_legal"];
            BotReport greedy = bots["greedy_clear"];
            BotReport rescue = bots["rescue_focused"];
            BotReport dock = bots["dock_safe"];
            string? role = brief?.Role;

            if (rescue.WinRate < greedy.WinRate)
            {
                signals.Add("greedy_clear_outperforms_rescue_focused");
            }

            if (rescue.WinRate < 0.60d)
            {
                signals.Add("rescue_focused_low_win_rate");
            }

            if (random.WinRate > 0.50d && !IsRandomHighWinRateAllowedRole(role))
            {
                signals.Add("random_legal_high_win_rate");
            }

            if (dock.WinRate - rescue.WinRate >= 0.20d)
            {
                signals.Add("dock_safety_may_dominate_rescue");
            }

            if (IsDockSensitiveRole(role) && IsDominantNonWinReason(rescue, IsDockOverflowReason))
            {
                signals.Add("dock_overflow_too_prominent_for_role");
            }

            if (IsWaterSensitiveRole(role) && IsDominantNonWinReason(rescue, IsWaterLossReason))
            {
                if (brief?.ExpectedFailMode is null)
                {
                    notes.Add("Water prominence checked without brief expectedFailMode.");
                }
                else if (brief.ExpectedFailMode.IndexOf("water", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    signals.Add("water_loss_too_prominent_for_role");
                }
            }

            if (CountTargetProgressEvents(rescue.EventCounts) == 0 && targetCount > 0)
            {
                signals.Add("no_target_progress_events_seen");
            }

            if (rescue.MaxActionsReachedCount > rescue.SampleCount * 0.25d)
            {
                signals.Add("many_runs_reach_max_actions");
            }

            return signals;
        }

        private static bool IsDominantNonWinReason(BotReport bot, Func<string, bool> predicate)
        {
            int matchingCount = 0;
            int highestNonWinCount = 0;
            foreach (KeyValuePair<string, int> entry in bot.TerminalReasonCounts)
            {
                if (IsWin(entry.Key))
                {
                    continue;
                }

                highestNonWinCount = Math.Max(highestNonWinCount, entry.Value);
                if (predicate(entry.Key))
                {
                    matchingCount += entry.Value;
                }
            }

            return matchingCount > 0 && matchingCount >= highestNonWinCount;
        }

        private static bool IsRandomHighWinRateAllowedRole(string? role)
        {
            return string.Equals(role, "rule_teach", StringComparison.Ordinal)
                || string.Equals(role, "teach", StringComparison.Ordinal)
                || string.Equals(role, "release", StringComparison.Ordinal)
                || string.Equals(role, "recovery", StringComparison.Ordinal)
                || string.Equals(role, "spectacle", StringComparison.Ordinal);
        }

        private static bool IsDockSensitiveRole(string? role)
        {
            return string.Equals(role, "teach", StringComparison.Ordinal)
                || string.Equals(role, "practice", StringComparison.Ordinal)
                || string.Equals(role, "release", StringComparison.Ordinal)
                || string.Equals(role, "recovery", StringComparison.Ordinal);
        }

        private static bool IsWaterSensitiveRole(string? role)
        {
            return string.Equals(role, "rule_teach", StringComparison.Ordinal)
                || string.Equals(role, "teach", StringComparison.Ordinal)
                || string.Equals(role, "practice", StringComparison.Ordinal);
        }

        private static bool IsWin(string terminalReason)
        {
            return string.Equals(terminalReason, "Win", StringComparison.Ordinal);
        }

        private static bool IsDockOverflowReason(string terminalReason)
        {
            return !IsWin(terminalReason)
                && terminalReason.IndexOf("Dock", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsWaterLossReason(string terminalReason)
        {
            return !IsWin(terminalReason)
                && terminalReason.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountTargetProgressEvents(IReadOnlyDictionary<string, int> eventCounts)
        {
            int count = 0;
            foreach (KeyValuePair<string, int> entry in eventCounts)
            {
                if (string.Equals(entry.Key, "TargetProgressed", StringComparison.Ordinal)
                    || entry.Key.IndexOf("TargetProgress", StringComparison.Ordinal) >= 0)
                {
                    count += entry.Value;
                }
            }

            return count;
        }

        private static double Median(List<int> values)
        {
            values.Sort();
            int middle = values.Count / 2;
            if (values.Count % 2 == 1)
            {
                return values[middle];
            }

            return (values[middle - 1] + values[middle]) / 2.0d;
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            if (counts.TryGetValue(key, out int count))
            {
                counts[key] = count + 1;
            }
            else
            {
                counts.Add(key, 1);
            }
        }

        private sealed record TelemetryOptions(
            string? LevelId,
            string? Range,
            int Samples,
            int MaxActions,
            string OutputDirectory);

        private sealed record CandidateAction(
            TileCoord Coord,
            ImmutableArray<TileCoord> Group,
            DebrisType DebrisType)
        {
            public int GroupSize => Group.Length;

            public int LowestRow => Group.Max(static coord => coord.Row);

            public int LowestCol => Group.Min(static coord => coord.Col);
        }

        internal sealed record LevelTelemetryReport(
            [property: JsonPropertyName("levelId")] string LevelId,
            [property: JsonPropertyName("samplesPerBot")] int SamplesPerBot,
            [property: JsonPropertyName("maxActions")] int MaxActions,
            [property: JsonPropertyName("seedStart")] int SeedStart,
            [property: JsonPropertyName("seedEnd")] int SeedEnd,
            [property: JsonPropertyName("generatedAtUtc")] string GeneratedAtUtc,
            [property: JsonPropertyName("bots")] Dictionary<string, BotReport> Bots,
            [property: JsonPropertyName("difficultySignals")] List<string> DifficultySignals,
            [property: JsonPropertyName("notes")] List<string> Notes,
            [property: JsonPropertyName("briefRole")]
            [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            string? BriefRole,
            [property: JsonPropertyName("briefPrimarySkill")]
            [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            string? BriefPrimarySkill,
            [property: JsonPropertyName("briefTargetFirstAttemptWinRate")]
            [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            string? BriefTargetFirstAttemptWinRate,
            [property: JsonPropertyName("briefExpectedFailMode")]
            [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            string? BriefExpectedFailMode);

        internal sealed record BotReport(
            [property: JsonPropertyName("sampleCount")] int SampleCount,
            [property: JsonPropertyName("winCount")] int WinCount,
            [property: JsonPropertyName("winRate")] double WinRate,
            [property: JsonPropertyName("lossCount")] int LossCount,
            [property: JsonPropertyName("stalledCount")] int StalledCount,
            [property: JsonPropertyName("maxActionsReachedCount")] int MaxActionsReachedCount,
            [property: JsonPropertyName("medianActionsToWin")] double? MedianActionsToWin,
            [property: JsonPropertyName("medianActionsToTerminal")] double MedianActionsToTerminal,
            [property: JsonPropertyName("averageActionsToTerminal")] double AverageActionsToTerminal,
            [property: JsonPropertyName("terminalReasonCounts")] Dictionary<string, int> TerminalReasonCounts,
            [property: JsonPropertyName("dockOverflowCount")] int DockOverflowCount,
            [property: JsonPropertyName("waterLossCount")] int WaterLossCount,
            [property: JsonPropertyName("averageTargetsExtracted")] double AverageTargetsExtracted,
            [property: JsonPropertyName("targetExtractionOrderCounts")] Dictionary<string, int> TargetExtractionOrderCounts,
            [property: JsonPropertyName("eventCounts")] Dictionary<string, int> EventCounts);

        internal sealed record BotRunResult(
            string LevelId,
            int Seed,
            string Bot,
            int ActionsTaken,
            string TerminalReason,
            string Outcome,
            string[] EventTypeNames,
            int TargetsExtracted,
            string[] TargetExtractionOrder,
            ActionReport[] Actions);

        internal sealed record LevelBrief(
            string? Role,
            string? PrimarySkill,
            string? TargetFirstAttemptWinRate,
            string? ExpectedFailMode);

        internal sealed record ActionReport(
            int ActionIndex,
            int CandidateCount,
            int Row,
            int Col,
            int GroupSize,
            string Outcome,
            string[] EventTypeNames);
    }
}
