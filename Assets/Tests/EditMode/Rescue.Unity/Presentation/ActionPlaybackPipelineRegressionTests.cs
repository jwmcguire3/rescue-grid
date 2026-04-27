using System.Collections.Generic;
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
    public sealed class ActionPlaybackPipelineRegressionTests
    {
        [Test]
        public void PipelinePlayback_NormalRemoveGravitySpawnActionPreservesCanonicalOrder()
        {
            GameState state = CreateState(
                Rows(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new DebrisTile(DebrisType.B), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new TargetTile("hold", Extracted: false))),
                targets: HeldTarget());

            AssertPipelinePlan(
                state,
                new TileCoord(0, 0),
                ActionOutcome.Ok,
                nameof(GroupRemoved),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(GravitySettled),
                nameof(Spawned));
        }

        [Test]
        public void PipelinePlayback_BlockerDamageAndBreakActionPreservesCanonicalOrder()
        {
            GameState state = CreateState(
                Rows(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Crate, 1, null), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new TargetTile("hold", Extracted: false))),
                targets: HeldTarget());

            AssertPipelinePlan(
                state,
                new TileCoord(0, 0),
                ActionOutcome.Ok,
                nameof(GroupRemoved),
                nameof(BlockerDamaged),
                nameof(BlockerBroken),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(Spawned));
        }

        [Test]
        public void PipelinePlayback_IceRevealActionPreservesCanonicalOrder()
        {
            GameState state = CreateState(
                Rows(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.B)), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new TargetTile("hold", Extracted: false))),
                targets: HeldTarget());

            AssertPipelinePlan(
                state,
                new TileCoord(0, 0),
                ActionOutcome.Ok,
                nameof(GroupRemoved),
                nameof(BlockerDamaged),
                nameof(BlockerBroken),
                nameof(IceRevealed),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(GravitySettled),
                nameof(Spawned));
        }

        [Test]
        public void PipelinePlayback_TargetExtractionActionPreservesCanonicalOrder()
        {
            GameState state = CreateState(
                Rows(
                    Row(new BlockerTile(BlockerType.Crate, 2, null), new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.B)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new BlockerTile(BlockerType.Crate, 2, null)),
                    Row(new TargetTile("pup-1", Extracted: false), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A))),
                targets: ImmutableArray.Create(new TargetState("pup-1", new TileCoord(2, 0), Extracted: false, OneClearAway: true)));

            AssertPipelinePlan(
                state,
                new TileCoord(2, 1),
                ActionOutcome.Win,
                nameof(GroupRemoved),
                nameof(BlockerDamaged),
                nameof(BlockerDamaged),
                nameof(TargetExtractionLatched),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(TargetExtracted),
                nameof(Won));
        }

        [Test]
        public void PipelinePlayback_WaterRiseActionPreservesCanonicalOrder()
        {
            GameState state = CreateState(
                Rows(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new EmptyTile())),
                water: new WaterState(FloodedRows: 0, ActionsUntilRise: 1, RiseInterval: 3),
                targets: ImmutableArray.Create(new TargetState("hold", new TileCoord(0, 2), Extracted: false, OneClearAway: false)));

            AssertPipelinePlan(
                state,
                new TileCoord(0, 0),
                ActionOutcome.Ok,
                nameof(GroupRemoved),
                nameof(TargetOneClearAway),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(Spawned),
                nameof(WaterRose));
        }

        [Test]
        public void PipelinePlayback_DockInsertAndClearActionPreservesCanonicalOrder()
        {
            GameState state = CreateState(
                Rows(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new TargetTile("hold", Extracted: false))),
                dockSlots: DockSlots(DebrisType.A, DebrisType.A, null, null, null, null, null),
                targets: HeldTarget());

            AssertPipelinePlan(
                state,
                new TileCoord(0, 0),
                ActionOutcome.Ok,
                nameof(GroupRemoved),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(DockCleared),
                nameof(Spawned));
        }

        [Test]
        public void PipelinePlayback_DockJamTriggeredActionPreservesCanonicalOrder()
        {
            GameState state = CreateState(
                Rows(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new TargetTile("hold", Extracted: false))),
                dockSlots: DockSlots(DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E, DebrisType.B, DebrisType.C, null),
                targets: HeldTarget(),
                dockJamEnabled: true);

            AssertPipelinePlan(
                state,
                new TileCoord(0, 0),
                ActionOutcome.Ok,
                nameof(GroupRemoved),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(DockOverflowTriggered),
                nameof(DockWarningChanged),
                nameof(DockJamTriggered));
        }

        [Test]
        public void PipelinePlayback_WinActionStopsBeforeHazardPlayback()
        {
            GameState state = CreateState(
                Rows(
                    Row(new BlockerTile(BlockerType.Crate, 2, null), new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.B)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new BlockerTile(BlockerType.Crate, 2, null)),
                    Row(new TargetTile("pup-1", Extracted: false), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A))),
                water: new WaterState(FloodedRows: 0, ActionsUntilRise: 1, RiseInterval: 3),
                targets: ImmutableArray.Create(new TargetState("pup-1", new TileCoord(2, 0), Extracted: false, OneClearAway: true)));

            ActionResult result = Pipeline.RunAction(state, new ActionInput(new TileCoord(2, 1)));
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(state, new ActionInput(new TileCoord(2, 1)), result);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(result.Events, Has.Some.TypeOf<Won>());
            Assert.That(result.Events, Has.None.TypeOf<WaterRose>());
            Assert.That(PlaybackEventNames(plan), Is.EqualTo(new[]
            {
                nameof(GroupRemoved),
                nameof(BlockerDamaged),
                nameof(BlockerDamaged),
                nameof(TargetExtractionLatched),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(TargetExtracted),
                nameof(Won),
            }));
            Assert.That(plan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
        }

        [Test]
        public void PipelinePlayback_LossActionPreservesReadablePreLossFeedback()
        {
            GameState state = CreateState(
                Rows(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new TargetTile("hold", Extracted: false))),
                dockSlots: DockSlots(DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E, DebrisType.B, DebrisType.C, null),
                targets: HeldTarget());

            AssertPipelinePlan(
                state,
                new TileCoord(0, 0),
                ActionOutcome.LossDockOverflow,
                nameof(GroupRemoved),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(DockOverflowTriggered),
                nameof(DockWarningChanged),
                nameof(Lost));
        }

        [Test]
        public void PipelinePlayback_WaterLossActionPreservesWaterRiseBeforeFinalSync()
        {
            GameState state = CreateState(
                Rows(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new TargetTile("pup-1", Extracted: false))),
                water: new WaterState(FloodedRows: 0, ActionsUntilRise: 1, RiseInterval: 3),
                targets: ImmutableArray.Create(new TargetState("pup-1", new TileCoord(2, 2), Extracted: false, OneClearAway: false)));

            AssertPipelinePlan(
                state,
                new TileCoord(0, 0),
                ActionOutcome.LossWaterOnTarget,
                nameof(GroupRemoved),
                nameof(DockInserted),
                nameof(DockInserted),
                nameof(Spawned),
                nameof(WaterRose),
                nameof(Lost));
        }

        [Test]
        public void PipelinePlayback_AllSupportedRealEventStreamsPreserveCanonicalMappedOrder()
        {
            foreach (Scenario scenario in Scenarios())
            {
                ActionResult result = Pipeline.RunAction(scenario.State, new ActionInput(scenario.Input));
                ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(scenario.State, new ActionInput(scenario.Input), result);

                Assert.That(result.Outcome, Is.EqualTo(scenario.ExpectedOutcome), scenario.Name);
                Assert.That(
                    plan.Take(plan.Count - 1).Select(step => step.StepType),
                    Is.EqualTo(ExpectedPlaybackStepTypes(result.Events)),
                    scenario.Name);
                Assert.That(plan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync), scenario.Name);
            }
        }

        private static void AssertPipelinePlan(
            GameState state,
            TileCoord input,
            ActionOutcome expectedOutcome,
            params string[] expectedPlaybackEventNames)
        {
            ActionInput actionInput = new ActionInput(input);
            ActionResult result = Pipeline.RunAction(state, actionInput);
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(state, actionInput, result);

            Assert.That(result.Outcome, Is.EqualTo(expectedOutcome), string.Join(", ", result.Events.Select(ev => ev.GetType().Name)));
            Assert.That(PlaybackEventNames(plan), Is.EqualTo(expectedPlaybackEventNames));
            Assert.That(plan.Take(plan.Count - 1).Select(step => step.StepType), Is.EqualTo(ExpectedPlaybackStepTypes(result.Events)));
            Assert.That(plan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
        }

        private static IEnumerable<ActionPlaybackStepType> ExpectedPlaybackStepTypes(ImmutableArray<ActionEvent> events)
        {
            foreach (ActionEvent actionEvent in events)
            {
                ActionPlaybackStepType? stepType = actionEvent switch
                {
                    GroupRemoved => ActionPlaybackStepType.RemoveGroup,
                    BlockerDamaged => ActionPlaybackStepType.BreakBlockerOrReveal,
                    BlockerBroken => ActionPlaybackStepType.BreakBlockerOrReveal,
                    IceRevealed => ActionPlaybackStepType.BreakBlockerOrReveal,
                    TargetProgressed => ActionPlaybackStepType.TargetReaction,
                    TargetOneClearAway => ActionPlaybackStepType.TargetReaction,
                    TargetExtractionLatched => ActionPlaybackStepType.TargetLatch,
                    DockInserted => ActionPlaybackStepType.DockFeedback,
                    DockCleared => ActionPlaybackStepType.DockFeedback,
                    DockOverflowTriggered => ActionPlaybackStepType.DockOverflow,
                    DockWarningChanged => ActionPlaybackStepType.DockFeedback,
                    DockJamTriggered => ActionPlaybackStepType.DockFeedback,
                    GravitySettled => ActionPlaybackStepType.Gravity,
                    Spawned => ActionPlaybackStepType.Spawn,
                    WaterWarning => ActionPlaybackStepType.WaterWarning,
                    VinePreviewChanged => ActionPlaybackStepType.VinePreview,
                    VineGrown => ActionPlaybackStepType.VineGrowth,
                    TargetExtracted => ActionPlaybackStepType.TargetExtract,
                    WaterRose => ActionPlaybackStepType.WaterRise,
                    Won => ActionPlaybackStepType.TerminalOutcome,
                    Lost => ActionPlaybackStepType.TerminalOutcome,
                    _ => null,
                };

                if (stepType.HasValue)
                {
                    yield return stepType.Value;
                }

                if (actionEvent is Won or Lost)
                {
                    yield break;
                }
            }
        }

        private static string[] PlaybackEventNames(ActionPlaybackPlan plan)
        {
            return plan
                .Take(plan.Count - 1)
                .Select(step => step.SourceEventName!)
                .ToArray();
        }

        private static ImmutableArray<ImmutableArray<Tile>> Rows(params ImmutableArray<Tile>[] rows)
        {
            return ImmutableArray.Create(rows);
        }

        private static ImmutableArray<Tile> Row(params Tile[] tiles)
        {
            return ImmutableArray.Create(tiles);
        }

        private static ImmutableArray<DebrisType?> DockSlots(params DebrisType?[] slots)
        {
            return ImmutableArray.Create(slots);
        }

        private static ImmutableArray<TargetState> HeldTarget()
        {
            return ImmutableArray.Create(new TargetState("hold", new TileCoord(2, 2), Extracted: false, OneClearAway: false));
        }

        private static GameState CreateState(
            ImmutableArray<ImmutableArray<Tile>> rows,
            ImmutableArray<DebrisType?>? dockSlots = null,
            WaterState? water = null,
            ImmutableArray<TargetState>? targets = null,
            bool dockJamEnabled = false)
        {
            int height = rows.Length;
            int width = height > 0 ? rows[0].Length : 0;
            CoreBoard board = new CoreBoard(width, height, rows);

            return new GameState(
                Board: board,
                Dock: new CoreDock(
                    dockSlots ?? DockSlots(null, null, null, null, null, null, null),
                    Size: 7),
                Water: water ?? new WaterState(FloodedRows: 0, ActionsUntilRise: 10, RiseInterval: 10),
                Vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                Targets: targets ?? ImmutableArray<TargetState>.Empty,
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
                SpawnRecoveryCounter: 0,
                DockJamEnabled: dockJamEnabled);
        }

        private static IEnumerable<Scenario> Scenarios()
        {
            yield return new Scenario(
                "normal",
                CreateState(
                    Rows(
                        Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                        Row(new DebrisTile(DebrisType.B), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                        Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new TargetTile("hold", Extracted: false))),
                    targets: HeldTarget()),
                new TileCoord(0, 0),
                ActionOutcome.Ok);

            yield return new Scenario(
                "ice",
                CreateState(
                    Rows(
                        Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                        Row(new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.B)), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                        Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new TargetTile("hold", Extracted: false))),
                    targets: HeldTarget()),
                new TileCoord(0, 0),
                ActionOutcome.Ok);

            yield return new Scenario(
                "dock clear",
                CreateState(
                    Rows(
                        Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                        Row(new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                        Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new TargetTile("hold", Extracted: false))),
                    dockSlots: DockSlots(DebrisType.A, DebrisType.A, null, null, null, null, null),
                    targets: HeldTarget()),
                new TileCoord(0, 0),
                ActionOutcome.Ok);

            yield return new Scenario(
                "water loss",
                CreateState(
                    Rows(
                        Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                        Row(new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                        Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new TargetTile("pup-1", Extracted: false))),
                    water: new WaterState(FloodedRows: 0, ActionsUntilRise: 1, RiseInterval: 3),
                    targets: ImmutableArray.Create(new TargetState("pup-1", new TileCoord(2, 2), Extracted: false, OneClearAway: false))),
                new TileCoord(0, 0),
                ActionOutcome.LossWaterOnTarget);

            yield return new Scenario(
                "dock jam",
                CreateState(
                    Rows(
                        Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                        Row(new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null)),
                        Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null), new TargetTile("hold", Extracted: false))),
                    dockSlots: DockSlots(DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E, DebrisType.B, DebrisType.C, null),
                    targets: HeldTarget(),
                    dockJamEnabled: true),
                new TileCoord(0, 0),
                ActionOutcome.Ok);
        }

        private readonly struct Scenario
        {
            public Scenario(string name, GameState state, TileCoord input, ActionOutcome expectedOutcome)
            {
                Name = name;
                State = state;
                Input = input;
                ExpectedOutcome = expectedOutcome;
            }

            public string Name { get; }

            public GameState State { get; }

            public TileCoord Input { get; }

            public ActionOutcome ExpectedOutcome { get; }
        }
    }
}
