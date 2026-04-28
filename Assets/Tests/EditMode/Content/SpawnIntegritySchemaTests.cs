using System;
using System.Linq;
using NUnit.Framework;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    public sealed class SpawnIntegritySchemaTests
    {
        [Test]
        public void LoadLevel_OmittedSpawnIntegrity_UsesStrictDefaults()
        {
            GameState state = Loader.LoadLevel(CreateLevel(), seed: 7);

            Assert.That(state.LevelConfig.SpawnIntegrity.AllowExactTripleSpawns, Is.False);
            Assert.That(state.LevelConfig.SpawnIntegrity.AllowOversizedSpawnGroups, Is.False);
        }

        [Test]
        public void LoadLevel_ExplicitSpawnIntegrity_MapsToLevelConfig()
        {
            LevelJson level = CreateLevel() with
            {
                Assistance = new AssistanceJson
                {
                    Chance = 0.7d,
                    ConsecutiveEmergencyCap = 2,
                    SpawnIntegrity = new SpawnIntegrityJson
                    {
                        AllowExactTripleSpawns = true,
                        AllowOversizedSpawnGroups = true,
                    },
                },
                Meta = CreateMeta() with
                {
                    Notes = "Scripted relief for a tutorial beat.",
                },
            };

            GameState state = Loader.LoadLevel(level, seed: 7);

            Assert.That(state.LevelConfig.SpawnIntegrity.AllowExactTripleSpawns, Is.True);
            Assert.That(state.LevelConfig.SpawnIntegrity.AllowOversizedSpawnGroups, Is.True);
        }

        [Test]
        public void Validate_ExceptionPolicyWithoutNotes_Warns()
        {
            LevelJson level = CreateLevel() with
            {
                Assistance = new AssistanceJson
                {
                    Chance = 0.7d,
                    ConsecutiveEmergencyCap = 2,
                    SpawnIntegrity = new SpawnIntegrityJson
                    {
                        AllowExactTripleSpawns = true,
                        AllowOversizedSpawnGroups = true,
                    },
                },
            };

            ValidationResult result = Validator.Validate(level);

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.Errors.Select(error => error.Code), Has.Member("phase1.spawnIntegrity.exactTripleException"));
            Assert.That(result.Errors.Select(error => error.Code), Has.Member("phase1.spawnIntegrity.oversizedException"));
        }

        private static LevelJson CreateLevel()
        {
            return new LevelJson
            {
                Id = "LX",
                Name = "Spawn Integrity Fixture",
                Board = new BoardJson
                {
                    Width = 3,
                    Height = 3,
                    Tiles = new[]
                    {
                        new[] { "A", "A", "." },
                        new[] { ".", "T0", "." },
                        new[] { "B", "B", "." },
                    },
                },
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D },
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
                InitialFloodedRows = 0,
                Water = new WaterJson { RiseInterval = 10 },
                Vine = new VineJson { GrowthThreshold = 4, GrowthPriority = Array.Empty<TileCoordJson>() },
                Dock = new DockJson { Size = 7, JamEnabled = false },
                Assistance = new AssistanceJson { Chance = 0.7d, ConsecutiveEmergencyCap = 2 },
                Meta = CreateMeta(),
            };
        }

        private static MetaJson CreateMeta()
        {
            return new MetaJson
            {
                Intent = "Test spawn integrity schema.",
                ExpectedPath = "N/A",
                ExpectedFailMode = "N/A",
                WhatItProves = "Spawn integrity policy loads and validates.",
            };
        }
    }
}
