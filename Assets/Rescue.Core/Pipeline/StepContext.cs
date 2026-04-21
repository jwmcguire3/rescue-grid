using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline
{
    internal sealed record StepContext(
        bool IsValidInput,
        ActionInput Input,
        DebrisType? ValidatedGroupType,
        ImmutableArray<TileCoord> ValidatedGroupCoords,
        ImmutableArray<DebrisType> RemovedDebris,
        ImmutableArray<TileCoord> AdjacentBlockersHit,
        ImmutableArray<TileCoord> BrokenBlockers,
        bool VineClearedThisAction,
        int PendingDockOverflowCount,
        int ClearedDockTriplesThisAction,
        DockWarningLevel DockWarningBefore,
        DockWarningLevel DockWarningAfter,
        ImmutableArray<string> ExtractedTargetIdsThisAction,
        bool WaterRisePending,
        bool VineGrowthPreviewPending,
        bool VineGrowthPending,
        bool IsWin)
    {
        public static StepContext Create(GameState state, ActionInput input)
        {
            DockWarningLevel warning = DockHelpers.GetWarningLevel(state.Dock);
            return new StepContext(
                IsValidInput: false,
                Input: input,
                ValidatedGroupType: null,
                ValidatedGroupCoords: ImmutableArray<TileCoord>.Empty,
                RemovedDebris: ImmutableArray<DebrisType>.Empty,
                AdjacentBlockersHit: ImmutableArray<TileCoord>.Empty,
                BrokenBlockers: ImmutableArray<TileCoord>.Empty,
                VineClearedThisAction: false,
                PendingDockOverflowCount: 0,
                ClearedDockTriplesThisAction: 0,
                DockWarningBefore: warning,
                DockWarningAfter: warning,
                ExtractedTargetIdsThisAction: ImmutableArray<string>.Empty,
                WaterRisePending: false,
                VineGrowthPreviewPending: false,
                VineGrowthPending: false,
                IsWin: false);
        }
    }
}
