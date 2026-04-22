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

namespace Rescue.SolveAuthoringTool
{
    internal static class Program
    {
        private const int FirstSeed = 1;
        private const int LastSeed = 200;
        private const int AlternateSeedOffset = 1000;
        private const int MaxDepth = 10;
        private const int CandidateCap = 10;
        private const int RandomRolloutCount = 3000;

        private static readonly string LevelsDirectory = Path.Combine("Assets", "StreamingAssets", "Levels");
        private static readonly string OutputDirectory = Path.Combine("Assets", "Resources", "Levels");

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length >= 1 && string.Equals(args[0], "--replay", StringComparison.Ordinal))
                {
                    return ReplayScript(args);
                }

                if (args.Length >= 1 && string.Equals(args[0], "--search", StringComparison.Ordinal))
                {
                    return SearchScript(args);
                }

                if (args.Length >= 1 && string.Equals(args[0], "--search-random", StringComparison.Ordinal))
                {
                    return SearchRandomScript(args);
                }

                if (args.Length >= 1 && string.Equals(args[0], "--moves", StringComparison.Ordinal))
                {
                    return MovesScript(args);
                }

                IReadOnlyList<string> levelIds = ParseLevelIds(args);
                Directory.CreateDirectory(OutputDirectory);

                foreach (string levelId in levelIds)
                {
                    LevelJson level = LoadLevel(levelId);
                    SolveScript solve = AuthorSolve(level);
                    string outputPath = Path.Combine(OutputDirectory, levelId + ".solve.json");
                    WriteSolve(outputPath, solve);
                    Console.WriteLine($"Authored {levelId} -> {outputPath}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static int ReplayScript(string[] args)
        {
            if (args.Length < 4)
            {
                throw new ArgumentException("Usage: --replay <levelId> <seed> <row,col;row,col;...>");
            }

            LevelJson level = LoadLevel(args[1]);
            int seed = int.Parse(args[2]);
            ImmutableArray<TileCoord> actions = ParseActions(args[3]);
            GameState state = Loader.LoadLevel(level, seed);
            Console.WriteLine("initial");
            PrintBoard(state);
            Console.WriteLine(Fingerprint(state));

            for (int i = 0; i < actions.Length; i++)
            {
                ActionResult result = Pipeline.RunAction(state, new ActionInput(actions[i]), new RunOptions(RecordSnapshot: false));
                Console.WriteLine($"step {i + 1}: tap {actions[i].Row},{actions[i].Col} -> {result.Outcome}");
                for (int eventIndex = 0; eventIndex < result.Events.Length; eventIndex++)
                {
                    Console.WriteLine("  " + result.Events[eventIndex]);
                }

                state = result.State;
                Console.WriteLine("  board");
                PrintBoard(state);
                Console.WriteLine("  state " + Fingerprint(state));
            }

            return 0;
        }

        private static int SearchScript(string[] args)
        {
            if (args.Length < 2)
            {
                throw new ArgumentException("Usage: --search <levelId> [seed] [maxDepth]");
            }

            LevelJson level = LoadLevel(args[1]);
            int maxDepth = args.Length >= 4 ? int.Parse(args[3]) : MaxDepth;
            if (args.Length >= 3)
            {
                int seed = int.Parse(args[2]);
                SearchNode? solved = SearchForWin(level, seed, maxDepth);
                if (solved is null)
                {
                    Console.WriteLine($"No win found for {level.Id} seed {seed} up to depth {maxDepth}.");
                    return 2;
                }

                Console.WriteLine($"Win found for {level.Id} seed {seed}: {FormatActions(solved.Actions)}");
                return 0;
            }

            for (int seed = FirstSeed; seed <= LastSeed; seed++)
            {
                SearchNode? solved = SearchForWin(level, seed, maxDepth);
                if (solved is not null)
                {
                    Console.WriteLine($"Win found for {level.Id} seed {seed}: {FormatActions(solved.Actions)}");
                    return 0;
                }
            }

            Console.WriteLine($"No win found for {level.Id} in seeds {FirstSeed}-{LastSeed}.");
            return 2;
        }

        private static int SearchRandomScript(string[] args)
        {
            if (args.Length < 3)
            {
                throw new ArgumentException("Usage: --search-random <levelId> <seed> [maxDepth]");
            }

            LevelJson level = LoadLevel(args[1]);
            int seed = int.Parse(args[2]);
            int maxDepth = args.Length >= 4 ? int.Parse(args[3]) : MaxDepth;
            SearchNode? solved = SearchRandomRollouts(Loader.LoadLevel(level, seed), seed, maxDepth);
            if (solved is null || solved.Outcome != ActionOutcome.Win)
            {
                Console.WriteLine($"No random win found for {level.Id} seed {seed}.");
                return 2;
            }

            Console.WriteLine($"Random win found for {level.Id} seed {seed}: {FormatActions(solved.Actions)}");
            return 0;
        }

        private static int MovesScript(string[] args)
        {
            if (args.Length < 3)
            {
                throw new ArgumentException("Usage: --moves <levelId> <seed> [row,col;row,col;...]");
            }

            LevelJson level = LoadLevel(args[1]);
            int seed = int.Parse(args[2]);
            ImmutableArray<TileCoord> appliedActions = args.Length >= 4
                ? ParseActions(args[3])
                : ImmutableArray<TileCoord>.Empty;

            GameState state = Loader.LoadLevel(level, seed);
            for (int i = 0; i < appliedActions.Length; i++)
            {
                ActionResult result = Pipeline.RunAction(state, new ActionInput(appliedActions[i]), new RunOptions(RecordSnapshot: false));
                state = result.State;
                if (result.Outcome != ActionOutcome.Ok)
                {
                    Console.WriteLine($"Sequence is already terminal at step {i + 1}: {result.Outcome}");
                    return 0;
                }
            }

            Console.WriteLine($"Candidate moves for {level.Id} seed {seed} after {FormatActions(appliedActions)}");
            PrintBoard(state);
            foreach (CandidateAction action in EnumerateCandidateActions(state))
            {
                Console.WriteLine($"  {action.Coord.Row},{action.Coord.Col} size={action.GroupSize} route={action.RouteHits} tripleDistance={action.SizeDistanceFromTriple}");
            }

            return 0;
        }

        private static IReadOnlyList<string> ParseLevelIds(string[] args)
        {
            if (args.Length > 0)
            {
                return args;
            }

            List<string> levelIds = new List<string>();
            for (int index = 1; index <= 15; index++)
            {
                levelIds.Add("L" + index.ToString("00"));
            }

            return levelIds;
        }

        private static ImmutableArray<TileCoord> ParseActions(string raw)
        {
            string[] parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            ImmutableArray<TileCoord>.Builder actions = ImmutableArray.CreateBuilder<TileCoord>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] coord = parts[i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                actions.Add(new TileCoord(int.Parse(coord[0]), int.Parse(coord[1])));
            }

            return actions.ToImmutable();
        }

        private static LevelJson LoadLevel(string levelId)
        {
            string path = Path.Combine(LevelsDirectory, levelId + ".json");
            string json = File.ReadAllText(path);
            return ContentJson.DeserializeLevel(json);
        }

        private static SolveScript AuthorSolve(LevelJson level)
        {
            (int seed, SearchNode solved) = FindWinScript(level);

            int alternateSeed = seed + AlternateSeedOffset;
            ImmutableArray<string> defaultTrajectory = Replay(level, seed, solved.Actions);
            ImmutableArray<string> alternateTrajectory = Replay(level, alternateSeed, solved.Actions);
            bool alternateDiverges = !defaultTrajectory.SequenceEqual(alternateTrajectory, StringComparer.Ordinal);

            return new SolveScript(
                LevelId: level.Id,
                Seed: seed,
                AlternateSeed: alternateSeed,
                ExpectedOutcome: solved.Outcome.ToString(),
                ExpectAlternateSeedDivergence: alternateDiverges,
                Actions: solved.Actions.Select(static coord => new SolveAction(coord.Row, coord.Col)).ToArray());
        }

        private static (int Seed, SearchNode Node) FindWinScript(LevelJson level)
        {
            for (int seed = FirstSeed; seed <= LastSeed; seed++)
            {
                SearchNode? terminal = SearchForWin(level, seed, MaxDepth);
                if (terminal is not null)
                {
                    return (seed, terminal);
                }
            }

            throw new InvalidOperationException($"Could not find a win script for {level.Id} with seeds {FirstSeed}-{LastSeed}.");
        }

        private static SearchNode? SearchForWin(LevelJson level, int seed, int maxDepth)
        {
            GameState initial = Loader.LoadLevel(level, seed);
            SearchNode? stochastic = SearchRandomRollouts(initial, seed, maxDepth);
            if (stochastic is not null)
            {
                return stochastic;
            }

            for (int depthLimit = 1; depthLimit <= maxDepth; depthLimit++)
            {
                Dictionary<string, int> seenAtDepth = new Dictionary<string, int>(StringComparer.Ordinal);
                SearchNode? solved = SearchDepthFirst(
                    initial,
                    ImmutableArray<TileCoord>.Empty,
                    depthRemaining: depthLimit,
                    seenAtDepth);
                if (solved is not null)
                {
                    return solved;
                }
            }

            return null;
        }

        private static SearchNode? SearchRandomRollouts(GameState initial, int seed, int maxDepth)
        {
            Random random = new Random(seed * 7919);
            SearchNode? best = null;

            for (int rollout = 0; rollout < RandomRolloutCount; rollout++)
            {
                GameState state = initial;
                ImmutableArray<TileCoord> actions = ImmutableArray<TileCoord>.Empty;

                for (int depth = 0; depth < maxDepth; depth++)
                {
                    List<CandidateAction> candidates = EnumerateCandidateActions(state).ToList();
                    if (candidates.Count == 0)
                    {
                        break;
                    }

                    CandidateAction chosen = WeightedPick(candidates, random);
                    ActionResult result = Pipeline.RunAction(state, new ActionInput(chosen.Coord), new RunOptions(RecordSnapshot: false));
                    if (result.Events.Any(static ev => ev is InvalidInput))
                    {
                        break;
                    }

                    actions = actions.Add(chosen.Coord);
                    SearchNode node = new SearchNode(
                        result.State,
                        actions,
                        result.Outcome,
                        Score(result.State, result.Events));

                    if (best is null || node.Score > best.Score)
                    {
                        best = node;
                    }

                    if (result.Outcome == ActionOutcome.Win)
                    {
                        return node;
                    }

                    if (result.Outcome != ActionOutcome.Ok)
                    {
                        break;
                    }

                    state = result.State;
                }
            }

            return null;
        }

        private static SearchNode? SearchDepthFirst(
            GameState state,
            ImmutableArray<TileCoord> actions,
            int depthRemaining,
            Dictionary<string, int> seenAtDepth)
        {
            if (depthRemaining <= 0)
            {
                return null;
            }

            string fingerprint = Fingerprint(state);
            if (seenAtDepth.TryGetValue(fingerprint, out int knownDepth) && knownDepth >= depthRemaining)
            {
                return null;
            }

            seenAtDepth[fingerprint] = depthRemaining;

            foreach (ActionInput input in EnumerateCandidateInputs(state))
            {
                ActionResult result = Pipeline.RunAction(state, input, new RunOptions(RecordSnapshot: false));
                if (result.Events.Any(static ev => ev is InvalidInput))
                {
                    continue;
                }

                ImmutableArray<TileCoord> nextActions = actions.Add(input.TappedCoord);
                SearchNode candidate = new SearchNode(
                    result.State,
                    nextActions,
                    result.Outcome,
                    Score(result.State, result.Events));

                if (result.Outcome == ActionOutcome.Win)
                {
                    return candidate;
                }

                if (result.Outcome != ActionOutcome.Ok)
                {
                    continue;
                }

                SearchNode? solved = SearchDepthFirst(result.State, nextActions, depthRemaining - 1, seenAtDepth);
                if (solved is not null)
                {
                    return solved;
                }
            }

            return null;
        }

        private static IEnumerable<ActionInput> EnumerateCandidateInputs(GameState state)
        {
            return EnumerateCandidateActions(state)
                .Select(static candidate => new ActionInput(candidate.Coord));
        }

        private static IEnumerable<CandidateAction> EnumerateCandidateActions(GameState state)
        {
            List<CandidateAction> candidates = new List<CandidateAction>();
            UrgentRoute? urgentRoute = SpawnOps.FindUrgentRoute(state);

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

                    int routeHits = urgentRoute.HasValue ? CountRouteHits(group.Value, urgentRoute.Value) : 0;
                    candidates.Add(new CandidateAction(
                        coord,
                        group.Value.Length,
                        routeHits,
                        Math.Abs(group.Value.Length - 3)));
                }
            }

            return candidates
                .OrderByDescending(static candidate => candidate.RouteHits)
                .ThenBy(static candidate => candidate.SizeDistanceFromTriple)
                .ThenByDescending(static candidate => candidate.GroupSize)
                .ThenBy(static candidate => candidate.Coord.Row)
                .ThenBy(static candidate => candidate.Coord.Col)
                .Take(CandidateCap);
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

        private static int CountRouteHits(ImmutableArray<TileCoord> group, UrgentRoute route)
        {
            int hits = 0;
            for (int i = 0; i < group.Length; i++)
            {
                TileCoord coord = group[i];
                if (ContainsCoord(route.HardRouteCells, coord))
                {
                    hits += 3;
                }
                else if (ContainsCoord(route.SoftRouteCells, coord))
                {
                    hits += 1;
                }
            }

            return hits;
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

        private static int Score(GameState state, ImmutableArray<ActionEvent> events)
        {
            int score = 0;
            int extractedTargets = 0;
            int blockedNeighbors = 0;

            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                if (target.Extracted)
                {
                    extractedTargets++;
                    score += 4000;
                    continue;
                }

                blockedNeighbors += CountBlockedRequiredNeighbors(state.Board, target.Coord);
                score -= target.Coord.Row * 20;
                if (target.OneClearAway)
                {
                    score += 600;
                }
            }

            score -= blockedNeighbors * 160;
            score -= DockHelpers.Occupancy(state.Dock) * 120;
            score -= state.Water.FloodedRows * 220;
            score -= state.ActionCount * 25;
            score -= state.DockJamActive ? 1500 : 0;
            score -= state.Frozen ? 2500 : 0;

            for (int i = 0; i < events.Length; i++)
            {
                score += events[i] switch
                {
                    TargetExtracted => 2500,
                    TargetOneClearAway => 500,
                    BlockerBroken => 180,
                    DockCleared => 220,
                    WaterRose => -350,
                    DockJamTriggered => -1200,
                    Lost => -5000,
                    Won => 10000,
                    _ => 0,
                };
            }

            return score + (extractedTargets * 2000);
        }

        private static CandidateAction WeightedPick(IReadOnlyList<CandidateAction> candidates, Random random)
        {
            double total = 0.0d;
            double[] weights = new double[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                CandidateAction candidate = candidates[i];
                double weight = 1.0d
                    + (candidate.RouteHits * 6.0d)
                    + Math.Max(0, 4 - candidate.SizeDistanceFromTriple)
                    + candidate.GroupSize;
                weights[i] = weight;
                total += weight;
            }

            double roll = random.NextDouble() * total;
            double cumulative = 0.0d;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private static int CountBlockedRequiredNeighbors(Board board, TileCoord targetCoord)
        {
            int blocked = 0;
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, targetCoord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (BoardHelpers.GetTile(board, neighbors[i]) is not EmptyTile)
                {
                    blocked++;
                }
            }

            return blocked;
        }

        private static ImmutableArray<string> Replay(LevelJson level, int seed, IReadOnlyList<TileCoord> actions)
        {
            ImmutableArray<string>.Builder frames = ImmutableArray.CreateBuilder<string>(actions.Count + 1);
            GameState state = Loader.LoadLevel(level, seed);
            frames.Add(Fingerprint(state));

            for (int i = 0; i < actions.Count; i++)
            {
                ActionResult result = Pipeline.RunAction(state, new ActionInput(actions[i]), new RunOptions(RecordSnapshot: false));
                state = result.State;
                frames.Add(Fingerprint(state) + "|" + result.Outcome);
            }

            return frames.ToImmutable();
        }

        private static string Fingerprint(GameState state)
        {
            List<string> rows = new List<string>(state.Board.Height);
            for (int row = 0; row < state.Board.Height; row++)
            {
                List<string> tiles = new List<string>(state.Board.Width);
                for (int col = 0; col < state.Board.Width; col++)
                {
                    tiles.Add(TileCode(BoardHelpers.GetTile(state.Board, new TileCoord(row, col))));
                }

                rows.Add(string.Join(",", tiles));
            }

            List<string> dock = new List<string>(state.Dock.Slots.Length);
            for (int i = 0; i < state.Dock.Slots.Length; i++)
            {
                dock.Add(state.Dock.Slots[i]?.ToString() ?? ".");
            }

            return string.Join("|", new[]
            {
                string.Join("/", rows),
                string.Join(",", dock),
                state.Water.FloodedRows.ToString(),
                state.Water.ActionsUntilRise.ToString(),
                state.Water.PauseUntilFirstAction.ToString(),
                state.Vine.ActionsSinceLastClear.ToString(),
                state.Vine.PriorityCursor.ToString(),
                state.Vine.PendingGrowthTile?.ToString() ?? "none",
                string.Join(",", state.ExtractedTargetOrder),
                state.RngState.S0.ToString(),
                state.RngState.S1.ToString(),
                state.ActionCount.ToString(),
                state.DockJamUsed.ToString(),
                state.DockJamActive.ToString(),
                state.Frozen.ToString(),
            });
        }

        private static string TileCode(Tile tile)
        {
            return tile switch
            {
                EmptyTile => ".",
                FloodedTile => "~",
                DebrisTile debris => debris.Type.ToString(),
                BlockerTile blocker when blocker.Type == BlockerType.Crate => blocker.Hp > 1 ? "CX" : "CR",
                BlockerTile blocker when blocker.Type == BlockerType.Vine => "V",
                BlockerTile blocker when blocker.Type == BlockerType.Ice && blocker.Hidden is not null => "I" + blocker.Hidden.Type,
                BlockerTile blocker when blocker.Type == BlockerType.Ice => "I?",
                TargetTile target => "T" + target.TargetId + (target.Extracted ? "!" : string.Empty),
                _ => tile.GetType().Name,
            };
        }

        private static void WriteSolve(string outputPath, SolveScript solve)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            File.WriteAllText(outputPath, JsonSerializer.Serialize(solve, options));
        }

        private static string FormatActions(ImmutableArray<TileCoord> actions)
        {
            return string.Join(";", actions.Select(static coord => $"{coord.Row},{coord.Col}"));
        }

        private static void PrintBoard(GameState state)
        {
            for (int row = 0; row < state.Board.Height; row++)
            {
                List<string> cells = new List<string>(state.Board.Width);
                for (int col = 0; col < state.Board.Width; col++)
                {
                    cells.Add(TileCode(BoardHelpers.GetTile(state.Board, new TileCoord(row, col))).PadRight(2));
                }

                Console.WriteLine("  " + string.Join(" ", cells));
            }

            Console.WriteLine("  dock: " + string.Join(" ", state.Dock.Slots.Select(static slot => slot?.ToString() ?? ".")));
            Console.WriteLine($"  actionCount={state.ActionCount} flooded={state.Water.FloodedRows} untilRise={state.Water.ActionsUntilRise} dockJamUsed={state.DockJamUsed} dockJamActive={state.DockJamActive} frozen={state.Frozen}");
        }

        private sealed record CandidateAction(TileCoord Coord, int GroupSize, int RouteHits, int SizeDistanceFromTriple);

        private sealed record SearchNode(GameState State, ImmutableArray<TileCoord> Actions, ActionOutcome Outcome, int Score);

        private sealed record SolveScript(
            string LevelId,
            int Seed,
            int AlternateSeed,
            string ExpectedOutcome,
            bool ExpectAlternateSeedDivergence,
            SolveAction[] Actions);

        private sealed record SolveAction(int Row, int Col);
    }
}
