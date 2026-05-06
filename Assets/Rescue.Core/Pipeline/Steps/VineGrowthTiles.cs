using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class VineGrowthTiles
    {
        public static bool IsValidGrowthTile(Board board, VineState vine, ImmutableArray<TargetState> targets, TileCoord coord)
        {
            if (!BoardHelpers.InBounds(board, coord))
            {
                return false;
            }

            Tile tile = BoardHelpers.GetTile(board, coord);
            return tile is EmptyTile
                || tile is DebrisTile
                || tile is RescuePathTile rescuePath && IsUnlatchedRescuePath(rescuePath, targets);
        }

        public static bool IsReservedFutureGrowthTile(Board board, VineState vine, TileCoord coord)
        {
            return BoardHelpers.InBounds(board, coord)
                && BoardHelpers.GetTile(board, coord) is EmptyTile
                && IsFuturePriorityCoord(vine, coord);
        }

        private static bool IsUnlatchedRescuePath(RescuePathTile rescuePath, ImmutableArray<TargetState> targets)
        {
            for (int i = 0; i < rescuePath.TargetIds.Length; i++)
            {
                string targetId = rescuePath.TargetIds[i];
                for (int j = 0; j < targets.Length; j++)
                {
                    TargetState target = targets[j];
                    if (target.TargetId == targetId && (target.Extracted || target.ExtractableLatched))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsFuturePriorityCoord(VineState vine, TileCoord coord)
        {
            int startIndex = vine.PriorityCursor < 0 ? 0 : vine.PriorityCursor;
            for (int i = startIndex; i < vine.GrowthPriorityList.Length; i++)
            {
                if (vine.GrowthPriorityList[i] == coord)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
