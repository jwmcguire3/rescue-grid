using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;
using Rescue.Core.Undo;

namespace Rescue.Core.Tests.Undo
{
    public sealed class UndoFlowTests
    {
        [Test]
        public void SnapshotActionUndoRestoresEqualPreActionStateAndConsumesUndo()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B),
                    PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D, DebrisType.B),
                    PipelineTestFixtures.EmptyRow(3)));

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));
            GameState restored = UndoGuard.PerformUndo(result.State, result.Snapshot!);

            Assert.That(result.Snapshot, Is.Not.Null);
            Assert.That(restored, Is.EqualTo(original with { UndoAvailable = false }));
            Assert.That(restored.UndoAvailable, Is.False);
            Assert.That(ReferenceEquals(result.Snapshot!.CapturedState, original), Is.True);
            Assert.That(result.Snapshot.CapturedState.UndoAvailable, Is.True);
            Assert.That(ReferenceEquals(restored, original), Is.False);
        }

        [Test]
        public void UndoPreservesRngStateForSubsequentActionResolution()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B, DebrisType.B),
                    PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.C, DebrisType.D, DebrisType.D),
                    PipelineTestFixtures.EmptyRow(4)))
                with
                {
                    RngState = new RngState(0x12345678u, 0x9ABCDEF0u),
                };

            ActionResult firstAction = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));
            GameState restored = UndoGuard.PerformUndo(firstAction.State, firstAction.Snapshot!);

            ActionInput nextInput = new ActionInput(new TileCoord(0, 2));
            ActionResult expectedNext = Rescue.Core.Pipeline.Pipeline.RunAction(
                original with { UndoAvailable = false },
                nextInput,
                new RunOptions(RecordSnapshot: false));
            ActionResult actualNext = Rescue.Core.Pipeline.Pipeline.RunAction(
                restored,
                nextInput,
                new RunOptions(RecordSnapshot: false));

            Assert.That(restored.RngState, Is.EqualTo(original.RngState));
            AssertGameStatesEqual(expectedNext.State, actualNext.State);
            AssertActionEventSequenceEqual(expectedNext.Events, actualNext.Events);
            Assert.That(actualNext.Outcome, Is.EqualTo(expectedNext.Outcome));
        }

        [Test]
        public void UndoRestoresExtractedTargetOrderAndUnextractsTargetState()
        {
            ImmutableArray<TargetState> targets = ImmutableArray.Create(
                new TargetState("target-1", new TileCoord(2, 0), Extracted: false, OneClearAway: true));
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B),
                PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D, DebrisType.B),
                PipelineTestFixtures.TargetRow("target-1", 3));
            GameState original = PipelineTestFixtures.CreateState(board, targets: targets);

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));
            GameState postExtraction = result.State with
            {
                Targets = ImmutableArray.Create(
                    new TargetState("target-1", new TileCoord(2, 0), Extracted: true, OneClearAway: false)),
                ExtractedTargetOrder = ImmutableArray.Create("target-1"),
            };

            GameState restored = UndoGuard.PerformUndo(postExtraction, result.Snapshot!);

            Assert.That(restored.Targets, Is.EqualTo(original.Targets));
            Assert.That(restored.ExtractedTargetOrder, Is.EqualTo(original.ExtractedTargetOrder));
            Assert.That(restored, Is.EqualTo(original with { UndoAvailable = false }));
        }

        [Test]
        public void UndoRestoresPreActionTargetReadinessState()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.C),
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.B), new TargetTile("target", Extracted: false), new EmptyTile()),
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.B), new EmptyTile(), new EmptyTile())),
                targets: ImmutableArray.Create(
                    new TargetState("target", new TileCoord(1, 1), TargetReadiness.Progressing)));

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));
            GameState restored = UndoGuard.PerformUndo(result.State, result.Snapshot!);

            Assert.That(result.State.Targets[0].Readiness, Is.EqualTo(TargetReadiness.OneClearAway));
            Assert.That(restored.Targets[0].Readiness, Is.EqualTo(TargetReadiness.Progressing));
            Assert.That(restored, Is.EqualTo(original with { UndoAvailable = false }));
        }

        [Test]
        public void UndoRestoresPreActionExtractionLatchState()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.C),
                    PipelineTestFixtures.Row(new EmptyTile(), new TargetTile("target", Extracted: false), new EmptyTile()),
                    PipelineTestFixtures.EmptyRow(3)),
                targets: ImmutableArray.Create(
                    new TargetState("target", new TileCoord(1, 1), TargetReadiness.ExtractableLatched)));

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));
            GameState restored = UndoGuard.PerformUndo(result.State, result.Snapshot!);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(result.State.Targets[0].Readiness, Is.EqualTo(TargetReadiness.Extracted));
            Assert.That(restored.Targets[0].Readiness, Is.EqualTo(TargetReadiness.ExtractableLatched));
            Assert.That(restored, Is.EqualTo(original with { UndoAvailable = false }));
        }

        [Test]
        public void UndoRestoresWaterStateExactly()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B),
                    PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D, DebrisType.B),
                    PipelineTestFixtures.EmptyRow(3)))
                with
                {
                    Water = new WaterState(FloodedRows: 2, ActionsUntilRise: 1, RiseInterval: 5),
                };

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));
            GameState postHazard = result.State with
            {
                Water = new WaterState(FloodedRows: 3, ActionsUntilRise: 5, RiseInterval: 5),
            };

            GameState restored = UndoGuard.PerformUndo(postHazard, result.Snapshot!);

            Assert.That(restored.Water, Is.EqualTo(original.Water));
            Assert.That(restored, Is.EqualTo(original with { UndoAvailable = false }));
        }

        [Test]
        public void UndoRestoresDistressedTargetStateExactly()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B),
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new TargetTile("target", Extracted: false))),
                targets: ImmutableArray.Create(
                    new TargetState("target", new TileCoord(1, 2), TargetReadiness.Distressed)))
                with
                {
                    LevelConfig = PipelineTestFixtures.CreateLevelConfig() with
                    {
                        WaterContactMode = WaterContactMode.OneTickGrace,
                    },
                };

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));
            GameState postAction = result.State with
            {
                Targets = ImmutableArray.Create(
                    new TargetState("target", new TileCoord(1, 2), TargetReadiness.Extracted)),
            };

            GameState restored = UndoGuard.PerformUndo(postAction, result.Snapshot!);

            Assert.That(restored.Targets, Is.EqualTo(original.Targets));
            Assert.That(restored.LevelConfig.WaterContactMode, Is.EqualTo(WaterContactMode.OneTickGrace));
            Assert.That(restored, Is.EqualTo(original with { UndoAvailable = false }));
        }

        [Test]
        public void UndoAfterDistressedExpiredLossRestoresPreActionDistressedState()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B),
                    PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.C, DebrisType.B),
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new TargetTile("target", Extracted: false))),
                targets: ImmutableArray.Create(
                    new TargetState("target", new TileCoord(2, 2), TargetReadiness.Distressed)))
                with
                {
                    Water = new WaterState(FloodedRows: 1, ActionsUntilRise: 3, RiseInterval: 3),
                    LevelConfig = PipelineTestFixtures.CreateLevelConfig() with
                    {
                        WaterContactMode = WaterContactMode.OneTickGrace,
                    },
                };

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.LossDistressedExpired));
            Assert.That(result.State.Frozen, Is.True);
            Assert.That(UndoGuard.CanUndo(result.State, result.Snapshot), Is.True);

            GameState restored = UndoGuard.PerformUndo(result.State, result.Snapshot!);

            Assert.That(restored.Targets[0].Readiness, Is.EqualTo(TargetReadiness.Distressed));
            Assert.That(restored.Frozen, Is.False);
            Assert.That(restored, Is.EqualTo(original with { UndoAvailable = false }));
        }

        [Test]
        public void UndoRestoresAssistedSpawnDeterminismStateExactly()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B),
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D, DebrisType.E)))
                with
                {
                    ConsecutiveEmergencySpawns = 1,
                    SpawnRecoveryCounter = 2,
                    DebugSpawnOverride = new SpawnOverride(ForceEmergency: true, OverrideAssistanceChance: 1.0d),
                    LevelConfig = PipelineTestFixtures.CreateLevelConfig(assistanceChance: 1.0d) with
                    {
                        ConsecutiveEmergencyCap = 10,
                    },
                };

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));
            GameState restored = UndoGuard.PerformUndo(result.State, result.Snapshot!);

            Assert.That(result.State.ConsecutiveEmergencySpawns, Is.Not.EqualTo(original.ConsecutiveEmergencySpawns));
            Assert.That(restored.ConsecutiveEmergencySpawns, Is.EqualTo(original.ConsecutiveEmergencySpawns));
            Assert.That(restored.SpawnRecoveryCounter, Is.EqualTo(original.SpawnRecoveryCounter));
            Assert.That(restored.DebugSpawnOverride, Is.EqualTo(original.DebugSpawnOverride));
            Assert.That(restored.RngState, Is.EqualTo(original.RngState));
            Assert.That(restored, Is.EqualTo(original with { UndoAvailable = false }));
        }

        [Test]
        public void UndoRestoresVineStateExactly()
        {
            ImmutableArray<TileCoord> growthPriority = ImmutableArray.Create(
                new TileCoord(1, 1),
                new TileCoord(1, 2),
                new TileCoord(2, 2));
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B),
                    PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D, DebrisType.B),
                    PipelineTestFixtures.EmptyRow(3)))
                with
                {
                    Vine = new VineState(
                        ActionsSinceLastClear: 2,
                        GrowthThreshold: 4,
                        GrowthPriorityList: growthPriority,
                        PriorityCursor: 1,
                        PendingGrowthTile: new TileCoord(1, 2)),
                };

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));
            GameState postGrowth = result.State with
            {
                Vine = new VineState(
                    ActionsSinceLastClear: 3,
                    GrowthThreshold: 4,
                    GrowthPriorityList: growthPriority,
                    PriorityCursor: 2,
                    PendingGrowthTile: new TileCoord(2, 2)),
                };

            GameState restored = UndoGuard.PerformUndo(postGrowth, result.Snapshot!);

            Assert.That(restored.Vine, Is.EqualTo(original.Vine));
            Assert.That(restored, Is.EqualTo(original with { UndoAvailable = false }));
        }

        [Test]
        public void UndoCannotChainAfterConsumptionEvenIfNewSnapshotExists()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B),
                    PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D, DebrisType.B),
                    PipelineTestFixtures.EmptyRow(3)));

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));
            GameState restored = UndoGuard.PerformUndo(result.State, result.Snapshot!);
            Snapshot newSnapshot = SnapshotHelpers.Take(restored);

            Assert.That(restored.UndoAvailable, Is.False);
            Assert.That(UndoGuard.CanUndo(restored, newSnapshot), Is.False);
        }

        [Test]
        public void UndoAfterFrozenLossStateRestoresPreLossState()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B),
                    PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D, DebrisType.B),
                    PipelineTestFixtures.EmptyRow(3)));

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));
            GameState frozenLossState = result.State with { Frozen = true };

            Assert.That(UndoGuard.CanUndo(frozenLossState, result.Snapshot), Is.True);

            GameState restored = UndoGuard.PerformUndo(frozenLossState, result.Snapshot!);

            Assert.That(restored, Is.EqualTo(original with { UndoAvailable = false }));
            Assert.That(restored.Frozen, Is.False);
        }

        private static void AssertGameStatesEqual(GameState expected, GameState actual)
        {
            AssertBoardEqual(expected.Board, actual.Board);
            Assert.That(actual.Dock.Size, Is.EqualTo(expected.Dock.Size));
            AssertNullableDebrisSequenceEqual(expected.Dock.Slots, actual.Dock.Slots);
            Assert.That(actual.Water, Is.EqualTo(expected.Water));
            Assert.That(actual.Vine.ActionsSinceLastClear, Is.EqualTo(expected.Vine.ActionsSinceLastClear));
            Assert.That(actual.Vine.GrowthThreshold, Is.EqualTo(expected.Vine.GrowthThreshold));
            Assert.That(actual.Vine.GrowthPriorityList, Is.EqualTo(expected.Vine.GrowthPriorityList).AsCollection);
            Assert.That(actual.Vine.PriorityCursor, Is.EqualTo(expected.Vine.PriorityCursor));
            Assert.That(actual.Vine.PendingGrowthTile, Is.EqualTo(expected.Vine.PendingGrowthTile));
            Assert.That(actual.Targets, Is.EqualTo(expected.Targets).AsCollection);
            AssertLevelConfigEqual(expected.LevelConfig, actual.LevelConfig);
            Assert.That(actual.RngState, Is.EqualTo(expected.RngState));
            Assert.That(actual.ActionCount, Is.EqualTo(expected.ActionCount));
            Assert.That(actual.DockJamUsed, Is.EqualTo(expected.DockJamUsed));
            Assert.That(actual.UndoAvailable, Is.EqualTo(expected.UndoAvailable));
            Assert.That(actual.ExtractedTargetOrder, Is.EqualTo(expected.ExtractedTargetOrder).AsCollection);
            Assert.That(actual.Frozen, Is.EqualTo(expected.Frozen));
            Assert.That(actual.ConsecutiveEmergencySpawns, Is.EqualTo(expected.ConsecutiveEmergencySpawns));
            Assert.That(actual.SpawnRecoveryCounter, Is.EqualTo(expected.SpawnRecoveryCounter));
            Assert.That(actual.DockJamEnabled, Is.EqualTo(expected.DockJamEnabled));
            Assert.That(actual.DockJamActive, Is.EqualTo(expected.DockJamActive));
        }

        private static void AssertBoardEqual(Board expected, Board actual)
        {
            Assert.That(actual.Width, Is.EqualTo(expected.Width));
            Assert.That(actual.Height, Is.EqualTo(expected.Height));
            Assert.That(actual.Tiles.Length, Is.EqualTo(expected.Tiles.Length));

            for (int row = 0; row < expected.Tiles.Length; row++)
            {
                Assert.That(actual.Tiles[row].Length, Is.EqualTo(expected.Tiles[row].Length), $"Board row length mismatch at {row}.");
                for (int col = 0; col < expected.Tiles[row].Length; col++)
                {
                    Assert.That(actual.Tiles[row][col], Is.EqualTo(expected.Tiles[row][col]), $"Board tile mismatch at ({row}, {col}).");
                }
            }
        }

        private static void AssertActionEventSequenceEqual(
            ImmutableArray<ActionEvent> expected,
            ImmutableArray<ActionEvent> actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                AssertActionEventEqual(expected[i], actual[i], i);
            }
        }

        private static void AssertActionEventEqual(ActionEvent expected, ActionEvent actual, int index)
        {
            Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()), $"Event type mismatch at index {index}.");

            switch (expected)
            {
                case GroupRemoved expectedGroupRemoved:
                    GroupRemoved actualGroupRemoved = (GroupRemoved)actual;
                    Assert.That(actualGroupRemoved.Type, Is.EqualTo(expectedGroupRemoved.Type), $"GroupRemoved type mismatch at index {index}.");
                    AssertTileCoordSequenceEqual(expectedGroupRemoved.Coords, actualGroupRemoved.Coords, $"GroupRemoved coords mismatch at index {index}.");
                    return;
                case DockInserted expectedDockInserted:
                    DockInserted actualDockInserted = (DockInserted)actual;
                    AssertDebrisSequenceEqual(expectedDockInserted.Pieces, actualDockInserted.Pieces, $"DockInserted pieces mismatch at index {index}.");
                    Assert.That(actualDockInserted.OccupancyAfterInsert, Is.EqualTo(expectedDockInserted.OccupancyAfterInsert), $"DockInserted occupancy mismatch at index {index}.");
                    Assert.That(actualDockInserted.OverflowCount, Is.EqualTo(expectedDockInserted.OverflowCount), $"DockInserted overflow mismatch at index {index}.");
                    return;
                case GravitySettled expectedGravitySettled:
                    GravitySettled actualGravitySettled = (GravitySettled)actual;
                    AssertMoveSequenceEqual(expectedGravitySettled.Moves, actualGravitySettled.Moves, $"GravitySettled moves mismatch at index {index}.");
                    return;
                case Spawned expectedSpawned:
                    Spawned actualSpawned = (Spawned)actual;
                    AssertSpawnSequenceEqual(expectedSpawned.Pieces, actualSpawned.Pieces, $"Spawned pieces mismatch at index {index}.");
                    return;
                case Won expectedWon:
                    Won actualWon = (Won)actual;
                    Assert.That(actualWon.ExtractedTargetOrder, Is.EqualTo(expectedWon.ExtractedTargetOrder).AsCollection, $"Won extracted order mismatch at index {index}.");
                    Assert.That(actualWon.FinalExtractedTargetId, Is.EqualTo(expectedWon.FinalExtractedTargetId), $"Won final target mismatch at index {index}.");
                    Assert.That(actualWon.TotalActions, Is.EqualTo(expectedWon.TotalActions), $"Won total actions mismatch at index {index}.");
                    return;
                default:
                    Assert.That(actual, Is.EqualTo(expected), $"Event mismatch at index {index}.");
                    return;
            }
        }

        private static void AssertNullableDebrisSequenceEqual(
            ImmutableArray<DebrisType?> expected,
            ImmutableArray<DebrisType?> actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"Dock slot mismatch at index {i}.");
            }
        }

        private static void AssertTileCoordSequenceEqual(
            ImmutableArray<TileCoord> expected,
            ImmutableArray<TileCoord> actual,
            string messagePrefix)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{messagePrefix} length.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"{messagePrefix} coord {i}.");
            }
        }

        private static void AssertDebrisSequenceEqual(
            ImmutableArray<DebrisType> expected,
            ImmutableArray<DebrisType> actual,
            string messagePrefix)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{messagePrefix} length.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"{messagePrefix} item {i}.");
            }
        }

        private static void AssertLevelConfigEqual(LevelConfig expected, LevelConfig actual)
        {
            Assert.That(actual.DebrisTypePool.Length, Is.EqualTo(expected.DebrisTypePool.Length));
            for (int i = 0; i < expected.DebrisTypePool.Length; i++)
            {
                Assert.That(actual.DebrisTypePool[i], Is.EqualTo(expected.DebrisTypePool[i]));
            }
            Assert.That(actual.AssistanceChance, Is.EqualTo(expected.AssistanceChance));
            Assert.That(actual.ConsecutiveEmergencyCap, Is.EqualTo(expected.ConsecutiveEmergencyCap));
            Assert.That(actual.IsRuleTeach, Is.EqualTo(expected.IsRuleTeach));
            Assert.That(actual.WaterContactMode, Is.EqualTo(expected.WaterContactMode));
            Assert.That(actual.BaseDistribution?.Count ?? 0, Is.EqualTo(expected.BaseDistribution?.Count ?? 0));

            if (expected.BaseDistribution is null || actual.BaseDistribution is null)
            {
                Assert.That(actual.BaseDistribution is null, Is.EqualTo(expected.BaseDistribution is null));
                return;
            }

            foreach ((DebrisType key, double value) in expected.BaseDistribution)
            {
                Assert.That(actual.BaseDistribution.TryGetValue(key, out double actualValue), Is.True);
                Assert.That(actualValue, Is.EqualTo(value));
            }
        }

        private static void AssertMoveSequenceEqual(
            ImmutableArray<(TileCoord From, TileCoord To)> expected,
            ImmutableArray<(TileCoord From, TileCoord To)> actual,
            string messagePrefix)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{messagePrefix} length.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i].From, Is.EqualTo(expected[i].From), $"{messagePrefix} from {i}.");
                Assert.That(actual[i].To, Is.EqualTo(expected[i].To), $"{messagePrefix} to {i}.");
            }
        }

        private static void AssertSpawnSequenceEqual(
            ImmutableArray<SpawnedPiece> expected,
            ImmutableArray<SpawnedPiece> actual,
            string messagePrefix)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{messagePrefix} length.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i].Coord, Is.EqualTo(expected[i].Coord), $"{messagePrefix} coord {i}.");
                Assert.That(actual[i].Type, Is.EqualTo(expected[i].Type), $"{messagePrefix} type {i}.");
            }
        }
    }
}
