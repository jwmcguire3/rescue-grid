using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.Audio;
using Rescue.Unity.Presentation;
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
        public void HapticEventClassifier_MapsSupportedEvents(
            ActionEvent actionEvent,
            HapticEventId expectedId,
            HapticPatternStyle expectedStyle,
            float expectedIntensity,
            int expectedDurationMs)
        {
            bool classified = HapticEventClassifier.TryClassify(actionEvent, out HapticFeedbackSignal signal);

            Assert.That(classified, Is.True);
            Assert.That(signal.Id, Is.EqualTo(expectedId));
            Assert.That(signal.Pattern.Style, Is.EqualTo(expectedStyle));
            Assert.That(signal.Pattern.Intensity, Is.EqualTo(expectedIntensity).Within(0.001f));
            Assert.That(signal.Pattern.DurationMs, Is.EqualTo(expectedDurationMs));
            Assert.That(signal.Priority, Is.GreaterThan(0));
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
            Assert.That(signal.Pattern.Intensity, Is.EqualTo(0.90f).Within(0.001f));
            Assert.That(signal.Pattern.Style, Is.EqualTo(HapticPatternStyle.Failure));
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
            Assert.That(signal.Pattern.Style, Is.EqualTo(HapticPatternStyle.Success));
            Assert.That(signal.Pattern.Intensity, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(signal.Pattern.SecondPulseIntensity, Is.EqualTo(0.30f).Within(0.001f));
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
        public void HapticEventRouter_RoutesManualSignalsWithCooldownSuppression()
        {
            SpyHapticEventRouter router = CreateRouter();

            router.RouteManual(HapticEventId.UndoUsed);
            router.RouteManual(HapticEventId.RetryConfirmed);

            Assert.That(router.PlayedSignals.Count, Is.EqualTo(1));
            Assert.That(router.PlayedSignals[0].Id, Is.EqualTo(HapticEventId.UndoUsed));
            Assert.That(router.PlayedSignals[0].Pattern.Intensity, Is.EqualTo(0.20f).Within(0.001f));
        }

        [Test]
        public void HapticEventRouter_AppliesStrengthMultiplierBeforeAdapterPlayback()
        {
            RecordingAdapter adapter = new RecordingAdapter();
            AdapterHapticEventRouter router = CreateAdapterRouter(adapter);
            AudioSettingsController settings = CreateSettings();
            settings.SetHapticsStrength(0.5f);
            router.SettingsController = settings;

            router.Route(HapticEventClassifier.CreateManual(HapticEventId.RetryConfirmed));

            Assert.That(adapter.PlayedPatterns.Count, Is.EqualTo(1));
            Assert.That(adapter.PlayedPatterns[0].Intensity, Is.EqualTo(0.125f).Within(0.001f));
            Assert.That(adapter.PlayedPatterns[0].DurationMs, Is.EqualTo(40));
        }

        [Test]
        public void HapticEventRouter_RoutesPlaybackBeatAndSuppressesTargetExtractWhenWinIsHeadline()
        {
            SpyHapticEventRouter router = CreateRouter();
            ActionResult result = CreateResult(
                ActionOutcome.Win,
                new TargetExtracted("pup-1", new TileCoord(2, 1)),
                new Won("pup-1", TotalActions: 4, ExtractedTargetOrder: ImmutableArray.Create("pup-1")));
            router.BeginActionRoute(result);

            router.RoutePlaybackBeat(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                result.State,
                CreatePlaybackStep(new TargetExtracted("pup-1", new TileCoord(2, 1))));
            router.RoutePlaybackBeat(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                result.State,
                CreatePlaybackStep(new Won("pup-1", TotalActions: 4, ExtractedTargetOrder: ImmutableArray.Create("pup-1"))));

            Assert.That(router.PlayedSignals.Count, Is.EqualTo(1));
            Assert.That(router.PlayedSignals[0].Id, Is.EqualTo(HapticEventId.Win));
        }

        private static IEnumerable<TestCaseData> SupportedEventCases()
        {
            yield return new TestCaseData(new InvalidInput(new TileCoord(0, 0), InvalidInputReason.SingleTile), HapticEventId.InvalidTap, HapticPatternStyle.Tick, 0.12f, 25);
            yield return new TestCaseData(new BlockerBroken(new TileCoord(1, 1), BlockerType.Crate), HapticEventId.BlockerBreak, HapticPatternStyle.Pop, 0.30f, 40);
            yield return new TestCaseData(new IceRevealed(new TileCoord(1, 2), DebrisType.B), HapticEventId.BlockerBreak, HapticPatternStyle.Pop, 0.30f, 40);
            yield return new TestCaseData(new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution), HapticEventId.DockCaution, HapticPatternStyle.Tick, 0.22f, 35);
            yield return new TestCaseData(new DockWarningChanged(DockWarningLevel.Caution, DockWarningLevel.Acute), HapticEventId.DockAcute, HapticPatternStyle.Pulse, 0.35f, 45);
            yield return new TestCaseData(new DockWarningChanged(DockWarningLevel.Acute, DockWarningLevel.Fail), HapticEventId.DockOverflow, HapticPatternStyle.Pulse, 0.45f, 40);
            yield return new TestCaseData(new DockJamTriggered(OverflowCount: 1), HapticEventId.DockJam, HapticPatternStyle.Warning, 0.65f, 70);
            yield return new TestCaseData(new DockOverflowTriggered(OverflowCount: 1), HapticEventId.DockOverflow, HapticPatternStyle.Failure, 0.90f, 125);
            yield return new TestCaseData(new TargetProgressed("pup-1", new TileCoord(2, 1)), HapticEventId.TargetNearRescue, HapticPatternStyle.Lift, 0.22f, 40);
            yield return new TestCaseData(new TargetOneClearAway("pup-1", new TileCoord(2, 1)), HapticEventId.TargetNearRescue, HapticPatternStyle.Lift, 0.22f, 40);
            yield return new TestCaseData(new TargetExtracted("pup-1", new TileCoord(2, 1)), HapticEventId.TargetExtract, HapticPatternStyle.Pop, 0.38f, 50);
            yield return new TestCaseData(new WaterWarning(ActionsUntilRise: 1, NextFloodRow: 4), HapticEventId.WaterWarning, HapticPatternStyle.Warning, 0.30f, 45);
            yield return new TestCaseData(new WaterRose(FloodedRow: 4), HapticEventId.WaterRise, HapticPatternStyle.Pulse, 0.45f, 75);
            yield return new TestCaseData(new Lost(ActionOutcome.LossWaterOnTarget), HapticEventId.WaterLoss, HapticPatternStyle.Failure, 0.90f, 125);
            yield return new TestCaseData(new VinePreviewChanged(new TileCoord(3, 2)), HapticEventId.VinePreview, HapticPatternStyle.Tick, 0.16f, 30);
            yield return new TestCaseData(new VineGrown(new TileCoord(3, 2)), HapticEventId.VineGrow, HapticPatternStyle.Warning, 0.40f, 60);
            yield return new TestCaseData(new Won("pup-1", TotalActions: 4, ExtractedTargetOrder: ImmutableArray.Create("pup-1")), HapticEventId.Win, HapticPatternStyle.Success, 0.45f, 55);
        }

        private static IEnumerable<TestCaseData> UnmappedEventCases()
        {
            yield return new TestCaseData(new BlockerDamaged(new TileCoord(1, 0), BlockerType.Crate, RemainingHp: 1));
            yield return new TestCaseData(new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))));
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

        private AdapterHapticEventRouter CreateAdapterRouter(RecordingAdapter adapter)
        {
            GameObject gameObject = new GameObject("AdapterHapticRouter");
            createdObjects.Add(gameObject);
            AdapterHapticEventRouter router = gameObject.AddComponent<AdapterHapticEventRouter>();
            router.Adapter = adapter;
            return router;
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

        private static ActionPlaybackStep CreatePlaybackStep(ActionEvent actionEvent)
        {
            return new ActionPlaybackStep(ActionPlaybackStepType.RemoveGroup, actionEvent.GetType().Name, actionEvent);
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

        private sealed class AdapterHapticEventRouter : HapticEventRouter
        {
            public IHapticPlatformAdapter? Adapter { get; set; }

            protected override IHapticPlatformAdapter CreatePlatformAdapter()
            {
                return Adapter ?? base.CreatePlatformAdapter();
            }
        }

        private sealed class RecordingAdapter : IHapticPlatformAdapter
        {
            private readonly List<HapticPattern> playedPatterns = new List<HapticPattern>();

            public IReadOnlyList<HapticPattern> PlayedPatterns => playedPatterns;

            public bool SupportsAdvancedPatterns => true;

            public void Play(HapticPattern pattern)
            {
                playedPatterns.Add(pattern);
            }
        }
    }
}
