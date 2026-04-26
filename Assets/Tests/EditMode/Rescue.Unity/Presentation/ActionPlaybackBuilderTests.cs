using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class ActionPlaybackBuilderTests
    {
        [Test]
        public void Build_NormalActionEndsWithFinalSync()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))),
                    new DockInserted(ImmutableArray.Create(DebrisType.A, DebrisType.A), OccupancyAfterInsert: 2, OverflowCount: 0)));

            Assert.That(plan.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(plan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
        }

        [Test]
        public void Build_GravityComesBeforeSpawn()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0)))),
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.B)))));

            Assert.That(IndexOf(plan, ActionPlaybackStepType.Gravity), Is.LessThan(IndexOf(plan, ActionPlaybackStepType.Spawn)));
        }

        [Test]
        public void Build_WaterRiseComesAfterGravityAndSpawn()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 1), new TileCoord(1, 1)))),
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 1), DebrisType.C))),
                    new WaterRose(FloodedRow: 3)));

            int gravityIndex = IndexOf(plan, ActionPlaybackStepType.Gravity);
            int spawnIndex = IndexOf(plan, ActionPlaybackStepType.Spawn);
            int waterRiseIndex = IndexOf(plan, ActionPlaybackStepType.WaterRise);

            Assert.That(waterRiseIndex, Is.GreaterThan(gravityIndex));
            Assert.That(waterRiseIndex, Is.GreaterThan(spawnIndex));
        }

        [Test]
        public void Build_EmptyEventsStillProduceSafeFinalSync()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(1, 1)),
                CreateResult());

            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.That(plan[0].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
            Assert.That(plan[0].SourceEvent, Is.Null);
        }

        [Test]
        public void Build_PreservesExistingEventOrderAsMuchAsPossible()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution),
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))),
                    new TargetExtracted("pup-1", new TileCoord(2, 1))));

            Assert.That(plan.Select(step => step.StepType), Is.EqualTo(new[]
            {
                ActionPlaybackStepType.DockFeedback,
                ActionPlaybackStepType.RemoveGroup,
                ActionPlaybackStepType.TargetExtract,
                ActionPlaybackStepType.FinalSync,
            }));
        }

        [Test]
        public void Build_MapsExistingActionEventNamesWithoutInventingCoreEvents()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))),
                    new BlockerDamaged(new TileCoord(0, 2), BlockerType.Crate, RemainingHp: 0),
                    new BlockerBroken(new TileCoord(0, 2), BlockerType.Crate),
                    new IceRevealed(new TileCoord(1, 0), DebrisType.B),
                    new DockInserted(ImmutableArray.Create(DebrisType.A), OccupancyAfterInsert: 1, OverflowCount: 0),
                    new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 0),
                    new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution),
                    new DockJamTriggered(OverflowCount: 1),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0)))),
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C))),
                    new TargetExtracted("pup-1", new TileCoord(2, 1)),
                    new WaterRose(FloodedRow: 4)));

            Assert.That(plan.Take(plan.Count - 1).Select(step => (step.SourceEventName, step.StepType)), Is.EqualTo(new[]
            {
                ("GroupRemoved", ActionPlaybackStepType.RemoveGroup),
                ("BlockerDamaged", ActionPlaybackStepType.BreakBlockerOrReveal),
                ("BlockerBroken", ActionPlaybackStepType.BreakBlockerOrReveal),
                ("IceRevealed", ActionPlaybackStepType.BreakBlockerOrReveal),
                ("DockInserted", ActionPlaybackStepType.DockFeedback),
                ("DockCleared", ActionPlaybackStepType.DockFeedback),
                ("DockWarningChanged", ActionPlaybackStepType.DockFeedback),
                ("DockJamTriggered", ActionPlaybackStepType.DockFeedback),
                ("GravitySettled", ActionPlaybackStepType.Gravity),
                ("Spawned", ActionPlaybackStepType.Spawn),
                ("TargetExtracted", ActionPlaybackStepType.TargetExtract),
                ("WaterRose", ActionPlaybackStepType.WaterRise),
            }));
        }

        private static int IndexOf(ActionPlaybackPlan plan, ActionPlaybackStepType stepType)
        {
            for (int i = 0; i < plan.Count; i++)
            {
                if (plan[i].StepType == stepType)
                {
                    return i;
                }
            }

            Assert.Fail($"Expected step type {stepType}.");
            return -1;
        }

        private static ActionResult CreateResult(params ActionEvent[] events)
        {
            return new ActionResult(
                CreateState(),
                ImmutableArray.CreateRange(events),
                ActionOutcome.Ok,
                Snapshot: null);
        }

        private static GameState CreateState()
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.B),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile(),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new TargetTile("pup-1", Extracted: false),
                    new EmptyTile()));

            CoreBoard board = new CoreBoard(3, 3, rows);

            return new GameState(
                Board: board,
                Dock: new CoreDock(
                    ImmutableArray.Create<DebrisType?>(
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null),
                    Size: 7),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
                Vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                Targets: ImmutableArray.Create(new TargetState("pup-1", new TileCoord(2, 1), Extracted: false, OneClearAway: false)),
                LevelConfig: new LevelConfig(
                    ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C),
                    null,
                    0.0d,
                    2),
                RngState: new RngState(1u, 2u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }
    }
}
