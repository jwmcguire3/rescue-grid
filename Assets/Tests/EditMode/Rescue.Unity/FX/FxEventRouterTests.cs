using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.Presentation;
using Rescue.Unity.UI;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;

namespace Rescue.Unity.FX.Tests
{
    public sealed class FxEventRouterTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private const string TempFxPrefabPath = "Assets/Tests/EditMode/Rescue.Unity/FX/TempDebugInspectionFx.prefab";

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
            AssetDatabase.DeleteAsset(TempFxPrefabPath);
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
        public void FxEventRouter_DockInsertedDoesNotRouteDockInsert()
        {
            SpyFxEventRouter router = CreateRouter();
            GameState state = CreateState();
            ActionInput input = new ActionInput(new TileCoord(1, 1));
            ActionResult result = CreateResult(new DockInserted(
                ImmutableArray.Create(DebrisType.A, DebrisType.A),
                OccupancyAfterInsert: 2,
                OverflowCount: 0));

            router.Route(state, input, result);

            Assert.That(router.DockInsertCount, Is.EqualTo(0));
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
        public void FxEventRouter_DockJamPlaybackDoesNotRouteTerminalOverflowFx()
        {
            SpyFxEventRouter router = CreateRouter();
            GameState state = CreateState();

            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockJamTriggered(OverflowCount: 1)));

            Assert.That(router.DockWarningCount, Is.EqualTo(1));
            Assert.That(router.LossDockOverflowCount, Is.EqualTo(0));
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
                CreatePlaybackStep(ActionPlaybackStepType.VineGrowth, new VineGrown(new TileCoord(1, 1))));
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
            Assert.That(router.DockInsertCount, Is.EqualTo(0));
            Assert.That(router.DockTripleClearCount, Is.EqualTo(1));
            Assert.That(router.DockWarningCount, Is.EqualTo(2));
            Assert.That(router.NearRescueReliefCount, Is.EqualTo(1));
            Assert.That(router.TargetExtractionCount, Is.EqualTo(1));
            Assert.That(router.WaterRiseCount, Is.EqualTo(1));
            Assert.That(router.VineGrowthPreviewCount, Is.EqualTo(1));
            Assert.That(router.VineGrowthCount, Is.EqualTo(1));
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
                CreatePlaybackStep(ActionPlaybackStepType.WaterRise, new VinePreviewChanged(PendingTile: null))));
            Assert.DoesNotThrow(() => router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockWarningChanged(DockWarningLevel.Caution, DockWarningLevel.Safe))));

            Assert.That(router.VineGrowthPreviewCount, Is.EqualTo(0));
            Assert.That(router.VineGrowthCount, Is.EqualTo(0));
            Assert.That(router.DockWarningCount, Is.EqualTo(0));
        }

        [Test]
        public void FxEventRouter_PlaybackBeatUsesExpectedGridWorldPositionWhenLocationExists()
        {
            GameState state = CreateState();
            BoardGridViewPresenter grid = CreateGrid(state);
            DockViewPresenter dock = CreateDockView(CreateDockState(DebrisType.A, DebrisType.A, DebrisType.A, null, null, null, null));
            SpyFxEventRouter router = CreateRouter(grid, dock);

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
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockInserted(
                    ImmutableArray.Create(DebrisType.B, DebrisType.C),
                    OccupancyAfterInsert: 4,
                    OverflowCount: 0)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockCleared(
                    DebrisType.A,
                    SetsCleared: 1,
                    OccupancyAfterClear: 0)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.WaterRise, new WaterRose(FloodedRow: 2)));

            AssertVector3Equal(expectedGroupPosition, router.LastGroupClearPosition);
            AssertVector3Equal(expectedTargetPosition, router.LastTargetExtractionPosition);
            Assert.That(router.DockInsertCount, Is.EqualTo(0));
            Assert.That(router.DockTripleClearCount, Is.EqualTo(1));
            AssertVector3Equal(rowBounds.Center, router.LastWaterRisePosition);
        }

        [Test]
        public void FxEventRouter_PlaybackBeatRoutesEachDockClearedToDockTripleClear()
        {
            GameState state = CreateState();
            DockViewPresenter dock = CreateDockView(CreateDockState(DebrisType.A, DebrisType.A, DebrisType.A, DebrisType.B, DebrisType.B, DebrisType.B, null));
            SpyFxEventRouter router = CreateRouter(dock: dock);

            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockCleared(
                    DebrisType.A,
                    SetsCleared: 1,
                    OccupancyAfterClear: 3)));
            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockCleared(
                    DebrisType.B,
                    SetsCleared: 1,
                    OccupancyAfterClear: 0)));

            Assert.That(router.DockTripleClearCount, Is.EqualTo(2));
        }

        [Test]
        public void FxEventRouter_DockClearedWithTwoSetsRoutesTwoDockTripleClearFx()
        {
            GameState state = CreateState();
            SpyFxEventRouter router = CreateRouter();

            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockCleared(
                    DebrisType.A,
                    SetsCleared: 2,
                    OccupancyAfterClear: 1)));

            Assert.That(router.DockTripleClearCount, Is.EqualTo(2));
        }

        [Test]
        public void FxEventRouter_WaterRiseScalesSpawnedFxToRowWidth()
        {
            GameState sixWideState = CreateEmptyState(width: 6, height: 3);
            BoardGridViewPresenter sixWideGrid = CreateGrid(sixWideState);
            Transform sixWideFxRoot = CreateGameObject("SixWideFxRoot").transform;
            FxEventRouter sixWideRouter = CreateWaterRiseRouter(sixWideGrid, sixWideFxRoot, "SixWideWaterRiseFx");

            sixWideRouter.RoutePlaybackBeat(
                sixWideState,
                new ActionInput(new TileCoord(0, 0)),
                sixWideState,
                CreatePlaybackStep(ActionPlaybackStepType.WaterRise, new WaterRose(FloodedRow: 1)));

            Transform sixWideSpawned = sixWideFxRoot.Find(nameof(FxVisualRegistry.WaterRiseFx))
                ?? throw new AssertionException("Expected six-wide water rise FX to spawn.");
            Assert.That(sixWideSpawned.localScale.x, Is.EqualTo(6f).Within(0.001f));
            Assert.That(sixWideSpawned.localScale.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(sixWideSpawned.localScale.z, Is.EqualTo(1f).Within(0.001f));

            GameState eightWideState = CreateEmptyState(width: 8, height: 3);
            BoardGridViewPresenter eightWideGrid = CreateGrid(eightWideState);
            Transform eightWideFxRoot = CreateGameObject("EightWideFxRoot").transform;
            FxEventRouter eightWideRouter = CreateWaterRiseRouter(eightWideGrid, eightWideFxRoot, "EightWideWaterRiseFx");

            eightWideRouter.RoutePlaybackBeat(
                eightWideState,
                new ActionInput(new TileCoord(0, 0)),
                eightWideState,
                CreatePlaybackStep(ActionPlaybackStepType.WaterRise, new WaterWarning(NextFloodRow: 1, ActionsUntilRise: 0)));

            Transform eightWideSpawned = eightWideFxRoot.Find(nameof(FxVisualRegistry.WaterRiseFx))
                ?? throw new AssertionException("Expected eight-wide water rise FX to spawn.");
            Assert.That(eightWideSpawned.localScale.x, Is.EqualTo(8f).Within(0.001f));
            Assert.That(eightWideSpawned.localScale.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(eightWideSpawned.localScale.z, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FxEventRouter_WaterRiseWithMissingRowBoundsKeepsAuthoredScale()
        {
            GameState state = CreateEmptyState(width: 3, height: 3);
            BoardGridViewPresenter grid = CreateGrid(state);
            Transform fxRoot = CreateGameObject("FxRoot").transform;
            FxEventRouter router = CreateWaterRiseRouter(grid, fxRoot, "WaterRiseFx");

            Assert.DoesNotThrow(() => router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.WaterRise, new WaterRose(FloodedRow: 99))));

            Transform spawned = fxRoot.Find(nameof(FxVisualRegistry.WaterRiseFx))
                ?? throw new AssertionException("Expected fallback water rise FX to spawn.");
            Assert.That(spawned.localScale, Is.EqualTo(Vector3.one));
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
        public void FxEventRouter_SpawnedFxAlignToBoardPresentationPlane()
        {
            GameState state = CreateState();
            BoardGridViewPresenter grid = CreateGrid(state);
            grid.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            DockViewPresenter dock = CreateDockView(CreateDockState(null, null, null, null, null, null, null));
            dock.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            GameObject fxRoot = CreateGameObject("FxRoot");
            GameObject prefab = CreateGameObject("GroupClearPrefab");
            FxVisualRegistry registry = ScriptableObject.CreateInstance<FxVisualRegistry>();
            createdObjects.Add(registry);
            registry.GroupClearFx = prefab;

            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.BoardGrid = grid;
            router.DockView = dock;
            router.FxRoot = fxRoot.transform;
            router.FxRegistry = registry;

            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.RemoveGroup, new GroupRemoved(
                    DebrisType.A,
                    ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))));

            Transform? spawned = fxRoot.transform.Find(nameof(FxVisualRegistry.GroupClearFx));
            Vector3 expectedPosition =
                ((grid.GetCellWorldPosition(new TileCoord(0, 0)) + grid.GetCellWorldPosition(new TileCoord(0, 1))) * 0.5f)
                + (grid.transform.up * 0.28f);

            Assert.That(spawned, Is.Not.Null);
            Transform spawnedTransform = spawned ?? throw new AssertionException("Expected routed FX to spawn.");
            Quaternion expectedRotation = grid.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
            Assert.That(Quaternion.Angle(expectedRotation, spawnedTransform.rotation), Is.LessThanOrEqualTo(0.001f));
            AssertVector3Equal(expectedPosition, spawnedTransform.position);
        }

        [Test]
        public void FxEventRouter_DockWarningFxAlignsToDockPresentationPlaneWhenBoardAlsoAssigned()
        {
            GameState state = CreateState();
            BoardGridViewPresenter grid = CreateGrid(state);
            grid.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            DockViewPresenter dock = CreateDockView(CreateDockState(null, null, null, null, null, null, null));
            dock.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            GameObject fxRoot = CreateGameObject("FxRoot");
            GameObject prefab = CreateGameObject("DockInsertPrefab");
            FxVisualRegistry registry = ScriptableObject.CreateInstance<FxVisualRegistry>();
            createdObjects.Add(registry);
            registry.DockInsertFx = prefab;

            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.BoardGrid = grid;
            router.DockView = dock;
            router.FxRoot = fxRoot.transform;
            router.FxRegistry = registry;
            router.SpawnedFxSurfaceOffset = 0.5f;

            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockFeedback, new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution)));

            Transform? spawned = fxRoot.transform.Find($"{nameof(FxVisualRegistry.DockInsertFx)}_WarningFallback");
            Assert.That(spawned, Is.Not.Null);

            bool foundDockCenter = dock.TryGetDockCenterWorldPosition(out Vector3 dockCenter);
            Assert.That(foundDockCenter, Is.True);
            Vector3 expectedPosition = dockCenter + (dock.transform.up * 0.5f);
            Quaternion expectedRotation = dock.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
            Transform spawnedTransform = spawned ?? throw new AssertionException("Expected dock warning FX to spawn.");

            AssertVector3Equal(expectedPosition, spawnedTransform.position);
            Assert.That(Quaternion.Angle(expectedRotation, spawnedTransform.rotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        public void FxEventRouter_DockOverflowFxSpawnsAtDockCenter()
        {
            GameState state = CreateState();
            BoardGridViewPresenter grid = CreateGrid(state);
            DockViewPresenter dock = CreateDockView(CreateDockState(null, null, null, null, null, null, null));
            GameObject fxRoot = CreateGameObject("FxRoot");
            GameObject prefab = CreateGameObject("LossPrefab");
            FxVisualRegistry registry = ScriptableObject.CreateInstance<FxVisualRegistry>();
            createdObjects.Add(registry);
            registry.LossFx = prefab;

            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.BoardGrid = grid;
            router.DockView = dock;
            router.FxRoot = fxRoot.transform;
            router.FxRegistry = registry;
            router.SpawnedFxSurfaceOffset = 0.25f;

            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.DockOverflow, new DockOverflowTriggered(OverflowCount: 1)));

            Transform? spawned = fxRoot.transform.Find($"{nameof(FxVisualRegistry.LossFx)}_DockOverflow");
            Assert.That(spawned, Is.Not.Null);

            bool foundDockCenter = dock.TryGetDockCenterWorldPosition(out Vector3 dockCenter);
            Assert.That(foundDockCenter, Is.True);
            Transform spawnedTransform = spawned ?? throw new AssertionException("Expected dock overflow FX to spawn.");
            AssertVector3Equal(dockCenter + (dock.transform.up * 0.25f), spawnedTransform.position);
        }

        [Test]
        public void FxEventRouter_IceRevealedSpawnsTransientSpriteSequenceUnderFxRoot()
        {
            GameObject fxRoot = CreateGameObject("FxRoot");
            GameObject icePrefab = CreateSpriteFxPrefab("IceClearFx", frameCount: 4);
            FxVisualRegistry registry = ScriptableObject.CreateInstance<FxVisualRegistry>();
            createdObjects.Add(registry);
            registry.IceRevealFx = icePrefab;
            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.FxRoot = fxRoot.transform;
            router.FxRegistry = registry;
            GameState state = CreateState();

            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.BreakBlockerOrReveal, new IceRevealed(new TileCoord(1, 1), DebrisType.B)));

            Transform spawned = fxRoot.transform.Find(nameof(FxVisualRegistry.IceRevealFx))
                ?? throw new AssertionException("Expected IceRevealed to spawn the registered ice reveal FX under FXRoot.");
            SpriteSequenceFxPlayer player = spawned.GetComponent<SpriteSequenceFxPlayer>()
                ?? throw new AssertionException("Expected runtime ice FX to use SpriteSequenceFxPlayer.");

            Assert.That(player.FrameCount, Is.EqualTo(4));
            Assert.That(player.DestroyAfterPlayback, Is.True);
            Assert.That(GetSerializedBool(player, "loop"), Is.False);
            Assert.That(spawned.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(spawned.GetComponent<BoardCellView>(), Is.Null, "Ice FX must not masquerade as board content.");
            Assert.That(spawned.GetComponent<Collider>(), Is.Null, "Ice FX must not be tappable board content.");
            Assert.That(spawned.GetComponentInChildren<MeshRenderer>(includeInactive: true), Is.Null, "Ice FX should be a sprite sequence, not a tile-like mesh.");
        }

        [Test]
        public void FxEventRouter_AppliesFxPlaybackSpeedToSpawnedSpriteSequences()
        {
            GameObject fxRoot = CreateGameObject("FxRoot");
            GameObject icePrefab = CreateSpriteFxPrefab("IceClearFx", frameCount: 4);
            SpriteSequenceFxPlayer prefabPlayer = icePrefab.GetComponent<SpriteSequenceFxPlayer>()
                ?? throw new AssertionException("Expected prefab player.");
            SetPrivateField(prefabPlayer, "secondsPerFrame", 0.08f);
            FxVisualRegistry registry = ScriptableObject.CreateInstance<FxVisualRegistry>();
            createdObjects.Add(registry);
            registry.IceRevealFx = icePrefab;
            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.FxRoot = fxRoot.transform;
            router.FxRegistry = registry;
            router.FxPlaybackSpeedMultiplier = 2.0f;
            GameState state = CreateState();

            router.RoutePlaybackBeat(
                state,
                new ActionInput(new TileCoord(0, 0)),
                state,
                CreatePlaybackStep(ActionPlaybackStepType.BreakBlockerOrReveal, new IceRevealed(new TileCoord(1, 1), DebrisType.B)));

            Transform spawned = fxRoot.transform.Find(nameof(FxVisualRegistry.IceRevealFx))
                ?? throw new AssertionException("Expected IceRevealed to spawn the registered ice reveal FX under FXRoot.");
            SpriteSequenceFxPlayer player = spawned.GetComponent<SpriteSequenceFxPlayer>()
                ?? throw new AssertionException("Expected runtime ice FX to use SpriteSequenceFxPlayer.");

            Assert.That(player.AuthoredSecondsPerFrame, Is.EqualTo(0.08f));
            Assert.That(player.SecondsPerFrame, Is.EqualTo(0.04f));
        }

        [Test]
        public void FxEventRouter_FxPlaybackSpeedMultiplierIsClamped()
        {
            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();

            router.FxPlaybackSpeedMultiplier = 0.01f;
            Assert.That(router.FxPlaybackSpeedMultiplier, Is.EqualTo(FxEventRouter.MinFxPlaybackSpeedMultiplier));

            router.FxPlaybackSpeedMultiplier = 100.0f;
            Assert.That(router.FxPlaybackSpeedMultiplier, Is.EqualTo(FxEventRouter.MaxFxPlaybackSpeedMultiplier));
        }

        [Test]
        public void FxEventRouter_ClearSpawnedFxRemovesStaleChildrenFromFxRoot()
        {
            GameObject fxRoot = CreateGameObject("FxRoot");
            GameObject staleIceFx = CreateGameObject("IceRevealFx");
            staleIceFx.transform.SetParent(fxRoot.transform, false);
            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.FxRoot = fxRoot.transform;

            router.ClearSpawnedFx();

            Assert.That(fxRoot.transform.childCount, Is.EqualTo(0));
        }

        [Test]
        public void FxEventRouter_DiagnosticsLogPrefabAssignmentAndPosition()
        {
            GameObject fxRoot = CreateGameObject("FxRoot");
            GameObject prefab = CreateGameObject("GroupClearPrefab");
            FxVisualRegistry registry = ScriptableObject.CreateInstance<FxVisualRegistry>();
            createdObjects.Add(registry);
            registry.GroupClearFx = prefab;

            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.FxRoot = fxRoot.transform;
            router.FxRegistry = registry;
            router.DiagnosticsEnabled = true;

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(
                "\\[FX Diagnostics\\] hook=GroupClear source=GroupRemoved instance=GroupClearFx prefab=GroupClearPrefab assigned=yes position="));

            router.RoutePlaybackBeat(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateState(),
                CreatePlaybackStep(ActionPlaybackStepType.RemoveGroup, new GroupRemoved(
                    DebrisType.A,
                    ImmutableArray.Create(new TileCoord(9, 9)))));
        }

        [Test]
        public void SpriteSequenceFxPlayer_FrameSteppingClampsAndWrapsInEditMode()
        {
            GameObject gameObject = CreateGameObject("SpriteSequenceFx");
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            SpriteSequenceFxPlayer player = gameObject.AddComponent<SpriteSequenceFxPlayer>();
            Sprite[] frames =
            {
                CreateSprite(Color.red),
                CreateSprite(Color.green),
                CreateSprite(Color.blue),
            };
            SetPrivateField(player, "frames", frames);

            player.SetFrameIndex(99);
            Assert.That(player.CurrentFrameIndex, Is.EqualTo(2));
            Assert.That(renderer.sprite, Is.SameAs(frames[2]));

            player.NextFrame();
            Assert.That(player.CurrentFrameIndex, Is.EqualTo(0));
            Assert.That(renderer.sprite, Is.SameAs(frames[0]));

            player.PreviousFrame();
            Assert.That(player.CurrentFrameIndex, Is.EqualTo(2));
            Assert.That(renderer.sprite, Is.SameAs(frames[2]));

            player.SetFrameIndex(-5);
            Assert.That(player.CurrentFrameIndex, Is.EqualTo(0));
            Assert.That(renderer.sprite, Is.SameAs(frames[0]));

            player.NextFrame();
            player.StopPlayback();
            Assert.That(player.CurrentFrameIndex, Is.EqualTo(0));
        }

        [Test]
        public void SpriteSequenceFxPlayer_FramePlaybackSpeedScalesFromAuthoredDelay()
        {
            GameObject gameObject = CreateGameObject("SpriteSequenceFx");
            gameObject.AddComponent<SpriteRenderer>();
            SpriteSequenceFxPlayer player = gameObject.AddComponent<SpriteSequenceFxPlayer>();
            SetPrivateField(player, "secondsPerFrame", 0.08f);

            player.SetFramePlaybackSpeedMultiplier(2.0f);

            Assert.That(player.AuthoredSecondsPerFrame, Is.EqualTo(0.08f));
            Assert.That(player.SecondsPerFrame, Is.EqualTo(0.04f));

            player.SetFramePlaybackSpeedMultiplier(0.5f);

            Assert.That(player.AuthoredSecondsPerFrame, Is.EqualTo(0.08f));
            Assert.That(player.SecondsPerFrame, Is.EqualTo(0.16f));
        }

        [UnityTest]
        public System.Collections.IEnumerator SpriteSequenceFxPlayer_NonLoopingDestroyAfterPlaybackDestroysSelf()
        {
            GameObject gameObject = CreateGameObject("IceClearFx");
            gameObject.AddComponent<SpriteRenderer>();
            SpriteSequenceFxPlayer player = gameObject.AddComponent<SpriteSequenceFxPlayer>();
            SetPrivateField(player, "frames", new[] { CreateSprite(Color.white) });
            SetPrivateField(player, "secondsPerFrame", 0f);
            SetPrivateField(player, "loop", false);
            player.DestroyAfterPlayback = true;

            player.StartPlayback();
            yield return null;
            yield return null;

            Assert.That(gameObject == null, Is.True, "A non-looping ice clear FX should destroy its GameObject after the final frame.");
        }

        [Test]
        public void FxDebugCatalog_IncludesActiveFallbackAndUnhookedPrefabs()
        {
            FxVisualRegistry? loadedRegistry = AssetDatabase.LoadAssetAtPath<FxVisualRegistry>(
                "Assets/Rescue.Unity/Art/Registries/Phase1FxVisualRegistry.asset");
            Assert.That(loadedRegistry, Is.Not.Null);
            FxVisualRegistry registry = loadedRegistry ?? throw new AssertionException("Expected Phase 1 FX registry.");

            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.FxRegistry = registry;

            List<FxDebugCandidate> groupCandidates = FxDebugCatalog.GetCandidates(router, FxEventHook.GroupClear);
            Assert.That(groupCandidates.Exists(candidate =>
                ReferenceEquals(candidate.Prefab, registry.GroupClearFx) &&
                candidate.IsActive &&
                candidate.Label.Contains("[active]")), Is.True);

            List<FxDebugCandidate> dockWarningCandidates = FxDebugCatalog.GetCandidates(router, FxEventHook.DockWarning);
            Assert.That(dockWarningCandidates.Exists(candidate =>
                ReferenceEquals(candidate.Prefab, registry.DockInsertFx) &&
                candidate.IsFallback &&
                candidate.Label.Contains("[fallback]")), Is.True);

            List<FxDebugCandidate> unhookedCandidates = FxDebugCatalog.GetCandidates(router, null);
            Assert.That(unhookedCandidates.Exists(candidate =>
                candidate.IsUnhooked &&
                candidate.Label.Contains("[unhooked]") &&
                AssetDatabase.GetAssetPath(candidate.Prefab).StartsWith("Assets/Rescue.Unity/Art/Prefabs/", System.StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void FxDebugCatalog_LabelsEmptyPrefabAsNoSpriteRenderer()
        {
            GameObject emptyPrefab = CreateGameObject("EmptyFxShell");

            Assert.That(FxDebugFramePlayer.HasInspectableRenderer(emptyPrefab), Is.False);
            Assert.That(FxDebugFramePlayer.HasFramePlayer(emptyPrefab), Is.False);
        }

        [Test]
        public void FxEventRouter_ManualSpawnAppliesPresentationPlaneRotationAndSurfaceOffset()
        {
            GameState state = CreateState();
            BoardGridViewPresenter grid = CreateGrid(state);
            grid.transform.rotation = Quaternion.Euler(20f, 5f, 0f);
            GameObject fxRoot = CreateGameObject("FxRoot");
            GameObject prefab = CreateSpriteFxPrefab("ManualFx");

            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.BoardGrid = grid;
            router.FxRoot = fxRoot.transform;
            router.SpawnedFxPlaneEulerOffset = new Vector3(80f, 0f, 10f);
            router.SpawnedFxSurfaceOffset = 0.5f;

            Vector3 boardCenter = grid.GetCellWorldPosition(new TileCoord(1, 1));
            GameObject? spawned = router.SpawnManualDebugFx(prefab, "ManualFxInstance", FxEventHook.GroupClear, boardCenter);

            Assert.That(spawned, Is.Not.Null);
            Vector3 expectedPosition = boardCenter + (grid.transform.up * 0.5f);
            Quaternion expectedRotation = grid.transform.rotation * Quaternion.Euler(80f, 0f, 10f);
            GameObject spawnedInstance = spawned ?? throw new AssertionException("Expected manual FX to spawn.");
            AssertVector3Equal(expectedPosition, spawnedInstance.transform.position);
            Assert.That(Quaternion.Angle(expectedRotation, spawnedInstance.transform.rotation), Is.LessThanOrEqualTo(0.001f));

            SpriteSequenceFxPlayer? player = spawnedInstance.GetComponent<SpriteSequenceFxPlayer>();
            Assert.That(player, Is.Not.Null);
            SpriteSequenceFxPlayer playerComponent = player ?? throw new AssertionException("Expected manual FX player.");
            Assert.That(playerComponent.DestroyAfterPlayback, Is.False);
            Assert.That(playerComponent.IsPlaying, Is.False);
        }

        [Test]
        public void FxEventRouter_ManualSpawnComposesPrefabPose()
        {
            GameState state = CreateState();
            BoardGridViewPresenter grid = CreateGrid(state);
            grid.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            GameObject fxRoot = CreateGameObject("FxRoot");
            GameObject prefab = CreateSpriteFxPrefab("ManualFx");
            prefab.transform.localPosition = new Vector3(0f, 0f, -0.5f);
            prefab.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);

            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.BoardGrid = grid;
            router.FxRoot = fxRoot.transform;
            router.SpawnedFxPlaneEulerOffset = new Vector3(90f, 0f, 0f);
            router.SpawnedFxSurfaceOffset = 0f;

            Vector3 boardCenter = grid.GetCellWorldPosition(new TileCoord(1, 1));
            GameObject? spawned = router.SpawnManualDebugFx(prefab, "ManualFxInstance", FxEventHook.IceReveal, boardCenter);

            Assert.That(spawned, Is.Not.Null);
            Quaternion presentationRotation = grid.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
            Vector3 expectedPosition = boardCenter + (presentationRotation * prefab.transform.localPosition);
            Quaternion expectedRotation = presentationRotation * prefab.transform.localRotation;
            GameObject spawnedInstance = spawned ?? throw new AssertionException("Expected manual FX to spawn.");
            AssertVector3Equal(expectedPosition, spawnedInstance.transform.position);
            Assert.That(Quaternion.Angle(expectedRotation, spawnedInstance.transform.rotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        public void FxEventRouter_ManualSpawnAddsDebugPlayerWhenPrefabHasSpriteRendererOnly()
        {
            GameObject fxRoot = CreateGameObject("FxRoot");
            GameObject prefab = CreateSpriteOnlyFxPrefab("UnhookedFx", CreateSprite(Color.red));

            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.FxRoot = fxRoot.transform;

            GameObject? spawned = router.SpawnManualDebugFx(prefab, "ManualUnhookedFx", FxEventHook.GroupClear, Vector3.zero);

            GameObject instance = spawned ?? throw new AssertionException("Expected manual FX to spawn.");
            Assert.That(prefab.GetComponent<SpriteSequenceFxPlayer>(), Is.Null, "Manual inspection should not add a player to the source prefab.");
            SpriteSequenceFxPlayer? player = instance.GetComponentInChildren<SpriteSequenceFxPlayer>(includeInactive: true);
            Assert.That(player, Is.Not.Null, "Manual inspection should add a debug player to the spawned instance.");
            Assert.That(player?.FrameCount, Is.EqualTo(1));
            Assert.That(player?.DestroyAfterPlayback, Is.False);
            Assert.That(player?.IsPlaying, Is.False);
        }

        [Test]
        public void FxDebugFramePlayer_DebugPlayerStepsThroughDeterministicChildRendererFrames()
        {
            Sprite firstFrame = CreateSprite(Color.red);
            Sprite secondFrame = CreateSprite(Color.green);
            GameObject prefab = CreateSpriteOnlyFxPrefab("UnhookedFx", firstFrame);
            GameObject child = CreateGameObject("ChildRenderer");
            child.transform.SetParent(prefab.transform, false);
            child.AddComponent<SpriteRenderer>().sprite = secondFrame;

            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();

            GameObject? spawned = router.SpawnManualDebugFx(prefab, "ManualUnhookedFx", FxEventHook.GroupClear, Vector3.zero);

            GameObject instance = spawned ?? throw new AssertionException("Expected manual FX to spawn.");
            SpriteSequenceFxPlayer player = instance.GetComponentInChildren<SpriteSequenceFxPlayer>(includeInactive: true)
                ?? throw new AssertionException("Expected debug player.");
            SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();

            Assert.That(player.FrameCount, Is.EqualTo(2));
            Assert.That(player.CurrentFrameIndex, Is.EqualTo(0));
            Assert.That(renderer.sprite, Is.SameAs(firstFrame));

            player.NextFrame();

            Assert.That(player.CurrentFrameIndex, Is.EqualTo(1));
            Assert.That(renderer.sprite, Is.SameAs(secondFrame));
        }

        [Test]
        public void FxEventRouter_RuntimeRouteDoesNotAttachDebugPlayerToHookedPrefab()
        {
            GameObject fxRoot = CreateGameObject("FxRoot");
            GameObject hookedPrefab = CreateSpriteOnlyFxPrefab("HookedFxWithoutPlayer", CreateSprite(Color.red));
            FxVisualRegistry registry = ScriptableObject.CreateInstance<FxVisualRegistry>();
            createdObjects.Add(registry);
            registry.GroupClearFx = hookedPrefab;
            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.FxRoot = fxRoot.transform;
            router.FxRegistry = registry;

            router.Route(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))));

            Transform spawned = fxRoot.transform.Find(nameof(FxVisualRegistry.GroupClearFx))
                ?? throw new AssertionException("Expected hooked runtime FX to spawn.");
            Assert.That(spawned.GetComponentInChildren<SpriteSequenceFxPlayer>(includeInactive: true), Is.Null);
            Assert.That(hookedPrefab.GetComponentInChildren<SpriteSequenceFxPlayer>(includeInactive: true), Is.Null);
        }

        [Test]
        public void FxEventRouter_ManualInspectionDoesNotModifyPrefabAssetByDefault()
        {
            GameObject prefabSource = CreateSpriteOnlyFxPrefab("TempDebugInspectionFx", CreateSprite(Color.red));
            GameObject? savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabSource, TempFxPrefabPath);
            Assert.That(savedPrefab, Is.Not.Null);
            AssetDatabase.ImportAsset(TempFxPrefabPath);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TempFxPrefabPath)
                ?? throw new AssertionException("Expected temp prefab asset.");
            Assert.That(prefabAsset.GetComponentInChildren<SpriteSequenceFxPlayer>(includeInactive: true), Is.Null);

            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();

            GameObject? spawned = router.SpawnManualDebugFx(prefabAsset, "ManualAssetFx", FxEventHook.GroupClear, Vector3.zero);

            Assert.That(spawned, Is.Not.Null);
            Assert.That(spawned?.GetComponentInChildren<SpriteSequenceFxPlayer>(includeInactive: true), Is.Not.Null);
            GameObject reloadedPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TempFxPrefabPath)
                ?? throw new AssertionException("Expected temp prefab asset after manual inspection.");
            Assert.That(reloadedPrefabAsset.GetComponentInChildren<SpriteSequenceFxPlayer>(includeInactive: true), Is.Null);
        }

        [UnityTest]
        public System.Collections.IEnumerator FxEventRouter_PlayAllRegisteredFxForDiagnosticsSpawnsRegistryPrefabs()
        {
            GameObject fxRoot = CreateGameObject("FxRoot");
            GameObject groupPrefab = CreateSpriteFxPrefab("GroupFx");
            GameObject invalidPrefab = CreateSpriteFxPrefab("InvalidFx");
            FxVisualRegistry registry = ScriptableObject.CreateInstance<FxVisualRegistry>();
            createdObjects.Add(registry);
            registry.GroupClearFx = groupPrefab;
            registry.InvalidTapFx = invalidPrefab;

            FxEventRouter router = CreateGameObject("FxRouter").AddComponent<FxEventRouter>();
            router.FxRoot = fxRoot.transform;
            router.FxRegistry = registry;

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("\\[FX Diagnostics\\] hook=GroupClear"));
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("\\[FX Diagnostics\\] hook=InvalidTap"));

            router.PlayAllRegisteredFxForDiagnostics(new Vector3(1f, 2f, 3f), spacingSeconds: 0f);

            yield return null;
            yield return null;

            Assert.That(fxRoot.transform.Find(nameof(FxVisualRegistry.GroupClearFx)), Is.Not.Null);
            Assert.That(fxRoot.transform.Find(nameof(FxVisualRegistry.InvalidTapFx)), Is.Not.Null);
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
                new VineGrown(new TileCoord(1, 1)),
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
                FxEventHook.VineGrowth,
                FxEventHook.DockWarning,
                FxEventHook.WaterRise,
                FxEventHook.NearRescueRelief,
                FxEventHook.TargetExtraction,
                FxEventHook.LossWaterOnTarget,
            }));
        }

        private SpyFxEventRouter CreateRouter(BoardGridViewPresenter? grid = null, DockViewPresenter? dock = null)
        {
            GameObject gameObject = CreateGameObject("SpyFxRouter");
            SpyFxEventRouter router = gameObject.AddComponent<SpyFxEventRouter>();
            router.BoardGrid = grid;
            router.DockView = dock;
            return router;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private GameObject CreateSpriteFxPrefab(string name, int frameCount = 0)
        {
            GameObject prefab = CreateGameObject(name);
            prefab.SetActive(false);
            SpriteRenderer renderer = prefab.AddComponent<SpriteRenderer>();
            SpriteSequenceFxPlayer player = prefab.AddComponent<SpriteSequenceFxPlayer>();
            if (frameCount > 0)
            {
                Sprite[] frames = new Sprite[frameCount];
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    frames[frameIndex] = CreateSprite(Color.Lerp(Color.white, Color.cyan, frameIndex / Mathf.Max(1f, frameCount - 1f)));
                }

                renderer.sprite = frames[0];
                SetPrivateField(player, "frames", frames);
                SetPrivateField(player, "loop", false);
                player.DestroyAfterPlayback = true;
            }

            return prefab;
        }

        private GameObject CreateSpriteOnlyFxPrefab(string name, Sprite sprite)
        {
            GameObject prefab = CreateGameObject(name);
            SpriteRenderer renderer = prefab.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return prefab;
        }

        private Sprite CreateSprite(Color color)
        {
            return CreateSprite(color, width: 1, height: 1);
        }

        private Sprite CreateSprite(Color color, int width, int height, float pixelsPerUnit = 100f)
        {
            Texture2D texture = new Texture2D(width, height);
            createdObjects.Add(texture);
            Color[] pixels = new Color[width * height];
            for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
            {
                pixels[pixelIndex] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            createdObjects.Add(sprite);
            return sprite;
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

        private static GameState CreateEmptyState(int width, int height)
        {
            ImmutableArray<ImmutableArray<Tile>>.Builder rows = ImmutableArray.CreateBuilder<ImmutableArray<Tile>>(height);
            for (int row = 0; row < height; row++)
            {
                ImmutableArray<Tile>.Builder tiles = ImmutableArray.CreateBuilder<Tile>(width);
                for (int col = 0; col < width; col++)
                {
                    tiles.Add(new EmptyTile());
                }

                rows.Add(tiles.ToImmutable());
            }

            return CreateState() with
            {
                Board = new Board(width, height, rows.ToImmutable()),
                Targets = ImmutableArray<TargetState>.Empty,
            };
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

        private FxEventRouter CreateWaterRiseRouter(BoardGridViewPresenter grid, Transform fxRoot, string prefabName)
        {
            Sprite sprite = CreateSprite(Color.cyan, width: 100, height: 20);
            GameObject prefab = CreateSpriteOnlyFxPrefab(prefabName, sprite);
            FxVisualRegistry registry = ScriptableObject.CreateInstance<FxVisualRegistry>();
            createdObjects.Add(registry);
            registry.WaterRiseFx = prefab;

            FxEventRouter router = CreateGameObject($"{prefabName}Router").AddComponent<FxEventRouter>();
            router.BoardGrid = grid;
            router.FxRoot = fxRoot;
            router.FxRegistry = registry;
            return router;
        }

        private DockViewPresenter CreateDockView(GameState state)
        {
            GameObject dockObject = CreateGameObject("Dock");
            dockObject.transform.position = new Vector3(20f, 3f, -2f);
            DockViewPresenter dockPresenter = dockObject.AddComponent<DockViewPresenter>();
            Transform pieceContainer = CreateGameObject("DockPieces").transform;
            pieceContainer.SetParent(dockObject.transform, false);
            GameObject fallbackPiecePrefab = CreateGameObject("FallbackPiecePrefab");

            for (int slotIndex = 0; slotIndex < DockViewPresenter.Phase1SlotCount; slotIndex++)
            {
                GameObject anchor = CreateGameObject($"Slot_{slotIndex:00}");
                anchor.transform.SetParent(dockObject.transform, false);
                anchor.transform.localPosition = new Vector3(slotIndex * 2f, 0.5f, 0f);
            }

            SetPrivateField(dockPresenter, "pieceContainer", pieceContainer);
            SetPrivateField(dockPresenter, "fallbackPiecePrefab", fallbackPiecePrefab);
            dockPresenter.Rebuild(state);
            return dockPresenter;
        }

        private static GameState CreateDockState(
            DebrisType? slot0,
            DebrisType? slot1,
            DebrisType? slot2,
            DebrisType? slot3,
            DebrisType? slot4,
            DebrisType? slot5,
            DebrisType? slot6)
        {
            return CreateState() with
            {
                Dock = new Dock(
                    ImmutableArray.Create(slot0, slot1, slot2, slot3, slot4, slot5, slot6),
                    Size: DockViewPresenter.Phase1SlotCount),
            };
        }

        private static Vector3 GetDockSlotPosition(DockViewPresenter dock, int slotIndex)
        {
            bool found = dock.TryGetSlotWorldPosition(slotIndex, out Vector3 position);
            Assert.That(found, Is.True, $"Expected dock slot {slotIndex} to resolve.");
            return position;
        }

        private static void SetPrivateField(object target, string fieldName, object? value)
        {
            System.Reflection.FieldInfo? field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}'.");
            field?.SetValue(target, value);
        }

        private static bool GetSerializedBool(Object target, string propertyName)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Expected serialized property '{propertyName}'.");
            return property.boolValue;
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

            public int VineGrowthCount { get; private set; }

            public int WinCount { get; private set; }

            public int LossDockOverflowCount { get; private set; }

            public int LossWaterOnTargetCount { get; private set; }

            public Vector3 LastGroupClearPosition { get; private set; }

            public Vector3 LastTargetExtractionPosition { get; private set; }

            public Vector3 LastWaterRisePosition { get; private set; }

            public Vector3 LastDockInsertPosition { get; private set; }

            public Vector3 LastDockTripleClearPosition { get; private set; }

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

            protected override void PlayDockInsert(Vector3 worldPosition)
            {
                DockInsertCount++;
                LastDockInsertPosition = worldPosition;
            }

            protected override void PlayDockTripleClear(Vector3 worldPosition)
            {
                DockTripleClearCount++;
                LastDockTripleClearPosition = worldPosition;
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

            protected override void PlayVineGrowth(Vector3 worldPosition)
            {
                VineGrowthCount++;
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
