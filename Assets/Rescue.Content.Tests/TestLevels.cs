using Rescue.Content;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    internal static class TestLevels
    {
        public static LevelJson MinimalLevel()
        {
            return new LevelJson
            {
                Id = "LTest",
                Name = "Minimal",
                Board = new BoardJson
                {
                    Width = 3,
                    Height = 3,
                    Tiles = new[]
                    {
                        new[] { "A", "A", "." },
                        new[] { ".", "C1", "." },
                        new[] { ".", "T0", "." },
                    },
                },
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D },
                Targets = new[] { new TargetJson { Id = "0", Row = 2, Col = 1 } },
                InitialFloodedRows = 0,
                Water = new WaterJson
                {
                    RiseInterval = 10,
                },
                Vine = new VineJson
                {
                    GrowthThreshold = 4,
                    GrowthPriority = System.Array.Empty<TileCoordJson>(),
                },
                Dock = new DockJson
                {
                    Size = 7,
                    JamEnabled = false,
                },
                Assistance = new AssistanceJson
                {
                    Chance = 0.5d,
                    ConsecutiveEmergencyCap = 2,
                },
                Meta = new MetaJson
                {
                    Intent = "Minimal loader test.",
                    ExpectedPath = "Clear the crate-adjacent debris.",
                    ExpectedFailMode = "Ignore the target lane.",
                    WhatItProves = "Loader and validator basics.",
                },
            };
        }

        public static string Serialize(LevelJson level)
        {
            return ContentJson.SerializeLevel(level);
        }
    }
}
