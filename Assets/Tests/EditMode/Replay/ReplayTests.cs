using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Replay;
using Rescue.Telemetry;

namespace Rescue.Replay.Tests
{
    public sealed class ReplayTests
    {
        private string _testDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(
                Path.GetTempPath(),
                "RescueReplayTests_" + Guid.NewGuid().ToString("N"));
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
        public void ReplaySession_ReproducesScriptedTelemetryTrajectory()
        {
            LevelJson level = CreateTwoActionReplayLevel();
            int seed = 12345;
            ActionInput[] script =
            {
                new ActionInput(new TileCoord(2, 2)),
                new ActionInput(new TileCoord(3, 1)),
            };

            ScriptedRun run = WriteScriptedSession(level, seed, script, TempPath("session.jsonl"));

            ReplayResult replay = ReplayRunner.ReplaySession(
                run.SessionPath,
                (_, replaySeed) => Loader.LoadLevel(level, replaySeed));
            ReplayResult expected = BuildExpectedReplay(run, replay.SessionEvents);

            Assert.That(replay.Verified, Is.True);
            Assert.That(ReplayRunner.CompareTrajectories(expected, replay), Is.Empty);
        }

        [Test]
        public void CompareTrajectories_ReportsFirstDivergenceAfterActionMutation()
        {
            LevelJson level = CreateTwoActionReplayLevel();
            int seed = 12345;
            ActionInput[] script =
            {
                new ActionInput(new TileCoord(2, 2)),
                new ActionInput(new TileCoord(3, 1)),
            };

            ScriptedRun run = WriteScriptedSession(level, seed, script, TempPath("session.jsonl"));
            ReplayResult originalReplay = ReplayRunner.ReplaySession(
                run.SessionPath,
                (_, replaySeed) => Loader.LoadLevel(level, replaySeed));

            string mutatedPath = TempPath("mutated.jsonl");
            WriteMutatedSession(run.SessionPath, mutatedPath);

            ReplayResult mutatedReplay = ReplayRunner.ReplaySession(
                mutatedPath,
                (_, replaySeed) => Loader.LoadLevel(level, replaySeed));
            ImmutableArray<TrajectoryDiff> diffs = ReplayRunner.CompareTrajectories(originalReplay, mutatedReplay);

            Assert.That(diffs, Has.Length.EqualTo(1));
            Assert.That(diffs[0].Kind, Is.EqualTo(TrajectoryDiffKind.InputMismatch));
            Assert.That(diffs[0].FrameIndex, Is.EqualTo(1));
        }

        [Test]
        public void CompareTrajectories_FingerprintIncludesTargetReadiness()
        {
            GameState expectedState = CreateManualState(
                targets: ImmutableArray.Create(
                    new TargetState("target", new TileCoord(1, 1), TargetReadiness.OneClearAway)));
            GameState actualState = expectedState with
            {
                Targets = ImmutableArray.Create(
                    new TargetState("target", new TileCoord(1, 1), TargetReadiness.Distressed)),
            };

            ImmutableArray<TrajectoryDiff> diffs = ReplayRunner.CompareTrajectories(
                CreateSingleFrameReplay(expectedState),
                CreateSingleFrameReplay(actualState));

            Assert.That(diffs, Has.Length.EqualTo(1));
            Assert.That(diffs[0].Kind, Is.EqualTo(TrajectoryDiffKind.StateMismatch));
            Assert.That(diffs[0].Expected, Does.Contain("OneClearAway"));
            Assert.That(diffs[0].Actual, Does.Contain("Distressed"));
        }

        [Test]
        public void CompareTrajectories_FingerprintIncludesWaterContactMode()
        {
            GameState expectedState = CreateManualState(
                waterContactMode: WaterContactMode.ImmediateLoss);
            GameState actualState = expectedState with
            {
                LevelConfig = expectedState.LevelConfig with
                {
                    WaterContactMode = WaterContactMode.OneTickGrace,
                },
            };

            ImmutableArray<TrajectoryDiff> diffs = ReplayRunner.CompareTrajectories(
                CreateSingleFrameReplay(expectedState),
                CreateSingleFrameReplay(actualState));

            Assert.That(diffs, Has.Length.EqualTo(1));
            Assert.That(diffs[0].Kind, Is.EqualTo(TrajectoryDiffKind.StateMismatch));
            Assert.That(diffs[0].Expected, Does.Contain("waterMode=ImmediateLoss"));
            Assert.That(diffs[0].Actual, Does.Contain("waterMode=OneTickGrace"));
        }

        [Test]
        public void ReplaySession_ReproducesFinalRescueOverflowAction()
        {
            GameState initialState = CreateManualState(
                board: CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new TargetTile("target", Extracted: false), new DebrisTile(DebrisType.D), new DebrisTile(DebrisType.D))),
                dock: new Dock(
                    ImmutableArray.Create<DebrisType?>(
                        DebrisType.A,
                        DebrisType.A,
                        DebrisType.B,
                        DebrisType.B,
                        DebrisType.C,
                        DebrisType.C,
                        null),
                    Size: 7),
                targets: ImmutableArray.Create(
                    new TargetState("target", new TileCoord(2, 0), TargetReadiness.OneClearAway)));
            ActionInput[] script =
            {
                new ActionInput(new TileCoord(2, 1)),
            };
            string sessionPath = TempPath("final-rescue-overflow.jsonl");

            ScriptedRun run = WriteStateSession("LFinalRescueOverflow", initialState, seed: 99, script, sessionPath);
            ReplayResult replay = ReplayRunner.ReplaySession(sessionPath, (_, _) => initialState);
            ReplayResult expected = BuildExpectedReplay(run, replay.SessionEvents);

            Assert.That(replay.Verified, Is.True);
            Assert.That(ReplayRunner.CompareTrajectories(expected, replay), Is.Empty);
            Assert.That(replay.FinalFrame.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(replay.FinalFrame.Events, Has.Some.Matches<ActionEvent>(e =>
                e is DockInserted dockInserted
                && dockInserted.Pieces.Length == 1
                && dockInserted.Pieces[0] == DebrisType.D
                && dockInserted.OccupancyAfterInsert == 8
                && dockInserted.OverflowCount == 1));
            Assert.That(replay.FinalFrame.Events, Has.Some.TypeOf<TargetExtracted>());
            Assert.That(replay.FinalFrame.Events, Has.Some.TypeOf<Won>());
            Assert.That(replay.FinalFrame.Events, Has.None.TypeOf<Lost>());
        }

        [Test]
        public void ReplaySession_ReproducesAssistedSpawnDeterministicState()
        {
            GameState initialState = CreateManualState(
                board: CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A)),
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.D))),
                targets: ImmutableArray<TargetState>.Empty,
                debugSpawnOverride: new SpawnOverride(ForceEmergency: true, OverrideAssistanceChance: 1.0d),
                assistanceChance: 1.0d,
                consecutiveEmergencyCap: 10);
            ActionInput[] script =
            {
                new ActionInput(new TileCoord(0, 0)),
            };
            string sessionPath = TempPath("assisted-spawn.jsonl");

            ScriptedRun run = WriteStateSession("LAssistedSpawn", initialState, seed: 100, script, sessionPath);
            ReplayResult replay = ReplayRunner.ReplaySession(sessionPath, (_, _) => initialState);
            ReplayResult expected = BuildExpectedReplay(run, replay.SessionEvents);

            Assert.That(replay.Verified, Is.True);
            Assert.That(ReplayRunner.CompareTrajectories(expected, replay), Is.Empty);
            Assert.That(replay.FinalFrame.Events, Has.Some.TypeOf<DebugSpawnOverrideApplied>());
            Assert.That(replay.FinalFrame.Events, Has.Some.TypeOf<Spawned>());
            Assert.That(replay.FinalFrame.State.ConsecutiveEmergencySpawns, Is.GreaterThan(0));
            Assert.That(replay.FinalFrame.State.RngState, Is.EqualTo(run.Frames[^1].State.RngState));
        }

        private string TempPath(string fileName) => Path.Combine(_testDir, fileName);

        private static ScriptedRun WriteScriptedSession(LevelJson level, int seed, IReadOnlyList<ActionInput> script, string sessionPath)
        {
            List<ReplayFrame> frames = new List<ReplayFrame>();
            List<ActionTakenEvent> actionEvents = new List<ActionTakenEvent>();
            GameState current = Loader.LoadLevel(level, seed);
            long timestampMs = 1000;

            frames.Add(new ReplayFrame(
                FrameIndex: 0,
                ActionIndex: 0,
                Input: null,
                State: current,
                Outcome: null,
                Events: ImmutableArray<ActionEvent>.Empty,
                RngStateVerified: null));

            using (TelemetryLogger logger = new TelemetryLogger(sessionPath, TelemetryConfig.DevDefaults))
            {
                TelemetrySessionState session = new TelemetrySessionState { LevelStartMs = 0 };
                TelemetryHooks.OnLevelStart(level.Id, (ulong)(uint)seed, current, timestampMs, logger);

                for (int i = 0; i < script.Count; i++)
                {
                    ActionInput input = script[i];
                    GameState before = current;
                    ActionResult result = Pipeline.RunAction(current, input);
                    timestampMs += 100;
                    TelemetryHooks.OnAction(level.Id, before, input, result, (ulong)(uint)seed, timestampMs - 50, timestampMs, session, logger);

                    current = result.State;
                    frames.Add(new ReplayFrame(
                        FrameIndex: i + 1,
                        ActionIndex: result.State.ActionCount,
                        Input: input,
                        State: result.State,
                        Outcome: result.Outcome,
                        Events: result.Events,
                        RngStateVerified: true));

                    actionEvents.Add(FindActionTakenEvent(ReadEvents(sessionPath), i + 1));
                }
            }

            return new ScriptedRun(level.Id, seed, sessionPath, frames.ToImmutableArray(), actionEvents.ToImmutableArray());
        }

        private static ScriptedRun WriteStateSession(
            string levelId,
            GameState initialState,
            int seed,
            IReadOnlyList<ActionInput> script,
            string sessionPath)
        {
            List<ReplayFrame> frames = new List<ReplayFrame>();
            List<ActionTakenEvent> actionEvents = new List<ActionTakenEvent>();
            GameState current = initialState;
            long timestampMs = 1000;

            frames.Add(new ReplayFrame(
                FrameIndex: 0,
                ActionIndex: 0,
                Input: null,
                State: current,
                Outcome: null,
                Events: ImmutableArray<ActionEvent>.Empty,
                RngStateVerified: null));

            using (TelemetryLogger logger = new TelemetryLogger(sessionPath, TelemetryConfig.DevDefaults))
            {
                TelemetrySessionState session = new TelemetrySessionState { LevelStartMs = 0 };
                TelemetryHooks.OnLevelStart(levelId, (ulong)(uint)seed, current, timestampMs, logger);

                for (int i = 0; i < script.Count; i++)
                {
                    ActionInput input = script[i];
                    GameState before = current;
                    ActionResult result = Pipeline.RunAction(current, input);
                    timestampMs += 100;
                    TelemetryHooks.OnAction(levelId, before, input, result, (ulong)(uint)seed, timestampMs - 50, timestampMs, session, logger);

                    current = result.State;
                    frames.Add(new ReplayFrame(
                        FrameIndex: i + 1,
                        ActionIndex: result.State.ActionCount,
                        Input: input,
                        State: result.State,
                        Outcome: result.Outcome,
                        Events: result.Events,
                        RngStateVerified: true));

                    actionEvents.Add(FindActionTakenEvent(ReadEvents(sessionPath), i + 1));
                }
            }

            return new ScriptedRun(levelId, seed, sessionPath, frames.ToImmutableArray(), actionEvents.ToImmutableArray());
        }

        private static ReplayResult BuildExpectedReplay(ScriptedRun run, ImmutableArray<ITelemetryEvent> sessionEvents)
        {
            return new ReplayResult(run.SessionPath, run.LevelId, run.Seed, sessionEvents, run.Frames);
        }

        private static void WriteMutatedSession(string sourcePath, string destinationPath)
        {
            List<ITelemetryEvent> events = ReadEvents(sourcePath);
            bool mutated = false;
            using StreamWriter writer = new StreamWriter(destinationPath);
            for (int i = 0; i < events.Count; i++)
            {
                ITelemetryEvent current = events[i];
                if (!mutated && current is ActionTakenEvent actionTaken)
                {
                    current = actionTaken with { Input = new TileCoord(99, 99) };
                    mutated = true;
                }

                writer.WriteLine(JsonSerializer.Serialize(
                    current,
                    typeof(ITelemetryEvent),
                    TelemetryJsonConverter.OuterOptions));
            }
        }

        private static List<ITelemetryEvent> ReadEvents(string path)
        {
            List<ITelemetryEvent> events = new List<ITelemetryEvent>();
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                ITelemetryEvent? telemetryEvent = JsonSerializer.Deserialize<ITelemetryEvent>(
                    lines[i],
                    TelemetryJsonConverter.OuterOptions);
                if (telemetryEvent is not null)
                {
                    events.Add(telemetryEvent);
                }
            }

            return events;
        }

        private static ActionTakenEvent FindActionTakenEvent(List<ITelemetryEvent> events, int actionIndex)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] is ActionTakenEvent action && action.ActionIndex == actionIndex)
                {
                    return action;
                }
            }

            throw new InvalidOperationException($"No action_taken event was found for action index {actionIndex}.");
        }

        private static ReplayResult CreateSingleFrameReplay(GameState state)
        {
            return new ReplayResult(
                "manual.jsonl",
                "LManual",
                1,
                ImmutableArray<ITelemetryEvent>.Empty,
                ImmutableArray.Create(new ReplayFrame(
                    FrameIndex: 0,
                    ActionIndex: 0,
                    Input: null,
                    State: state,
                    Outcome: null,
                    Events: ImmutableArray<ActionEvent>.Empty,
                    RngStateVerified: null)));
        }

        private static GameState CreateManualState(
            Board? board = null,
            Dock? dock = null,
            ImmutableArray<TargetState>? targets = null,
            WaterContactMode waterContactMode = WaterContactMode.ImmediateLoss,
            SpawnOverride? debugSpawnOverride = null,
            double assistanceChance = 0.0d,
            int consecutiveEmergencyCap = 2)
        {
            Board resolvedBoard = board ?? CreateBoard(
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                Row(new EmptyTile(), new TargetTile("target", Extracted: false), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile()));
            ImmutableArray<TargetState> resolvedTargets = targets ?? ImmutableArray.Create(
                new TargetState("target", new TileCoord(1, 1), TargetReadiness.Trapped));

            return new GameState(
                Board: resolvedBoard,
                Dock: dock ?? new Dock(
                    ImmutableArray.Create<DebrisType?>(null, null, null, null, null, null, null),
                    Size: 7),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
                Vine: new VineState(
                    ActionsSinceLastClear: 0,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null),
                Targets: resolvedTargets,
                LevelConfig: new LevelConfig(
                    DebrisTypePool: ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E),
                    BaseDistribution: null,
                    AssistanceChance: assistanceChance,
                    ConsecutiveEmergencyCap: consecutiveEmergencyCap,
                    WaterContactMode: waterContactMode),
                RngState: new RngState(123u, 456u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0,
                DockJamEnabled: false,
                DockJamActive: false,
                DebugSpawnOverride: debugSpawnOverride);
        }

        private static Board CreateBoard(params ImmutableArray<Tile>[] rows)
        {
            return new Board(rows[0].Length, rows.Length, rows.ToImmutableArray());
        }

        private static ImmutableArray<Tile> Row(params Tile[] tiles)
        {
            return tiles.ToImmutableArray();
        }

        private static LevelJson CreateTwoActionReplayLevel()
        {
            return new LevelJson
            {
                Id = "LReplay",
                Name = "Replay Test",
                Board = new BoardJson
                {
                    Width = 4,
                    Height = 4,
                    Tiles = new[]
                    {
                        new[] { "A", "A", ".", "." },
                        new[] { ".", ".", "CR", "CR" },
                        new[] { ".", ".", "B", "B" },
                        new[] { ".", "C", "C", "T0" },
                    },
                },
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E },
                Targets = new[] { new TargetJson { Id = "0", Row = 3, Col = 3 } },
                InitialFloodedRows = 0,
                Water = new WaterJson { RiseInterval = 10 },
                Vine = new VineJson
                {
                    GrowthThreshold = 4,
                    GrowthPriority = Array.Empty<TileCoordJson>(),
                },
                Dock = new DockJson
                {
                    Size = 7,
                    JamEnabled = false,
                },
                Assistance = new AssistanceJson
                {
                    Chance = 0.5d,
                    ConsecutiveEmergencyCap = 2,
                },
                Meta = new MetaJson
                {
                    Intent = "Replay regression.",
                    ExpectedPath = "Take two deterministic actions.",
                    ExpectedFailMode = "Mutated telemetry diverges.",
                    WhatItProves = "Replay reproduces state-by-state trajectory.",
                    IsRuleTeach = false,
                },
            };
        }

        private sealed class ScriptedRun
        {
            public ScriptedRun(
                string levelId,
                int seed,
                string sessionPath,
                ImmutableArray<ReplayFrame> frames,
                ImmutableArray<ActionTakenEvent> actionEvents)
            {
                LevelId = levelId;
                Seed = seed;
                SessionPath = sessionPath;
                Frames = frames;
                ActionEvents = actionEvents;
            }

            public string LevelId { get; }

            public int Seed { get; }

            public string SessionPath { get; }

            public ImmutableArray<ReplayFrame> Frames { get; }

            public ImmutableArray<ActionTakenEvent> ActionEvents { get; }
        }
    }
}
