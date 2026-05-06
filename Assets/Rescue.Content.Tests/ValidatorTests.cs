using System;
using System.Linq;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    public sealed class ValidatorTests
    {
        [Test]
        public void Validate_ValidLevel_Passes()
        {
            string json = TestLevels.Serialize(TestLevels.MinimalLevel());

            ValidationResult result = Validator.Validate(json);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void Validate_OutOfBoundsTarget_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Targets = new[] { new TargetJson { Id = "0", Row = 4, Col = 1 } },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "target.bounds"), Is.True);
        }

        [Test]
        public void Validate_DuplicateTargetIds_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Targets = new[]
                {
                    new TargetJson { Id = "0", Row = 2, Col = 1 },
                    new TargetJson { Id = "0", Row = 0, Col = 2 },
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "target.duplicateId"), Is.True);
        }

        [Test]
        public void Validate_WrongSizeTilesArray_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = TestLevels.MinimalLevel().Board with
                {
                    Tiles = new[]
                    {
                        new[] { ".", ".", "." },
                        new[] { ".", "CR", "." },
                    },
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "board.tiles.height"), Is.True);
        }

        [Test]
        public void Validate_UnknownTileCode_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = TestLevels.MinimalLevel().Board with
                {
                    Tiles = new[]
                    {
                        new[] { "Z", "A", "." },
                        new[] { ".", "CR", "." },
                        new[] { ".", "T0", "." },
                    },
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "tile.unknown"), Is.True);
        }

        [Test]
        public void Validate_ImpossibleStart_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                InitialFloodedRows = 1,
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
                Board = TestLevels.MinimalLevel().Board with
                {
                    Tiles = new[]
                    {
                        new[] { ".", ".", "." },
                        new[] { ".", "T0", "." },
                        new[] { ".", ".", "." },
                    },
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "heuristic.impossibleStart"), Is.True);
        }

        [Test]
        public void Validate_UnreachableTarget_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = TestLevels.MinimalLevel().Board with
                {
                    Tiles = new[]
                    {
                        new[] { "CX", "CX", "CX" },
                        new[] { "CX", "T0", "CX" },
                        new[] { "CX", ".", "CX" },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "heuristic.unreachableTarget"), Is.True);
        }

        [Test]
        public void Validate_RuleTeachLevelWithoutPositiveRiseInterval_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Meta = TestLevels.MinimalLevel().Meta with
                {
                    IsRuleTeach = true,
                },
                Water = new WaterJson
                {
                    RiseInterval = 0,
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "water.ruleTeachRiseInterval"), Is.True);
        }

        [Test]
        public void Validate_DockJamOutsideTeachingLevels_DoesNotWarnInCoreValidation()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L03",
                Dock = TestLevels.MinimalLevel().Dock with
                {
                    JamEnabled = true,
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.Errors.Any(error => error.Code == "phase1.dockJamLevel"), Is.False);
        }

        [Test]
        public void Validate_WrongDebrisPoolSizeForLevelBand_DoesNotWarnInCoreValidation()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L05",
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.Errors.Any(error => error.Code == "phase1.debrisPoolSize"), Is.False);
        }

        [Test]
        public void Validate_NonRuleTeachWaterIntervalBelowSix_DoesNotWarnInCoreValidation()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L13",
                Water = new WaterJson
                {
                    RiseInterval = 5,
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.Errors.Any(error => error.Code == "phase1.waterIntervalBelow6"), Is.False);
        }

        [Test]
        public void Validate_L07ActiveVineGrowth_DoesNotWarnInCoreValidation()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L07",
                Vine = new VineJson
                {
                    GrowthThreshold = 4,
                    GrowthPriority = new[] { new TileCoordJson { Row = 0, Col = 0 } },
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.Errors.Any(error => error.Code == "phase1.l07VineGrowth"), Is.False);
        }

        [Test]
        public void Validate_ReinforcedCrate_DoesNotWarnInCoreValidation()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = TestLevels.MinimalLevel().Board with
                {
                    Tiles = new[]
                    {
                        new[] { "CX", ".", "." },
                        new[] { ".", ".", "." },
                        new[] { ".", "T0", "." },
                    },
                },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.Errors.Any(error => error.Code == "phase1.reinforcedCrate"), Is.False);
        }

        [Test]
        public void Phase1PolicyValidator_DockJamOutsideTeachingLevels_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L03",
                Dock = TestLevels.MinimalLevel().Dock with
                {
                    JamEnabled = true,
                },
            };

            ValidationResult result = Phase1PolicyValidator.Validate(level, Phase1TestManifest());

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.dockJamLevel"), Is.True);
        }

        [Test]
        public void Phase1PolicyValidator_WrongDebrisPoolSizeForLevelBand_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L05",
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E },
            };

            ValidationResult result = Phase1PolicyValidator.Validate(level, Phase1TestManifest());

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.debrisPoolSize"), Is.True);
        }

        [Test]
        public void Phase1PolicyValidator_NonRuleTeachWaterIntervalBelowSix_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L13",
                Water = new WaterJson
                {
                    RiseInterval = 5,
                },
            };

            ValidationResult result = Phase1PolicyValidator.Validate(level, Phase1TestManifest());

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.waterIntervalBelow6"), Is.True);
        }

        [Test]
        public void Phase1PolicyValidator_L07ActiveVineGrowth_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L07",
                Vine = new VineJson
                {
                    GrowthThreshold = 4,
                    GrowthPriority = new[] { new TileCoordJson { Row = 0, Col = 0 } },
                },
            };

            ValidationResult result = Phase1PolicyValidator.Validate(level, Phase1TestManifest());

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.l07VineGrowth"), Is.True);
        }

        [Test]
        public void Phase1PolicyValidator_ReinforcedCrate_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = TestLevels.MinimalLevel().Board with
                {
                    Tiles = new[]
                    {
                        new[] { "CX", ".", "." },
                        new[] { ".", ".", "." },
                        new[] { ".", "T0", "." },
                    },
                },
            };

            ValidationResult result = Phase1PolicyValidator.Validate(level, Phase1TestManifest(new[] { "LTest" }));

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.reinforcedCrate"), Is.True);
        }

        [Test]
        public void Phase1PolicyValidator_SpawnIntegrityExceptionWithoutNotes_Warns()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Assistance = TestLevels.MinimalLevel().Assistance with
                {
                    SpawnIntegrity = new SpawnIntegrityJson
                    {
                        AllowExactTripleSpawns = true,
                    },
                },
            };

            ValidationResult result = Phase1PolicyValidator.Validate(level, Phase1TestManifest(new[] { "LTest" }));

            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.spawnIntegrity.exactTripleException"), Is.True);
        }

        [Test]
        public void Phase1PolicyValidator_L16AndL20UseManifestDebrisBand_Warns()
        {
            LevelPacketManifest manifest = Phase1TestManifest(new[] { "L00", "L16", "L20" }) with
            {
                DebrisPoolBands = new[]
                {
                    new DebrisPoolBand
                    {
                        FirstLevelId = "L16",
                        LastLevelId = "L20",
                        DebrisTypePoolSize = 6,
                    },
                },
            };
            LevelJson l16 = TestLevels.MinimalLevel() with
            {
                Id = "L16",
                DebrisTypePool = FiveDebrisTypes(),
            };
            LevelJson l20 = TestLevels.MinimalLevel() with
            {
                Id = "L20",
                DebrisTypePool = FiveDebrisTypes(),
            };

            ValidationResult l16Result = Phase1PolicyValidator.Validate(l16, manifest);
            ValidationResult l20Result = Phase1PolicyValidator.Validate(l20, manifest);

            Assert.That(l16Result.Errors.Any(error => error.Code == "phase1.debrisPoolSize"), Is.True);
            Assert.That(l20Result.Errors.Any(error => error.Code == "phase1.debrisPoolSize"), Is.True);
        }

        [Test]
        public void Phase1PolicyValidator_LevelOutsideManifest_SkipsPacketBandRules()
        {
            LevelPacketManifest manifest = Phase1TestManifest(new[] { "L00", "L01" }) with
            {
                DockJamLevelIds = new[] { "L01" },
                StaticVineIntroLevelIds = new[] { "L01" },
                WaterIntervalMinimum = 8,
                DebrisPoolBands = new[]
                {
                    new DebrisPoolBand
                    {
                        FirstLevelId = "L00",
                        LastLevelId = "L01",
                        DebrisTypePoolSize = 6,
                    },
                },
            };
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L99",
                Dock = TestLevels.MinimalLevel().Dock with { JamEnabled = true },
                DebrisTypePool = FiveDebrisTypes(),
                Water = new WaterJson { RiseInterval = 4 },
                Vine = new VineJson
                {
                    GrowthThreshold = 4,
                    GrowthPriority = new[] { new TileCoordJson { Row = 0, Col = 0 } },
                },
            };

            ValidationResult result = Phase1PolicyValidator.Validate(level, manifest);

            Assert.That(result.Errors.Select(error => error.Code), Has.Member("phase1.packet.levelNotInManifest"));
            Assert.That(result.Errors.Any(error => error.Code == "phase1.dockJamLevel"), Is.False);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.debrisPoolSize"), Is.False);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.l07VineGrowth"), Is.False);
            Assert.That(result.Errors.Any(error => error.Code == "phase1.waterIntervalBelow6"), Is.False);
        }

        [Test]
        public void Phase1PolicyValidator_DockJamFollowsManifest_NotHardcodedL01L02()
        {
            LevelPacketManifest manifest = Phase1TestManifest(new[] { "L01", "L02", "L03" }) with
            {
                DockJamLevelIds = new[] { "L03" },
            };
            LevelJson l01 = TestLevels.MinimalLevel() with
            {
                Id = "L01",
                Dock = TestLevels.MinimalLevel().Dock with { JamEnabled = true },
            };
            LevelJson l03 = TestLevels.MinimalLevel() with
            {
                Id = "L03",
                Dock = TestLevels.MinimalLevel().Dock with { JamEnabled = false },
            };

            ValidationResult l01Result = Phase1PolicyValidator.Validate(l01, manifest);
            ValidationResult l03Result = Phase1PolicyValidator.Validate(l03, manifest);

            Assert.That(l01Result.Errors.Any(error => error.Code == "phase1.dockJamLevel"), Is.True);
            Assert.That(l03Result.Errors.Any(error => error.Code == "phase1.dockJamLevel"), Is.True);
        }

        [Test]
        public void Phase1PolicyValidator_StaticVineIntroFollowsManifest_NotHardcodedL07()
        {
            LevelPacketManifest manifest = Phase1TestManifest(new[] { "L07", "L08" }) with
            {
                StaticVineIntroLevelIds = new[] { "L08" },
            };
            LevelJson l07 = ActiveVineGrowthLevel("L07");
            LevelJson l08 = ActiveVineGrowthLevel("L08");

            ValidationResult l07Result = Phase1PolicyValidator.Validate(l07, manifest);
            ValidationResult l08Result = Phase1PolicyValidator.Validate(l08, manifest);

            Assert.That(l07Result.Errors.Any(error => error.Code == "phase1.l07VineGrowth"), Is.False);
            Assert.That(l08Result.Errors.Any(error => error.Code == "phase1.l07VineGrowth"), Is.True);
        }

        [Test]
        public void Phase1PolicyValidator_WaterMinimumFollowsManifest()
        {
            LevelPacketManifest manifest = Phase1TestManifest(new[] { "L04" }) with
            {
                WaterIntervalMinimum = 8,
            };
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L04",
                Water = new WaterJson { RiseInterval = 7 },
            };

            ValidationResult result = Phase1PolicyValidator.Validate(level, manifest);

            Assert.That(result.Errors.Any(error => error.Code == "phase1.waterIntervalBelow6"), Is.True);
        }

        [Test]
        public void Phase1PolicyValidator_DefaultManifestConvenienceOverload_LoadsRepoManifest()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Id = "L20",
                DebrisTypePool = FiveDebrisTypes(),
            };

            ValidationResult result = Phase1PolicyValidator.Validate(level);

            Assert.That(result.Errors.Any(error => error.Code == "phase1.debrisPoolSize"), Is.True);
        }

        [Test]
        public void Validate_TargetAdjacentCrateWithoutStableDamageAccess_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = new BoardJson
                {
                    Width = 4,
                    Height = 4,
                    Tiles = new[]
                    {
                        new[] { ".", ".", "CR", "." },
                        new[] { ".", "T0", "CR", "CR" },
                        new[] { ".", ".", "CR", "." },
                        new[] { "A", "A", ".", "." },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "heuristic.rescuePathBlockedBlocker"), Is.True);
        }

        [Test]
        public void Validate_TargetAdjacentCrateWithExternalDamageAccess_PassesRescuePathBlockedRule()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = new BoardJson
                {
                    Width = 4,
                    Height = 4,
                    Tiles = new[]
                    {
                        new[] { ".", ".", "A", "." },
                        new[] { ".", "T0", "CR", "CR" },
                        new[] { ".", ".", "CR", "." },
                        new[] { "B", "B", ".", "." },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.Errors.Any(error => error.Code == "heuristic.rescuePathBlockedBlocker"), Is.False);
        }

        [Test]
        public void Validate_TargetAdjacentVineWithoutStableDamageAccess_Fails()
        {
            LevelJson level = TestLevels.MinimalLevel() with
            {
                Board = new BoardJson
                {
                    Width = 4,
                    Height = 4,
                    Tiles = new[]
                    {
                        new[] { ".", ".", "CR", "." },
                        new[] { ".", "T0", "V", "CR" },
                        new[] { ".", ".", "CR", "." },
                        new[] { "A", "A", ".", "." },
                    },
                },
                Targets = new[] { new TargetJson { Id = "0", Row = 1, Col = 1 } },
            };

            ValidationResult result = Validator.Validate(TestLevels.Serialize(level));

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors.Any(error => error.Code == "heuristic.rescuePathBlockedBlocker"), Is.True);
        }

        [Test]
        public void Validate_AuthoredL00Level_Passes()
        {
            string json = File.ReadAllText(GetAuthoredL00Path());
            ValidationResult result = Validator.Validate(json);

            Assert.That(result.HasErrors, Is.False, string.Join(", ", result.Errors.Select(error => error.Code)));
        }

        [Test]
        public void AuthoredL00Level_ExpectedPath_WinsAfterFirstWaterRise()
        {
            LevelJson level = ContentJson.DeserializeLevel(File.ReadAllText(GetAuthoredL00Path()));
            GameState state = Loader.LoadLevel(level, seed: 0);

            ActionResult first = Pipeline.RunAction(state, new ActionInput(new TileCoord(4, 0)), new RunOptions(RecordSnapshot: false));

            Assert.That(first.Outcome, Is.EqualTo(ActionOutcome.Ok));
            Assert.That(first.Events, Has.Some.EqualTo(new WaterRose(6)));

            ActionResult second = Pipeline.RunAction(first.State, new ActionInput(new TileCoord(1, 2)), new RunOptions(RecordSnapshot: false));

            Assert.That(second.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(second.Events, Has.Some.EqualTo(new TargetExtracted("0", new TileCoord(2, 0))));
            Assert.That(second.Events, Has.Some.TypeOf<Won>());
        }

        private static string GetAuthoredL00Path()
        {
            string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? throw new IOException("Could not resolve test assembly directory.");
            string projectRoot = Path.GetFullPath(Path.Combine(root, "..", ".."));
            return Path.Combine(projectRoot, "Assets", "StreamingAssets", "Levels", "L00.json");
        }

        private static LevelPacketManifest Phase1TestManifest()
        {
            return Phase1TestManifest(new[]
            {
                "L00",
                "L01",
                "L02",
                "L03",
                "L04",
                "L05",
                "L06",
                "L07",
                "L08",
                "L09",
                "L10",
                "L11",
                "L12",
                "L13",
                "L14",
                "L15",
                "L16",
                "L17",
                "L18",
                "L19",
                "L20",
            });
        }

        private static LevelPacketManifest Phase1TestManifest(string[] expectedLevelIds)
        {
            return new LevelPacketManifest
            {
                PacketId = "phase1-test",
                DisplayName = "Phase 1 Test Packet",
                FirstLevelId = expectedLevelIds[0],
                LastLevelId = expectedLevelIds[expectedLevelIds.Length - 1],
                ExpectedLevelIds = expectedLevelIds,
                RuleTeachLevelIds = Contains(expectedLevelIds, "L00") ? new[] { "L00" } : Array.Empty<string>(),
                DockJamLevelIds = FilterExpected(expectedLevelIds, new[] { "L01", "L02" }),
                StaticVineIntroLevelIds = Contains(expectedLevelIds, "L07") ? new[] { "L07" } : Array.Empty<string>(),
                DebrisPoolBands = CreateDefaultBands(expectedLevelIds),
                WaterIntervalMinimum = 6,
                Notes = "Test manifest.",
            };
        }

        private static DebrisPoolBand[] CreateDefaultBands(string[] expectedLevelIds)
        {
            string first = expectedLevelIds[0];
            string last = expectedLevelIds[expectedLevelIds.Length - 1];
            if (Contains(expectedLevelIds, "L04") && Contains(expectedLevelIds, "L05"))
            {
                return new[]
                {
                    new DebrisPoolBand
                    {
                        FirstLevelId = first,
                        LastLevelId = "L04",
                        DebrisTypePoolSize = 5,
                    },
                    new DebrisPoolBand
                    {
                        FirstLevelId = "L05",
                        LastLevelId = last,
                        DebrisTypePoolSize = 6,
                    },
                };
            }

            return new[]
            {
                new DebrisPoolBand
                {
                    FirstLevelId = first,
                    LastLevelId = last,
                    DebrisTypePoolSize = 5,
                },
            };
        }

        private static string[] FilterExpected(string[] expectedLevelIds, string[] policyIds)
        {
            int count = 0;
            for (int i = 0; i < policyIds.Length; i++)
            {
                if (Contains(expectedLevelIds, policyIds[i]))
                {
                    count++;
                }
            }

            string[] filtered = new string[count];
            int index = 0;
            for (int i = 0; i < policyIds.Length; i++)
            {
                if (Contains(expectedLevelIds, policyIds[i]))
                {
                    filtered[index] = policyIds[i];
                    index++;
                }
            }

            return filtered;
        }

        private static bool Contains(string[] values, string value)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static DebrisType[] FiveDebrisTypes()
        {
            return new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E };
        }

        private static LevelJson ActiveVineGrowthLevel(string levelId)
        {
            return TestLevels.MinimalLevel() with
            {
                Id = levelId,
                Vine = new VineJson
                {
                    GrowthThreshold = 4,
                    GrowthPriority = new[] { new TileCoordJson { Row = 0, Col = 0 } },
                },
            };
        }
    }
}
