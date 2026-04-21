using System;
using System.Collections.Immutable;

namespace Rescue.Core.State
{
    public static class BoardHelpers
    {
        public static Tile GetTile(Board board, TileCoord coord)
        {
            return board.Tiles[coord.Row][coord.Col];
        }

        public static Board SetTile(Board board, TileCoord coord, Tile tile)
        {
            ImmutableArray<Tile> row = board.Tiles[coord.Row];
            ImmutableArray<Tile> updatedRow = row.SetItem(coord.Col, tile);
            ImmutableArray<ImmutableArray<Tile>> updatedTiles = board.Tiles.SetItem(coord.Row, updatedRow);
            return board with { Tiles = updatedTiles };
        }

        public static ImmutableArray<TileCoord> OrthogonalNeighbors(Board board, TileCoord coord)
        {
            ImmutableArray<TileCoord>.Builder neighbors = ImmutableArray.CreateBuilder<TileCoord>(4);
            TryAddNeighbor(board, neighbors, coord.Row - 1, coord.Col);
            TryAddNeighbor(board, neighbors, coord.Row, coord.Col + 1);
            TryAddNeighbor(board, neighbors, coord.Row + 1, coord.Col);
            TryAddNeighbor(board, neighbors, coord.Row, coord.Col - 1);
            return neighbors.ToImmutable();
        }

        public static ImmutableArray<TileCoord> FindAll(Board board, Func<Tile, bool> predicate)
        {
            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            ImmutableArray<TileCoord>.Builder matches = ImmutableArray.CreateBuilder<TileCoord>();
            for (int row = 0; row < board.Height; row++)
            {
                ImmutableArray<Tile> currentRow = board.Tiles[row];
                for (int col = 0; col < board.Width; col++)
                {
                    if (predicate(currentRow[col]))
                    {
                        matches.Add(new TileCoord(row, col));
                    }
                }
            }

            return matches.ToImmutable();
        }

        public static bool InBounds(Board board, TileCoord coord)
        {
            return coord.Row >= 0
                && coord.Row < board.Height
                && coord.Col >= 0
                && coord.Col < board.Width;
        }

        private static void TryAddNeighbor(Board board, ImmutableArray<TileCoord>.Builder neighbors, int row, int col)
        {
            TileCoord coord = new TileCoord(row, col);
            if (InBounds(board, coord))
            {
                neighbors.Add(coord);
            }
        }
    }
}
