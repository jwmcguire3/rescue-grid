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
    public sealed class FairnessReproductionTests
    {
        private string _testDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(
                Path.GetTempPath(),
                "RescueFairnessReplayTests_" + Guid.NewGuid().ToString("N"));
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
        public void SavedWaterLossLog_ReplaysTheExactFailure()
        {
            LevelJson level = CreateWaterLossLevel();
            int seed = 777;
            ActionInput[] script =
            {
                new ActionInput(new TileCoord(0, 0)),
            };

            string sessionPath = Path.Combine(_testDir, "water-loss-session.jsonl");
            WriteSession(level, seed, script, sessionPath);

            ReplayResult replay = ReplayRunner.ReplaySession(
                sessionPath,
                (_, replaySeed) => Loader.LoadLevel(level, replaySeed));

            Assert.That(replay.Verified, Is.True);
            Assert.That(replay.FinalFrame.Outcome, Is.EqualTo(ActionOutcome.LossWaterOnTarget));
        }

        [TestCase(WaterContactMode.ImmediateLoss, ActionOutcome.LossWaterOnTarget)]
        [TestCase(WaterContactMode.OneTickGrace, ActionOutcome.LossDistressedExpired)]
        public void WaterModeReplay_ReproducesConfiguredContactMode(
            WaterContactMode waterContactMode,
            ActionOutcome expectedOutcome)
        {
            LevelJson level = CreateWaterModeReplayLevel(waterContactMode);
            int seed = 778;
            ActionInput[] script = waterContactMode == WaterContactMode.ImmediateLoss
                ? new[] { new ActionInput(new TileCoord(0, 0)) }
                : new[]
                {
                    new ActionInput(new TileCoord(0, 0)),
                    new ActionInput(new TileCoord(1, 0)),
                };

            string sessionPath = Path.Combine(_testDir, $"water-mode-{waterContactMode}.jsonl");
            WriteSession(level, seed, script, sessionPath);

            ReplayResult replay = ReplayRunner.ReplaySession(
                sessionPath,
                (_, replaySeed) => Loader.LoadLevel(level, replaySeed));

            Assert.That(replay.Verified, Is.True);
            Assert.That(replay.InitialFrame.State.LevelConfig.WaterContactMode, Is.EqualTo(waterContactMode));
            Assert.That(replay.FinalFrame.Outcome, Is.EqualTo(expectedOutcome));
        }

        [Test]
        public void CaptureOnLossDump_PreservesEnoughDataToReplayIdenticalFailure()
        {
            LevelJson level = CreateWaterLossLevel();
            int seed = 888;
            ActionInput[] script =
            {
                new ActionInput(new TileCoord(0, 0)),
            };

            string sessionPath = Path.Combine(_testDir, "captured-session.jsonl");
            WriteSession(level, seed, script, sessionPath);

            ReplayResult originalReplay = ReplayRunner.ReplaySession(
                sessionPath,
                (_, replaySeed) => Loader.LoadLevel(level, replaySeed));

            string capturedPath = ReplayRunner.CaptureLossSession(
                sessionPath,
                level.Id,
                seed,
                Path.Combine(_testDir, "losses"),
                new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero),
                retentionCap: 5);

            ReplayResult capturedReplay = ReplayRunner.ReplaySession(
                capturedPath,
                (_, replaySeed) => Loader.LoadLevel(level, replaySeed));
            ImmutableArray<TrajectoryDiff> diffs = ReplayRunner.CompareTrajectories(originalReplay, capturedReplay);

            Assert.That(File.Exists(capturedPath), Is.True);
            Assert.That(capturedReplay.Verified, Is.True);
            Assert.That(capturedReplay.FinalFrame.Outcome, Is.EqualTo(ActionOutcome.LossWaterOnTarget));
            Assert.That(diffs, Is.Empty);
        }

        private static void WriteSession(LevelJson level, int seed, IReadOnlyList<ActionInput> script, string sessionPath)
        {
            GameState current = Loader.LoadLevel(level, seed);
            long timestampMs = 1000;

            using TelemetryLogger logger = new TelemetryLogger(sessionPath, TelemetryConfig.DevDefaults);
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
            }
        }

        private static LevelJson CreateWaterLossLevel()
        {
            return new LevelJson
            {
                Id = "LWaterLoss",
                Name = "Water Loss Replay",
                Board = new BoardJson
                {
                    Width = 3,
                    Height = 3,
                    Tiles = new[]
                    {
                        new[] { "A", "A", "." },
                        new[] { ".", ".", "." },
                        new[] { "B", "T0", "B" },
                    },
                },
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D },
                Targets = new[] { new TargetJson { Id = "0", Row = 2, Col = 1 } },
                InitialFloodedRows = 0,
                Water = new WaterJson { RiseInterval = 1 },
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
                    Intent = "Capture a water loss for fairness reproduction.",
                    ExpectedPath = "Clear the opening pair and flood the bottom row.",
                    ExpectedFailMode = "Immediate water loss.",
                    WhatItProves = "Loss dumps carry enough data for exact replay.",
                    IsRuleTeach = false,
                },
            };
        }

        private static LevelJson CreateWaterModeReplayLevel(WaterContactMode waterContactMode)
        {
            return new LevelJson
            {
                Id = "LWaterMode",
                Name = "Water Mode Replay",
                Board = new BoardJson
                {
                    Width = 3,
                    Height = 3,
                    Tiles = new[]
                    {
                        new[] { "A", "A", "." },
                        new[] { "C", "C", "B" },
                        new[] { ".", ".", "T0" },
                    },
                },
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D },
                Targets = new[] { new TargetJson { Id = "0", Row = 2, Col = 2 } },
                InitialFloodedRows = 0,
                Water = new WaterJson
                {
                    RiseInterval = 1,
                    ContactMode = waterContactMode,
                },
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
                    Intent = "Replay both water contact modes.",
                    ExpectedPath = "Immediate loses on first contact; grace expires on the second unresolved action.",
                    ExpectedFailMode = "Configured water mode is not replayed.",
                    WhatItProves = "Replay keeps water mode in the authoritative loaded state.",
                    IsRuleTeach = false,
                },
            };
        }
    }
}
