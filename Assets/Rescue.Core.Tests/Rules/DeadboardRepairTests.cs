using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Rules;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Rules
{
    public sealed class DeadboardRepairTests
    {
        private static readonly WaterState DryWater = new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3);

        [Test]
        public void HardNoValidGroupStateGetsRepairedToAtLeastOneLegalGroup()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B),
                PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D));

            DeadboardRepairResult result = DeadboardRepairOps.RepairHardNoValidGroups(board, DryWater);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(GroupOps.HasValidGroup(result.Board, DryWater), Is.True);
            Assert.That(result.Changes, Is.Not.Empty);
        }

        [Test]
        public void RepairChangesOnlyDebrisTypesAndDoesNotMovePieces()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B),
                PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D));

            DeadboardRepairResult result = DeadboardRepairOps.RepairHardNoValidGroups(board, DryWater);

            Assert.That(result.Succeeded, Is.True);
            AssertBoardShapeAndNonDebrisTilesUnchanged(board, result.Board);
            AssertDebrisCoordinatesEqual(board, result.Board);
        }

        [Test]
        public void RepairPreservesDebrisTypeCountsWhenASwapIsAvailable()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B),
                PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.A));

            DeadboardRepairResult result = DeadboardRepairOps.RepairHardNoValidGroups(board, DryWater);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(CountDebrisTypes(result.Board), Is.EqualTo(CountDebrisTypes(board)));
            Assert.That(result.Changes.Length, Is.EqualTo(2));
        }

        [Test]
        public void RepairDoesNotAlterTargetsBlockersVinesFloodedRowsRescuePathOrHiddenIce()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.Row(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.B),
                    new TargetTile("target", Extracted: false),
                    new RescuePathTile(ImmutableArray.Create("target"))),
                PipelineTestFixtures.Row(
                    new BlockerTile(BlockerType.Crate, 1, null),
                    new BlockerTile(BlockerType.Vine, 1, null),
                    new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.A)),
                    new FloodedTile()),
                PipelineTestFixtures.Row(
                    new DebrisTile(DebrisType.C),
                    new DebrisTile(DebrisType.D),
                    new DebrisTile(DebrisType.E),
                    new DebrisTile(DebrisType.F)));

            DeadboardRepairResult result = DeadboardRepairOps.RepairHardNoValidGroups(board, DryWater);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(BoardHelpers.GetTile(result.Board, new TileCoord(0, 2)), Is.EqualTo(BoardHelpers.GetTile(board, new TileCoord(0, 2))));
            Assert.That(BoardHelpers.GetTile(result.Board, new TileCoord(0, 3)), Is.EqualTo(BoardHelpers.GetTile(board, new TileCoord(0, 3))));
            Assert.That(BoardHelpers.GetTile(result.Board, new TileCoord(1, 0)), Is.EqualTo(BoardHelpers.GetTile(board, new TileCoord(1, 0))));
            Assert.That(BoardHelpers.GetTile(result.Board, new TileCoord(1, 1)), Is.EqualTo(BoardHelpers.GetTile(board, new TileCoord(1, 1))));
            Assert.That(BoardHelpers.GetTile(result.Board, new TileCoord(1, 2)), Is.EqualTo(BoardHelpers.GetTile(board, new TileCoord(1, 2))));
            Assert.That(BoardHelpers.GetTile(result.Board, new TileCoord(1, 3)), Is.EqualTo(BoardHelpers.GetTile(board, new TileCoord(1, 3))));
        }

        [Test]
        public void RepairUsesOnlyDryActiveCellsAndIgnoresFloodedRows()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B),
                PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D),
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A));
            WaterState water = new WaterState(FloodedRows: 1, ActionsUntilRise: 3, RiseInterval: 3);

            DeadboardRepairResult result = DeadboardRepairOps.RepairHardNoValidGroups(board, water);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(BoardHelpers.GetTile(result.Board, new TileCoord(2, 0)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.Board, new TileCoord(2, 1)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            for (int i = 0; i < result.Changes.Length; i++)
            {
                Assert.That(result.Changes[i].Coord.Row, Is.LessThan(2));
            }
        }

        [Test]
        public void RepairAvoidsExactTripleWhenPairOnlyRepairIsPossible()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B, DebrisType.D),
                PipelineTestFixtures.DebrisRow(DebrisType.E, DebrisType.A, DebrisType.C));

            DeadboardRepairResult result = DeadboardRepairOps.RepairHardNoValidGroups(board, DryWater);
            ImmutableArray<TileCoord>? group = GroupOps.FindGroup(result.Board, result.Changes[0].Coord);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(group.HasValue, Is.True);
            ImmutableArray<TileCoord> resolvedGroup = group ?? ImmutableArray<TileCoord>.Empty;
            Assert.That(resolvedGroup.Length, Is.EqualTo(2));
        }

        [Test]
        public void RepairDoesNotCreateGroupLargerThanFive()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B, DebrisType.C),
                PipelineTestFixtures.DebrisRow(DebrisType.D, DebrisType.E, DebrisType.F),
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B, DebrisType.C));

            DeadboardRepairResult result = DeadboardRepairOps.RepairHardNoValidGroups(board, DryWater);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(MaxGroupSize(result.Board), Is.LessThanOrEqualTo(5));
        }

        [Test]
        public void RepairIsDeterministic()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B, DebrisType.C),
                PipelineTestFixtures.DebrisRow(DebrisType.D, DebrisType.E, DebrisType.F),
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.C, DebrisType.E));

            DeadboardRepairResult first = DeadboardRepairOps.RepairHardNoValidGroups(board, DryWater);
            DeadboardRepairResult second = DeadboardRepairOps.RepairHardNoValidGroups(board, DryWater);

            AssertBoardsEqual(first.Board, second.Board);
            Assert.That(second.Succeeded, Is.EqualTo(first.Succeeded));
            Assert.That(second.Reason, Is.EqualTo(first.Reason));
            Assert.That(second.SkippedReason, Is.EqualTo(first.SkippedReason));
            AssertDebrisTypeChangesEqual(first.Changes, second.Changes);
        }

        [Test]
        public void NoRepairWhenAValidMoveAlreadyExists()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A),
                PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D));

            DeadboardRepairResult result = DeadboardRepairOps.RepairHardNoValidGroups(board, DryWater);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.SkippedReason, Is.EqualTo(DeadboardRepairSkippedReason.ExistingValidGroup));
            Assert.That(result.Board, Is.EqualTo(board));
        }

        [Test]
        public void NoRepairWhenImpossibleDueToInsufficientEligibleCells()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.Row(new DebrisTile(DebrisType.A), new FloodedTile()));

            DeadboardRepairResult result = DeadboardRepairOps.RepairHardNoValidGroups(board, DryWater);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.SkippedReason, Is.EqualTo(DeadboardRepairSkippedReason.InsufficientEligibleCells));
            Assert.That(result.Board, Is.EqualTo(board));
        }

        private static void AssertBoardShapeAndNonDebrisTilesUnchanged(Board expected, Board actual)
        {
            Assert.That(actual.Width, Is.EqualTo(expected.Width));
            Assert.That(actual.Height, Is.EqualTo(expected.Height));

            for (int row = 0; row < expected.Height; row++)
            {
                for (int col = 0; col < expected.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    Tile expectedTile = BoardHelpers.GetTile(expected, coord);
                    Tile actualTile = BoardHelpers.GetTile(actual, coord);
                    if (expectedTile is DebrisTile)
                    {
                        Assert.That(actualTile, Is.TypeOf<DebrisTile>(), $"Expected debris at ({row}, {col}).");
                    }
                    else
                    {
                        Assert.That(actualTile, Is.EqualTo(expectedTile), $"Protected tile changed at ({row}, {col}).");
                    }
                }
            }
        }

        private static void AssertBoardsEqual(Board expected, Board actual)
        {
            Assert.That(actual.Width, Is.EqualTo(expected.Width));
            Assert.That(actual.Height, Is.EqualTo(expected.Height));
            for (int row = 0; row < expected.Height; row++)
            {
                for (int col = 0; col < expected.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    Assert.That(
                        BoardHelpers.GetTile(actual, coord),
                        Is.EqualTo(BoardHelpers.GetTile(expected, coord)),
                        $"Tile mismatch at ({row}, {col}).");
                }
            }
        }

        private static void AssertDebrisTypeChangesEqual(
            ImmutableArray<DebrisTypeChange> expected,
            ImmutableArray<DebrisTypeChange> actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"Change mismatch at {i}.");
            }
        }

        private static void AssertDebrisCoordinatesEqual(Board expected, Board actual)
        {
            for (int row = 0; row < expected.Height; row++)
            {
                for (int col = 0; col < expected.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    Assert.That(
                        BoardHelpers.GetTile(actual, coord) is DebrisTile,
                        Is.EqualTo(BoardHelpers.GetTile(expected, coord) is DebrisTile),
                        $"Debris occupancy changed at ({row}, {col}).");
                }
            }
        }

        private static int[] CountDebrisTypes(Board board)
        {
            int[] counts = new int[6];
            for (int row = 0; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    if (BoardHelpers.GetTile(board, new TileCoord(row, col)) is DebrisTile debris)
                    {
                        counts[(int)debris.Type]++;
                    }
                }
            }

            return counts;
        }

        private static int MaxGroupSize(Board board)
        {
            HashSet<TileCoord> seen = new HashSet<TileCoord>();
            int max = 0;
            for (int row = 0; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (seen.Contains(coord))
                    {
                        continue;
                    }

                    ImmutableArray<TileCoord>? group = GroupOps.FindGroup(board, coord);
                    if (!group.HasValue)
                    {
                        seen.Add(coord);
                        continue;
                    }

                    for (int i = 0; i < group.Value.Length; i++)
                    {
                        seen.Add(group.Value[i]);
                    }

                    if (group.Value.Length > max)
                    {
                        max = group.Value.Length;
                    }
                }
            }

            return max;
        }
    }
}
