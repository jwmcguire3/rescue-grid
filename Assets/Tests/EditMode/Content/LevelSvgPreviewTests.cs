using NUnit.Framework;
using Rescue.Content;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    public sealed class LevelSvgPreviewTests
    {
        [Test]
        public void Render_ValidLevel_ContainsSvgRoot()
        {
            string result = LevelSvgPreview.Render(PreviewLevel());

            Assert.That(result, Does.Contain("<svg"));
        }

        [Test]
        public void Render_ValidLevel_RendersOneGridCellPerBoardTile()
        {
            LevelJson level = PreviewLevel();

            string result = LevelSvgPreview.Render(level);

            Assert.That(CountOccurrences(result, @"<rect class=""cell "), Is.EqualTo(level.Board.Width * level.Board.Height));
        }

        [Test]
        public void Render_Targets_IncludesTargetLabels()
        {
            string result = LevelSvgPreview.Render(PreviewLevel());

            Assert.That(result, Does.Contain(@"class=""tile-code target-label"""));
            Assert.That(result, Does.Contain(">T0<"));
        }

        [Test]
        public void Render_WithInitialFloodedRows_IncludesFloodedRowMarker()
        {
            string result = LevelSvgPreview.Render(PreviewLevel() with
            {
                InitialFloodedRows = 1,
            });

            Assert.That(result, Does.Contain(@"class=""flooded-row-marker"""));
        }

        [Test]
        public void Render_WithActiveWater_IncludesForecastRowMarker()
        {
            string result = LevelSvgPreview.Render(PreviewLevel() with
            {
                InitialFloodedRows = 1,
                Water = new WaterJson { RiseInterval = 8 },
            });

            Assert.That(result, Does.Contain(@"class=""forecast-row-marker"""));
            Assert.That(result, Does.Contain(@"data-row=""2"""));
        }

        private static LevelJson PreviewLevel()
        {
            return new LevelJson
            {
                Id = "LSVG",
                Name = "SVG Preview",
                Board = new BoardJson
                {
                    Width = 4,
                    Height = 4,
                    Tiles = new[]
                    {
                        new[] { "A", "A", "CR", "." },
                        new[] { "CX", "IA", "V", "B" },
                        new[] { ".", "T0", "C", "D" },
                        new[] { "E", ".", ".", "." },
                    },
                },
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E },
                Targets = new[] { new TargetJson { Id = "0", Row = 2, Col = 1 } },
                InitialFloodedRows = 0,
                Water = new WaterJson { RiseInterval = 10 },
                Vine = new VineJson { GrowthThreshold = 4, GrowthPriority = System.Array.Empty<TileCoordJson>() },
                Dock = new DockJson { Size = 7, JamEnabled = false },
                Assistance = new AssistanceJson { Chance = 0.5d, ConsecutiveEmergencyCap = 2 },
                Meta = new MetaJson
                {
                    Intent = "Test SVG preview.",
                    ExpectedPath = "N/A",
                    ExpectedFailMode = "N/A",
                    WhatItProves = "LevelSvgPreview renders readability markers.",
                },
            };
        }

        private static int CountOccurrences(string value, string needle)
        {
            int count = 0;
            int index = 0;
            while ((index = value.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
        }
    }
}
