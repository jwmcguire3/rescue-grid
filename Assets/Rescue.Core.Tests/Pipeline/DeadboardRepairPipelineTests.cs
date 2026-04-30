using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rules;
using Rescue.Core.State;
using Rescue.Core.Undo;

namespace Rescue.Core.Tests.Pipeline
{
    public sealed class DeadboardRepairPipelineTests
    {
        [Test]
        public void RepairEventIsEmittedOnlyAfterOkReturnControlHardNoMove()
        {
            GameState state = CreatePipelineHardNoMoveState();

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(
                state,
                new ActionInput(new TileCoord(2, 0)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Ok));
            Assert.That(result.Events, Has.Some.TypeOf<DeadboardMinimalShuffleApplied>());
            Assert.That(GroupOps.HasValidGroup(result.State.Board, result.State.Water), Is.True);

            DeadboardMinimalShuffleApplied repair = GetSingleRepairEvent(result.Events);
            Assert.That(repair.Reason, Is.EqualTo("hard_no_valid_groups"));
            Assert.That(repair.Succeeded, Is.True);
            Assert.That(repair.SkippedReason, Is.Null);
            Assert.That(repair.Changes, Is.Not.Empty);
        }

        [Test]
        public void NoRepairAfterWin()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new DebrisTile(DebrisType.B)),
                    PipelineTestFixtures.Row(new EmptyTile(), new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.D)),
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.A)),
                targets: ImmutableArray.Create(new TargetState("target-1", new TileCoord(0, 0), Extracted: true, OneClearAway: false)))
                with
                {
                    ExtractedTargetOrder = ImmutableArray.Create("target-1"),
                };

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(2, 0)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(result.Events, Has.None.TypeOf<DeadboardMinimalShuffleApplied>());
        }

        [Test]
        public void NoRepairAfterLoss()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A)),
                dockJamEnabled: false) with
                {
                    Dock = new Dock(
                        ImmutableArray.Create<DebrisType?>(
                            DebrisType.B,
                            DebrisType.C,
                            DebrisType.D,
                            DebrisType.E,
                            DebrisType.F,
                            DebrisType.B,
                            DebrisType.C),
                        Size: 7),
                };

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(0, 0)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.LossDockOverflow));
            Assert.That(result.Events, Has.None.TypeOf<DeadboardMinimalShuffleApplied>());
        }

        [Test]
        public void UndoAfterRepairedActionRestoresExactPreActionState()
        {
            GameState original = CreatePipelineHardNoMoveState();

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(
                original,
                new ActionInput(new TileCoord(2, 0)));
            Assert.That(result.Snapshot, Is.Not.Null);
            Snapshot snapshot = result.Snapshot ?? throw new AssertionException("Expected an undo snapshot.");
            GameState restored = UndoGuard.PerformUndo(result.State, snapshot);

            Assert.That(result.Events, Has.Some.TypeOf<DeadboardMinimalShuffleApplied>());
            Assert.That(restored, Is.EqualTo(original with { UndoAvailable = false }));
        }

        [Test]
        public void RepairLeavesNonBoardStateUnchangedWhenApplied()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B),
                    PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D)),
                targets: ImmutableArray.Create(
                    new TargetState("target", new TileCoord(0, 0), TargetReadiness.ExtractableLatched))) with
                {
                    Dock = new Dock(
                        ImmutableArray.Create<DebrisType?>(
                            DebrisType.A,
                            DebrisType.B,
                            null,
                            null,
                            null,
                            null,
                            null),
                        Size: 7),
                    Water = new WaterState(FloodedRows: 0, ActionsUntilRise: 2, RiseInterval: 5),
                    Vine = new VineState(
                        ActionsSinceLastClear: 2,
                        GrowthThreshold: 4,
                        GrowthPriorityList: ImmutableArray.Create(new TileCoord(1, 1)),
                        PriorityCursor: 0,
                        PendingGrowthTile: new TileCoord(1, 1)),
                    ExtractedTargetOrder = ImmutableArray.Create("saved"),
                    DockJamUsed = true,
                    ConsecutiveEmergencySpawns = 1,
                    SpawnRecoveryCounter = 2,
                };

            DeadboardRepairResult repair = DeadboardRepairOps.RepairHardNoValidGroups(original.Board, original.Water);
            GameState repaired = original with { Board = repair.Board };

            Assert.That(repair.Succeeded, Is.True);
            Assert.That(repaired.Dock, Is.EqualTo(original.Dock));
            Assert.That(repaired.Water, Is.EqualTo(original.Water));
            Assert.That(repaired.Vine, Is.EqualTo(original.Vine));
            Assert.That(repaired.Targets, Is.EqualTo(original.Targets).AsCollection);
            Assert.That(repaired.ExtractedTargetOrder, Is.EqualTo(original.ExtractedTargetOrder).AsCollection);
            Assert.That(repaired.DockJamUsed, Is.EqualTo(original.DockJamUsed));
            Assert.That(repaired.ConsecutiveEmergencySpawns, Is.EqualTo(original.ConsecutiveEmergencySpawns));
            Assert.That(repaired.SpawnRecoveryCounter, Is.EqualTo(original.SpawnRecoveryCounter));
        }

        private static GameState CreatePipelineHardNoMoveState()
        {
            return PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B, DebrisType.C),
                    PipelineTestFixtures.Row(
                        new BlockerTile(BlockerType.Crate, 2, null),
                        new BlockerTile(BlockerType.Crate, 2, null),
                        new BlockerTile(BlockerType.Crate, 2, null)),
                    PipelineTestFixtures.DebrisRow(DebrisType.D, DebrisType.D, DebrisType.E)))
                with
                {
                    Water = new WaterState(FloodedRows: 0, ActionsUntilRise: 1, RiseInterval: 3),
                };
        }

        private static DeadboardMinimalShuffleApplied GetSingleRepairEvent(ImmutableArray<ActionEvent> events)
        {
            DeadboardMinimalShuffleApplied? repair = null;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i] is DeadboardMinimalShuffleApplied candidate)
                {
                    Assert.That(repair, Is.Null);
                    repair = candidate;
                }
            }

            Assert.That(repair, Is.Not.Null);
            return repair ?? throw new AssertionException("Expected a repair event.");
        }
    }
}
