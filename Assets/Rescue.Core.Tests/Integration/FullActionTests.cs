using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Integration
{
    public sealed class FullActionTests
    {
        [Test]
        public void FullActionOnSingleTargetLevelFollowsStablePipelineEventOrder()
        {
            GameState state = IntegrationTestFixtures.CreateSingleActionWinState();

            List<string> trace = new List<string>();
            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(
                state,
                new ActionInput(new TileCoord(3, 0)),
                options: null,
                observer: step => trace.Add(step.StepName));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(trace, Is.EqualTo(new[]
            {
                "Step01_AcceptInput",
                "Step02_RemoveGroup",
                "Step03_DamageBlockers",
                "Step04_ResolveBreaks",
                "Step05_UpdateTargets",
                "Step06_InsertDock",
                "Step07_ClearDock",
                "Step08_Extract",
                "Step09_CheckWin",
            }));
            IntegrationTestFixtures.AssertEventTypeOrder(result.Events,
                nameof(GroupRemoved),
                nameof(BlockerDamaged),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(DockCleared),
                nameof(TargetExtracted),
                nameof(Won));
        }

        [Test]
        public void LastMoveRescueWinsBeforeWaterWouldFloodTargetRow()
        {
            GameState state = IntegrationTestFixtures.CreateSingleActionWinState() with
            {
                Water = new WaterState(FloodedRows: 0, ActionsUntilRise: 1, RiseInterval: 3),
            };

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(3, 1)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(result.Events, Has.Some.TypeOf<Won>());
            Assert.That(result.Events, Has.None.TypeOf<WaterRose>());
            Assert.That(result.Events, Has.None.TypeOf<Lost>());
        }

        [Test]
        public void LosingMoveWithDockOverflowEmitsLostWithDockReason()
        {
            GameState state = IntegrationTestFixtures.CreateDockOverflowLossState();

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(0, 0)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.LossDockOverflow));
            Assert.That(result.Events, Has.Some.TypeOf<DockOverflowTriggered>());
            Assert.That(result.Events, Has.None.TypeOf<TargetExtracted>());
            Assert.That(result.Events[^1], Is.EqualTo(new Lost(ActionOutcome.LossDockOverflow)));
        }

        [Test]
        public void TemporaryDockOverflowThatClearsTripleDoesNotLose()
        {
            GameState state = IntegrationTestFixtures.CreateTemporaryDockOverflowClearState();

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(0, 0)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Ok));
            Assert.That(result.State.Frozen, Is.False);
            Assert.That(DockHelpers.Occupancy(result.State.Dock), Is.EqualTo(5));
            Assert.That(result.Events, Has.Some.TypeOf<DockCleared>());
            Assert.That(result.Events, Has.None.TypeOf<DockOverflowTriggered>());
            Assert.That(result.Events, Has.None.TypeOf<Lost>());
        }

        [Test]
        public void TargetOneClearAwayFiresOnActionBeforeExtraction()
        {
            GameState state = IntegrationTestFixtures.CreateTwoBeatRescueState();

            ActionResult firstAction = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(2, 2)));
            ActionResult secondAction = Rescue.Core.Pipeline.Pipeline.RunAction(firstAction.State, new ActionInput(new TileCoord(3, 1)));

            Assert.That(firstAction.Outcome, Is.EqualTo(ActionOutcome.Ok));
            Assert.That(firstAction.Events, Has.Some.EqualTo(new TargetOneClearAway("pup", new TileCoord(3, 3))));
            Assert.That(firstAction.Events, Has.None.TypeOf<TargetExtracted>());
            Assert.That(firstAction.Events, Has.None.TypeOf<Won>());

            Assert.That(secondAction.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(secondAction.Events, Has.Some.EqualTo(new TargetExtracted("pup", new TileCoord(3, 3))));
            Assert.That(secondAction.Events, Has.Some.TypeOf<Won>());
        }

        [Test]
        public void RescueActionEmitsTargetExtractedBeforeGravityAndSpawn()
        {
            GameState state = IntegrationTestFixtures.CreateNonFinalExtractionState();

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(3, 1)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Ok));
            IntegrationTestFixtures.AssertEventAppearsBefore<TargetExtracted, GravitySettled>(result.Events);
            IntegrationTestFixtures.AssertEventAppearsBefore<TargetExtracted, Spawned>(result.Events);
        }

        [Test]
        public void GravityAndSpawnCannotPreventExtractionAfterLatch()
        {
            GameState state = IntegrationTestFixtures.CreateNonFinalExtractionState();

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(3, 1)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Ok));
            Assert.That(result.State.Targets[0].Extracted, Is.True);
            Assert.That(result.Events, Has.Some.EqualTo(new TargetExtracted("pup", new TileCoord(3, 3))));
            Assert.That(result.Events, Has.Some.TypeOf<GravitySettled>());
            Assert.That(result.Events, Has.Some.TypeOf<Spawned>());
        }

        [Test]
        public void FinalRescueWinsEvenIfSameActionWouldOverflowDock()
        {
            GameState state = IntegrationTestFixtures.CreateFinalRescueWithDockOverflowState();

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(3, 1)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(result.Events, Has.Some.TypeOf<DockOverflowTriggered>());
            Assert.That(result.Events, Has.Some.TypeOf<TargetExtracted>());
            Assert.That(result.Events, Has.Some.TypeOf<Won>());
            Assert.That(result.Events, Has.None.TypeOf<Lost>());
        }

        [Test]
        public void NonFinalRescuePlusDockOverflowFailsBeforeGravitySpawnAndHazards()
        {
            GameState state = IntegrationTestFixtures.CreateNonFinalRescueWithDockOverflowState();

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(3, 1)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.LossDockOverflow));
            Assert.That(result.Events, Has.Some.TypeOf<TargetExtracted>());
            Assert.That(result.Events, Has.Some.TypeOf<DockOverflowTriggered>());
            Assert.That(result.Events, Has.Some.EqualTo(new Lost(ActionOutcome.LossDockOverflow)));
            Assert.That(result.Events, Has.None.TypeOf<GravitySettled>());
            Assert.That(result.Events, Has.None.TypeOf<Spawned>());
            Assert.That(result.Events, Has.None.TypeOf<WaterRose>());
            Assert.That(result.Events, Has.None.TypeOf<VineGrown>());
        }

        [Test]
        public void FinalRescueSkipsHazardTickAndWaterRise()
        {
            GameState state = IntegrationTestFixtures.CreateFinalRescueWithDockOverflowState() with
            {
                Water = new WaterState(FloodedRows: 0, ActionsUntilRise: 1, RiseInterval: 3),
                Vine = new VineState(
                    ActionsSinceLastClear: 3,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray.Create(new TileCoord(0, 0)),
                    PriorityCursor: 0,
                    PendingGrowthTile: new TileCoord(0, 0)),
            };

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(3, 1)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(result.State.Water.ActionsUntilRise, Is.EqualTo(1));
            Assert.That(result.State.Vine.ActionsSinceLastClear, Is.EqualTo(3));
            Assert.That(result.Events, Has.None.TypeOf<WaterRose>());
            Assert.That(result.Events, Has.None.TypeOf<VineGrown>());
            Assert.That(result.Events, Has.None.TypeOf<Lost>());
        }

        [Test]
        public void WonEventIncludesFinalTargetIdTotalActionsAndExtractionOrder()
        {
            GameState state = IntegrationTestFixtures.CreateSingleActionWinState() with
            {
                ActionCount = 4,
                Targets = ImmutableArray.Create(
                    new TargetState("alpha", new TileCoord(0, 0), Extracted: true, OneClearAway: false),
                    new TargetState("bravo", new TileCoord(3, 3), Extracted: false, OneClearAway: false)),
                ExtractedTargetOrder = ImmutableArray.Create("alpha"),
            };

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(3, 0)));
            Won won = IntegrationTestFixtures.FindEvent<Won>(result.Events);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(won.FinalExtractedTargetId, Is.EqualTo("bravo"));
            Assert.That(won.TotalActions, Is.EqualTo(5));
            Assert.That(won.ExtractedTargetOrder, Is.EqualTo(new[] { "alpha", "bravo" }).AsCollection);
        }
    }

    internal static class IntegrationTestFixtures
    {
        public static GameState CreateSingleActionWinState()
        {
            return PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.D), new DebrisTile(DebrisType.E)),
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, Hidden: null)),
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, Hidden: null), new EmptyTile()),
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new TargetTile("pup", Extracted: false))),
                targets: ImmutableArray.Create(new TargetState("pup", new TileCoord(3, 3), Extracted: false, OneClearAway: false)),
                actionCount: 0)
                with
                {
                    LevelConfig = PipelineTestFixtures.CreateLevelConfig(0.0d, null, DebrisType.A),
                };
        }

        public static GameState CreateDockOverflowLossState()
        {
            return PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A)),
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.B), new TargetTile("pup", Extracted: false))),
                targets: ImmutableArray.Create(new TargetState("pup", new TileCoord(1, 1), Extracted: false, OneClearAway: false)))
                with
                {
                    Dock = new Dock(
                        ImmutableArray.Create<DebrisType?>(
                            DebrisType.B,
                            DebrisType.C,
                            DebrisType.D,
                            DebrisType.E,
                            DebrisType.B,
                            DebrisType.C,
                            null),
                        Size: 7),
                    LevelConfig = PipelineTestFixtures.CreateLevelConfig(0.0d, null, DebrisType.A),
                };
        }

        public static GameState CreateTemporaryDockOverflowClearState()
        {
            return PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile(), new EmptyTile()),
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.C), new BlockerTile(BlockerType.Crate, 2, Hidden: null), new EmptyTile()),
                    PipelineTestFixtures.Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, Hidden: null), new TargetTile("pup", Extracted: false), new EmptyTile()),
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile())),
                targets: ImmutableArray.Create(new TargetState("pup", new TileCoord(2, 2), Extracted: false, OneClearAway: false)))
                with
                {
                    Dock = new Dock(
                        ImmutableArray.Create<DebrisType?>(
                            DebrisType.A,
                            DebrisType.A,
                            DebrisType.B,
                            DebrisType.C,
                            DebrisType.D,
                            DebrisType.E,
                            null),
                        Size: 7),
                    LevelConfig = PipelineTestFixtures.CreateLevelConfig(0.0d, null, DebrisType.A),
                };
        }

        public static GameState CreateTwoBeatRescueState()
        {
            return PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.D), new DebrisTile(DebrisType.E)),
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, Hidden: null), new BlockerTile(BlockerType.Crate, 2, Hidden: null)),
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.B)),
                    PipelineTestFixtures.Row(new EmptyTile(), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new TargetTile("pup", Extracted: false))),
                targets: ImmutableArray.Create(new TargetState("pup", new TileCoord(3, 3), Extracted: false, OneClearAway: false)))
                with
                {
                    LevelConfig = PipelineTestFixtures.CreateLevelConfig(0.0d, null, DebrisType.A),
                };
        }

        public static GameState CreateNonFinalExtractionState()
        {
            return PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.D), new DebrisTile(DebrisType.E)),
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.C), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    PipelineTestFixtures.Row(new EmptyTile(), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new TargetTile("pup", Extracted: false)),
                    PipelineTestFixtures.Row(new TargetTile("hold", Extracted: false), new BlockerTile(BlockerType.Crate, 2, Hidden: null), new BlockerTile(BlockerType.Crate, 2, Hidden: null), new EmptyTile())),
                targets: ImmutableArray.Create(
                    new TargetState("pup", new TileCoord(3, 3), Extracted: false, OneClearAway: true),
                    new TargetState("hold", new TileCoord(4, 0), Extracted: false, OneClearAway: false)))
                with
                {
                    LevelConfig = PipelineTestFixtures.CreateLevelConfig(0.0d, null, DebrisType.A),
                };
        }

        public static GameState CreateFinalRescueWithDockOverflowState()
        {
            return PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.D), new DebrisTile(DebrisType.E)),
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.C), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    PipelineTestFixtures.Row(new EmptyTile(), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new TargetTile("pup", Extracted: false))),
                targets: ImmutableArray.Create(new TargetState("pup", new TileCoord(3, 3), Extracted: false, OneClearAway: true)))
                with
            {
                Dock = OverflowDock(),
                LevelConfig = PipelineTestFixtures.CreateLevelConfig(0.0d, null, DebrisType.A),
            };
        }

        public static GameState CreateNonFinalRescueWithDockOverflowState()
        {
            return CreateNonFinalExtractionState() with
            {
                Dock = OverflowDock(),
            };
        }

        private static Dock OverflowDock()
        {
            return new Dock(
                ImmutableArray.Create<DebrisType?>(
                    DebrisType.B,
                    DebrisType.C,
                    DebrisType.D,
                    DebrisType.E,
                    DebrisType.B,
                    DebrisType.C,
                    null),
                Size: 7);
        }

        public static T FindEvent<T>(ImmutableArray<ActionEvent> events)
            where T : ActionEvent
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i] is T typed)
                {
                    return typed;
                }
            }

            Assert.Fail($"Expected event of type {typeof(T).Name}.");
            throw new AssertionException($"Expected event of type {typeof(T).Name}.");
        }

        public static void AssertEventTypeOrder(ImmutableArray<ActionEvent> events, params string[] expectedTypes)
        {
            Assert.That(events.Length, Is.EqualTo(expectedTypes.Length));
            for (int i = 0; i < expectedTypes.Length; i++)
            {
                Assert.That(events[i].GetType().Name, Is.EqualTo(expectedTypes[i]), $"Unexpected event type at index {i}.");
            }
        }

        public static void AssertEventAppearsBefore<TBefore, TAfter>(ImmutableArray<ActionEvent> events)
            where TBefore : ActionEvent
            where TAfter : ActionEvent
        {
            int beforeIndex = IndexOf<TBefore>(events);
            int afterIndex = IndexOf<TAfter>(events);
            Assert.That(beforeIndex, Is.GreaterThanOrEqualTo(0), $"Expected {typeof(TBefore).Name} event.");
            Assert.That(afterIndex, Is.GreaterThanOrEqualTo(0), $"Expected {typeof(TAfter).Name} event.");
            Assert.That(beforeIndex, Is.LessThan(afterIndex));
        }

        private static int IndexOf<T>(ImmutableArray<ActionEvent> events)
            where T : ActionEvent
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i] is T)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
