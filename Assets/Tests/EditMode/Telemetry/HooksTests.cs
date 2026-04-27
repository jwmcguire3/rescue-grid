using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Telemetry;

namespace Rescue.Telemetry.Tests
{
    public sealed class HooksTests
    {
        private string _testDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(
                Path.GetTempPath(),
                "RescueTelemetryHooksTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }

        [Test]
        public void OnLevelStart_EmitsCorrectEvent()
        {
            GameState state = CreateWinState();
            string path = TempPath();

            using (TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults))
            {
                TelemetryHooks.OnLevelStart("L1", 999UL, state, timestampMs: 0, logger);
            }

            List<ITelemetryEvent> events = ReadEvents(path);

            Assert.That(events, Has.Count.EqualTo(1));
            LevelStartEvent ev = (LevelStartEvent)events[0];
            Assert.That(ev.LevelId, Is.EqualTo("L1"));
            Assert.That(ev.Seed, Is.EqualTo(999UL));
            Assert.That(ev.TargetCount, Is.EqualTo(state.Targets.Length));
            Assert.That(ev.VineGrowthThreshold, Is.EqualTo(state.Vine.GrowthThreshold));
            Assert.That(ev.RiseInterval, Is.EqualTo(state.Water.RiseInterval));
            Assert.That(ev.WaterMode, Is.EqualTo(state.LevelConfig.WaterContactMode.ToString()));
        }

        [Test]
        public void OnAction_InvalidInput_EmitsOnlyInvalidTapEvent()
        {
            GameState state = CreateWinState();
            string path = TempPath();

            ActionInput badInput = new ActionInput(new TileCoord(99, 99));
            ActionResult result = Pipeline.RunAction(state, badInput);

            using (TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults))
            {
                TelemetrySessionState session = new TelemetrySessionState { LevelStartMs = 0 };
                TelemetryHooks.OnAction("L1", state, badInput, result, 42UL, 100, 200, session, logger);
            }

            List<ITelemetryEvent> events = ReadEvents(path);

            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0], Is.TypeOf<InvalidTapEvent>());
            Assert.That(((InvalidTapEvent)events[0]).Reason, Is.EqualTo(InvalidTapReasons.OutOfBounds));
        }

        [Test]
        public void OnAction_ValidWinningAction_EmitsExpectedEventTypes()
        {
            GameState state = CreateWinState();
            string path = TempPath();

            ActionInput input = new ActionInput(new TileCoord(3, 0));
            ActionResult result = Pipeline.RunAction(state, input);

            using (TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults))
            {
                TelemetrySessionState session = new TelemetrySessionState { LevelStartMs = 0 };
                TelemetryHooks.OnAction("L1", state, input, result, 42UL, 1000, 1200, session, logger);
            }

            List<ITelemetryEvent> events = ReadEvents(path);

            Assert.That(events, Has.Some.TypeOf<TimeToFirstActionEvent>(), "Must emit time_to_first_action on first action.");
            Assert.That(events, Has.Some.TypeOf<ActionTakenEvent>(), "Must emit action_taken.");
            Assert.That(events, Has.Some.TypeOf<DockOccupancyEvent>(), "Must emit dock_occupancy.");
            Assert.That(events, Has.Some.TypeOf<TargetExtractedEvent>(), "Must emit target_extracted.");
            Assert.That(events, Has.Some.TypeOf<LevelWinEvent>(), "Must emit level_win.");
        }

        [Test]
        public void OnAction_TimeToFirstAction_UsesLevelStartMs()
        {
            GameState state = CreateWinState();
            string path = TempPath();

            ActionInput input = new ActionInput(new TileCoord(3, 0));
            ActionResult result = Pipeline.RunAction(state, input);

            using (TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults))
            {
                TelemetrySessionState session = new TelemetrySessionState { LevelStartMs = 5000 };
                TelemetryHooks.OnAction("L1", state, input, result, 42UL,
                    actionStartMs: 8000, actionEndMs: 8100, session, logger);
            }

            List<ITelemetryEvent> events = ReadEvents(path);

            TimeToFirstActionEvent? ev = FindEvent<TimeToFirstActionEvent>(events);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev!.FirstActionMs, Is.EqualTo(8000 - 5000));
        }

        [Test]
        public void OnAction_SecondAction_EmitsIdleTimeWithCorrectMs()
        {
            GameState state = CreateTwoActionState();
            string path = TempPath();

            ActionInput input1 = new ActionInput(new TileCoord(2, 2));
            ActionResult result1 = Pipeline.RunAction(state, input1);

            ActionInput input2 = new ActionInput(new TileCoord(3, 1));
            ActionResult result2 = Pipeline.RunAction(result1.State, input2);

            using (TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults))
            {
                TelemetrySessionState session = new TelemetrySessionState { LevelStartMs = 0 };
                TelemetryHooks.OnAction("L1", state, input1, result1, 42UL,
                    actionStartMs: 1000, actionEndMs: 1200, session, logger);
                TelemetryHooks.OnAction("L1", result1.State, input2, result2, 42UL,
                    actionStartMs: 3000, actionEndMs: 3100, session, logger);
            }

            List<ITelemetryEvent> events = ReadEvents(path);

            IdleTimeEvent? idleEv = FindEvent<IdleTimeEvent>(events);
            Assert.That(idleEv, Is.Not.Null, "idle_time must be emitted on the second action.");
            Assert.That(idleEv!.IdleMs, Is.EqualTo(3000 - 1200));
        }

        [Test]
        public void OnAction_HazardProximityToTarget_EmittedPerUnextractedTarget()
        {
            GameState state = CreateStateWithUnextractedTarget();
            string path = TempPath();

            ActionInput input = new ActionInput(new TileCoord(2, 0));
            ActionResult result = Pipeline.RunAction(state, input);

            Assume.That(result.Events.Length, Is.GreaterThan(0));
            Assume.That(result.Events[0], Is.Not.TypeOf<InvalidInput>(), "Tap must be valid.");

            using (TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults))
            {
                TelemetrySessionState session = new TelemetrySessionState { LevelStartMs = 0 };
                TelemetryHooks.OnAction("L1", state, input, result, 1UL, 0, 100, session, logger);
            }

            List<ITelemetryEvent> events = ReadEvents(path);

            HazardProximityToTargetEvent? proximity = FindEvent<HazardProximityToTargetEvent>(events);
            Assert.That(proximity, Is.Not.Null, "hazard_proximity_to_target must be emitted for each unextracted target.");
            Assert.That(proximity!.TargetId, Is.EqualTo("t0"));
            // Target at row 0, board height 3, 0 flooded rows → lastDryRow = 3-0-1 = 2.
            // waterDistanceRows = 0 - 2 = -2.
            Assert.That(proximity.WaterDistanceRows, Is.EqualTo(-2));
            Assert.That(proximity.VineAdjacency, Is.False);
        }

        [Test]
        public void OnAction_ActionTaken_ContainsRngStatesAndSeed()
        {
            GameState state = CreateWinState();
            string path = TempPath();

            RngState rngBefore = state.RngState;
            ActionInput input = new ActionInput(new TileCoord(3, 0));
            ActionResult result = Pipeline.RunAction(state, input);

            using (TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults))
            {
                TelemetrySessionState session = new TelemetrySessionState { LevelStartMs = 0 };
                TelemetryHooks.OnAction("L1", state, input, result, 42UL, 0, 100, session, logger);
            }

            List<ITelemetryEvent> events = ReadEvents(path);

            ActionTakenEvent? ev = FindEvent<ActionTakenEvent>(events);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev!.RngStateBefore, Is.EqualTo(rngBefore));
            Assert.That(ev.RngStateAfter, Is.EqualTo(result.State.RngState));
            Assert.That(ev.Seed, Is.EqualTo(42UL));
            Assert.That(ev.UndoAvailable, Is.EqualTo(state.UndoAvailable));
            Assert.That(ev.Input, Is.EqualTo(input.TappedCoord));
        }

        [Test]
        public void OnAction_ActionTaken_ReplayProducesSameActionCount()
        {
            GameState initialState = CreateTwoActionState();
            string path = TempPath();

            ActionInput input1 = new ActionInput(new TileCoord(2, 2));
            ActionResult result1 = Pipeline.RunAction(initialState, input1);

            ActionInput input2 = new ActionInput(new TileCoord(3, 1));
            ActionResult result2 = Pipeline.RunAction(result1.State, input2);

            using (TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults))
            {
                TelemetrySessionState session = new TelemetrySessionState { LevelStartMs = 0 };
                TelemetryHooks.OnAction("L1", initialState, input1, result1, 1UL, 0, 100, session, logger);
                TelemetryHooks.OnAction("L1", result1.State, input2, result2, 1UL, 200, 300, session, logger);
            }

            List<ITelemetryEvent> allEvents = ReadEvents(path);
            List<ActionTakenEvent> actionEvents = new List<ActionTakenEvent>();
            foreach (ITelemetryEvent e in allEvents)
            {
                if (e is ActionTakenEvent at) actionEvents.Add(at);
            }

            Assert.That(actionEvents, Has.Count.EqualTo(2));

            // Replay from identical initial state
            GameState replayState = initialState;
            ActionResult? replayFinal = null;
            foreach (ActionTakenEvent ev in actionEvents)
            {
                replayFinal = Pipeline.RunAction(replayState, new ActionInput(ev.Input));
                replayState = replayFinal.State;
            }

            Assert.That(replayFinal, Is.Not.Null);
            Assert.That(replayState.ActionCount, Is.EqualTo(result2.State.ActionCount));
            Assert.That(replayFinal!.Outcome, Is.EqualTo(result2.Outcome));
        }

        [Test]
        public void OnAction_DockLoss_EmitsLevelLossWithDockOverflowReason()
        {
            GameState state = CreateDockOverflowLossState();
            string path = TempPath();

            ActionInput input = new ActionInput(new TileCoord(0, 0));
            ActionResult result = Pipeline.RunAction(state, input);

            using (TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults))
            {
                TelemetrySessionState session = new TelemetrySessionState { LevelStartMs = 0 };
                TelemetryHooks.OnAction("L1", state, input, result, 1UL, 0, 100, session, logger);
            }

            List<ITelemetryEvent> events = ReadEvents(path);

            LevelLossEvent? lossEv = FindEvent<LevelLossEvent>(events);
            Assert.That(lossEv, Is.Not.Null, "LevelLossEvent must be emitted on dock overflow loss.");
            Assert.That(lossEv!.Reason, Is.EqualTo(LossReasons.DockOverflow));
        }

        [Test]
        public void OnAction_DistressedEventsAndExpiredLoss_EmitTelemetry()
        {
            GameState state = CreateDistressedExpiredLossState();
            string path = TempPath();

            ActionInput input = new ActionInput(new TileCoord(0, 0));
            ActionResult result = Pipeline.RunAction(state, input);

            using (TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults))
            {
                TelemetrySessionState session = new TelemetrySessionState { LevelStartMs = 0 };
                TelemetryHooks.OnAction("L1", state, input, result, 1UL, 0, 100, session, logger);
            }

            List<ITelemetryEvent> events = ReadEvents(path);

            TargetDistressedEvent? distressed = FindEvent<TargetDistressedEvent>(events);
            LevelLossEvent? loss = FindEvent<LevelLossEvent>(events);
            TargetLostEvent? targetLost = FindEvent<TargetLostEvent>(events);

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.LossDistressedExpired));
            Assert.That(distressed, Is.Not.Null);
            Assert.That(distressed!.Transition, Is.EqualTo("expired"));
            Assert.That(loss, Is.Not.Null);
            Assert.That(loss!.Reason, Is.EqualTo(LossReasons.DistressedExpired));
            Assert.That(targetLost, Is.Not.Null);
            Assert.That(targetLost!.TargetId, Is.EqualTo("pup"));
        }

        [Test]
        public void OnLevelAbandoned_EmitsLossWithManualAbandonReason()
        {
            string path = TempPath();

            using (TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults))
            {
                TelemetryHooks.OnLevelAbandoned("L1", actionCount: 5, timestampMs: 9000, logger);
            }

            List<ITelemetryEvent> events = ReadEvents(path);

            Assert.That(events, Has.Count.EqualTo(1));
            LevelLossEvent ev = (LevelLossEvent)events[0];
            Assert.That(ev.Reason, Is.EqualTo(LossReasons.ManualAbandon));
            Assert.That(ev.ActionCount, Is.EqualTo(5));
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private string TempPath() =>
            Path.Combine(_testDir, Guid.NewGuid().ToString("N") + ".jsonl");

        private static List<ITelemetryEvent> ReadEvents(string path)
        {
            List<ITelemetryEvent> result = new List<ITelemetryEvent>();
            if (!File.Exists(path)) return result;

            string[] lines = File.ReadAllLines(path);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                ITelemetryEvent? ev = JsonSerializer.Deserialize<ITelemetryEvent>(
                    line, TelemetryJsonConverter.OuterOptions);
                if (ev is not null) result.Add(ev);
            }

            return result;
        }

        private static T? FindEvent<T>(List<ITelemetryEvent> events) where T : class, ITelemetryEvent
        {
            foreach (ITelemetryEvent ev in events)
            {
                if (ev is T typed) return typed;
            }

            return null;
        }

        // ── state fixtures ────────────────────────────────────────────────────

        private static Dock EmptyDock() =>
            new Dock(
                ImmutableArray.Create<DebrisType?>(null, null, null, null, null, null, null),
                Size: 7);

        private static VineState NoVine() =>
            new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null);

        private static LevelConfig SimpleConfig() =>
            new LevelConfig(
                ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D),
                null, 0.0d, 2);

        // 4x4 board. Tapping (3,0) removes 3x A in row 3 and wins (target at (3,3)).
        private static GameState CreateWinState()
        {
            ImmutableArray<Tile> row0 = ImmutableArray.Create<Tile>(
                new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.C),
                new DebrisTile(DebrisType.D), new DebrisTile(DebrisType.E));
            ImmutableArray<Tile> row1 = ImmutableArray.Create<Tile>(
                new EmptyTile(), new EmptyTile(),
                new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null));
            ImmutableArray<Tile> row2 = ImmutableArray.Create<Tile>(
                new EmptyTile(), new EmptyTile(),
                new BlockerTile(BlockerType.Crate, 2, null), new EmptyTile());
            ImmutableArray<Tile> row3 = ImmutableArray.Create<Tile>(
                new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A),
                new DebrisTile(DebrisType.A), new TargetTile("pup", Extracted: false));

            Board board = new Board(4, 4, ImmutableArray.Create(row0, row1, row2, row3));

            return new GameState(
                Board: board,
                Dock: EmptyDock(),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 10, RiseInterval: 10),
                Vine: NoVine(),
                Targets: ImmutableArray.Create(new TargetState("pup", new TileCoord(3, 3), false, false)),
                LevelConfig: SimpleConfig(),
                RngState: new RngState(123u, 456u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }

        // Two-action win: first tap (2,2) then (3,1).
        private static GameState CreateTwoActionState()
        {
            ImmutableArray<Tile> row0 = ImmutableArray.Create<Tile>(
                new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.C),
                new DebrisTile(DebrisType.D), new DebrisTile(DebrisType.E));
            ImmutableArray<Tile> row1 = ImmutableArray.Create<Tile>(
                new EmptyTile(), new EmptyTile(),
                new BlockerTile(BlockerType.Crate, 2, null), new BlockerTile(BlockerType.Crate, 2, null));
            ImmutableArray<Tile> row2 = ImmutableArray.Create<Tile>(
                new EmptyTile(), new EmptyTile(),
                new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.B));
            ImmutableArray<Tile> row3 = ImmutableArray.Create<Tile>(
                new EmptyTile(), new DebrisTile(DebrisType.A),
                new DebrisTile(DebrisType.A), new TargetTile("pup", Extracted: false));

            Board board = new Board(4, 4, ImmutableArray.Create(row0, row1, row2, row3));

            return new GameState(
                Board: board,
                Dock: EmptyDock(),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 10, RiseInterval: 10),
                Vine: NoVine(),
                Targets: ImmutableArray.Create(new TargetState("pup", new TileCoord(3, 3), false, false)),
                LevelConfig: SimpleConfig(),
                RngState: new RngState(123u, 456u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }

        // 3x3 board with target at (0,2), one blocked neighbor, and group of A's at row 2.
        // waterDistanceRows for target: targetRow(0) - (height(3) - floodedRows(0) - 1) = 0-2 = -2.
        private static GameState CreateStateWithUnextractedTarget()
        {
            ImmutableArray<Tile> row0 = ImmutableArray.Create<Tile>(
                new EmptyTile(), new EmptyTile(), new TargetTile("t0", Extracted: false));
            ImmutableArray<Tile> row1 = ImmutableArray.Create<Tile>(
                new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 2, null));
            ImmutableArray<Tile> row2 = ImmutableArray.Create<Tile>(
                new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile());

            Board board = new Board(3, 3, ImmutableArray.Create(row0, row1, row2));

            return new GameState(
                Board: board,
                Dock: EmptyDock(),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 10, RiseInterval: 10),
                Vine: NoVine(),
                Targets: ImmutableArray.Create(new TargetState("t0", new TileCoord(0, 2), false, false)),
                LevelConfig: SimpleConfig(),
                RngState: new RngState(1u, 2u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }

        // Dock at 6/7, group of 2 A's → overflow → LossDockOverflow.
        private static GameState CreateDockOverflowLossState()
        {
            ImmutableArray<Tile> row0 = ImmutableArray.Create<Tile>(
                new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A));
            ImmutableArray<Tile> row1 = ImmutableArray.Create<Tile>(
                new DebrisTile(DebrisType.B), new TargetTile("pup", Extracted: false));

            Board board = new Board(2, 2, ImmutableArray.Create(row0, row1));

            return new GameState(
                Board: board,
                Dock: new Dock(
                    ImmutableArray.Create<DebrisType?>(
                        DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E,
                        DebrisType.B, DebrisType.C, null),
                    Size: 7),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 10, RiseInterval: 10),
                Vine: NoVine(),
                Targets: ImmutableArray.Create(new TargetState("pup", new TileCoord(1, 1), false, false)),
                LevelConfig: SimpleConfig(),
                RngState: new RngState(1u, 2u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }

        private static GameState CreateDistressedExpiredLossState()
        {
            ImmutableArray<Tile> row0 = ImmutableArray.Create<Tile>(
                new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A));
            ImmutableArray<Tile> row1 = ImmutableArray.Create<Tile>(
                new TargetTile("pup", Extracted: false), new BlockerTile(BlockerType.Crate, 2, Hidden: null));

            Board board = new Board(2, 2, ImmutableArray.Create(row0, row1));

            return new GameState(
                Board: board,
                Dock: EmptyDock(),
                Water: new WaterState(FloodedRows: 1, ActionsUntilRise: 10, RiseInterval: 10),
                Vine: NoVine(),
                Targets: ImmutableArray.Create(new TargetState("pup", new TileCoord(1, 0), TargetReadiness.Distressed)),
                LevelConfig: SimpleConfig() with { WaterContactMode = WaterContactMode.OneTickGrace },
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
