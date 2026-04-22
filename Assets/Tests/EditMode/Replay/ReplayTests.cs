using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using Rescue.Content;
using Rescue.Core.Pipeline;
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
