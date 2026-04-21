using NUnit.Framework;
using Rescue.Content;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    public sealed class AsciiPreviewTests
    {
        // 3x4 board with one of every Phase 1 tile type.
        // Row 0: "."  "A"  "CR"
        // Row 1: "CX" "IA" "V"
        // Row 2: "T0" "B"  "."
        // Row 3: "."  "."  "."
        private static LevelJson AllTilesLevel() => new LevelJson
        {
            Id = "LX",
            Name = "All Tiles",
            Board = new BoardJson
            {
                Width = 3,
                Height = 4,
                Tiles = new[]
                {
                    new[] { ".", "A",  "CR" },
                    new[] { "CX", "IA", "V"  },
                    new[] { "T0", "B",  "."  },
                    new[] { ".",  ".",  "."  },
                },
            },
            DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D },
            Targets = new[] { new TargetJson { Id = "0", Row = 2, Col = 0 } },
            InitialFloodedRows = 0,
            Water = new WaterJson { RiseInterval = 12 },
            Vine = new VineJson { GrowthThreshold = 4, GrowthPriority = System.Array.Empty<TileCoordJson>() },
            Dock = new DockJson { Size = 7, JamEnabled = false },
            Assistance = new AssistanceJson { Chance = 0.70d, ConsecutiveEmergencyCap = 2 },
            Meta = new MetaJson
            {
                Intent = "Test all tile codes.",
                ExpectedPath = "N/A",
                ExpectedFailMode = "N/A",
                WhatItProves = "AsciiPreview renders every Phase 1 tile code.",
            },
        };

        [Test]
        public void Render_KnownFixture_ExactMatch()
        {
            LevelJson level = AllTilesLevel();

            string result = AsciiPreview.Render(level);

            // Header: id — name  [w×h]  water:interval  flooded:rows
            // Each cell is 2 chars wide (right-padded), separated by single space.
            string nl = System.Environment.NewLine;
            string expected =
                $"LX \u2014 All Tiles  [3\u00d74]  water:12  flooded:0{nl}" +
                $".  A  CR{nl}" +
                $"CX IA V {nl}" +
                $"T0 B  . {nl}" +
                $".  .  . {nl}";

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Render_EachTileType_ProducesDistinctSymbol()
        {
            LevelJson level = AllTilesLevel();

            string result = AsciiPreview.Render(level);

            Assert.That(result, Does.Contain("."));
            Assert.That(result, Does.Contain("A"));
            Assert.That(result, Does.Contain("CR"));
            Assert.That(result, Does.Contain("CX"));
            Assert.That(result, Does.Contain("IA"));
            Assert.That(result, Does.Contain("V"));
            Assert.That(result, Does.Contain("T0"));
            Assert.That(result, Does.Contain("B"));
        }

        [Test]
        public void Render_WithFloodedRows_BottomRowsAreWaves()
        {
            LevelJson level = AllTilesLevel() with
            {
                Id = "LF",
                InitialFloodedRows = 2,
            };

            string result = AsciiPreview.Render(level);

            string[] lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            // Line 0 is the header. Board rows start at line 1.
            // Height=4, floodStart=4-2=2. Rows 0-1 are tiles, rows 2-3 are flooded.
            Assert.That(lines[1], Is.EqualTo(".  A  CR"));
            Assert.That(lines[2], Is.EqualTo("CX IA V "));
            Assert.That(lines[3], Is.EqualTo("~  ~  ~ "));
            Assert.That(lines[4], Is.EqualTo("~  ~  ~ "));
        }

        [Test]
        public void Render_WaterDisabled_HeaderShowsOff()
        {
            LevelJson level = AllTilesLevel() with
            {
                Water = new WaterJson { RiseInterval = 0 },
            };

            string result = AsciiPreview.Render(level);

            Assert.That(result, Does.StartWith("LX \u2014 All Tiles  [3\u00d74]  water:off  flooded:0"));
        }

        [Test]
        public void Render_IceTile_IncludesHiddenDebrisLetter()
        {
            // Board with IB ice tile
            LevelJson level = new LevelJson
            {
                Id = "LI",
                Name = "Ice Test",
                Board = new BoardJson
                {
                    Width = 2,
                    Height = 2,
                    Tiles = new[]
                    {
                        new[] { "IB", "IC" },
                        new[] { "T0", "A"  },
                    },
                },
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D },
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 0 } },
                InitialFloodedRows = 0,
                Water = new WaterJson { RiseInterval = 10 },
                Vine = new VineJson { GrowthThreshold = 4, GrowthPriority = System.Array.Empty<TileCoordJson>() },
                Dock = new DockJson { Size = 7, JamEnabled = false },
                Assistance = new AssistanceJson { Chance = 0.60d, ConsecutiveEmergencyCap = 2 },
                Meta = new MetaJson
                {
                    Intent = "Test ice rendering.",
                    ExpectedPath = "N/A",
                    ExpectedFailMode = "N/A",
                    WhatItProves = "Ice tiles render as I<X> not plain I.",
                },
            };

            string result = AsciiPreview.Render(level);

            Assert.That(result, Does.Contain("IB"));
            Assert.That(result, Does.Contain("IC"));
            Assert.That(result, Does.Not.Contain("\nI \n"));
        }
    }
}
