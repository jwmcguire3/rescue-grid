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
            Assert.That(actual.RngState, Is.EqualTo(expected.RngState));
            Assert.That(actual.ActionCount, Is.EqualTo(expected.ActionCount));
            Assert.That(actual.DockJamUsed, Is.EqualTo(expected.DockJamUsed));
            Assert.That(actual.UndoAvailable, Is.EqualTo(expected.UndoAvailable));
            Assert.That(actual.ExtractedTargetOrder, Is.EqualTo(expected.ExtractedTargetOrder).AsCollection);
            Assert.That(actual.Frozen, Is.EqualTo(expected.Frozen));
            Assert.That(actual.ConsecutiveEmergencySpawns, Is.EqualTo(expected.ConsecutiveEmergencySpawns));
            Assert.That(actual.SpawnRecoveryCounter, Is.EqualTo(expected.SpawnRecoveryCounter));
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
                    Assert.That(actualDockInserted.Pieces, Is.EqualTo(expectedDockInserted.Pieces).AsCollection, $"DockInserted pieces mismatch at index {index}.");
                    Assert.That(actualDockInserted.OccupancyAfterInsert, Is.EqualTo(expectedDockInserted.OccupancyAfterInsert), $"DockInserted occupancy mismatch at index {index}.");
                    Assert.That(actualDockInserted.OverflowCount, Is.EqualTo(expectedDockInserted.OverflowCount), $"DockInserted overflow mismatch at index {index}.");
                    return;
                case GravitySettled expectedGravitySettled:
                    GravitySettled actualGravitySettled = (GravitySettled)actual;
                    Assert.That(actualGravitySettled.Moves, Is.EqualTo(expectedGravitySettled.Moves).AsCollection, $"GravitySettled moves mismatch at index {index}.");
                    return;
                case Spawned expectedSpawned:
                    Spawned actualSpawned = (Spawned)actual;
                    Assert.That(actualSpawned.Pieces, Is.EqualTo(expectedSpawned.Pieces).AsCollection, $"Spawned pieces mismatch at index {index}.");
                    return;
                case Won expectedWon:
                    Won actualWon = (Won)actual;
                    Assert.That(actualWon.ExtractedTargetOrder, Is.EqualTo(expectedWon.ExtractedTargetOrder).AsCollection, $"Won extracted order mismatch at index {index}.");
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
    }
}
