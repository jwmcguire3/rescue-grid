using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text;

namespace Rescue.Content
{
    public static class LevelSvgPreview
    {
        private const int CellSize = 48;
        private const int HeaderHeight = 86;
        private const int Margin = 18;

        public static string Render(LevelJson level)
        {
            if (level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            int width = (Margin * 2) + (level.Board.Width * CellSize);
            int height = HeaderHeight + Margin + (level.Board.Height * CellSize);
            int forecastRow = level.Board.Height - level.InitialFloodedRows - 1;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            sb.AppendLine(
                $@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""{width}"" height=""{height}"" viewBox=""0 0 {width} {height}"" role=""img"" aria-label=""{EscapeAttribute(level.Id)} level preview"">");
            AppendStyles(sb);
            AppendHeader(sb, level);
            AppendBoard(sb, level, forecastRow);
            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static void AppendStyles(StringBuilder sb)
        {
            sb.AppendLine("<style>");
            sb.AppendLine("  .background { fill: #f7f3ea; }");
            sb.AppendLine("  .header-title { fill: #1f2933; font: 700 18px Arial, sans-serif; }");
            sb.AppendLine("  .header-meta { fill: #52606d; font: 12px Arial, sans-serif; }");
            sb.AppendLine("  .cell { stroke: #9aa5b1; stroke-width: 1; rx: 4; ry: 4; }");
            sb.AppendLine("  .cell-empty { fill: #ffffff; }");
            sb.AppendLine("  .cell-debris { fill: #eef2f7; }");
            sb.AppendLine("  .cell-crate { fill: #c68b59; }");
            sb.AppendLine("  .cell-crate-reinforced { fill: #9f5f33; }");
            sb.AppendLine("  .cell-ice { fill: #c8ebff; }");
            sb.AppendLine("  .cell-vine { fill: #b8d98b; }");
            sb.AppendLine("  .cell-target { fill: #ffd7d2; stroke: #d64545; stroke-width: 2; }");
            sb.AppendLine("  .tile-code { fill: #1f2933; font: 700 14px Arial, sans-serif; text-anchor: middle; dominant-baseline: central; }");
            sb.AppendLine("  .target-label { fill: #8a1c1c; }");
            sb.AppendLine("  .flooded-row-marker { fill: #2f80ed; fill-opacity: 0.25; stroke: #1f6fd1; stroke-width: 2; }");
            sb.AppendLine("  .forecast-row-marker { fill: none; stroke: #2f80ed; stroke-width: 3; stroke-dasharray: 8 5; }");
            sb.AppendLine("  .target-neighbor-outline { fill: none; stroke: #f59e0b; stroke-width: 3; stroke-dasharray: 4 4; pointer-events: none; }");
            sb.AppendLine("</style>");
        }

        private static void AppendHeader(StringBuilder sb, LevelJson level)
        {
            string water = level.Water.RiseInterval == 0 ? "off" : level.Water.RiseInterval.ToString(CultureInfo.InvariantCulture);
            string title = $"{level.Id} - {level.Name}";
            string meta = $"board {level.Board.Width}x{level.Board.Height} | water interval {water} | flooded rows {level.InitialFloodedRows} | dock jam {level.Dock.JamEnabled} | assistance {level.Assistance.Chance.ToString("0.###", CultureInfo.InvariantCulture)}";

            sb.AppendLine($@"<rect class=""background"" x=""0"" y=""0"" width=""100%"" height=""100%"" />");
            sb.AppendLine($@"<text class=""header-title"" x=""{Margin}"" y=""30"">{EscapeText(title)}</text>");
            sb.AppendLine($@"<text class=""header-meta"" x=""{Margin}"" y=""55"">{EscapeText(meta)}</text>");
        }

        private static void AppendBoard(StringBuilder sb, LevelJson level, int forecastRow)
        {
            for (int row = 0; row < level.Board.Height; row++)
            {
                bool flooded = ContentTileParser.IsFloodedRow(level, row);
                bool forecast = level.Water.RiseInterval > 0 && row == forecastRow;

                for (int col = 0; col < level.Board.Width; col++)
                {
                    string code = level.Board.Tiles[row][col];
                    if (!ContentTileParser.TryParseCell(code, out ContentCellInfo cell))
                    {
                        cell = default;
                    }

                    int x = Margin + (col * CellSize);
                    int y = HeaderHeight + (row * CellSize);
                    string cellClass = "cell " + CellClass(cell);
                    sb.AppendLine($@"<rect class=""{cellClass}"" data-row=""{row}"" data-col=""{col}"" x=""{x}"" y=""{y}"" width=""{CellSize}"" height=""{CellSize}"" />");
                    sb.AppendLine($@"<text class=""tile-code{TargetTextClass(cell)}"" x=""{x + (CellSize / 2)}"" y=""{y + (CellSize / 2)}"">{EscapeText(code)}</text>");
                }

                if (forecast)
                {
                    AppendRowMarker(sb, "forecast-row-marker", row, level.Board.Width);
                }

                if (flooded)
                {
                    AppendRowMarker(sb, "flooded-row-marker", row, level.Board.Width);
                }
            }

            AppendTargetNeighborOutlines(sb, level);
        }

        private static void AppendRowMarker(StringBuilder sb, string className, int row, int boardWidth)
        {
            int x = Margin;
            int y = HeaderHeight + (row * CellSize);
            sb.AppendLine($@"<rect class=""{className}"" data-row=""{row}"" x=""{x}"" y=""{y}"" width=""{boardWidth * CellSize}"" height=""{CellSize}"" />");
        }

        private static void AppendTargetNeighborOutlines(StringBuilder sb, LevelJson level)
        {
            for (int i = 0; i < level.Targets.Length; i++)
            {
                TargetJson target = level.Targets[i];
                ImmutableArray<ContentCellCoord> neighbors = ContentTileParser.GetRequiredNeighbors(
                    level.Board.Height,
                    level.Board.Width,
                    new ContentCellCoord(target.Row, target.Col));

                for (int n = 0; n < neighbors.Length; n++)
                {
                    ContentCellCoord neighbor = neighbors[n];
                    int x = Margin + (neighbor.Col * CellSize) + 4;
                    int y = HeaderHeight + (neighbor.Row * CellSize) + 4;
                    int size = CellSize - 8;
                    sb.AppendLine($@"<rect class=""target-neighbor-outline"" data-target=""{EscapeAttribute(target.Id)}"" data-row=""{neighbor.Row}"" data-col=""{neighbor.Col}"" x=""{x}"" y=""{y}"" width=""{size}"" height=""{size}"" />");
                }
            }
        }

        private static string CellClass(ContentCellInfo cell)
        {
            return cell.Kind switch
            {
                ContentCellKind.Empty => "cell-empty",
                ContentCellKind.Debris => "cell-debris",
                ContentCellKind.Crate when cell.Hp > 1 => "cell-crate-reinforced",
                ContentCellKind.Crate => "cell-crate",
                ContentCellKind.Ice => "cell-ice",
                ContentCellKind.Vine => "cell-vine",
                ContentCellKind.Target => "cell-target",
                _ => "cell-empty",
            };
        }

        private static string TargetTextClass(ContentCellInfo cell)
        {
            return cell.Kind == ContentCellKind.Target ? " target-label" : string.Empty;
        }

        private static string EscapeText(string value)
        {
            return WebUtility.HtmlEncode(value);
        }

        private static string EscapeAttribute(string value)
        {
            return WebUtility.HtmlEncode(value);
        }
    }
}
