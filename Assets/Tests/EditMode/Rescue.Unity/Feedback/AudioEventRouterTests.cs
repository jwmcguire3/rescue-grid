using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.Presentation;
using UnityEngine;

namespace Rescue.Unity.Feedback.Tests
{
    public sealed class AudioEventRouterTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] is not null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void AudioEventRouter_MissingRegistryDoesNotThrow()
        {
            AudioEventRouter router = CreateGameObject("AudioRouter").AddComponent<AudioEventRouter>();

            Assert.DoesNotThrow(() => router.Route(new GroupRemoved(
                DebrisType.A,
                ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))));
        }

        [Test]
        public void AudioEventRouter_MissingAudioSourceDoesNotThrow()
        {
            AudioEventRouter router = CreateGameObject("AudioRouter").AddComponent<AudioEventRouter>();
            router.Registry = CreateRegistry(Entry(FeedbackEventId.GroupClear));

            Assert.DoesNotThrow(() => router.Route(new GroupRemoved(
                DebrisType.A,
                ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))));
        }

        [Test]
        public void AudioEventRouter_MissingClipDoesNotThrow()
        {
            AudioEventRouter router = CreateGameObject("AudioRouter").AddComponent<AudioEventRouter>();
            router.AudioSource = router.gameObject.AddComponent<AudioSource>();
            router.Registry = CreateRegistry(new AudioFeedbackEntry(
                FeedbackEventId.GroupClear,
                new AudioClip?[] { null }));

            Assert.DoesNotThrow(() => router.Route(new GroupRemoved(
                DebrisType.A,
                ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))));
        }

        [Test]
        public void AudioEventRouter_GravitySettleAggregatesToSinglePlaybackRequest()
        {
            SpyAudioEventRouter router = CreateSpyRouter();
            router.Registry = CreateRegistry(Entry(FeedbackEventId.GravitySettle));

            router.RoutePlaybackBeat(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateState(),
                CreatePlaybackStep(new GravitySettled(ImmutableArray.Create(
                    (new TileCoord(0, 0), new TileCoord(1, 0)),
                    (new TileCoord(0, 1), new TileCoord(1, 1)),
                    (new TileCoord(0, 2), new TileCoord(1, 2))))));

            Assert.That(router.PlayedIds, Is.EqualTo(new[] { FeedbackEventId.GravitySettle }));
        }

        [Test]
        public void AudioEventRouter_SpawnLandAggregatesToSinglePlaybackRequest()
        {
            SpyAudioEventRouter router = CreateSpyRouter();
            router.Registry = CreateRegistry(Entry(FeedbackEventId.SpawnLand));

            router.RoutePlaybackBeat(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateState(),
                CreatePlaybackStep(new Spawned(ImmutableArray.Create(
                    (new TileCoord(0, 0), DebrisType.A),
                    (new TileCoord(0, 1), DebrisType.B),
                    (new TileCoord(0, 2), DebrisType.C)))));

            Assert.That(router.PlayedIds, Is.EqualTo(new[] { FeedbackEventId.SpawnLand }));
        }

        [Test]
        public void AudioEventRouter_RouteResultSignalsHandlesNonPlaybackSignals()
        {
            SpyAudioEventRouter router = CreateSpyRouter();
            router.Registry = CreateRegistry(
                Entry(FeedbackEventId.InvalidTap),
                Entry(FeedbackEventId.WaterWarning),
                Entry(FeedbackEventId.TargetOneClearAway),
                Entry(FeedbackEventId.VinePreview),
                Entry(FeedbackEventId.VineGrow),
                Entry(FeedbackEventId.Win),
                Entry(FeedbackEventId.LossWaterOnTarget));

            ActionResult result = CreateResult(
                ActionOutcome.Win,
                new InvalidInput(new TileCoord(0, 0), InvalidInputReason.SingleTile),
                new WaterWarning(ActionsUntilRise: 1, NextFloodRow: 2),
                new TargetOneClearAway("pup-1", new TileCoord(2, 1)),
                new VinePreviewChanged(new TileCoord(1, 1)),
                new VineGrown(new TileCoord(1, 1)),
                new Won("pup-1", TotalActions: 4, ExtractedTargetOrder: ImmutableArray.Create("pup-1")),
                new Lost(ActionOutcome.LossWaterOnTarget));

            router.RouteResultSignals(CreateState(), new ActionInput(new TileCoord(0, 0)), result);

            Assert.That(router.PlayedIds, Is.EqualTo(new[]
            {
                FeedbackEventId.InvalidTap,
                FeedbackEventId.WaterWarning,
                FeedbackEventId.TargetOneClearAway,
                FeedbackEventId.VinePreview,
                FeedbackEventId.VineGrow,
                FeedbackEventId.Win,
                FeedbackEventId.LossWaterOnTarget,
            }));
        }

        [Test]
        public void AudioEventRouter_RoutePlaybackBeatHandlesMainPlaybackEvents()
        {
            SpyAudioEventRouter router = CreateSpyRouter();
            router.Registry = CreateRegistry(
                Entry(FeedbackEventId.GroupClear),
                Entry(FeedbackEventId.BlockerDamage),
                Entry(FeedbackEventId.CrateBreak),
                Entry(FeedbackEventId.IceReveal),
                Entry(FeedbackEventId.DockInsert),
                Entry(FeedbackEventId.DockTripleClear),
                Entry(FeedbackEventId.DockCaution),
                Entry(FeedbackEventId.GravitySettle),
                Entry(FeedbackEventId.SpawnLand),
                Entry(FeedbackEventId.TargetExtract),
                Entry(FeedbackEventId.WaterRise));

            ActionEvent[] events =
            {
                new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))),
                new BlockerDamaged(new TileCoord(1, 0), BlockerType.Crate, RemainingHp: 1),
                new BlockerBroken(new TileCoord(1, 0), BlockerType.Crate),
                new IceRevealed(new TileCoord(1, 1), DebrisType.B),
                new DockInserted(ImmutableArray.Create(DebrisType.A), OccupancyAfterInsert: 1, OverflowCount: 0),
                new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 0),
                new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution),
                new GravitySettled(ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0)))),
                new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C))),
                new TargetExtracted("pup-1", new TileCoord(2, 1)),
                new WaterRose(FloodedRow: 2),
            };

            for (int i = 0; i < events.Length; i++)
            {
                router.RoutePlaybackBeat(
                    CreateState(),
                    new ActionInput(new TileCoord(0, 0)),
                    CreateState(),
                    CreatePlaybackStep(events[i]));
            }

            Assert.That(router.PlayedIds, Is.EqualTo(new[]
            {
                FeedbackEventId.GroupClear,
                FeedbackEventId.BlockerDamage,
                FeedbackEventId.CrateBreak,
                FeedbackEventId.IceReveal,
                FeedbackEventId.DockInsert,
                FeedbackEventId.DockTripleClear,
                FeedbackEventId.DockCaution,
                FeedbackEventId.GravitySettle,
                FeedbackEventId.SpawnLand,
                FeedbackEventId.TargetExtract,
                FeedbackEventId.WaterRise,
            }));
        }

        [Test]
        public void AudioFeedbackRegistry_UsesFirstMatchingEntry()
        {
            AudioFeedbackRegistry registry = CreateRegistry(Entry(FeedbackEventId.Win));

            bool found = registry.TryGetEntry(FeedbackEventId.Win, out AudioFeedbackEntry? entry);

            Assert.That(found, Is.True);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry?.EventId, Is.EqualTo(FeedbackEventId.Win));
        }

        private SpyAudioEventRouter CreateSpyRouter()
        {
            GameObject gameObject = CreateGameObject("SpyAudioRouter");
            return gameObject.AddComponent<SpyAudioEventRouter>();
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private AudioFeedbackRegistry CreateRegistry(params AudioFeedbackEntry[] entries)
        {
            AudioFeedbackRegistry registry = ScriptableObject.CreateInstance<AudioFeedbackRegistry>();
            registry.SetEntries(entries);
            createdObjects.Add(registry);
            return registry;
        }

        private AudioFeedbackEntry Entry(FeedbackEventId eventId, int maxPlaysPerRoute = 1)
        {
            return new AudioFeedbackEntry(
                eventId,
                new[] { CreateClip(eventId.ToString()) },
                volume: 0.7f,
                pitchVariance: 0f,
                maxPlaysPerRoute);
        }

        private AudioClip CreateClip(string name)
        {
            AudioClip clip = AudioClip.Create(name, lengthSamples: 32, channels: 1, frequency: 8000, stream: false);
            createdObjects.Add(clip);
            return clip;
        }

        private static ActionPlaybackStep CreatePlaybackStep(ActionEvent actionEvent)
        {
            return new ActionPlaybackStep(ActionPlaybackStepType.RemoveGroup, actionEvent.GetType().Name, actionEvent);
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

        private sealed class SpyAudioEventRouter : AudioEventRouter
        {
            private readonly List<FeedbackEventId> playedIds = new List<FeedbackEventId>();

            public IReadOnlyList<FeedbackEventId> PlayedIds => playedIds;

            protected override void PlayClip(AudioClip clip, AudioFeedbackEntry entry, Vector3 worldPosition)
            {
                _ = clip;
                _ = worldPosition;
                playedIds.Add(entry.EventId);
            }
        }
    }
}
