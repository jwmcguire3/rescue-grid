using System;
using System.Text;

namespace Rescue.Content
{
    public static class AsciiPreview
    {
        // Each tile code is padded to this width so columns align.
        // Phase 1 max code length is 2 (CR, CX, IA-IE, T0-T9).
        private const int CellWidth = 2;

        public static string Render(LevelJson level)
        {
            if (level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            var sb = new StringBuilder();

            string waterDesc = level.Water.RiseInterval == 0 ? "off" : level.Water.RiseInterval.ToString();
            sb.AppendLine($"{level.Id} \u2014 {level.Name}  [{level.Board.Width}\u00d7{level.Board.Height}]  water:{waterDesc}  flooded:{level.InitialFloodedRows}");

            int floodStart = level.Board.Height - level.InitialFloodedRows;

            for (int row = 0; row < level.Board.Height; row++)
            {
                if (row >= floodStart)
                {
                    AppendFloodedRow(sb, level.Board.Width);
                }
                else
                {
                    AppendTileRow(sb, level.Board.Tiles[row]);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static void AppendTileRow(StringBuilder sb, string[] row)
        {
            for (int col = 0; col < row.Length; col++)
            {
                if (col > 0)
                {
                    sb.Append(' ');
                }

                AppendPadded(sb, row[col]);
            }
        }

        private static void AppendFloodedRow(StringBuilder sb, int width)
        {
            for (int col = 0; col < width; col++)
            {
                if (col > 0)
                {
                    sb.Append(' ');
                }

                AppendPadded(sb, "~");
            }
        }

        private static void AppendPadded(StringBuilder sb, string code)
        {
            sb.Append(code);
            for (int i = code.Length; i < CellWidth; i++)
            {
                sb.Append(' ');
            }
        }
    }
}
