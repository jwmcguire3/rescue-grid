using System;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.State;

namespace Rescue.Core.Tests.State
{
    public sealed class BoardHelpersTests
    {
        [Test]
        public void GetTileReturnsTileAtCoordinate()
        {
            Board board = CreateBoard(
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new DebrisTile(DebrisType.A)),
                ImmutableArray.Create<Tile>(
                    new FloodedTile(),
                    new TargetTile("target-1", Extracted: false)));

            Tile tile = BoardHelpers.GetTile(board, new TileCoord(1, 1));

            Assert.That(tile, Is.EqualTo(new TargetTile("target-1", false)));
        }

        [Test]
        public void SetTileReturnsUpdatedBoardWithoutChangingOriginal()
        {
            Board originalBoard = CreateBoard(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.B)),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile()));

            Board updatedBoard = BoardHelpers.SetTile(
                originalBoard,
                new TileCoord(0, 1),
                new BlockerTile(BlockerType.Crate, 1, Hidden: null));

            Assert.That(BoardHelpers.GetTile(updatedBoard, new TileCoord(0, 1)), Is.EqualTo(new BlockerTile(BlockerType.Crate, 1, null)));
            Assert.That(BoardHelpers.GetTile(originalBoard, new TileCoord(0, 1)), Is.EqualTo(new DebrisTile(DebrisType.B)));
        }

        [Test]
        public void OrthogonalNeighborsReturnsClippedNeighborsInReadingOrder()
        {
            Board board = CreateBoard(
                ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile(), new EmptyTile()));

            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, new TileCoord(0, 1));

            Assert.That(neighbors.Length, Is.EqualTo(3));
            Assert.That(neighbors[0], Is.EqualTo(new TileCoord(0, 2)));
            Assert.That(neighbors[1], Is.EqualTo(new TileCoord(1, 1)));
            Assert.That(neighbors[2], Is.EqualTo(new TileCoord(0, 0)));
        }

        [Test]
        public void FindAllReturnsMatchingCoordinates()
        {
            Board board = CreateBoard(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.B),
                    new BlockerTile(BlockerType.Vine, 1, null)),
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.B),
                    new TargetTile("target-2", Extracted: false),
                    new DebrisTile(DebrisType.B)));

            ImmutableArray<TileCoord> matches = BoardHelpers.FindAll(
                board,
                tile => tile is DebrisTile debris && debris.Type == DebrisType.B);

            Assert.That(matches.Length, Is.EqualTo(3));
            Assert.That(matches[0], Is.EqualTo(new TileCoord(0, 1)));
            Assert.That(matches[1], Is.EqualTo(new TileCoord(1, 0)));
            Assert.That(matches[2], Is.EqualTo(new TileCoord(1, 2)));
        }

        [Test]
        public void FindAllThrowsWhenPredicateIsNull()
        {
            Board board = CreateBoard(
                ImmutableArray.Create<Tile>(new EmptyTile()));
            var method = typeof(BoardHelpers).GetMethod(nameof(BoardHelpers.FindAll));

            Assert.That(method, Is.Not.Null);
            if (method is null)
            {
                return;
            }

            Assert.That(
                () => method.Invoke(null, new object?[] { board, null }),
                Throws.InnerException.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void InBoundsReturnsExpectedResult()
        {
            Board board = CreateBoard(
                ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile()),
                ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile()));

            Assert.That(BoardHelpers.InBounds(board, new TileCoord(1, 1)), Is.True);
            Assert.That(BoardHelpers.InBounds(board, new TileCoord(-1, 0)), Is.False);
            Assert.That(BoardHelpers.InBounds(board, new TileCoord(0, 2)), Is.False);
        }

        private static Board CreateBoard(params ImmutableArray<Tile>[] rows)
        {
            return new Board(rows[0].Length, rows.Length, rows.ToImmutableArray());
        }
    }
}
