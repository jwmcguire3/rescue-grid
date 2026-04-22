using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Telemetry;

namespace Rescue.Telemetry.Tests
{
    public sealed class LoggerTests
    {
        private string _testDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "RescueTelemetryTests_" + Guid.NewGuid().ToString("N"));
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
        public void Logger_AppendsValidJsonlOneEventPerLine()
        {
            string path = Path.Combine(_testDir, "session.jsonl");
            using TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults);

            logger.Append(new LevelStartEvent("L1", 0, 42UL, 0.7, 12, 0, 4, 1));
            logger.Append(new LevelWinEvent("L1", 100, 5, new[] { "dog0" }, false));

            string[] lines = File.ReadAllLines(path);
            Assert.That(lines, Has.Length.EqualTo(2), "One line per event.");

            foreach (string line in lines)
            {
                Assert.DoesNotThrow(
                    () => JsonDocument.Parse(line),
                    $"Every line must be valid JSON: {line}");
            }
        }

        [Test]
        public void Logger_CreatesOutputDirectoryIfMissing()
        {
            string nested = Path.Combine(_testDir, "nested", "deep");
            string path = Path.Combine(nested, "session.jsonl");

            using TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults);
            logger.Append(new LevelWinEvent("L1", 0, 1, new[] { "t0" }, false));

            Assert.That(File.Exists(path), Is.True);
        }

        [Test]
        [TestCaseSource(nameof(AllEventTypes))]
        public void Logger_RoundTripsAllEventTypesViaJson(ITelemetryEvent original)
        {
            string path = Path.Combine(_testDir, Guid.NewGuid().ToString("N") + ".jsonl");
            using TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults);

            logger.Append(original);

            string line = File.ReadAllText(path).Trim();
            ITelemetryEvent? roundTripped = JsonSerializer.Deserialize<ITelemetryEvent>(
                line,
                TelemetryJsonConverter.OuterOptions);

            Assert.That(roundTripped, Is.Not.Null);
            Assert.That(roundTripped!.EventType, Is.EqualTo(original.EventType));
            Assert.That(roundTripped.SchemaVersion, Is.EqualTo(1));
            Assert.That(roundTripped.LevelId, Is.EqualTo(original.LevelId));
        }

        [Test]
        public void Logger_IsThreadSafeUnderConcurrentAppend()
        {
            string path = Path.Combine(_testDir, "concurrent.jsonl");
            using TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults);

            const int TaskCount = 10;
            const int EventsPerTask = 100;

            Task[] tasks = new Task[TaskCount];
            for (int t = 0; t < TaskCount; t++)
            {
                int taskId = t;
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < EventsPerTask; i++)
                    {
                        logger.Append(new IdleTimeEvent(
                            LevelId: "L1",
                            TimestampMs: taskId * EventsPerTask + i,
                            ActionIndex: i,
                            IdleMs: 500));
                    }
                });
            }

            Task.WaitAll(tasks);

            string[] lines = File.ReadAllLines(path);
            Assert.That(lines, Has.Length.EqualTo(TaskCount * EventsPerTask),
                "All 1000 events must be written.");

            foreach (string line in lines)
            {
                Assert.DoesNotThrow(
                    () => JsonDocument.Parse(line),
                    $"Every line must be parseable JSON.");
            }
        }

        [Test]
        public void Logger_FlushesOnEveryAppend()
        {
            string path = Path.Combine(_testDir, "flush.jsonl");
            TelemetryLogger logger = new TelemetryLogger(path, TelemetryConfig.DevDefaults);

            logger.Append(new LevelStartEvent("L1", 0, 1UL, 0.5, 10, 0, 4, 1));

            // Read without disposing the logger — data must already be flushed.
            string content = File.ReadAllText(path);
            Assert.That(content.Trim(), Is.Not.Empty, "Data must be flushed immediately after Append.");

            logger.Dispose();
        }

        private static IEnumerable<ITelemetryEvent> AllEventTypes()
        {
            RngState rng = new RngState(1u, 2u);
            TileCoord coord = new TileCoord(2, 3);

            yield return new LevelStartEvent("L1", 0, 42UL, 0.7, 12, 0, 4, 2);
            yield return new LevelWinEvent("L1", 100, 5, new[] { "t0", "t1" }, false);
            yield return new LevelLossEvent("L1", 200, 8, LossReasons.DockOverflow, null);
            yield return new LevelLossEvent("L1", 200, 8, LossReasons.WaterOnTarget, "t0");
            yield return new DockOccupancyEvent("L1", 50, 3, 5, DockWarningLevel.Caution);
            yield return new WaterRiseEvent("L1", 80, 4, 2);
            yield return new VineGrowthEvent("L1", 90, 5, coord);
            yield return new UndoUsedEvent("L1", 110, 3);
            yield return new TargetExtractedEvent("L1", 120, 4, "t0");
            yield return new TargetLostEvent("L1", 130, 6, "t1");
            yield return new InvalidTapEvent("L1", 40, coord, InvalidTapReasons.IsolatedTile);
            yield return new IdleTimeEvent("L1", 60, 3, 1500);
            yield return new TimeToFirstActionEvent("L1", 30, 3000);
            yield return new HazardProximityToTargetEvent("L1", 70, 4, "t0", -2, false);
            yield return new DockJamTriggeredEvent("L1", 85, 5);
            yield return new DockJamResolvedEvent("L1", 95, 6, DockJamResolutions.TripleCleared);
            yield return new CaptureSnapshotEvent("L1", 100, 5, "{\"board\":{}}");
            yield return new ActionTakenEvent(
                "L1", 55, 3, coord, 42UL, rng, new RngState(3u, 4u), true, null);
        }
    }
}
