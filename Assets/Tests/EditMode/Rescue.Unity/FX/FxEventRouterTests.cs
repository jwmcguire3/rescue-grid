using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Presentation;
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
        public void FxEventRouter_PlaybackBeatRoutesKnownLocationAwareEvents()
        {
            GameState state = CreateState();
            BoardGridViewPresenter grid = CreateGrid(state);
            SpyFxEventRouter router = CreateRouter(grid);

            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.RemoveGroup, new InvalidInput(new TileCoord(0, 0), InvalidInputReason.SingleTile)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.RemoveGroup, new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.BreakBlockerOrReveal, new BlockerBroken(new TileCoord(0, 0), BlockerType.Crate)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.BreakBlockerOrReveal, new IceRevealed(new TileCoord(0, 1), DebrisType.B)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.BreakBlockerOrReveal, new BlockerBroken(new TileCoord(0, 2), BlockerType.Vine)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockInserted(
                    ImmutableArray.Create(DebrisType.A),
                    OccupancyAfterInsert: 1,
                    OverflowCount: 0)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 0)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockJamTriggered(OverflowCount: 1)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.TargetExtract, new TargetOneClearAway("pup-1", new TileCoord(2, 1))));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.TargetExtract, new TargetExtracted("pup-1", new TileCoord(2, 1))));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.WaterRise, new WaterRose(FloodedRow: 2)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.WaterRise, new VinePreviewChanged(new TileCoord(1, 1))));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.TerminalOutcome, new Won("pup-1", TotalActions: 4, ExtractedTargetOrder: ImmutableArray.Create("pup-1"))));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.TerminalOutcome, new Lost(ActionOutcome.LossDockOverflow)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.TerminalOutcome, new Lost(ActionOutcome.LossWaterOnTarget)));

            Assert.That(router.InvalidTapCount, Is.EqualTo(1));
            Assert.That(router.GroupClearCount, Is.EqualTo(1));
            Assert.That(router.CrateBreakCount, Is.EqualTo(1));
            Assert.That(router.IceRevealCount, Is.EqualTo(1));
            Assert.That(router.VineClearCount, Is.EqualTo(1));
            Assert.That(router.DockInsertCount, Is.EqualTo(1));
            Assert.That(router.DockTripleClearCount, Is.EqualTo(1));
            Assert.That(router.DockWarningCount, Is.EqualTo(2));
            Assert.That(router.NearRescueReliefCount, Is.EqualTo(1));
            Assert.That(router.TargetExtractionCount, Is.EqualTo(1));
            Assert.That(router.WaterRiseCount, Is.EqualTo(1));
            Assert.That(router.VineGrowthPreviewCount, Is.EqualTo(1));
            Assert.That(router.WinCount, Is.EqualTo(1));
            Assert.That(router.LossDockOverflowCount, Is.EqualTo(1));
            Assert.That(router.LossWaterOnTargetCount, Is.EqualTo(1));
        }

        [Test]
        public void FxEventRouter_PlaybackBeatSkipsIntentionallyUnsupportedEventsSafely()
        {
            GameState state = CreateState();
            SpyFxEventRouter router = CreateRouter();

            Assert.DoesNotThrow(() => router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.WaterRise, new VineGrown(new TileCoord(1, 1)))));
            Assert.DoesNotThrow(() => router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.WaterRise, new VinePreviewChanged(PendingTile: null))));
            Assert.DoesNotThrow(() => router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockWarningChanged(DockWarningLevel.Caution, DockWarningLevel.Safe))));

            Assert.That(router.VineGrowthPreviewCount, Is.EqualTo(1));
            Assert.That(router.DockWarningCount, Is.EqualTo(0));
        }

        [Test]
        public void FxEventRouter_PlaybackBeatUsesExpectedGridWorldPositionWhenLocationExists()
        {
            GameState state = CreateState();
            BoardGridViewPresenter grid = CreateGrid(state);
            SpyFxEventRouter router = CreateRouter(grid);

            Vector3 expectedGroupPosition =
                (grid.GetCellWorldPosition(new TileCoord(0, 0)) + grid.GetCellWorldPosition(new TileCoord(0, 1))) * 0.5f;
            Vector3 expectedTargetPosition = grid.GetCellWorldPosition(new TileCoord(2, 1));
            bool foundRow = grid.TryGetRowWorldBounds(2, out BoardGridViewPresenter.RowWorldBounds rowBounds);

            Assert.That(foundRow, Is.True);

            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.RemoveGroup, new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(2, 1)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.TargetExtract, new TargetExtracted("pup-1", new TileCoord(2, 1))));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.WaterRise, new WaterRose(FloodedRow: 2)));

            AssertVector3Equal(expectedGroupPosition, router.LastGroupClearPosition);
            AssertVector3Equal(expectedTargetPosition, router.LastTargetExtractionPosition);
            AssertVector3Equal(rowBounds.Center, router.LastWaterRisePosition);
        }

        [Test]
        public void FxEventRouter_PlaybackBeatMissingLocationDoesNotThrow()
        {
            SpyFxEventRouter router = CreateRouter();
            GameState state = CreateState();

            Assert.DoesNotThrow(() => router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.RemoveGroup, new InvalidInput(new TileCoord(9, 9), InvalidInputReason.OutOfBounds))));
            Assert.DoesNotThrow(() => router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.RemoveGroup, new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(9, 9))))));
            Assert.DoesNotThrow(() => router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.TargetExtract, new TargetExtracted("pup-1", new TileCoord(8, 8)))));
            Assert.DoesNotThrow(() => router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.WaterRise, new WaterRose(FloodedRow: 9))));
            Assert.DoesNotThrow(() => router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.TargetExtract, new TargetOneClearAway("pup-1", new TileCoord(8, 8)))));
            Assert.DoesNotThrow(() => router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.WaterRise, new VinePreviewChanged(new TileCoord(8, 8)))));
        }

        [Test]
        public void FxEventRouter_PlaybackBeatMissingPrefabOrConfigDoesNotThrow()
        {
            GameState state = CreateState();
            BoardGridViewPresenter grid = CreateGrid(state);
            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.BoardGrid = grid;

            Assert.DoesNotThrow(() => router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.RemoveGroup, new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))))));
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

        private SpyFxEventRouter CreateRouter(BoardGridViewPresenter? grid = null)
        {
            GameObject gameObject = CreateGameObject("SpyFxRouter");
            SpyFxEventRouter router = gameObject.AddComponent<SpyFxEventRouter>();
            router.BoardGrid = grid;
            return router;
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

        private static ActionPlaybackStep CreatePlaybackStep(ActionPlaybackStepType stepType, ActionEvent actionEvent)
        {
            return new ActionPlaybackStep(stepType, actionEvent.GetType().Name, actionEvent);
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

        private BoardGridViewPresenter CreateGrid(GameState state)
        {
            GameObject gridObject = CreateGameObject("Grid");
            BoardGridViewPresenter gridPresenter = gridObject.AddComponent<BoardGridViewPresenter>();
            Transform boardRoot = CreateGameObject("BoardRoot").transform;
            boardRoot.SetParent(gridObject.transform, false);
            GameObject tilePrefab = CreateGameObject("TilePrefab");

            SetPrivateField(gridPresenter, "boardRoot", boardRoot);
            SetPrivateField(gridPresenter, "dryTilePrefab", null);
            SetPrivateField(gridPresenter, "fallbackTilePrefab", tilePrefab);
            gridPresenter.RebuildGrid(state);
            return gridPresenter;
        }

        private static void SetPrivateField(object target, string fieldName, object? value)
        {
            System.Reflection.FieldInfo? field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}'.");
            field?.SetValue(target, value);
        }

        private static void AssertVector3Equal(Vector3 expected, Vector3 actual, float tolerance = 0.0001f)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThanOrEqualTo(tolerance));
        }

        private sealed class SpyFxEventRouter : FxEventRouter
        {
            public int GroupClearCount { get; private set; }

            public int CrateBreakCount { get; private set; }

            public int IceRevealCount { get; private set; }

            public int VineClearCount { get; private set; }

            public int InvalidTapCount { get; private set; }

            public int DockInsertCount { get; private set; }

            public int DockTripleClearCount { get; private set; }

            public int DockWarningCount { get; private set; }

            public int WaterRiseCount { get; private set; }

            public int NearRescueReliefCount { get; private set; }

            public int TargetExtractionCount { get; private set; }

            public int VineGrowthPreviewCount { get; private set; }

            public int WinCount { get; private set; }

            public int LossDockOverflowCount { get; private set; }

            public int LossWaterOnTargetCount { get; private set; }

            public Vector3 LastGroupClearPosition { get; private set; }

            public Vector3 LastTargetExtractionPosition { get; private set; }

            public Vector3 LastWaterRisePosition { get; private set; }

            protected override void PlayGroupClear(Vector3 worldPosition)
            {
                GroupClearCount++;
                LastGroupClearPosition = worldPosition;
            }

            protected override void PlayCrateBreak(Vector3 worldPosition)
            {
                CrateBreakCount++;
            }

            protected override void PlayIceReveal(Vector3 worldPosition)
            {
                IceRevealCount++;
            }

            protected override void PlayVineClear(Vector3 worldPosition)
            {
                VineClearCount++;
            }

            protected override void PlayInvalidTap()
            {
                InvalidTapCount++;
            }

            protected override void PlayInvalidTap(Vector3 worldPosition)
            {
                InvalidTapCount++;
            }

            protected override void PlayDockInsert()
            {
                DockInsertCount++;
            }

            protected override void PlayDockTripleClear(Vector3 worldPosition)
            {
                DockTripleClearCount++;
            }

            protected override void PlayDockWarning()
            {
                DockWarningCount++;
            }

            protected override void PlayWaterRise(Vector3 worldPosition)
            {
                WaterRiseCount++;
                LastWaterRisePosition = worldPosition;
            }

            protected override void PlayTargetExtraction(Vector3 worldPosition)
            {
                TargetExtractionCount++;
                LastTargetExtractionPosition = worldPosition;
            }

            protected override void PlayVineGrowthPreview(Vector3 worldPosition)
            {
                VineGrowthPreviewCount++;
            }

            protected override void PlayNearRescueRelief(Vector3 worldPosition)
            {
                NearRescueReliefCount++;
            }

            protected override void PlayWin()
            {
                WinCount++;
            }

            protected override void PlayLossDockOverflow()
            {
                LossDockOverflowCount++;
            }

            protected override void PlayLossWaterOnTarget()
            {
                LossWaterOnTargetCount++;
            }
        }
    }
}
