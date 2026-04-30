using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Rules
{
    public enum DeadboardRepairReason
    {
        HardNoValidGroups,
    }

    public enum DeadboardRepairSkippedReason
    {
        None,
        ExistingValidGroup,
        InsufficientEligibleCells,
        NoAdjacentEligiblePair,
        NoValidMinimalRepair,
    }

    public readonly record struct DebrisTypeChange(
        TileCoord Coord,
        DebrisType Before,
        DebrisType After);

    public readonly record struct DeadboardRepairResult(
        Board Board,
        bool Succeeded,
        DeadboardRepairReason Reason,
        DeadboardRepairSkippedReason SkippedReason,
        ImmutableArray<DebrisTypeChange> Changes);

    public static class DeadboardRepairOps
    {
        public static DeadboardRepairResult RepairHardNoValidGroups(Board board, WaterState water)
        {
            if (GroupOps.HasValidGroup(board, water))
            {
                return Skipped(board, DeadboardRepairSkippedReason.ExistingValidGroup);
            }

            ImmutableArray<EligibleDebris> eligible = CollectEligibleDebris(board, water);
            if (eligible.Length < 2)
            {
                return Skipped(board, DeadboardRepairSkippedReason.InsufficientEligibleCells);
            }

            RepairCandidate? best = null;
            bool foundAdjacentPair = false;
            for (int i = 0; i < eligible.Length; i++)
            {
                for (int j = i + 1; j < eligible.Length; j++)
                {
                    if (!AreOrthogonallyAdjacent(eligible[i].Coord, eligible[j].Coord))
                    {
                        continue;
                    }

                    foundAdjacentPair = true;
                    ConsiderCandidate(board, water, eligible, eligible[i], eligible[j], eligible[i].Type, ref best);
                    ConsiderCandidate(board, water, eligible, eligible[i], eligible[j], eligible[j].Type, ref best);
                }
            }

            if (!foundAdjacentPair)
            {
                return Skipped(board, DeadboardRepairSkippedReason.NoAdjacentEligiblePair);
            }

            if (!best.HasValue)
            {
                return Skipped(board, DeadboardRepairSkippedReason.NoValidMinimalRepair);
            }

            RepairCandidate chosen = best.Value;
            return new DeadboardRepairResult(
                chosen.Board,
                Succeeded: true,
                DeadboardRepairReason.HardNoValidGroups,
                DeadboardRepairSkippedReason.None,
                chosen.Changes);
        }

        private static void ConsiderCandidate(
            Board board,
            WaterState water,
            ImmutableArray<EligibleDebris> eligible,
            EligibleDebris left,
            EligibleDebris right,
            DebrisType targetType,
            ref RepairCandidate? best)
        {
            EligibleDebris changing = left.Type == targetType ? right : left;
            if (changing.Type == targetType)
            {
                return;
            }

            RepairCandidate? swapCandidate = TryBuildSwapCandidate(board, water, eligible, left, right, changing, targetType);
            if (swapCandidate.HasValue && (!best.HasValue || CompareCandidates(swapCandidate.Value, best.Value) < 0))
            {
                best = swapCandidate;
            }

            RepairCandidate? reassignCandidate = TryBuildReassignCandidate(board, water, left, right, changing, targetType);
            if (reassignCandidate.HasValue && (!best.HasValue || CompareCandidates(reassignCandidate.Value, best.Value) < 0))
            {
                best = reassignCandidate;
            }
        }

        private static RepairCandidate? TryBuildSwapCandidate(
            Board board,
            WaterState water,
            ImmutableArray<EligibleDebris> eligible,
            EligibleDebris left,
            EligibleDebris right,
            EligibleDebris changing,
            DebrisType targetType)
        {
            for (int i = 0; i < eligible.Length; i++)
            {
                EligibleDebris donor = eligible[i];
                if (donor.Coord == left.Coord || donor.Coord == right.Coord || donor.Type != targetType)
                {
                    continue;
                }

                Board repaired = BoardHelpers.SetTile(board, changing.Coord, new DebrisTile(targetType));
                repaired = BoardHelpers.SetTile(repaired, donor.Coord, new DebrisTile(changing.Type));
                ImmutableArray<DebrisTypeChange> changes = ImmutableArray.Create(
                    new DebrisTypeChange(changing.Coord, changing.Type, targetType),
                    new DebrisTypeChange(donor.Coord, donor.Type, changing.Type));

                RepairCandidate? candidate = BuildValidCandidate(
                    repaired,
                    water,
                    left.Coord,
                    changes,
                    PreservesCounts: true,
                    DonorIndex: StableIndex(board, donor.Coord));
                if (candidate.HasValue)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static RepairCandidate? TryBuildReassignCandidate(
            Board board,
            WaterState water,
            EligibleDebris left,
            EligibleDebris right,
            EligibleDebris changing,
            DebrisType targetType)
        {
            Board repaired = BoardHelpers.SetTile(board, changing.Coord, new DebrisTile(targetType));
            ImmutableArray<DebrisTypeChange> changes = ImmutableArray.Create(
                new DebrisTypeChange(changing.Coord, changing.Type, targetType));

            return BuildValidCandidate(
                repaired,
                water,
                left.Coord,
                changes,
                PreservesCounts: false,
                DonorIndex: int.MaxValue);
        }

        private static RepairCandidate? BuildValidCandidate(
            Board repaired,
            WaterState water,
            TileCoord repairedPairCoord,
            ImmutableArray<DebrisTypeChange> changes,
            bool PreservesCounts,
            int DonorIndex)
        {
            if (!GroupOps.HasValidGroup(repaired, water) || HasGroupLargerThan(repaired, water, 5))
            {
                return null;
            }

            ImmutableArray<TileCoord>? repairedGroup = GroupOps.FindGroup(repaired, repairedPairCoord);
            int repairedGroupSize = repairedGroup?.Length ?? 0;
            if (repairedGroupSize < 2 || repairedGroupSize > 5)
            {
                return null;
            }

            return new RepairCandidate(
                repaired,
                changes,
                repairedGroupSize,
                PreservesCounts,
                DonorIndex);
        }

        private static ImmutableArray<EligibleDebris> CollectEligibleDebris(Board board, WaterState water)
        {
            ImmutableArray<EligibleDebris>.Builder eligible = ImmutableArray.CreateBuilder<EligibleDebris>();
            for (int row = 0; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (!IsDry(board, water, coord) || BoardHelpers.GetTile(board, coord) is not DebrisTile debris)
                    {
                        continue;
                    }

                    eligible.Add(new EligibleDebris(coord, debris.Type));
                }
            }

            return eligible.ToImmutable();
        }

        private static bool HasGroupLargerThan(Board board, WaterState water, int maxSize)
        {
            HashSet<TileCoord> visited = new HashSet<TileCoord>();
            for (int row = 0; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (visited.Contains(coord) || !IsDry(board, water, coord))
                    {
                        continue;
                    }

                    ImmutableArray<TileCoord>? group = GroupOps.FindGroup(board, coord);
                    if (!group.HasValue)
                    {
                        visited.Add(coord);
                        continue;
                    }

                    for (int i = 0; i < group.Value.Length; i++)
                    {
                        visited.Add(group.Value[i]);
                    }

                    if (group.Value.Length > maxSize)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int CompareCandidates(RepairCandidate left, RepairCandidate right)
        {
            int leftPairChanged = CountPairChanges(left.Changes);
            int rightPairChanged = CountPairChanges(right.Changes);
            int pairChangeComparison = leftPairChanged.CompareTo(rightPairChanged);
            if (pairChangeComparison != 0)
            {
                return pairChangeComparison;
            }

            int exactTripleComparison = IsExactTriple(left).CompareTo(IsExactTriple(right));
            if (exactTripleComparison != 0)
            {
                return exactTripleComparison;
            }

            int countPreservationComparison = right.PreservesCounts.CompareTo(left.PreservesCounts);
            if (countPreservationComparison != 0)
            {
                return countPreservationComparison;
            }

            int groupSizeComparison = left.RepairedGroupSize.CompareTo(right.RepairedGroupSize);
            if (groupSizeComparison != 0)
            {
                return groupSizeComparison;
            }

            int changeCountComparison = left.Changes.Length.CompareTo(right.Changes.Length);
            if (changeCountComparison != 0)
            {
                return changeCountComparison;
            }

            int firstCoordComparison = StableIndex(left.Board, left.Changes[0].Coord)
                .CompareTo(StableIndex(right.Board, right.Changes[0].Coord));
            if (firstCoordComparison != 0)
            {
                return firstCoordComparison;
            }

            return left.DonorIndex.CompareTo(right.DonorIndex);
        }

        private static int CountPairChanges(ImmutableArray<DebrisTypeChange> changes)
        {
            return changes.Length > 0 ? 1 : 0;
        }

        private static int IsExactTriple(RepairCandidate candidate)
        {
            return candidate.RepairedGroupSize == 3 ? 1 : 0;
        }

        private static bool AreOrthogonallyAdjacent(TileCoord left, TileCoord right)
        {
            int rowDistance = left.Row > right.Row ? left.Row - right.Row : right.Row - left.Row;
            int colDistance = left.Col > right.Col ? left.Col - right.Col : right.Col - left.Col;
            return rowDistance + colDistance == 1;
        }

        private static bool IsDry(Board board, WaterState water, TileCoord coord)
        {
            if (BoardHelpers.GetTile(board, coord) is FloodedTile)
            {
                return false;
            }

            int floodStartRow = board.Height - water.FloodedRows;
            return water.FloodedRows <= 0 || coord.Row < floodStartRow;
        }

        private static int StableIndex(Board board, TileCoord coord)
        {
            return (coord.Row * board.Width) + coord.Col;
        }

        private static DeadboardRepairResult Skipped(Board board, DeadboardRepairSkippedReason reason)
        {
            return new DeadboardRepairResult(
                board,
                Succeeded: false,
                DeadboardRepairReason.HardNoValidGroups,
                reason,
                ImmutableArray<DebrisTypeChange>.Empty);
        }

        private readonly record struct EligibleDebris(TileCoord Coord, DebrisType Type);

        private readonly record struct RepairCandidate(
            Board Board,
            ImmutableArray<DebrisTypeChange> Changes,
            int RepairedGroupSize,
            bool PreservesCounts,
            int DonorIndex);
    }
}
