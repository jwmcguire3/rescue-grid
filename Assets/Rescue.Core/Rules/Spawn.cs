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

    public static class SpawnOps
    {
        private const double EmergencyChanceBonus = 0.2d;
        private const double DockCompletionBonus = 3.0d;
        private const double RecoveryPairBonus = 4.0d;

        public static SpawnBias ComputeSpawnBias(GameState state, LevelConfig config)
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

            ApplyDockAwareAssistance(state, assistedWeights);
            ApplyRouteAssistHooks(state, assistedWeights);
            ApplyRecoveryBias(state, assistedWeights);

            bool isEmergency = IsEmergencyActive(state, config);
            double effectiveAssistanceChance = ClampChance(config.AssistanceChance + (isEmergency ? EmergencyChanceBonus : 0.0d));

            return new SpawnBias(
                BlendWeights(pool, baseWeights, assistedWeights, effectiveAssistanceChance),
                effectiveAssistanceChance,
                isEmergency);
        }

        internal static DebrisType ChooseNextSpawn(GameState state, TileCoord spawnCoord, SeededRng rng)
        {
            if (rng is null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            SpawnBias baseBias = ComputeSpawnBias(state, state.LevelConfig);
            ImmutableArray<(DebrisType Type, double Weight)> weights = baseBias.Weights;

            if (state.SpawnRecoveryCounter > 0)
            {
                weights = ApplySpawnCoordRecoveryBias(weights, state.Board, spawnCoord);
            }

            List<(DebrisType item, double weight)> weightedItems = new List<(DebrisType item, double weight)>(weights.Length);
            for (int i = 0; i < weights.Length; i++)
            {
                weightedItems.Add((weights[i].Type, weights[i].Weight));
            }

            return rng.WeightedPick(weightedItems);
        }

        internal static bool IsEmergencyActive(GameState state, LevelConfig config)
        {
            if (!IsEmergencyRequested(state))
            {
                return false;
            }

            return state.ConsecutiveEmergencySpawns < config.ConsecutiveEmergencyCap;
        }

        internal static bool BoardIsSingletonOnly(Board board)
        {
            for (int row = 0; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (GroupOps.FindGroup(board, coord) is { } group && group.Length >= 2)
                    {
                        return false;
                    }
                }
            }

            return true;
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

        private static void ApplyDockAwareAssistance(GameState state, Dictionary<DebrisType, double> weights)
        {
            int[] counts = new int[Enum.GetValues(typeof(DebrisType)).Length];
            for (int i = 0; i < state.Dock.Slots.Length; i++)
            {
                DebrisType? slot = state.Dock.Slots[i];
                if (slot.HasValue)
                {
                    counts[(int)slot.Value]++;
                }
            }

            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 2)
                {
                    DebrisType type = (DebrisType)i;
                    if (weights.ContainsKey(type))
                    {
                        weights[type] += DockCompletionBonus;
                    }
                }
            }
        }

        private static void ApplyRouteAssistHooks(GameState state, Dictionary<DebrisType, double> weights)
        {
            _ = state;
            _ = weights;

            // TODO(B9+): prefer debris types that complete a reachable pair near the urgent route.
            // TODO(B9+): prefer debris types adjacent to the most urgent target path.
        }

        private static void ApplyRecoveryBias(GameState state, Dictionary<DebrisType, double> weights)
        {
            if (state.SpawnRecoveryCounter <= 0)
            {
                return;
            }

            ImmutableHashSet<DebrisType> pairCompletingTypes = FindPairCompletingTypes(state.Board);
            foreach (DebrisType type in pairCompletingTypes)
            {
                if (weights.ContainsKey(type))
                {
                    weights[type] += RecoveryPairBonus;
                }
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

        private static bool IsEmergencyRequested(GameState state)
        {
            return DockHelpers.Occupancy(state.Dock) >= 5 || HasTargetOneWaterRiseFromLoss(state);
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

        private static ImmutableHashSet<DebrisType> FindPairCompletingTypes(Board board)
        {
            ImmutableHashSet<DebrisType>.Builder types = ImmutableHashSet.CreateBuilder<DebrisType>();
            for (int row = 0; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (WouldCreatePairAt(board, coord, out DebrisType? pairType) && pairType.HasValue)
                    {
                        types.Add(pairType.Value);
                    }
                }
            }

            return types.ToImmutable();
        }

        private static ImmutableArray<(DebrisType Type, double Weight)> ApplySpawnCoordRecoveryBias(
            ImmutableArray<(DebrisType Type, double Weight)> weights,
            Board board,
            TileCoord spawnCoord)
        {
            if (!WouldCreatePairAt(board, spawnCoord, out DebrisType? pairType) || !pairType.HasValue)
            {
                return weights;
            }

            ImmutableArray<(DebrisType Type, double Weight)>.Builder updated = ImmutableArray.CreateBuilder<(DebrisType Type, double Weight)>(weights.Length);
            for (int i = 0; i < weights.Length; i++)
            {
                (DebrisType Type, double Weight) entry = weights[i];
                if (entry.Type == pairType.Value)
                {
                    updated.Add((entry.Type, entry.Weight + RecoveryPairBonus));
                }
                else
                {
                    updated.Add(entry);
                }
            }

            return updated.ToImmutable();
        }

        private static bool WouldCreatePairAt(Board board, TileCoord coord, out DebrisType? pairType)
        {
            pairType = null;
            if (!BoardHelpers.InBounds(board, coord) || BoardHelpers.GetTile(board, coord) is not EmptyTile)
            {
                return false;
            }

            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, coord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (BoardHelpers.GetTile(board, neighbors[i]) is DebrisTile debris)
                {
                    pairType = debris.Type;
                    return true;
                }
            }

            return false;
        }
    }
}
