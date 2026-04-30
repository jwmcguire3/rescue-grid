using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step07_Gravity
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            Board updatedBoard = state.Board;
            ImmutableDictionary<TileCoord, SpawnLineage> lineageByCoord = state.SpawnLineageByCoord;
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();
            int dryHeight = state.Board.Height - state.Water.FloodedRows;

            (updatedBoard, lineageByCoord) = CollapseVertically(updatedBoard, lineageByCoord, dryHeight, events);

            bool movedDiagonally;
            do
            {
                (updatedBoard, lineageByCoord, movedDiagonally) = SettleDiagonally(
                    updatedBoard,
                    lineageByCoord,
                    dryHeight,
                    events);

                if (movedDiagonally)
                {
                    (updatedBoard, lineageByCoord) = CollapseVertically(updatedBoard, lineageByCoord, dryHeight, events);
                }
            }
            while (movedDiagonally);

            return new StepResult(
                state with
                {
                    Board = updatedBoard,
                    SpawnLineageByCoord = lineageByCoord,
                },
                context,
                events.ToImmutable());
        }

        private static (Board Board, ImmutableDictionary<TileCoord, SpawnLineage> LineageByCoord) CollapseVertically(
            Board board,
            ImmutableDictionary<TileCoord, SpawnLineage> lineageByCoord,
            int dryHeight,
            ImmutableArray<ActionEvent>.Builder events)
        {
            Board updatedBoard = board;
            ImmutableDictionary<TileCoord, SpawnLineage> updatedLineage = lineageByCoord;

            for (int col = 0; col < updatedBoard.Width; col++)
            {
                int row = dryHeight - 1;
                while (row >= 0)
                {
                    if (IsGravityBarrier(BoardHelpers.GetTile(updatedBoard, new TileCoord(row, col))))
                    {
                        row--;
                        continue;
                    }

                    int segmentEnd = row;
                    while (row >= 0 && !IsGravityBarrier(BoardHelpers.GetTile(updatedBoard, new TileCoord(row, col))))
                    {
                        row--;
                    }

                    int segmentStart = row + 1;
                    (updatedBoard, updatedLineage) = CollapseSegment(
                        updatedBoard,
                        updatedLineage,
                        col,
                        segmentStart,
                        segmentEnd,
                        events);
                }
            }

            return (updatedBoard, updatedLineage);
        }

        private static (
            Board Board,
            ImmutableDictionary<TileCoord, SpawnLineage> LineageByCoord,
            bool Moved) SettleDiagonally(
                Board board,
                ImmutableDictionary<TileCoord, SpawnLineage> lineageByCoord,
                int dryHeight,
                ImmutableArray<ActionEvent>.Builder events)
        {
            Board updatedBoard = board;
            ImmutableDictionary<TileCoord, SpawnLineage> updatedLineage = lineageByCoord;
            bool moved = false;

            for (int row = dryHeight - 1; row >= 0; row--)
            {
                for (int col = 0; col < updatedBoard.Width; col++)
                {
                    TileCoord destination = new TileCoord(row, col);
                    if (!CanAcceptDiagonalSettling(updatedBoard, destination, dryHeight))
                    {
                        continue;
                    }

                    if (!IsStableLandingDestination(updatedBoard, destination, dryHeight))
                    {
                        continue;
                    }

                    if (CanBeFilledVertically(updatedBoard, destination))
                    {
                        continue;
                    }

                    if (!HasGravityBarrierAbove(updatedBoard, destination))
                    {
                        continue;
                    }

                    TileCoord? source = FindDiagonalSource(updatedBoard, destination);
                    if (!source.HasValue)
                    {
                        continue;
                    }

                    TileCoord sourceCoord = source.Value;
                    DebrisTile debris = (DebrisTile)BoardHelpers.GetTile(updatedBoard, sourceCoord);
                    updatedBoard = BoardHelpers.SetTile(updatedBoard, sourceCoord, new EmptyTile());
                    updatedBoard = BoardHelpers.SetTile(updatedBoard, destination, debris);
                    if (updatedLineage.TryGetValue(sourceCoord, out SpawnLineage lineage))
                    {
                        updatedLineage = updatedLineage.Remove(sourceCoord).SetItem(destination, lineage);
                    }

                    ImmutableArray<(TileCoord From, TileCoord To)> move = ImmutableArray.Create((sourceCoord, destination));
                    events.Add(new GravitySettled(move));
                    events.Add(new DiagonalSettlingApplied(move));
                    moved = true;
                }
            }

            return (updatedBoard, updatedLineage, moved);
        }

        private static (Board Board, ImmutableDictionary<TileCoord, SpawnLineage> LineageByCoord) CollapseSegment(
            Board board,
            ImmutableDictionary<TileCoord, SpawnLineage> lineageByCoord,
            int col,
            int segmentStart,
            int segmentEnd,
            ImmutableArray<ActionEvent>.Builder events)
        {
            Board updatedBoard = board;
            ImmutableDictionary<TileCoord, SpawnLineage> updatedLineage = lineageByCoord;
            int targetRow = FindNextFillableRow(updatedBoard, col, segmentEnd, segmentStart);

            for (int row = segmentEnd; row >= segmentStart; row--)
            {
                TileCoord sourceCoord = new TileCoord(row, col);
                if (BoardHelpers.GetTile(updatedBoard, sourceCoord) is not DebrisTile debris)
                {
                    continue;
                }

                TileCoord targetCoord = new TileCoord(targetRow, col);
                if (targetRow != row)
                {
                    updatedBoard = BoardHelpers.SetTile(updatedBoard, sourceCoord, new EmptyTile());
                    updatedBoard = BoardHelpers.SetTile(updatedBoard, targetCoord, debris);
                    if (updatedLineage.TryGetValue(sourceCoord, out SpawnLineage lineage))
                    {
                        updatedLineage = updatedLineage.Remove(sourceCoord).SetItem(targetCoord, lineage);
                    }

                    events.Add(new GravitySettled(ImmutableArray.Create((sourceCoord, targetCoord))));
                }

                targetRow = FindNextFillableRow(updatedBoard, col, targetRow - 1, segmentStart);
            }

            for (int row = targetRow; row >= segmentStart; row--)
            {
                TileCoord coord = new TileCoord(row, col);
                if (BoardHelpers.GetTile(updatedBoard, coord) is RescuePathTile)
                {
                    continue;
                }

                if (BoardHelpers.GetTile(updatedBoard, coord) is not EmptyTile)
                {
                    updatedBoard = BoardHelpers.SetTile(updatedBoard, coord, new EmptyTile());
                    updatedLineage = updatedLineage.Remove(coord);
                }
            }

            return (updatedBoard, updatedLineage);
        }

        private static int FindNextFillableRow(Board board, int col, int startRow, int segmentStart)
        {
            for (int row = startRow; row >= segmentStart; row--)
            {
                Tile tile = BoardHelpers.GetTile(board, new TileCoord(row, col));
                if (tile is not RescuePathTile)
                {
                    return row;
                }
            }

            return segmentStart - 1;
        }

        private static bool CanAcceptDiagonalSettling(Board board, TileCoord destination, int dryHeight)
        {
            return destination.Row >= 0
                && destination.Row < dryHeight
                && BoardHelpers.InBounds(board, destination)
                && BoardHelpers.GetTile(board, destination) is EmptyTile;
        }

        private static bool IsStableLandingDestination(Board board, TileCoord destination, int dryHeight)
        {
            for (int row = destination.Row + 1; row < dryHeight; row++)
            {
                Tile tile = BoardHelpers.GetTile(board, new TileCoord(row, destination.Col));
                if (tile is RescuePathTile)
                {
                    continue;
                }

                return tile is not EmptyTile;
            }

            return true;
        }

        private static bool CanBeFilledVertically(Board board, TileCoord destination)
        {
            for (int row = destination.Row - 1; row >= 0; row--)
            {
                Tile tile = BoardHelpers.GetTile(board, new TileCoord(row, destination.Col));
                if (IsGravityBarrier(tile))
                {
                    return false;
                }

                if (tile is DebrisTile)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasGravityBarrierAbove(Board board, TileCoord destination)
        {
            for (int row = destination.Row - 1; row >= 0; row--)
            {
                if (IsGravityBarrier(BoardHelpers.GetTile(board, new TileCoord(row, destination.Col))))
                {
                    return true;
                }
            }

            return false;
        }

        private static TileCoord? FindDiagonalSource(Board board, TileCoord destination)
        {
            TileCoord aboveLeft = new TileCoord(destination.Row - 1, destination.Col - 1);
            if (IsDiagonalSource(board, aboveLeft))
            {
                return aboveLeft;
            }

            TileCoord aboveRight = new TileCoord(destination.Row - 1, destination.Col + 1);
            if (IsDiagonalSource(board, aboveRight))
            {
                return aboveRight;
            }

            return null;
        }

        private static bool IsDiagonalSource(Board board, TileCoord coord)
        {
            return BoardHelpers.InBounds(board, coord)
                && BoardHelpers.GetTile(board, coord) is DebrisTile;
        }

        private static bool IsGravityBarrier(Tile tile)
        {
            return tile is FloodedTile or BlockerTile or TargetTile;
        }
    }
}
