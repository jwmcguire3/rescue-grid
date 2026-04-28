using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.Rng;
using Rescue.Core.State;

namespace Rescue.Core.Rules
{
    public readonly record struct SpawnBias(
        ImmutableArray<(DebrisType Type, double Weight)> Weights,
        double EffectiveAssistanceChance,
        bool IsEmergency);

    internal enum RoutePairQuality
    {
        None,
        Soft,
        Hard,
    }

    internal readonly record struct RouteAssistResult(
        RoutePairQuality PairQuality,
        bool HasAdjacency);

    internal readonly record struct SpawnCandidate(
        DebrisType Type,
        double Weight,
        int GroupSize,
        int ExistingGroupPieces,
        bool IsExactTriple,
        bool IsOversized,
        bool IsAllowed);

    internal readonly record struct UrgentRoute(
        string TargetId,
        TileCoord TargetCoord,
        int WaterRisesRemaining,
        int BlockedRequiredNeighbors,
        int StableTargetIndex,
        ImmutableArray<TileCoord> HardRouteCells,
        ImmutableArray<TileCoord> SoftRouteCells);

    public static class SpawnOps
    {
        private const double EmergencyChanceBonus = 0.2d;
        private const double SingletonRecoveryBonus = 100.0d;
        private const double DockCompletionBonus = 70.0d;
        private const double ReachablePairRouteBonus = 40.0d;
        private const double RouteAdjacencyBonus = 15.0d;
        // TODO(B4.5 follow-up): add the 45% final probability cap if route-boost state tracking enters scope.
        // TODO(B4.5 follow-up): add repeated route-boost decay once LastRouteBoostedType/ConsecutiveRouteBoosts exist.
        // TODO(B4.5 follow-up): add single-savior suppression once route-boost event tracking enters scope.

        public static SpawnBias ComputeSpawnBias(GameState state, LevelConfig config)
        {
            return ComputeSpawnBias(state, config, state.DebugSpawnOverride, spawnCoord: null);
        }

        internal static SpawnBias ComputeSpawnBias(GameState state, LevelConfig config, TileCoord? spawnCoord)
        {
            return ComputeSpawnBias(state, config, state.DebugSpawnOverride, spawnCoord);
        }

        internal static SpawnBias ComputeSpawnBias(
            GameState state,
            LevelConfig config,
            SpawnOverride? spawnOverride,
            TileCoord? spawnCoord)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            ImmutableArray<DebrisType> pool = GetValidatedPool(config);
            Dictionary<DebrisType, double> baseWeights = CreateBaseWeights(pool, config);
            Dictionary<DebrisType, double> assistedWeights = new Dictionary<DebrisType, double>(baseWeights);

            ApplyAssistanceBonuses(state, pool, assistedWeights, spawnCoord, includeRecoveryBonus: false);

            bool isEmergency = IsEmergencyActive(state, config, spawnOverride);
            double baseAssistanceChance = spawnOverride?.OverrideAssistanceChance ?? config.AssistanceChance;
            double effectiveAssistanceChance = ClampChance(baseAssistanceChance + (isEmergency ? EmergencyChanceBonus : 0.0d));

            ImmutableArray<(DebrisType Type, double Weight)> weights = BlendWeights(pool, baseWeights, assistedWeights, effectiveAssistanceChance);
            if (spawnCoord.HasValue && state.SpawnRecoveryCounter > 0)
            {
                weights = ApplyRecoveryBias(weights, state.Board, spawnCoord.Value);
            }

            return new SpawnBias(weights, effectiveAssistanceChance, isEmergency);
        }

        internal static DebrisType ChooseNextSpawn(GameState state, Board preSpawnBoard, TileCoord spawnCoord, SeededRng rng)
        {
            if (rng is null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            SpawnBias bias = ComputeSpawnBias(state, state.LevelConfig, state.DebugSpawnOverride, spawnCoord);
            List<(DebrisType item, double weight)> weightedItems = new List<(DebrisType item, double weight)>(bias.Weights.Length);
            List<SpawnCandidate> allowedCandidates = new List<SpawnCandidate>(bias.Weights.Length);
            for (int i = 0; i < bias.Weights.Length; i++)
            {
                (DebrisType type, double weight) = bias.Weights[i];
                SpawnCandidate candidate = EvaluateSpawnCandidate(state, preSpawnBoard, spawnCoord, type, weight);
                if (candidate.IsAllowed)
                {
                    allowedCandidates.Add(candidate);
                    weightedItems.Add((type, weight));
                }
            }

            if (HasPositiveWeight(weightedItems))
            {
                return rng.WeightedPick(weightedItems);
            }

            if (allowedCandidates.Count > 0)
            {
                return ChooseBestFallback(allowedCandidates);
            }

            return ChooseFallbackSpawn(state, preSpawnBoard, spawnCoord, bias.Weights);
        }

        internal static SpawnedPiece DescribeSpawnedPiece(
            GameState state,
            TileCoord spawnCoord,
            DebrisType debrisType,
            int lineageId)
        {
            SpawnBias bias = ComputeSpawnBias(state, state.LevelConfig, state.DebugSpawnOverride, spawnCoord);
            bool emergencyRequested = IsEmergencyRequested(state, state.DebugSpawnOverride);
            ImmutableArray<SpawnAssistReason> reasons = BuildAssistReasons(
                state,
                spawnCoord,
                debrisType,
                emergencyRequested,
                bias.IsEmergency,
                bias.EffectiveAssistanceChance);
            UrgentRoute? urgentRoute = FindUrgentRoute(state);

            return new SpawnedPiece(
                spawnCoord,
                debrisType,
                lineageId,
                reasons,
                BuildTriggerContext(state, emergencyRequested),
                urgentRoute?.TargetId,
                urgentRoute?.TargetCoord,
                urgentRoute?.WaterRisesRemaining ?? 0,
                DockHelpers.Occupancy(state.Dock),
                state.SpawnRecoveryCounter,
                emergencyRequested,
                bias.IsEmergency,
                bias.EffectiveAssistanceChance);
        }

        internal static bool IsEmergencyActive(GameState state, LevelConfig config, SpawnOverride? spawnOverride = null)
        {
            if (!IsEmergencyRequested(state, spawnOverride))
            {
                return false;
            }

            return state.ConsecutiveEmergencySpawns < config.ConsecutiveEmergencyCap;
        }

        internal static SpawnCandidate EvaluateSpawnCandidate(
            GameState state,
            Board preSpawnBoard,
            TileCoord spawnCoord,
            DebrisType debrisType,
            double weight)
        {
            Board simulatedBoard = BoardHelpers.SetTile(state.Board, spawnCoord, new DebrisTile(debrisType));
            ImmutableArray<TileCoord>? group = GroupOps.FindGroup(simulatedBoard, spawnCoord);
            int groupSize = group?.Length ?? 1;
            int existingPieces = group.HasValue
                ? CountExistingGroupPieces(preSpawnBoard, group.Value, debrisType)
                : 0;
            bool isExactTriple = groupSize == 3;
            bool isOversized = groupSize > 5;
            bool exactTripleAllowed = state.LevelConfig.SpawnIntegrity.AllowExactTripleSpawns
                || state.LevelConfig.IsRuleTeach
                || state.SpawnRecoveryCounter > 0;
            bool oversizedAllowed = state.LevelConfig.SpawnIntegrity.AllowOversizedSpawnGroups
                || (isOversized && existingPieces > groupSize / 2);
            bool allowed = (!isExactTriple || exactTripleAllowed)
                && (!isOversized || oversizedAllowed);

            return new SpawnCandidate(
                debrisType,
                weight,
                groupSize,
                existingPieces,
                isExactTriple,
                isOversized,
                allowed);
        }

        private static DebrisType ChooseFallbackSpawn(
            GameState state,
            Board preSpawnBoard,
            TileCoord spawnCoord,
            ImmutableArray<(DebrisType Type, double Weight)> weights)
        {
            SpawnCandidate? best = null;
            for (int i = 0; i < weights.Length; i++)
            {
                (DebrisType type, double weight) = weights[i];
                SpawnCandidate candidate = EvaluateSpawnCandidate(state, preSpawnBoard, spawnCoord, type, weight);
                if (state.SpawnRecoveryCounter > 0 && candidate.IsExactTriple)
                {
                    if (!best.HasValue || CompareFallback(candidate, best.Value) < 0)
                    {
                        best = candidate;
                    }

                    continue;
                }

                if (state.SpawnRecoveryCounter > 0)
                {
                    continue;
                }

                if (!best.HasValue || CompareFallback(candidate, best.Value) < 0)
                {
                    best = candidate;
                }
            }

            if (best.HasValue)
            {
                return best.Value.Type;
            }

            for (int i = 0; i < weights.Length; i++)
            {
                (DebrisType type, double weight) = weights[i];
                SpawnCandidate candidate = EvaluateSpawnCandidate(state, preSpawnBoard, spawnCoord, type, weight);
                if (!best.HasValue || CompareFallback(candidate, best.Value) < 0)
                {
                    best = candidate;
                }
            }

            return best?.Type ?? weights[0].Type;
        }

        private static bool HasPositiveWeight(IReadOnlyList<(DebrisType item, double weight)> weightedItems)
        {
            for (int i = 0; i < weightedItems.Count; i++)
            {
                if (weightedItems[i].weight > 0.0d)
                {
                    return true;
                }
            }

            return false;
        }

        private static DebrisType ChooseBestFallback(IReadOnlyList<SpawnCandidate> candidates)
        {
            SpawnCandidate best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (CompareFallback(candidates[i], best) < 0)
                {
                    best = candidates[i];
                }
            }

            return best.Type;
        }

        private static int CompareFallback(SpawnCandidate left, SpawnCandidate right)
        {
            int sizeComparison = left.GroupSize.CompareTo(right.GroupSize);
            if (sizeComparison != 0)
            {
                return sizeComparison;
            }

            int weightComparison = right.Weight.CompareTo(left.Weight);
            if (weightComparison != 0)
            {
                return weightComparison;
            }

            return left.Type.CompareTo(right.Type);
        }

        private static int CountExistingGroupPieces(
            Board preSpawnBoard,
            ImmutableArray<TileCoord> group,
            DebrisType debrisType)
        {
            int count = 0;
            for (int i = 0; i < group.Length; i++)
            {
                if (BoardHelpers.GetTile(preSpawnBoard, group[i]) is DebrisTile existing
                    && existing.Type == debrisType)
                {
                    count++;
                }
            }

            return count;
        }

        internal static bool BoardIsSingletonOnly(Board board)
        {
            for (int row = 0; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (GroupOps.FindGroup(board, coord) is { Length: >= 2 })
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal static UrgentRoute? FindUrgentRoute(GameState state)
        {
            UrgentRoute? best = null;
            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                if (target.Extracted)
                {
                    continue;
                }

                UrgentRoute candidate = new UrgentRoute(
                    target.TargetId,
                    target.Coord,
                    CountFutureWaterRisesBeforeFlood(state, target.Coord),
                    CountBlockedRequiredNeighbors(state.Board, target.Coord),
                    GetStableTargetIndex(state.Board, target.Coord),
                    BuildHardRouteCells(state.Board, target.Coord),
                    ImmutableArray<TileCoord>.Empty);
                ImmutableArray<TileCoord> softRouteCells = BuildSoftRouteCells(state.Board, candidate.HardRouteCells);
                candidate = candidate with { SoftRouteCells = softRouteCells };

                if (!best.HasValue || CompareUrgency(candidate, best.Value) < 0)
                {
                    best = candidate;
                }
            }

            return best;
        }

        internal static RouteAssistResult EvaluateRouteAssist(Board board, TileCoord spawnCoord, DebrisType debrisType, UrgentRoute route)
        {
            if (!BoardHelpers.InBounds(board, spawnCoord) || BoardHelpers.GetTile(board, spawnCoord) is not EmptyTile)
            {
                return new RouteAssistResult(RoutePairQuality.None, HasAdjacency: false);
            }

            Board simulatedBoard = BoardHelpers.SetTile(board, spawnCoord, new DebrisTile(debrisType));
            ImmutableArray<TileCoord>? group = GroupOps.FindGroup(simulatedBoard, spawnCoord);
            if (group.HasValue)
            {
                bool touchesHard = ContainsAny(group.Value, route.HardRouteCells);
                if (touchesHard)
                {
                    return new RouteAssistResult(RoutePairQuality.Hard, HasAdjacency: false);
                }

                bool touchesSoft = ContainsAny(group.Value, route.SoftRouteCells);
                if (touchesSoft)
                {
                    return new RouteAssistResult(RoutePairQuality.Soft, HasAdjacency: false);
                }
            }

            return new RouteAssistResult(
                RoutePairQuality.None,
                HasAdjacency: IsRouteAdjacent(spawnCoord, route));
        }

        private static ImmutableArray<DebrisType> GetValidatedPool(LevelConfig config)
        {
            if (config.DebrisTypePool.IsDefaultOrEmpty)
            {
                throw new InvalidOperationException("LevelConfig must define at least one debris type.");
            }

            return config.DebrisTypePool;
        }

        private static Dictionary<DebrisType, double> CreateBaseWeights(
            ImmutableArray<DebrisType> pool,
            LevelConfig config)
        {
            Dictionary<DebrisType, double> weights = new Dictionary<DebrisType, double>(pool.Length);
            double total = 0.0d;

            for (int i = 0; i < pool.Length; i++)
            {
                DebrisType type = pool[i];
                double weight = ResolveBaseWeight(type, config);
                weights[type] = weight;
                total += weight;
            }

            if (total > 0.0d)
            {
                return weights;
            }

            for (int i = 0; i < pool.Length; i++)
            {
                weights[pool[i]] = 1.0d;
            }

            return weights;
        }

        private static double ResolveBaseWeight(DebrisType type, LevelConfig config)
        {
            if (config.BaseDistribution is null)
            {
                return 1.0d;
            }

            if (!config.BaseDistribution.TryGetValue(type, out double weight))
            {
                return 0.0d;
            }

            if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0.0d)
            {
                throw new InvalidOperationException($"Invalid base spawn weight for debris type {type}.");
            }

            return weight;
        }

        private static void ApplyAssistanceBonuses(
            GameState state,
            ImmutableArray<DebrisType> pool,
            Dictionary<DebrisType, double> weights,
            TileCoord? spawnCoord,
            bool includeRecoveryBonus)
        {
            int[] dockCounts = CountDockPieces(state.Dock);
            ImmutableHashSet<DebrisType> recoveryTypes = ImmutableHashSet<DebrisType>.Empty;
            UrgentRoute? urgentRoute = null;
            Dictionary<DebrisType, RouteAssistResult>? routeAssist = null;

            if (spawnCoord.HasValue && includeRecoveryBonus)
            {
                recoveryTypes = FindPairCompletingTypesAt(state.Board, spawnCoord.Value);
            }

            if (spawnCoord.HasValue)
            {
                urgentRoute = FindUrgentRoute(state);
            }

            if (spawnCoord.HasValue && urgentRoute.HasValue)
            {
                routeAssist = new Dictionary<DebrisType, RouteAssistResult>(pool.Length);
                for (int i = 0; i < pool.Length; i++)
                {
                    DebrisType type = pool[i];
                    routeAssist[type] = EvaluateRouteAssist(state.Board, spawnCoord.Value, type, urgentRoute.Value);
                }
            }

            for (int i = 0; i < pool.Length; i++)
            {
                DebrisType type = pool[i];
                double bonus = 0.0d;

                if (state.SpawnRecoveryCounter > 0 && recoveryTypes.Contains(type))
                {
                    bonus += SingletonRecoveryBonus;
                }

                if (dockCounts[(int)type] == 2)
                {
                    bonus += DockCompletionBonus;
                }

                if (routeAssist is not null && routeAssist.TryGetValue(type, out RouteAssistResult assist))
                {
                    if (assist.PairQuality != RoutePairQuality.None)
                    {
                        bonus += ReachablePairRouteBonus;
                    }
                    else if (assist.HasAdjacency)
                    {
                        bonus += RouteAdjacencyBonus;
                    }
                }

                weights[type] += bonus;
            }
        }

        private static ImmutableArray<(DebrisType Type, double Weight)> BlendWeights(
            ImmutableArray<DebrisType> pool,
            IReadOnlyDictionary<DebrisType, double> baseWeights,
            IReadOnlyDictionary<DebrisType, double> assistedWeights,
            double assistanceChance)
        {
            ImmutableArray<(DebrisType Type, double Weight)>.Builder blended = ImmutableArray.CreateBuilder<(DebrisType Type, double Weight)>(pool.Length);
            for (int i = 0; i < pool.Length; i++)
            {
                DebrisType type = pool[i];
                double baseWeight = baseWeights[type];
                double assistedWeight = assistedWeights[type];
                double weight = baseWeight + ((assistedWeight - baseWeight) * assistanceChance);
                blended.Add((type, weight));
            }

            return blended.ToImmutable();
        }

        internal static bool IsEmergencyRequested(GameState state, SpawnOverride? spawnOverride = null)
        {
            if (spawnOverride?.ForceEmergency == true)
            {
                return true;
            }

            if (spawnOverride?.ForceEmergency == false)
            {
                return false;
            }

            return DockHelpers.Occupancy(state.Dock) >= 5 || HasTargetOneWaterRiseFromLoss(state);
        }

        internal static bool IsEmergencyDockPressure(GameState state)
        {
            return DockHelpers.Occupancy(state.Dock) >= 5;
        }

        internal static bool IsEmergencyWaterPressure(GameState state)
        {
            return HasTargetOneWaterRiseFromLoss(state);
        }

        private static ImmutableArray<SpawnAssistReason> BuildAssistReasons(
            GameState state,
            TileCoord spawnCoord,
            DebrisType debrisType,
            bool emergencyRequested,
            bool emergencyApplied,
            double effectiveAssistanceChance)
        {
            ImmutableArray<SpawnAssistReason>.Builder reasons = ImmutableArray.CreateBuilder<SpawnAssistReason>();
            if (state.DebugSpawnOverride is not null)
            {
                reasons.Add(SpawnAssistReason.DebugOverride);
            }

            if (emergencyRequested && emergencyApplied)
            {
                if (IsEmergencyWaterPressure(state))
                {
                    reasons.Add(SpawnAssistReason.EmergencyWaterPressure);
                }

                if (IsEmergencyDockPressure(state))
                {
                    reasons.Add(SpawnAssistReason.EmergencyDockPressure);
                }
            }

            int[] dockCounts = CountDockPieces(state.Dock);
            if (dockCounts[(int)debrisType] == 2)
            {
                reasons.Add(SpawnAssistReason.DockCompletion);
            }

            if (state.SpawnRecoveryCounter > 0
                && FindPairCompletingTypesAt(state.Board, spawnCoord).Contains(debrisType))
            {
                reasons.Add(SpawnAssistReason.SingletonRecovery);
            }

            UrgentRoute? urgentRoute = FindUrgentRoute(state);
            if (urgentRoute.HasValue)
            {
                RouteAssistResult routeAssist = EvaluateRouteAssist(state.Board, spawnCoord, debrisType, urgentRoute.Value);
                if (routeAssist.PairQuality == RoutePairQuality.Hard)
                {
                    reasons.Add(SpawnAssistReason.RouteHardPair);
                }
                else if (routeAssist.PairQuality == RoutePairQuality.Soft)
                {
                    reasons.Add(SpawnAssistReason.RouteSoftPair);
                }
                else if (routeAssist.HasAdjacency)
                {
                    reasons.Add(SpawnAssistReason.RouteAdjacency);
                }
            }

            if (reasons.Count == 0 && effectiveAssistanceChance > 0.0d)
            {
                reasons.Add(SpawnAssistReason.BaselineAssistance);
            }

            return reasons.ToImmutable();
        }

        private static ImmutableArray<string> BuildTriggerContext(GameState state, bool emergencyRequested)
        {
            ImmutableArray<string>.Builder context = ImmutableArray.CreateBuilder<string>();
            if (state.DebugSpawnOverride is not null)
            {
                context.Add("debug_override");
            }

            if (emergencyRequested)
            {
                if (IsEmergencyDockPressure(state))
                {
                    context.Add("dock_pressure");
                }

                if (IsEmergencyWaterPressure(state))
                {
                    context.Add("water_pressure");
                }
            }

            if (state.SpawnRecoveryCounter > 0)
            {
                context.Add("singleton_recovery_active");
            }

            if (FindUrgentRoute(state).HasValue)
            {
                context.Add("urgent_route_available");
            }

            if (context.Count == 0)
            {
                context.Add("baseline");
            }

            return context.ToImmutable();
        }

        private static bool HasTargetOneWaterRiseFromLoss(GameState state)
        {
            if (state.Water.FloodedRows >= state.Board.Height)
            {
                return false;
            }

            int nextFloodRow = state.Board.Height - state.Water.FloodedRows - 1;
            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                if (!target.Extracted && target.Coord.Row == nextFloodRow)
                {
                    return true;
                }
            }

            return false;
        }

        private static double ClampChance(double chance)
        {
            if (chance < 0.0d)
            {
                return 0.0d;
            }

            if (chance > 1.0d)
            {
                return 1.0d;
            }

            return chance;
        }

        private static int[] CountDockPieces(Dock dock)
        {
            int[] counts = new int[Enum.GetValues(typeof(DebrisType)).Length];
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                DebrisType? slot = dock.Slots[i];
                if (slot.HasValue)
                {
                    counts[(int)slot.Value]++;
                }
            }

            return counts;
        }

        private static ImmutableHashSet<DebrisType> FindPairCompletingTypesAt(Board board, TileCoord coord)
        {
            if (!BoardHelpers.InBounds(board, coord) || BoardHelpers.GetTile(board, coord) is not EmptyTile)
            {
                return ImmutableHashSet<DebrisType>.Empty;
            }

            ImmutableHashSet<DebrisType>.Builder types = ImmutableHashSet.CreateBuilder<DebrisType>();
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, coord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (BoardHelpers.GetTile(board, neighbors[i]) is DebrisTile debris)
                {
                    types.Add(debris.Type);
                }
            }

            return types.ToImmutable();
        }

        private static ImmutableArray<(DebrisType Type, double Weight)> ApplyRecoveryBias(
            ImmutableArray<(DebrisType Type, double Weight)> weights,
            Board board,
            TileCoord spawnCoord)
        {
            ImmutableHashSet<DebrisType> recoveryTypes = FindPairCompletingTypesAt(board, spawnCoord);
            if (recoveryTypes.Count == 0)
            {
                return weights;
            }

            ImmutableArray<(DebrisType Type, double Weight)>.Builder updated = ImmutableArray.CreateBuilder<(DebrisType Type, double Weight)>(weights.Length);
            for (int i = 0; i < weights.Length; i++)
            {
                (DebrisType Type, double Weight) entry = weights[i];
                updated.Add(recoveryTypes.Contains(entry.Type)
                    ? (entry.Type, entry.Weight + SingletonRecoveryBonus)
                    : entry);
            }

            return updated.ToImmutable();
        }

        private static int CompareUrgency(UrgentRoute left, UrgentRoute right)
        {
            int waterComparison = left.WaterRisesRemaining.CompareTo(right.WaterRisesRemaining);
            if (waterComparison != 0)
            {
                return waterComparison;
            }

            int blockedComparison = left.BlockedRequiredNeighbors.CompareTo(right.BlockedRequiredNeighbors);
            if (blockedComparison != 0)
            {
                return blockedComparison;
            }

            return left.StableTargetIndex.CompareTo(right.StableTargetIndex);
        }

        private static int CountFutureWaterRisesBeforeFlood(GameState state, TileCoord targetCoord)
        {
            int nextFloodRow = state.Board.Height - state.Water.FloodedRows - 1;
            int risesRemaining = nextFloodRow - targetCoord.Row;
            return risesRemaining < 0 ? 0 : risesRemaining;
        }

        private static int CountBlockedRequiredNeighbors(Board board, TileCoord targetCoord)
        {
            int count = 0;
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, targetCoord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (!IsOpenRequiredNeighbor(BoardHelpers.GetTile(board, neighbors[i])))
                {
                    count++;
                }
            }

            return count;
        }

        private static int GetStableTargetIndex(Board board, TileCoord targetCoord)
        {
            return (targetCoord.Row * board.Width) + targetCoord.Col;
        }

        private static ImmutableArray<TileCoord> BuildHardRouteCells(Board board, TileCoord targetCoord)
        {
            ImmutableArray<TileCoord>.Builder hardRoute = ImmutableArray.CreateBuilder<TileCoord>();
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, targetCoord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                TileCoord neighbor = neighbors[i];
                Tile tile = BoardHelpers.GetTile(board, neighbor);
                if (tile is DebrisTile or BlockerTile)
                {
                    hardRoute.Add(neighbor);
                }
            }

            return hardRoute.ToImmutable();
        }

        private static ImmutableArray<TileCoord> BuildSoftRouteCells(Board board, ImmutableArray<TileCoord> hardRouteCells)
        {
            HashSet<TileCoord> softRoute = new HashSet<TileCoord>();
            for (int i = 0; i < hardRouteCells.Length; i++)
            {
                ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, hardRouteCells[i]);
                for (int j = 0; j < neighbors.Length; j++)
                {
                    TileCoord neighbor = neighbors[j];
                    Tile tile = BoardHelpers.GetTile(board, neighbor);
                    if (tile is FloodedTile || ContainsCoord(hardRouteCells, neighbor))
                    {
                        continue;
                    }

                    softRoute.Add(neighbor);
                }
            }

            return SortCoords(softRoute, board.Width);
        }

        private static bool IsOpenRequiredNeighbor(Tile tile)
        {
            return tile is EmptyTile;
        }

        private static bool IsRouteAdjacent(TileCoord coord, UrgentRoute route)
        {
            if (ContainsCoord(route.HardRouteCells, coord) || ContainsCoord(route.SoftRouteCells, coord))
            {
                return true;
            }

            for (int i = 0; i < route.HardRouteCells.Length; i++)
            {
                if (ManhattanDistance(coord, route.HardRouteCells[i]) == 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ManhattanDistance(TileCoord left, TileCoord right)
        {
            return Math.Abs(left.Row - right.Row) + Math.Abs(left.Col - right.Col);
        }

        private static bool ContainsAny(ImmutableArray<TileCoord> group, ImmutableArray<TileCoord> routeCells)
        {
            for (int i = 0; i < group.Length; i++)
            {
                if (ContainsCoord(routeCells, group[i]))
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

        private static ImmutableArray<TileCoord> SortCoords(HashSet<TileCoord> coords, int boardWidth)
        {
            List<TileCoord> ordered = new List<TileCoord>(coords.Count);
            foreach (TileCoord coord in coords)
            {
                ordered.Add(coord);
            }

            ordered.Sort((left, right) =>
            {
                int leftIndex = (left.Row * boardWidth) + left.Col;
                int rightIndex = (right.Row * boardWidth) + right.Col;
                return leftIndex.CompareTo(rightIndex);
            });

            return ordered.ToImmutableArray();
        }
    }
}
