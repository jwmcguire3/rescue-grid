using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Content
{
    internal static class ContentTileParser
    {
        public static bool TryParseCell(string code, out ContentCellInfo cell)
        {
            if (code == ".")
            {
                cell = new ContentCellInfo(ContentCellKind.Empty, null, null, null, Hp: 0);
                return true;
            }

            if (TryParseDebris(code, out DebrisType debrisType))
            {
                cell = new ContentCellInfo(ContentCellKind.Debris, debrisType, null, null, Hp: 0);
                return true;
            }

            if (code == "CR")
            {
                cell = new ContentCellInfo(ContentCellKind.Crate, null, null, null, Hp: 1);
                return true;
            }

            if (code == "CX")
            {
                cell = new ContentCellInfo(ContentCellKind.Crate, null, null, null, Hp: 2);
                return true;
            }

            if (code == "V")
            {
                cell = new ContentCellInfo(ContentCellKind.Vine, null, null, null, Hp: 1);
                return true;
            }

            if (code.Length == 2 && code[0] == 'I' && TryParseDebris(code[1].ToString(), out DebrisType hiddenType))
            {
                cell = new ContentCellInfo(ContentCellKind.Ice, null, hiddenType, null, Hp: 1);
                return true;
            }

            if (code.Length >= 2 && code[0] == 'T')
            {
                cell = new ContentCellInfo(ContentCellKind.Target, null, null, code[1..], Hp: 0);
                return true;
            }

            cell = default;
            return false;
        }

        public static bool TryParseDebris(string code, out DebrisType debrisType)
        {
            debrisType = default;
            return code switch
            {
                "A" => Assign(DebrisType.A, out debrisType),
                "B" => Assign(DebrisType.B, out debrisType),
                "C" => Assign(DebrisType.C, out debrisType),
                "D" => Assign(DebrisType.D, out debrisType),
                "E" => Assign(DebrisType.E, out debrisType),
                "F" => Assign(DebrisType.F, out debrisType),
                _ => false,
            };
        }

        public static bool IsInBounds(int height, int width, int row, int col)
        {
            return row >= 0 && row < height && col >= 0 && col < width;
        }

        public static ImmutableArray<ContentCellCoord> GetRequiredNeighbors(int height, int width, ContentCellCoord coord)
        {
            ImmutableArray<ContentCellCoord>.Builder neighbors = ImmutableArray.CreateBuilder<ContentCellCoord>(4);
            TryAdd(height, width, coord.Row - 1, coord.Col, neighbors);
            TryAdd(height, width, coord.Row, coord.Col + 1, neighbors);
            TryAdd(height, width, coord.Row + 1, coord.Col, neighbors);
            TryAdd(height, width, coord.Row, coord.Col - 1, neighbors);
            return neighbors.ToImmutable();
        }

        public static bool IsFloodedRow(LevelJson level, int row)
        {
            return row >= level.Board.Height - level.InitialFloodedRows;
        }

        public static bool IsBlocker(ContentCellInfo cell)
        {
            return cell.Kind is ContentCellKind.Crate or ContentCellKind.Ice or ContentCellKind.Vine;
        }

        private static bool Assign(DebrisType debrisTypeValue, out DebrisType debrisType)
        {
            debrisType = debrisTypeValue;
            return true;
        }

        private static void TryAdd(
            int height,
            int width,
            int row,
            int col,
            ImmutableArray<ContentCellCoord>.Builder neighbors)
        {
            if (IsInBounds(height, width, row, col))
            {
                neighbors.Add(new ContentCellCoord(row, col));
            }
        }
    }

    internal readonly record struct ContentCellCoord(int Row, int Col);

    internal enum ContentCellKind
    {
        Empty,
        Debris,
        Crate,
        Ice,
        Vine,
        Target,
    }

    internal readonly record struct ContentCellInfo(
        ContentCellKind Kind,
        DebrisType? DebrisType,
        DebrisType? HiddenDebrisType,
        string? TargetId,
        int Hp);
}
