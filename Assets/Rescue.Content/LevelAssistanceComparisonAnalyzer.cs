using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rescue.Core.Pipeline;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.Content
{
    public static class LevelAssistanceComparisonAnalyzer
    {
        public const int DefaultFirstSeed = 1;
        public const int DefaultLastSeed = 200;
        public const int DefaultMaxDepth = 10;
        public const int DefaultMaxSearchNodes = 20000;
        public const string DependencyWarning = "No-assistance solve failed; level may depend on assisted spawn.";

        private const int CandidateCap = 10;
        private const int RandomRolloutCount = 3000;

        public static AssistanceComparisonResult Compare(LevelJson level, AssistanceComparisonOptions? options = null)
        {
            if (level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            AssistanceComparisonOptions effectiveOptions = options ?? AssistanceComparisonOptions.Default;
            if (effectiveOptions.MaxDepth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Max depth must be positive.");
            }

            AssistanceModeResult authored = SearchMode(level, AssistanceComparisonMode.Authored, effectiveOptions);
            AssistanceModeResult noAssistance = SearchMode(level, AssistanceComparisonMode.NoAssistance, effectiveOptions);
            AssistanceModeResult maximumEmergency = SearchMode(level, AssistanceComparisonMode.MaximumEmergency, effectiveOptions);
            AssistanceModeResult authoredNoEmergency = SearchMode(level, AssistanceComparisonMode.AuthoredNoEmergency, effectiveOptions);

            bool dependencyWarning = authored.WinFound && !noAssistance.WinFound;
            bool authoredUsedEmergency = authored.WinFound
                && authored.Events.Any(static ev => ev is Spawned spawned
                    && spawned.Pieces.Any(static piece => piece.EmergencyApplied));
            bool authoredEmergencyOnlyWin = authoredUsedEmergency && !authoredNoEmergency.WinFound;

            ImmutableArray<string> authoredTrajectory = authored.WinFound
                ? Replay(level, authored.SeedUsed, authored.Actions, ModeOverrides(AssistanceComparisonMode.Authored))
                : ImmutableArray<string>.Empty;

            AssistanceModeResult[] visibleModes =
            {
                authored,
                noAssistance,
                maximumEmergency,
            };

            ImmutableArray<AssistanceModeResult>.Builder modes = ImmutableArray.CreateBuilder<AssistanceModeResult>(visibleModes.Length);
            for (int i = 0; i < visibleModes.Length; i++)
            {
                AssistanceModeResult mode = visibleModes[i];
                bool? diverges = null;
                if (authored.WinFound)
                {
                    ImmutableArray<string> trajectory = Replay(
                        level,
                        authored.SeedUsed,
                        authored.Actions,
                        ModeOverrides(mode.Mode));
                    diverges = !authoredTrajectory.SequenceEqual(trajectory, StringComparer.Ordinal);
                }

                modes.Add(mode with { TrajectoryDivergesFromAuthored = diverges });
            }

            return new AssistanceComparisonResult(
                level.Id,
                modes.ToImmutable(),
                dependencyWarning,
                authoredEmergencyOnlyWin,
                authoredUsedEmergency);
        }

        private static AssistanceModeResult SearchMode(
            LevelJson level,
            AssistanceComparisonMode mode,
            AssistanceComparisonOptions options)
        {
            LevelTuningOverrides? overrides = ModeOverrides(mode);
            SearchNode? solved;
            if (options.Seed.HasValue)
            {
                solved = SearchForWin(level, options.Seed.Value, options.MaxDepth, options.MaxSearchNodes, overrides);
                return ToResult(mode, options.Seed.Value, solved);
            }

            SearchNode? bestTerminal = null;
            int lastTriedSeed = options.FirstSeed;
            for (int seed = options.FirstSeed; seed <= options.LastSeed; seed++)
            {
                lastTriedSeed = seed;
                solved = SearchForWin(level, seed, options.MaxDepth, options.MaxSearchNodes, overrides);
                if (solved is not null && solved.Outcome == ActionOutcome.Win)
                {
                    return ToResult(mode, seed, solved);
                }

                if (solved is not null && (bestTerminal is null || solved.Score > bestTerminal.Score))
                {
                    bestTerminal = solved;
                }
            }

            return ToResult(mode, lastTriedSeed, bestTerminal);
        }

        private static AssistanceModeResult ToResult(
            AssistanceComparisonMode mode,
            int seed,
            SearchNode? solved)
        {
            if (solved is null)
            {
                return new AssistanceModeResult(
                    mode,
                    WinFound: false,
                    SeedUsed: seed,
                    ActionCount: 0,
                    Outcome: "NoWinFound",
                    TrajectoryDivergesFromAuthored: null,
                    Actions: ImmutableArray<TileCoord>.Empty,
                    Events: ImmutableArray<ActionEvent>.Empty);
            }

            return new AssistanceModeResult(
                mode,
                WinFound: solved.Outcome == ActionOutcome.Win,
                SeedUsed: seed,
                ActionCount: solved.Actions.Length,
                Outcome: solved.Outcome == ActionOutcome.Ok ? "NoWinFound" : solved.Outcome.ToString(),
                TrajectoryDivergesFromAuthored: null,
                Actions: solved.Actions,
                Events: solved.Events);
        }

        private static LevelTuningOverrides? ModeOverrides(AssistanceComparisonMode mode)
        {
            return mode switch
            {
                AssistanceComparisonMode.Authored => LevelTuningOverrides.None,
                AssistanceComparisonMode.NoAssistance => new LevelTuningOverrides(
                    AssistanceChance: 0.0d,
                    ForceEmergencyAssistance: false),
                AssistanceComparisonMode.MaximumEmergency => new LevelTuningOverrides(
                    AssistanceChance: 1.0d,
                    ForceEmergencyAssistance: true),
                AssistanceComparisonMode.AuthoredNoEmergency => new LevelTuningOverrides(
                    ForceEmergencyAssistance: false),
                _ => LevelTuningOverrides.None,
            };
        }

        private static SearchNode? SearchForWin(
            LevelJson level,
            int seed,
            int maxDepth,
            int maxSearchNodes,
            LevelTuningOverrides? overrides)
        {
            GameState initial = Loader.LoadLevel(level, seed, overrides);
            SearchNode? stochastic = SearchRandomRollouts(initial, seed, maxDepth);
            if (stochastic is not null && stochastic.Outcome == ActionOutcome.Win)
            {
                return stochastic;
            }

            SearchNode? best = stochastic;
            for (int depthLimit = 1; depthLimit <= maxDepth; depthLimit++)
            {
                Dictionary<string, int> seenAtDepth = new Dictionary<string, int>(StringComparer.Ordinal);
                int visitedNodes = 0;
                SearchNode? solved = SearchDepthFirst(
                    initial,
                    ImmutableArray<TileCoord>.Empty,
                    ImmutableArray<ActionEvent>.Empty,
                    depthLimit,
                    maxSearchNodes,
                    ref visitedNodes,
                    seenAtDepth);
                if (solved is not null && solved.Outcome == ActionOutcome.Win)
                {
                    return solved;
                }

                if (solved is not null && (best is null || solved.Score > best.Score))
                {
                    best = solved;
                }
            }

            return best;
        }

        private static SearchNode? SearchRandomRollouts(GameState initial, int seed, int maxDepth)
        {
            Random random = new Random(seed * 7919);
            SearchNode? best = null;

            for (int rollout = 0; rollout < RandomRolloutCount; rollout++)
            {
                GameState state = initial;
                ImmutableArray<TileCoord> actions = ImmutableArray<TileCoord>.Empty;
                ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();

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
                    Append(events, result.Events);
                    SearchNode node = new SearchNode(
                        result.State,
                        actions,
                        result.Outcome,
                        Score(result.State, result.Events),
                        events.ToImmutable());

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

            return best;
        }

        private static SearchNode? SearchDepthFirst(
            GameState state,
            ImmutableArray<TileCoord> actions,
            ImmutableArray<ActionEvent> events,
            int depthRemaining,
            int maxSearchNodes,
            ref int visitedNodes,
            Dictionary<string, int> seenAtDepth)
        {
            if (depthRemaining <= 0)
            {
                return null;
            }

            if (visitedNodes >= maxSearchNodes)
            {
                return null;
            }

            visitedNodes++;
            string fingerprint = Fingerprint(state);
            if (seenAtDepth.TryGetValue(fingerprint, out int knownDepth) && knownDepth >= depthRemaining)
            {
                return null;
            }

            seenAtDepth[fingerprint] = depthRemaining;
            SearchNode? best = null;

            foreach (ActionInput input in EnumerateCandidateInputs(state))
            {
                ActionResult result = Pipeline.RunAction(state, input, new RunOptions(RecordSnapshot: false));
                if (result.Events.Any(static ev => ev is InvalidInput))
                {
                    continue;
                }

                ImmutableArray<TileCoord> nextActions = actions.Add(input.TappedCoord);
                ImmutableArray<ActionEvent> nextEvents = events.AddRange(result.Events);
                SearchNode candidate = new SearchNode(
                    result.State,
                    nextActions,
                    result.Outcome,
                    Score(result.State, result.Events),
                    nextEvents);

                if (result.Outcome == ActionOutcome.Win)
                {
                    return candidate;
                }

                if (result.Outcome != ActionOutcome.Ok)
                {
                    if (best is null || candidate.Score > best.Score)
                    {
                        best = candidate;
                    }

                    continue;
                }

                SearchNode? solved = SearchDepthFirst(
                    result.State,
                    nextActions,
                    nextEvents,
                    depthRemaining - 1,
                    maxSearchNodes,
                    ref visitedNodes,
                    seenAtDepth);
                if (solved is not null && solved.Outcome == ActionOutcome.Win)
                {
                    return solved;
                }

                SearchNode? comparable = solved ?? candidate;
                if (best is null || comparable.Score > best.Score)
                {
                    best = comparable;
                }
            }

            return best;
        }

        private static IEnumerable<ActionInput> EnumerateCandidateInputs(GameState state)
        {
            return EnumerateCandidateActions(state)
                .Select(static candidate => new ActionInput(candidate.Coord));
        }

        private static IEnumerable<CandidateAction> EnumerateCandidateActions(GameState state)
        {
            List<CandidateAction> candidates = new List<CandidateAction>();
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
                        group.Value.Length,
                        CountRouteHits(state.Board, group.Value),
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

        private static int CountRouteHits(Board board, ImmutableArray<TileCoord> group)
        {
            int hits = 0;
            for (int i = 0; i < group.Length; i++)
            {
                ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, group[i]);
                for (int j = 0; j < neighbors.Length; j++)
                {
                    if (BoardHelpers.GetTile(board, neighbors[j]) is TargetTile)
                    {
                        hits += 3;
                    }
                    else if (BoardHelpers.GetTile(board, neighbors[j]) is BlockerTile)
                    {
                        hits += 1;
                    }
                }
            }

            return hits;
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
                if (target.Readiness == TargetReadiness.Progressing)
                {
                    score += 250;
                }
                else if (target.OneClearAway)
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
                    TargetExtractionLatched => 900,
                    TargetOneClearAway => 500,
                    TargetProgressed => 220,
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

        private static int CountBlockedRequiredNeighbors(Board board, TileCoord targetCoord)
        {
            int blocked = 0;
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, targetCoord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (BoardHelpers.GetTile(board, neighbors[i]) is not EmptyTile and not RescuePathTile)
                {
                    blocked++;
                }
            }

            return blocked;
        }

        private static ImmutableArray<string> Replay(
            LevelJson level,
            int seed,
            IReadOnlyList<TileCoord> actions,
            LevelTuningOverrides? overrides)
        {
            ImmutableArray<string>.Builder frames = ImmutableArray.CreateBuilder<string>(actions.Count + 1);
            GameState state = Loader.LoadLevel(level, seed, overrides);
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

            List<string> targets = new List<string>(state.Targets.Length);
            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                targets.Add($"{target.TargetId}@{target.Coord.Row},{target.Coord.Col}:{target.Readiness}");
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
                string.Join(";", targets),
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
                RescuePathTile => "R",
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

        private static void Append(ImmutableArray<ActionEvent>.Builder builder, ImmutableArray<ActionEvent> events)
        {
            for (int i = 0; i < events.Length; i++)
            {
                builder.Add(events[i]);
            }
        }

        private sealed record CandidateAction(TileCoord Coord, int GroupSize, int RouteHits, int SizeDistanceFromTriple);

        private sealed record SearchNode(
            GameState State,
            ImmutableArray<TileCoord> Actions,
            ActionOutcome Outcome,
            int Score,
            ImmutableArray<ActionEvent> Events);
    }

    public sealed record AssistanceComparisonOptions(
        int? Seed,
        int MaxDepth,
        int FirstSeed,
        int LastSeed)
    {
        public int MaxSearchNodes { get; init; } = LevelAssistanceComparisonAnalyzer.DefaultMaxSearchNodes;

        public static AssistanceComparisonOptions Default { get; } = new(
            Seed: null,
            MaxDepth: LevelAssistanceComparisonAnalyzer.DefaultMaxDepth,
            FirstSeed: LevelAssistanceComparisonAnalyzer.DefaultFirstSeed,
            LastSeed: LevelAssistanceComparisonAnalyzer.DefaultLastSeed);
    }

    public enum AssistanceComparisonMode
    {
        Authored,
        NoAssistance,
        MaximumEmergency,
        AuthoredNoEmergency,
    }

    public sealed record AssistanceComparisonResult(
        string LevelId,
        ImmutableArray<AssistanceModeResult> Modes,
        bool NoAssistanceFailsAuthoredSucceeds,
        bool AuthoredEmergencyOnlyWin,
        bool AuthoredUsedEmergencyAssistance);

    public sealed record AssistanceModeResult(
        AssistanceComparisonMode Mode,
        bool WinFound,
        int SeedUsed,
        int ActionCount,
        string Outcome,
        bool? TrajectoryDivergesFromAuthored,
        ImmutableArray<TileCoord> Actions,
        ImmutableArray<ActionEvent> Events);
}
