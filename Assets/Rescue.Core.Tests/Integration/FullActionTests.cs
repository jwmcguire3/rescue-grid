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
                "Step05_InsertDock",
                "Step06_ClearDock",
                "Step07_Gravity",
                "Step08_Spawn",
                "Step09_Extract",
                "Step10_CheckWin",
            }));
            IntegrationTestFixtures.AssertEventTypeOrder(result.Events,
                nameof(GroupRemoved),
                nameof(BlockerDamaged),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(DockCleared),
                nameof(GravitySettled),
                nameof(GravitySettled),
                nameof(GravitySettled),
                nameof(Spawned),
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

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(3, 0)));

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
    }
}
