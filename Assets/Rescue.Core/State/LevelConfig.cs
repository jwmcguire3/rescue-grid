using System.Collections.Immutable;

namespace Rescue.Core.State
{
    public sealed record SpawnIntegrityPolicy(
        bool AllowExactTripleSpawns = false,
        bool AllowOversizedSpawnGroups = false);

    public sealed record LevelConfig
    {
        public LevelConfig(
            ImmutableArray<DebrisType> DebrisTypePool,
            ImmutableDictionary<DebrisType, double>? BaseDistribution,
            double AssistanceChance,
            int ConsecutiveEmergencyCap = 2,
            bool IsRuleTeach = false,
            WaterContactMode WaterContactMode = WaterContactMode.ImmediateLoss,
            SpawnIntegrityPolicy? SpawnIntegrityPolicy = null)
        {
            this.DebrisTypePool = DebrisTypePool;
            this.BaseDistribution = BaseDistribution;
            this.AssistanceChance = AssistanceChance;
            this.ConsecutiveEmergencyCap = ConsecutiveEmergencyCap;
            this.IsRuleTeach = IsRuleTeach;
            this.WaterContactMode = WaterContactMode;
            SpawnIntegrity = SpawnIntegrityPolicy ?? new SpawnIntegrityPolicy();
        }

        public ImmutableArray<DebrisType> DebrisTypePool { get; init; }

        public ImmutableDictionary<DebrisType, double>? BaseDistribution { get; init; }

        public double AssistanceChance { get; init; }

        public int ConsecutiveEmergencyCap { get; init; }

        public bool IsRuleTeach { get; init; }

        public WaterContactMode WaterContactMode { get; init; }

        public SpawnIntegrityPolicy SpawnIntegrity { get; init; }
    }
}
