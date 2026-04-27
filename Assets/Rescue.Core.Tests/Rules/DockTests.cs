using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Pipeline.Steps;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Rules
{
    public sealed class DockTests
    {
        [Test]
        public void InsertThreeMatchingPiecesIntoEmptyDockClearsImmediately()
        {
            GameState state = CreateStateWithDock();
            StepResult cleared = RunDockSteps(state, ImmutableArray.Create(DebrisType.A, DebrisType.A, DebrisType.A));

            AssertSlotsEqual(CreateSlots(), cleared.State.Dock.Slots);
            Assert.That(cleared.Context.ClearedDockTriplesThisAction, Is.EqualTo(1));
            Assert.That(cleared.Events, Is.EqualTo(new ActionEvent[]
            {
                new DockCleared(DebrisType.A, 1, 0),
            }).AsCollection);
        }

        [Test]
        public void InsertFourMatchingPiecesClearsThreeAndLeavesOne()
        {
            GameState state = CreateStateWithDock();
            StepResult inserted = Step05_InsertDock.Run(state, CreateDockContext(state, ImmutableArray.Create(DebrisType.B, DebrisType.B, DebrisType.B, DebrisType.B)));
            StepResult cleared = Step06_ClearDock.Run(inserted.State, inserted.Context);

            AssertSlotsEqual(CreateSlots(DebrisType.B), cleared.State.Dock.Slots);
            Assert.That(cleared.Context.ClearedDockTriplesThisAction, Is.EqualTo(1));
        }

        [Test]
        public void InsertSixMatchingPiecesClearsTwoTriples()
        {
            GameState state = CreateStateWithDock();
            StepResult cleared = RunDockSteps(state, ImmutableArray.Create(
                DebrisType.C,
                DebrisType.C,
                DebrisType.C,
                DebrisType.C,
                DebrisType.C,
                DebrisType.C));

            AssertSlotsEqual(CreateSlots(), cleared.State.Dock.Slots);
            Assert.That(cleared.Context.ClearedDockTriplesThisAction, Is.EqualTo(2));
            Assert.That(cleared.Events, Is.EqualTo(new ActionEvent[]
            {
                new DockCleared(DebrisType.C, 1, 0),
                new DockCleared(DebrisType.C, 1, 0),
            }).AsCollection);
        }

        [Test]
        public void InsertThatTemporarilyExceedsCapacityKeepsOverflowPiecesForClearStep()
        {
            GameState state = CreateStateWithDock(DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E);
            StepResult inserted = Step05_InsertDock.Run(
                state,
                CreateDockContext(state, ImmutableArray.Create(DebrisType.A, DebrisType.A, DebrisType.A)));

            AssertSlotsEqual(CreateSlots(
                DebrisType.A,
                DebrisType.B,
                DebrisType.C,
                DebrisType.D,
                DebrisType.E,
                DebrisType.A,
                DebrisType.A,
                DebrisType.A), inserted.State.Dock.Slots);
            Assert.That(inserted.Context.PendingDockOverflowCount, Is.EqualTo(0));
            Assert.That(inserted.Events, Has.None.TypeOf<DockOverflowTriggered>());
        }

        [Test]
        public void TemporaryOverflowThatClearsTripleDoesNotRemainOverflowAfterCompaction()
        {
            GameState state = CreateStateWithDock(
                DebrisType.A,
                DebrisType.A,
                DebrisType.B,
                DebrisType.C,
                DebrisType.D,
                DebrisType.E);
            StepResult inserted = Step05_InsertDock.Run(
                state,
                CreateDockContext(state, ImmutableArray.Create(DebrisType.A, DebrisType.A)));
            StepResult cleared = Step06_ClearDock.Run(inserted.State, inserted.Context);

            AssertSlotsEqual(CreateSlots(
                DebrisType.B,
                DebrisType.C,
                DebrisType.D,
                DebrisType.E,
                DebrisType.A), cleared.State.Dock.Slots);
            Assert.That(cleared.Context.ClearedDockTriplesThisAction, Is.EqualTo(1));
            Assert.That(cleared.Context.PendingDockOverflowCount, Is.EqualTo(0));
            Assert.That(cleared.Events, Has.None.TypeOf<DockOverflowTriggered>());
        }

        [Test]
        public void PostClearOverflowSetsOverflowFlag()
        {
            GameState state = CreateStateWithDock(
                DebrisType.B,
                DebrisType.C,
                DebrisType.D,
                DebrisType.E,
                DebrisType.B,
                DebrisType.C);
            StepResult inserted = Step05_InsertDock.Run(
                state,
                CreateDockContext(state, ImmutableArray.Create(DebrisType.A, DebrisType.A)));
            StepResult cleared = Step06_ClearDock.Run(inserted.State, inserted.Context);

            AssertSlotsEqual(CreateSlots(
                DebrisType.B,
                DebrisType.C,
                DebrisType.D,
                DebrisType.E,
                DebrisType.B,
                DebrisType.C,
                DebrisType.A,
                DebrisType.A), cleared.State.Dock.Slots);
            Assert.That(cleared.Context.PendingDockOverflowCount, Is.EqualTo(1));
            Assert.That(cleared.Events, Does.Contain(new DockOverflowTriggered(1)));
        }

        [Test]
        public void CompactionAfterClearLeavesNoGaps()
        {
            GameState state = CreateStateWithDock(
                DebrisType.A,
                DebrisType.B,
                DebrisType.B,
                DebrisType.B,
                DebrisType.C,
                null,
                DebrisType.D);
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0)));
            StepResult cleared = Step06_ClearDock.Run(state, context);

            AssertSlotsEqual(CreateSlots(
                DebrisType.A,
                DebrisType.C,
                DebrisType.D), cleared.State.Dock.Slots);
        }

        [Test]
        public void WarningLevelChangesEmitAtThresholds()
        {
            GameState state = CreateStateWithDock(
                DebrisType.A,
                DebrisType.B,
                DebrisType.C,
                DebrisType.D,
                DebrisType.E,
                DebrisType.E,
                DebrisType.E);
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0)));
            StepResult cleared = Step06_ClearDock.Run(state, context);

            Assert.That(cleared.Context.DockWarningBefore, Is.EqualTo(DockWarningLevel.Fail));
            Assert.That(cleared.Context.DockWarningAfter, Is.EqualTo(DockWarningLevel.Safe));
            Assert.That(cleared.Events, Does.Contain(new DockWarningChanged(DockWarningLevel.Fail, DockWarningLevel.Safe)));
        }

        private static StepResult RunDockSteps(GameState state, ImmutableArray<DebrisType> removedDebris)
        {
            StepResult inserted = Step05_InsertDock.Run(state, CreateDockContext(state, removedDebris));
            return Step06_ClearDock.Run(inserted.State, inserted.Context);
        }

        private static StepContext CreateDockContext(GameState state, ImmutableArray<DebrisType> removedDebris)
        {
            return StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                IsValidInput = true,
                RemovedDebris = removedDebris,
            };
        }

        private static GameState CreateStateWithDock(
            DebrisType? slot0 = null,
            DebrisType? slot1 = null,
            DebrisType? slot2 = null,
            DebrisType? slot3 = null,
            DebrisType? slot4 = null,
            DebrisType? slot5 = null,
            DebrisType? slot6 = null)
        {
            return PipelineTestFixtures.CreateState(PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1))) with
            {
                Dock = new Dock(CreateSlots(slot0, slot1, slot2, slot3, slot4, slot5, slot6), Size: 7),
            };
        }

        private static ImmutableArray<DebrisType?> CreateSlots(params DebrisType?[] slots)
        {
            ImmutableArray<DebrisType?>.Builder builder = ImmutableArray.CreateBuilder<DebrisType?>();
            for (int i = 0; i < slots.Length; i++)
            {
                builder.Add(slots[i]);
            }

            while (builder.Count < 7)
            {
                builder.Add(null);
            }

            return builder.ToImmutable();
        }

        private static void AssertSlotsEqual(
            ImmutableArray<DebrisType?> expected,
            ImmutableArray<DebrisType?> actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"Dock slot mismatch at index {i}.");
            }
        }
    }
}
