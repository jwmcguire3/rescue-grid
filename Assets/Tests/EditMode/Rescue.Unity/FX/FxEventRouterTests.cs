using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.FX.Tests
{
    public sealed class FxEventRouterTests
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
        public void FxEventRouter_InvalidInputRoutesInvalidTap()
        {
            SpyFxEventRouter router = CreateRouter();
            GameState state = CreateState();
            ActionInput input = new ActionInput(new TileCoord(9, 9));
            ActionResult result = CreateResult(new InvalidInput(input.TappedCoord, InvalidInputReason.OutOfBounds));

            router.Route(state, input, result);

            Assert.That(router.InvalidTapCount, Is.EqualTo(1));
        }

        [Test]
        public void FxEventRouter_GroupRemovedRoutesGroupClear()
        {
            SpyFxEventRouter router = CreateRouter();
            GameState state = CreateState();
            ActionInput input = new ActionInput(new TileCoord(0, 0));
            ActionResult result = CreateResult(new GroupRemoved(
                DebrisType.A,
                ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))));

            router.Route(state, input, result);

            Assert.That(router.GroupClearCount, Is.EqualTo(1));
        }

        [Test]
        public void FxEventRouter_DockInsertedRoutesDockInsert()
        {
            SpyFxEventRouter router = CreateRouter();
            GameState state = CreateState();
            ActionInput input = new ActionInput(new TileCoord(1, 1));
            ActionResult result = CreateResult(new DockInserted(
                ImmutableArray.Create(DebrisType.A, DebrisType.A),
                OccupancyAfterInsert: 2,
                OverflowCount: 0));

            router.Route(state, input, result);

            Assert.That(router.DockInsertCount, Is.EqualTo(1));
        }

        [Test]
        public void FxEventRouter_WinOutcomeRoutesWin()
        {
            SpyFxEventRouter router = CreateRouter();
            GameState state = CreateState();
            ActionInput input = new ActionInput(new TileCoord(2, 2));
            ActionResult result = new ActionResult(
                state,
                ImmutableArray<ActionEvent>.Empty,
                ActionOutcome.Win,
                Snapshot: null);

            router.Route(state, input, result);

            Assert.That(router.WinCount, Is.EqualTo(1));
        }

        [Test]
        public void FxEventRouter_LossDockOverflowRoutesLossDockOverflow()
        {
            SpyFxEventRouter router = CreateRouter();
            GameState state = CreateState();
            ActionInput input = new ActionInput(new TileCoord(0, 0));
            ActionResult result = new ActionResult(
                state,
                ImmutableArray<ActionEvent>.Empty,
                ActionOutcome.LossDockOverflow,
                Snapshot: null);

            router.Route(state, input, result);

            Assert.That(router.LossDockOverflowCount, Is.EqualTo(1));
        }

        [Test]
        public void FxEventRouter_MissingRegistryDoesNotThrow()
        {
            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            GameState state = CreateState();
            ActionInput input = new ActionInput(new TileCoord(0, 0));
            ActionResult result = CreateResult(new GroupRemoved(
                DebrisType.A,
                ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))));

            Assert.DoesNotThrow(() => router.Route(state, input, result));
        }

        [Test]
        public void FxEventClassifier_DoesNotDuplicateWinWhenOutcomeAndEventMatch()
        {
            GameState state = CreateState();
            ActionResult result = new ActionResult(
                state,
                ImmutableArray.Create<ActionEvent>(
                    new Won("pup-1", TotalActions: 7, ExtractedTargetOrder: ImmutableArray.Create("pup-1"))),
                ActionOutcome.Win,
                Snapshot: null);

            ImmutableArray<FxEventHook> hooks = FxEventClassifier.Classify(
                state,
                new ActionInput(new TileCoord(0, 0)),
                result);

            Assert.That(hooks, Is.EqualTo(new[] { FxEventHook.Win }));
        }

        [Test]
        public void FxEventClassifier_IgnoresDockWarningResetToSafe()
        {
            GameState state = CreateState();
            ActionResult result = CreateResult(new DockWarningChanged(DockWarningLevel.Caution, DockWarningLevel.Safe));

            ImmutableArray<FxEventHook> hooks = FxEventClassifier.Classify(
                state,
                new ActionInput(new TileCoord(0, 0)),
                result);

            Assert.That(hooks, Is.Empty);
        }

        [Test]
        public void FxEventClassifier_MapsSupplementalHooks()
        {
            GameState state = CreateState();
            ActionResult result = CreateResult(
                new BlockerBroken(new TileCoord(0, 0), BlockerType.Crate),
                new IceRevealed(new TileCoord(0, 1), DebrisType.B),
                new BlockerBroken(new TileCoord(0, 2), BlockerType.Vine),
                new VinePreviewChanged(new TileCoord(1, 1)),
                new DockCleared(DebrisType.C, SetsCleared: 1, OccupancyAfterClear: 3),
                new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution),
                new WaterRose(FloodedRow: 4),
                new TargetOneClearAway("pup-1", new TileCoord(2, 2)),
                new TargetExtracted("pup-1", new TileCoord(2, 2)),
                new Lost(ActionOutcome.LossWaterOnTarget));

            ImmutableArray<FxEventHook> hooks = FxEventClassifier.Classify(
                state,
                new ActionInput(new TileCoord(0, 0)),
                result);

            Assert.That(hooks, Is.EqualTo(new[]
            {
                FxEventHook.CrateBreak,
                FxEventHook.IceReveal,
                FxEventHook.VineClear,
                FxEventHook.VineGrowthPreview,
                FxEventHook.DockTripleClear,
                FxEventHook.DockWarning,
                FxEventHook.WaterRise,
                FxEventHook.NearRescueRelief,
                FxEventHook.TargetExtraction,
                FxEventHook.LossWaterOnTarget,
            }));
        }

        private SpyFxEventRouter CreateRouter()
        {
            GameObject gameObject = CreateGameObject("SpyFxRouter");
            return gameObject.AddComponent<SpyFxEventRouter>();
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static ActionResult CreateResult(params ActionEvent[] events)
        {
            return new ActionResult(
                CreateState(),
                ImmutableArray.CreateRange(events),
                ActionOutcome.Ok,
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

        private sealed class SpyFxEventRouter : FxEventRouter
        {
            public int GroupClearCount { get; private set; }

            public int InvalidTapCount { get; private set; }

            public int DockInsertCount { get; private set; }

            public int WinCount { get; private set; }

            public int LossDockOverflowCount { get; private set; }

            protected override void PlayGroupClear()
            {
                GroupClearCount++;
            }

            protected override void PlayInvalidTap()
            {
                InvalidTapCount++;
            }

            protected override void PlayDockInsert()
            {
                DockInsertCount++;
            }

            protected override void PlayWin()
            {
                WinCount++;
            }

            protected override void PlayLossDockOverflow()
            {
                LossDockOverflowCount++;
            }
        }
    }
}
