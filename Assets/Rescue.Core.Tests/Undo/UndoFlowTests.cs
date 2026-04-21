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
            Assert.That(actualNext.State, Is.EqualTo(expectedNext.State));
            Assert.That(actualNext.Events, Is.EqualTo(expectedNext.Events));
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
    }
}
