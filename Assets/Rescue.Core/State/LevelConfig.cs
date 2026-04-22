using System.Collections.Immutable;

namespace Rescue.Core.State
{
    public sealed record LevelConfig(
        ImmutableArray<DebrisType> DebrisTypePool,
        ImmutableDictionary<DebrisType, double>? BaseDistribution,
        double AssistanceChance,
        int ConsecutiveEmergencyCap = 2,
        bool IsRuleTeach = false);
}
