using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.Presentation;

namespace Rescue.Unity.Feedback.Tests
{
    public sealed class FeedbackEventClassifierTests
    {
        [TestCaseSource(nameof(SupportedEventCases))]
        public void FeedbackEventClassifier_MapsSupportedPhase1Events(
            ActionEvent actionEvent,
            FeedbackEventId expectedId)
        {
            bool classified = FeedbackEventClassifier.TryClassify(actionEvent, out FeedbackEvent feedbackEvent);

            Assert.That(classified, Is.True);
            Assert.That(feedbackEvent.Id, Is.EqualTo(expectedId));
            Assert.That(feedbackEvent.SourceEvent, Is.SameAs(actionEvent));
            Assert.That(feedbackEvent.DebugLabel, Is.EqualTo(actionEvent.GetType().Name));
        }

        [Test]
        public void FeedbackEventClassifier_MapsPlaybackStepSourceEvent()
        {
            ActionEvent actionEvent = new VineGrown(new TileCoord(2, 2));
            ActionPlaybackStep playbackStep = new ActionPlaybackStep(
                ActionPlaybackStepType.VineGrowth,
                nameof(VineGrown),
                actionEvent);

            bool classified = FeedbackEventClassifier.TryClassify(playbackStep, out FeedbackEvent feedbackEvent);

            Assert.That(classified, Is.True);
            Assert.That(feedbackEvent.Id, Is.EqualTo(FeedbackEventId.VineGrow));
            Assert.That(feedbackEvent.Location, Is.EqualTo(new TileCoord(2, 2)));
        }

        [Test]
        public void FeedbackEventClassifier_UnmappedEventsFailSoft()
        {
            Assert.DoesNotThrow(() =>
            {
                bool classified = FeedbackEventClassifier.TryClassify(
                    new DeadboardDiagnosticDetected(DeadboardDiagnosticReason.HardNoValidGroups, TargetId: null),
                    out FeedbackEvent feedbackEvent);

                Assert.That(classified, Is.False);
                Assert.That(feedbackEvent, Is.EqualTo(default(FeedbackEvent)));
            });
        }

        [Test]
        public void FeedbackEventClassifier_SkipsClearedVinePreviewSafely()
        {
            bool classified = FeedbackEventClassifier.TryClassify(
                new VinePreviewChanged(PendingTile: null),
                out FeedbackEvent feedbackEvent);

            Assert.That(classified, Is.False);
            Assert.That(feedbackEvent, Is.EqualTo(default(FeedbackEvent)));
        }

        [Test]
        public void FeedbackEventClassifier_ResultStreamMapsNonPlaybackEvents()
        {
            ActionResult result = CreateResult(
                ActionOutcome.Ok,
                new InvalidInput(new TileCoord(0, 0), InvalidInputReason.SingleTile),
                new WaterWarning(ActionsUntilRise: 1, NextFloodRow: 5),
                new TargetOneClearAway("pup-1", new TileCoord(2, 1)),
                new VinePreviewChanged(new TileCoord(3, 1)),
                new VineGrown(new TileCoord(3, 1)),
                new Won("pup-1", TotalActions: 4, ExtractedTargetOrder: ImmutableArray.Create("pup-1")));

            ImmutableArray<FeedbackEvent> feedbackEvents = FeedbackEventClassifier.Classify(result);

            Assert.That(feedbackEvents.Select(feedbackEvent => feedbackEvent.Id), Is.EqualTo(new[]
            {
                FeedbackEventId.InvalidTap,
                FeedbackEventId.WaterWarning,
                FeedbackEventId.TargetOneClearAway,
                FeedbackEventId.VinePreview,
                FeedbackEventId.VineGrow,
                FeedbackEventId.Win,
            }));
        }

        [TestCase(ActionOutcome.LossDockOverflow, FeedbackEventId.LossDockOverflow)]
        [TestCase(ActionOutcome.LossWaterOnTarget, FeedbackEventId.LossWaterOnTarget)]
        [TestCase(ActionOutcome.LossDistressedExpired, FeedbackEventId.LossWaterOnTarget)]
        public void FeedbackEventClassifier_LostMapsToCauseSpecificFeedback(
            ActionOutcome outcome,
            FeedbackEventId expectedId)
        {
            bool classified = FeedbackEventClassifier.TryClassify(
                new Lost(outcome),
                out FeedbackEvent feedbackEvent);

            Assert.That(classified, Is.True);
            Assert.That(feedbackEvent.Id, Is.EqualTo(expectedId));
        }

        [Test]
        public void FeedbackEventClassifier_OutcomeFallbackMapsTerminalFeedbackWhenEventIsAbsent()
        {
            ActionResult result = CreateResult(ActionOutcome.Win);

            ImmutableArray<FeedbackEvent> feedbackEvents = FeedbackEventClassifier.Classify(result);

            Assert.That(feedbackEvents.Select(feedbackEvent => feedbackEvent.Id), Is.EqualTo(new[]
            {
                FeedbackEventId.Win,
            }));
            Assert.That(feedbackEvents[0].SourceEvent, Is.Null);
            Assert.That(feedbackEvents[0].DebugLabel, Is.EqualTo("Outcome:Win"));
        }

        [Test]
        public void FeedbackEventClassifier_DoesNotDuplicateWinWhenResultEventAndOutcomeMatch()
        {
            ActionResult result = CreateResult(
                ActionOutcome.Win,
                new Won("pup-1", TotalActions: 4, ExtractedTargetOrder: ImmutableArray.Create("pup-1")),
                new Won("pup-1", TotalActions: 4, ExtractedTargetOrder: ImmutableArray.Create("pup-1")));

            ImmutableArray<FeedbackEvent> feedbackEvents = FeedbackEventClassifier.Classify(result);

            Assert.That(feedbackEvents.Select(feedbackEvent => feedbackEvent.Id), Is.EqualTo(new[]
            {
                FeedbackEventId.Win,
            }));
        }

        [Test]
        public void FeedbackEventClassifier_DoesNotDuplicateLostWhenResultEventAndOutcomeMatch()
        {
            ActionResult result = CreateResult(
                ActionOutcome.LossDockOverflow,
                new Lost(ActionOutcome.LossDockOverflow),
                new Lost(ActionOutcome.LossDockOverflow));

            ImmutableArray<FeedbackEvent> feedbackEvents = FeedbackEventClassifier.Classify(result);

            Assert.That(feedbackEvents.Select(feedbackEvent => feedbackEvent.Id), Is.EqualTo(new[]
            {
                FeedbackEventId.LossDockOverflow,
            }));
        }

        private static object[] SupportedEventCases()
        {
            return new object[]
            {
                new object[] { new InvalidInput(new TileCoord(0, 0), InvalidInputReason.SingleTile), FeedbackEventId.InvalidTap },
                new object[] { new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))), FeedbackEventId.GroupClear },
                new object[] { new BlockerDamaged(new TileCoord(1, 0), BlockerType.Crate, RemainingHp: 1), FeedbackEventId.BlockerDamage },
                new object[] { new BlockerBroken(new TileCoord(1, 1), BlockerType.Crate), FeedbackEventId.CrateBreak },
                new object[] { new IceRevealed(new TileCoord(1, 2), DebrisType.B), FeedbackEventId.IceReveal },
                new object[] { new BlockerBroken(new TileCoord(1, 3), BlockerType.Vine), FeedbackEventId.VineClear },
                new object[] { new DockInserted(ImmutableArray.Create(DebrisType.A), OccupancyAfterInsert: 1, OverflowCount: 0), FeedbackEventId.DockInsert },
                new object[] { new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 0), FeedbackEventId.DockTripleClear },
                new object[] { new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution), FeedbackEventId.DockCaution },
                new object[] { new DockWarningChanged(DockWarningLevel.Caution, DockWarningLevel.Acute), FeedbackEventId.DockAcute },
                new object[] { new DockWarningChanged(DockWarningLevel.Acute, DockWarningLevel.Fail), FeedbackEventId.DockJamOrFail },
                new object[] { new DockOverflowTriggered(OverflowCount: 1), FeedbackEventId.DockJamOrFail },
                new object[] { new DockJamTriggered(OverflowCount: 1), FeedbackEventId.DockJamOrFail },
                new object[] { new GravitySettled(ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0)))), FeedbackEventId.GravitySettle },
                new object[] { new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C))), FeedbackEventId.SpawnLand },
                new object[] { new TargetOneClearAway("pup-1", new TileCoord(2, 1)), FeedbackEventId.TargetOneClearAway },
                new object[] { new TargetExtracted("pup-1", new TileCoord(2, 1)), FeedbackEventId.TargetExtract },
                new object[] { new WaterWarning(ActionsUntilRise: 1, NextFloodRow: 4), FeedbackEventId.WaterWarning },
                new object[] { new WaterRose(FloodedRow: 4), FeedbackEventId.WaterRise },
                new object[] { new VinePreviewChanged(new TileCoord(3, 2)), FeedbackEventId.VinePreview },
                new object[] { new VineGrown(new TileCoord(3, 2)), FeedbackEventId.VineGrow },
                new object[] { new Won("pup-1", TotalActions: 4, ExtractedTargetOrder: ImmutableArray.Create("pup-1")), FeedbackEventId.Win },
                new object[] { new Lost(ActionOutcome.LossDockOverflow), FeedbackEventId.LossDockOverflow },
                new object[] { new Lost(ActionOutcome.LossWaterOnTarget), FeedbackEventId.LossWaterOnTarget },
            };
        }

        private static ActionResult CreateResult(ActionOutcome outcome, params ActionEvent[] events)
        {
            return new ActionResult(
                CreateState(),
                ImmutableArray.CreateRange(events),
                outcome,
                Snapshot: null);
        }

        private static GameState CreateState()
        {
            ImmutableArray<Tile> row0 = ImmutableArray.Create<Tile>(
                new EmptyTile(),
                new EmptyTile(),
                new EmptyTile());
            ImmutableArray<Tile> row1 = ImmutableArray.Create<Tile>(
                new EmptyTile(),
                new EmptyTile(),
                new EmptyTile());
            ImmutableArray<Tile> row2 = ImmutableArray.Create<Tile>(
                new EmptyTile(),
                new TargetTile("pup-1", Extracted: false),
                new EmptyTile());

            return new GameState(
                Board: new Board(3, 3, ImmutableArray.Create(row0, row1, row2)),
                Dock: new Dock(
                    ImmutableArray.Create<DebrisType?>(null, null, null, null, null, null, null),
                    Size: 7),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
                Vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                Targets: ImmutableArray.Create(new TargetState("pup-1", new TileCoord(2, 1), false, false)),
                LevelConfig: new LevelConfig(
                    ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C),
                    BaseDistribution: null,
                    AssistanceChance: 0.0d,
                    ConsecutiveEmergencyCap: 2),
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
