using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.Audio;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Rescue.Unity.Haptics.Tests
{
    public sealed class HapticEventRouterTests
    {
        private readonly List<UnityObject> createdObjects = new List<UnityObject>();

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsEnabledPrefsKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] is not null)
                {
                    UnityObject.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            PlayerPrefs.DeleteKey(AudioSettingsController.HapticsEnabledPrefsKey);
            PlayerPrefs.Save();
        }

        [TestCaseSource(nameof(SupportedEventCases))]
        public void HapticEventClassifier_MapsSupportedEvents(ActionEvent actionEvent, HapticEventId expectedId, float expectedIntensity)
        {
            bool classified = HapticEventClassifier.TryClassify(actionEvent, out HapticFeedbackSignal signal);

            Assert.That(classified, Is.True);
            Assert.That(signal.Id, Is.EqualTo(expectedId));
            Assert.That(signal.Intensity, Is.EqualTo(expectedIntensity).Within(0.001f));
        }

        [TestCaseSource(nameof(UnmappedEventCases))]
        public void HapticEventClassifier_SkipsEventsThatShouldNotVibrate(ActionEvent actionEvent)
        {
            bool classified = HapticEventClassifier.TryClassify(actionEvent, out HapticFeedbackSignal signal);

            Assert.That(classified, Is.False);
            Assert.That(signal, Is.EqualTo(default(HapticFeedbackSignal)));
        }

        [Test]
        public void HapticEventClassifier_SelectsHighestPrioritySignalPerResult()
        {
            ActionResult result = CreateResult(
                ActionOutcome.LossDockOverflow,
                new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))),
                new TargetExtracted("pup-1", new TileCoord(2, 1)),
                new DockOverflowTriggered(OverflowCount: 1));

            bool classified = HapticEventClassifier.TryClassify(result, out HapticFeedbackSignal signal);

            Assert.That(classified, Is.True);
            Assert.That(signal.Id, Is.EqualTo(HapticEventId.DockOverflow));
            Assert.That(signal.Intensity, Is.EqualTo(0.90f).Within(0.001f));
        }

        [Test]
        public void HapticEventClassifier_DedupesFinalExtractWithWinByChoosingWin()
        {
            ActionResult result = CreateResult(
                ActionOutcome.Win,
                new TargetExtracted("pup-1", new TileCoord(2, 1)),
                new Won("pup-1", TotalActions: 4, ExtractedTargetOrder: ImmutableArray.Create("pup-1")));

            bool classified = HapticEventClassifier.TryClassify(result, out HapticFeedbackSignal signal);

            Assert.That(classified, Is.True);
            Assert.That(signal.Id, Is.EqualTo(HapticEventId.Win));
            Assert.That(signal.Intensity, Is.EqualTo(0.55f).Within(0.001f));
        }

        [Test]
        public void HapticEventRouter_RespectsIndependentHapticsSetting()
        {
            SpyHapticEventRouter router = CreateRouter();
            AudioSettingsController settings = CreateSettings();
            settings.SetHapticsEnabled(false);
            router.SettingsController = settings;
            ActionResult result = CreateResult(ActionOutcome.Ok, new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Acute));

            router.RouteResultSignals(CreateState(), new ActionInput(new TileCoord(0, 0)), result);

            Assert.That(router.PlayedSignals, Is.Empty);
        }

        [Test]
        public void HapticEventRouter_RoutesManualUndoAndRetrySignals()
        {
            SpyHapticEventRouter router = CreateRouter();

            router.RouteManual(HapticEventId.UndoUsed);
            router.RouteManual(HapticEventId.RetryConfirmed);

            Assert.That(router.PlayedSignals.Count, Is.EqualTo(2));
            Assert.That(router.PlayedSignals[0].Id, Is.EqualTo(HapticEventId.UndoUsed));
            Assert.That(router.PlayedSignals[0].Intensity, Is.EqualTo(0.20f).Within(0.001f));
            Assert.That(router.PlayedSignals[1].Id, Is.EqualTo(HapticEventId.RetryConfirmed));
            Assert.That(router.PlayedSignals[1].Intensity, Is.EqualTo(0.25f).Within(0.001f));
        }

        private static IEnumerable<TestCaseData> SupportedEventCases()
        {
            yield return new TestCaseData(new InvalidInput(new TileCoord(0, 0), InvalidInputReason.SingleTile), HapticEventId.InvalidTap, 0.15f);
            yield return new TestCaseData(new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))), HapticEventId.GroupClear, 0.20f);
            yield return new TestCaseData(new BlockerBroken(new TileCoord(1, 1), BlockerType.Crate), HapticEventId.BlockerBreak, 0.30f);
            yield return new TestCaseData(new IceRevealed(new TileCoord(1, 2), DebrisType.B), HapticEventId.BlockerBreak, 0.30f);
            yield return new TestCaseData(new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution), HapticEventId.DockCaution, 0.25f);
            yield return new TestCaseData(new DockWarningChanged(DockWarningLevel.Caution, DockWarningLevel.Acute), HapticEventId.DockAcute, 0.65f);
            yield return new TestCaseData(new DockJamTriggered(OverflowCount: 1), HapticEventId.DockJam, 0.70f);
            yield return new TestCaseData(new DockOverflowTriggered(OverflowCount: 1), HapticEventId.DockOverflow, 0.90f);
            yield return new TestCaseData(new TargetProgressed("pup-1", new TileCoord(2, 1)), HapticEventId.TargetNearRescue, 0.25f);
            yield return new TestCaseData(new TargetOneClearAway("pup-1", new TileCoord(2, 1)), HapticEventId.TargetNearRescue, 0.25f);
            yield return new TestCaseData(new TargetExtracted("pup-1", new TileCoord(2, 1)), HapticEventId.TargetExtract, 0.40f);
            yield return new TestCaseData(new WaterWarning(ActionsUntilRise: 1, NextFloodRow: 4), HapticEventId.WaterWarning, 0.35f);
            yield return new TestCaseData(new WaterRose(FloodedRow: 4), HapticEventId.WaterRise, 0.50f);
            yield return new TestCaseData(new Lost(ActionOutcome.LossWaterOnTarget), HapticEventId.WaterLoss, 0.90f);
            yield return new TestCaseData(new VinePreviewChanged(new TileCoord(3, 2)), HapticEventId.VinePreview, 0.25f);
            yield return new TestCaseData(new VineGrown(new TileCoord(3, 2)), HapticEventId.VineGrow, 0.45f);
            yield return new TestCaseData(new Won("pup-1", TotalActions: 4, ExtractedTargetOrder: ImmutableArray.Create("pup-1")), HapticEventId.Win, 0.55f);
        }

        private static IEnumerable<TestCaseData> UnmappedEventCases()
        {
            yield return new TestCaseData(new BlockerDamaged(new TileCoord(1, 0), BlockerType.Crate, RemainingHp: 1));
            yield return new TestCaseData(new DockInserted(ImmutableArray.Create(DebrisType.A), OccupancyAfterInsert: 1, OverflowCount: 0));
            yield return new TestCaseData(new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 0));
            yield return new TestCaseData(new GravitySettled(ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0)))));
            yield return new TestCaseData(new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C))));
            yield return new TestCaseData(new VinePreviewChanged(PendingTile: null));
        }

        private SpyHapticEventRouter CreateRouter()
        {
            GameObject gameObject = new GameObject("SpyHapticRouter");
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<SpyHapticEventRouter>();
        }

        private AudioSettingsController CreateSettings()
        {
            GameObject gameObject = new GameObject("AudioSettings");
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<AudioSettingsController>();
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
                new DebrisTile(DebrisType.A),
                new DebrisTile(DebrisType.A),
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

        private sealed class SpyHapticEventRouter : HapticEventRouter
        {
            private readonly List<HapticFeedbackSignal> playedSignals = new List<HapticFeedbackSignal>();

            public IReadOnlyList<HapticFeedbackSignal> PlayedSignals => playedSignals;

            protected override void PlayHaptic(HapticFeedbackSignal signal)
            {
                playedSignals.Add(signal);
            }
        }
    }
}
