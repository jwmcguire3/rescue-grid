using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step07_Gravity
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            Board updatedBoard = state.Board;
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();
            int dryHeight = state.Board.Height - state.Water.FloodedRows;

            for (int col = 0; col < state.Board.Width; col++)
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
                    updatedBoard = CollapseSegment(updatedBoard, col, segmentStart, segmentEnd, events);
                }
            }

            return new StepResult(
                state with { Board = updatedBoard },
                context,
                events.ToImmutable());
        }

        private static Board CollapseSegment(
            Board board,
            int col,
            int segmentStart,
            int segmentEnd,
            ImmutableArray<ActionEvent>.Builder events)
        {
            Board updatedBoard = board;
            int targetRow = segmentEnd;

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
                    events.Add(new GravitySettled(ImmutableArray.Create((sourceCoord, targetCoord))));
                }

                targetRow--;
            }

            for (int row = targetRow; row >= segmentStart; row--)
            {
                TileCoord coord = new TileCoord(row, col);
                if (BoardHelpers.GetTile(updatedBoard, coord) is not EmptyTile)
                {
                    updatedBoard = BoardHelpers.SetTile(updatedBoard, coord, new EmptyTile());
                }
            }

            return updatedBoard;
        }

        private static bool IsGravityBarrier(Tile tile)
        {
            return tile is FloodedTile or BlockerTile or TargetTile;
        }
    }
}
