using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Rules;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Rules
{
    public sealed class GroupsTests
    {
        [Test]
        public void FindGroupReturnsNullForSingleTile()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B));

            ImmutableArray<TileCoord>? group = GroupOps.FindGroup(board, new TileCoord(0, 0));

            Assert.That(group, Is.Null);
        }

        [Test]
        public void FindGroupReturnsBothTilesForHorizontalPair()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A));

            ImmutableArray<TileCoord>? group = GroupOps.FindGroup(board, new TileCoord(0, 0));

            Assert.That(group.HasValue, Is.True);
            ImmutableArray<TileCoord> resolvedGroup = group ?? ImmutableArray<TileCoord>.Empty;
            Assert.That(resolvedGroup, Is.EquivalentTo(new[]
            {
                new TileCoord(0, 0),
                new TileCoord(0, 1),
            }));
        }

        [Test]
        public void FindGroupReturnsAllTilesForLShape()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A)),
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.B)));

            ImmutableArray<TileCoord>? group = GroupOps.FindGroup(board, new TileCoord(0, 0));

            Assert.That(group.HasValue, Is.True);
            ImmutableArray<TileCoord> resolvedGroup = group ?? ImmutableArray<TileCoord>.Empty;
            Assert.That(resolvedGroup, Is.EquivalentTo(new[]
            {
                new TileCoord(0, 0),
                new TileCoord(0, 1),
                new TileCoord(1, 0),
            }));
        }

        [Test]
        public void FindGroupDoesNotUseDiagonalAdjacency()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new DebrisTile(DebrisType.A)));

            ImmutableArray<TileCoord>? group = GroupOps.FindGroup(board, new TileCoord(0, 0));

            Assert.That(group, Is.Null);
        }

        [Test]
        public void FindGroupStopsAtDifferentDebrisTypeBoundary()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B, DebrisType.A));

            ImmutableArray<TileCoord>? group = GroupOps.FindGroup(board, new TileCoord(0, 0));

            Assert.That(group.HasValue, Is.True);
            ImmutableArray<TileCoord> resolvedGroup = group ?? ImmutableArray<TileCoord>.Empty;
            Assert.That(resolvedGroup, Is.EquivalentTo(new[]
            {
                new TileCoord(0, 0),
                new TileCoord(0, 1),
            }));
        }

        [Test]
        public void FindGroupDoesNotIncludeTileHiddenUnderIce()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.A))));

            ImmutableArray<TileCoord>? group = GroupOps.FindGroup(board, new TileCoord(0, 0));

            Assert.That(group, Is.Null);
        }

        [Test]
        public void FindGroupReturnsNullForFloodedTile()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new FloodedTile()));

            ImmutableArray<TileCoord>? group = GroupOps.FindGroup(board, new TileCoord(0, 1));

            Assert.That(group, Is.Null);
        }

        [Test]
        public void HasValidGroupReturnsTrueWhenDryTappableDebrisPairExists()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A));

            bool hasValidGroup = GroupOps.HasValidGroup(
                board,
                new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3));

            Assert.That(hasValidGroup, Is.True);
        }

        [Test]
        public void HasValidGroupReturnsFalseForSingleTilesOnly()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B));

            bool hasValidGroup = GroupOps.HasValidGroup(
                board,
                new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3));

            Assert.That(hasValidGroup, Is.False);
        }

        [Test]
        public void HasValidGroupIgnoresGroupsInFloodedRowsFromWaterState()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.B, DebrisType.C),
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A));

            bool hasValidGroup = GroupOps.HasValidGroup(
                board,
                new WaterState(FloodedRows: 1, ActionsUntilRise: 3, RiseInterval: 3));

            Assert.That(hasValidGroup, Is.False);
        }

        [Test]
        public void HasValidGroupDoesNotJoinDryDebrisToFloodedRowsFromWaterState()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A)),
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A)));

            bool hasValidGroup = GroupOps.HasValidGroup(
                board,
                new WaterState(FloodedRows: 1, ActionsUntilRise: 3, RiseInterval: 3));

            Assert.That(hasValidGroup, Is.False);
        }

        [Test]
        public void HasValidGroupDoesNotJoinDryDebrisToFloodedTile()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A)),
                ImmutableArray.Create<Tile>(new FloodedTile()));

            bool hasValidGroup = GroupOps.HasValidGroup(
                board,
                new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3));

            Assert.That(hasValidGroup, Is.False);
        }

        [Test]
        public void HasValidGroupFindsDryGroupsAboveFloodedRows()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A),
                PipelineTestFixtures.DebrisRow(DebrisType.B, DebrisType.B));

            bool hasValidGroup = GroupOps.HasValidGroup(
                board,
                new WaterState(FloodedRows: 1, ActionsUntilRise: 3, RiseInterval: 3));

            Assert.That(hasValidGroup, Is.True);
        }

        [Test]
        public void HasValidGroupIgnoresBlockedTargetRescuePathAndHiddenIceContents()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                ImmutableArray.Create<Tile>(
                    new BlockerTile(BlockerType.Crate, 1, Hidden: null),
                    new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.A))),
                ImmutableArray.Create<Tile>(
                    new TargetTile("target", Extracted: false),
                    new RescuePathTile(ImmutableArray.Create("target"))));

            bool hasValidGroup = GroupOps.HasValidGroup(
                board,
                new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3));

            Assert.That(hasValidGroup, Is.False);
        }
    }
}
