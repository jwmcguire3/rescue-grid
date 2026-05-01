using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Presentation;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.Unity.BoardPresentation.Tests
{
    public sealed class BoardContentViewPresenterTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] is null)
                {
                    continue;
                }

                Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void BoardContentViewPresenter_RendersDebrisTile()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(harness.ContentRoot.GetChild(0).name, Does.Contain("Debris_A"));
        }

        [Test]
        public void BoardContentViewPresenter_SyncImmediatePreservesDebrisPrefabRotation()
        {
            PresenterHarness harness = CreateHarness();
            PieceVisualRegistry pieceRegistry = CreateRegistry<PieceVisualRegistry>();
            GameObject debrisCPrefab = CreateTrackedGameObject("DebrisCPrefab");
            GameObject debrisDPrefab = CreateTrackedGameObject("DebrisDPrefab");
            debrisCPrefab.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            debrisDPrefab.transform.localRotation = Quaternion.Euler(0f, 220f, 0f);
            pieceRegistry.DebrisCPrefab = debrisCPrefab;
            pieceRegistry.DebrisDPrefab = debrisDPrefab;
            SetPrivateField(harness.ContentPresenter, "pieceRegistry", pieceRegistry);
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.C),
                    new DebrisTile(DebrisType.D))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? debrisC = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            GameObject? debrisD = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 1));
            Assert.That(debrisC, Is.Not.Null);
            Assert.That(debrisD, Is.Not.Null);
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), debrisC!.transform.localRotation), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 220f, 0f), debrisD!.transform.localRotation), Is.LessThan(0.001f));

            debrisC.transform.localRotation = Quaternion.identity;
            debrisD.transform.localRotation = Quaternion.identity;
            harness.ContentPresenter.ForceSyncToState(state);

            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), debrisC.transform.localRotation), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 220f, 0f), debrisD.transform.localRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void BoardContentViewPresenter_SyncImmediatePopulatesDebrisRegistryCorrectly()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? debrisObject = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(debrisObject, Is.Not.Null);
            Assert.That(debrisObject!.name, Does.Contain("Debris_A"));
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
        }

        [Test]
        public void BoardContentViewPresenter_SpawnedDebrisCarriesBoardCellViewCoord()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? debrisObject = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(debrisObject, Is.Not.Null);
            BoardCellView? cellView = debrisObject!.GetComponent<BoardCellView>();

            Assert.That(cellView, Is.Not.Null);
            Assert.That(cellView!.Coord, Is.EqualTo(new TileCoord(0, 0)));
        }

        [Test]
        public void BoardContentViewPresenter_FindsNearestDebrisVisualCoordInScreenSpace()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A),
                    new EmptyTile())));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);
            Camera camera = CreateTopDownCamera();
            GameObject? debrisObject = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 1));
            Assert.That(debrisObject, Is.Not.Null);
            Vector2 screenPosition = camera.WorldToScreenPoint(debrisObject!.transform.position);

            bool found = harness.ContentPresenter.TryFindNearestDebrisVisualCoord(
                camera,
                screenPosition + new Vector2(6f, 0f),
                state,
                24f,
                out TileCoord coord,
                out GameObject? visualObject);

            Assert.That(found, Is.True);
            Assert.That(coord, Is.EqualTo(new TileCoord(0, 1)));
            Assert.That(visualObject, Is.EqualTo(debrisObject));
        }

        [Test]
        public void BoardContentViewPresenter_RendersCrateIceVine()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            blockerRegistry.FallbackBlockerPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new BlockerTile(BlockerType.Crate, 1, null),
                    new BlockerTile(BlockerType.Ice, 1, null),
                    new BlockerTile(BlockerType.Vine, 1, null))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(3));
            Assert.That(FindChildByName(harness.ContentRoot, "Blocker_Crate"), Is.Not.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Blocker_Ice"), Is.Not.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Blocker_Vine"), Is.Not.Null);
        }

        [Test]
        public void BoardContentViewPresenter_SyncImmediatePopulatesBlockerRegistryCorrectlyWhenBlockersExist()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            blockerRegistry.FallbackBlockerPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new BlockerTile(BlockerType.Crate, 1, null))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? blockerObject = GetRegisteredPieceObject(harness.ContentPresenter, "Blockers", new TileCoord(0, 0));
            Assert.That(blockerObject, Is.Not.Null);
            Assert.That(blockerObject!.name, Does.Contain("Blocker_Crate"));
        }

        [Test]
        public void BoardContentViewPresenter_RendersHiddenIceDebris()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            blockerRegistry.FallbackBlockerPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.B)))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(2));
            Transform? ice = FindChildByName(harness.ContentRoot, "Blocker_Ice");
            Transform? hidden = FindChildByName(harness.ContentRoot, "HiddenDebris_B");

            Assert.That(ice, Is.Not.Null);
            Assert.That(hidden, Is.Not.Null);
            Assert.That(hidden!.position.y, Is.LessThan(ice!.position.y));
        }

        [Test]
        public void BoardContentViewPresenter_SyncImmediatePopulatesHiddenDebrisRegistryByCoord()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            blockerRegistry.FallbackBlockerPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.B)))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? hiddenDebrisObject = GetRegisteredPieceObject(harness.ContentPresenter, "HiddenDebris", new TileCoord(0, 0));
            Assert.That(hiddenDebrisObject, Is.Not.Null);
            Assert.That(hiddenDebrisObject!.name, Does.Contain("HiddenDebris_B"));
        }

        [Test]
        public void BoardContentViewPresenter_DoesNotRenderExtractedTarget()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: true))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(0));
        }

        [Test]
        public void BoardContentViewPresenter_RendersUnextractedTarget()
        {
            PresenterHarness harness = CreateHarness();
            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.FallbackTargetPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(FindChildByName(harness.ContentRoot, "Target_puppy-1"), Is.Not.Null);
            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? targetObject), Is.True);
            Assert.That(targetObject, Is.Not.Null);
        }

        [Test]
        public void BoardContentViewPresenter_AppliesTargetReadinessMarker()
        {
            PresenterHarness harness = CreateHarness();
            harness.FallbackPrefab.AddComponent<MeshRenderer>();
            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.FallbackTargetPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);

            GameState state = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))),
                ImmutableArray.Create(new TargetState("puppy-1", new TileCoord(0, 0), TargetReadiness.OneClearAway)));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? targetObject), Is.True);
            Assert.That(targetObject, Is.Not.Null);
            Assert.That(targetObject!.transform.localScale.x, Is.GreaterThan(1f));
            AssertTargetHasNoReadinessTint(targetObject);
            Transform? marker = targetObject.transform.Find("TargetReadabilityMarker_OneClearAway");
            Assert.That(marker, Is.Not.Null);
            AssertNeutralCircleMarker(marker!.gameObject, maxDiameter: 0.75f);

            GameState trappedState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))),
                ImmutableArray.Create(new TargetState("puppy-1", new TileCoord(0, 0), TargetReadiness.Trapped)));

            harness.ContentPresenter.SyncImmediate(trappedState);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? updatedTargetObject), Is.True);
            Assert.That(updatedTargetObject, Is.Not.Null);
            Assert.That(updatedTargetObject!.transform.Find("TargetReadabilityMarker_OneClearAway"), Is.Null);
            Assert.That(updatedTargetObject.transform.Find("TargetReadabilityMarker"), Is.Null);
        }

        [Test]
        public void BoardContentViewPresenter_RendersAndClearsVinePreview()
        {
            PresenterHarness harness = CreateHarness();
            GameState previewState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile())),
                vine: new VineState(
                    ActionsSinceLastClear: 3,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray.Create(new TileCoord(0, 0)),
                    PriorityCursor: 0,
                    PendingGrowthTile: new TileCoord(0, 0)));
            GameState resetState = previewState with
            {
                Vine = previewState.Vine with { ActionsSinceLastClear = 0, PendingGrowthTile = null },
            };

            harness.GridPresenter.RebuildGrid(previewState);
            harness.ContentPresenter.SyncImmediate(previewState);

            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Not.Null);

            harness.ContentPresenter.SyncImmediate(resetState);

            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Null);
        }

        [Test]
        public void BoardContentViewPresenter_RendersAndClearsRescuePath()
        {
            PresenterHarness harness = CreateHarness();
            GameState pathState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new RescuePathTile(ImmutableArray.Create("puppy-1")))));
            GameState emptyState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile())));

            harness.GridPresenter.RebuildGrid(pathState);
            harness.ContentPresenter.SyncImmediate(pathState);

            GameObject? pathObject = GetRegisteredPieceObject(harness.ContentPresenter, "RescuePath", new TileCoord(0, 0));
            Assert.That(pathObject, Is.Not.Null);
            Assert.That(pathObject!.name, Does.Contain("RescuePath"));
            AssertRescuePathMarkerShape(pathObject);
            Assert.That(pathObject.transform.position.y, Is.GreaterThan(0.17f));

            harness.ContentPresenter.SyncImmediate(emptyState);

            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "RescuePath", new TileCoord(0, 0)), Is.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "RescuePath"), Is.Null);
        }

        [Test]
        public void BoardContentViewPresenter_AnimateRescuePathLockedCreatesMarker()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new EmptyTile())));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.AnimateRescuePathLocked(new TargetRescuePathLocked(
                "puppy-1",
                ImmutableArray.Create(new TileCoord(0, 0))));

            GameObject? pathObject = GetRegisteredPieceObject(harness.ContentPresenter, "RescuePath", new TileCoord(0, 0));
            Assert.That(pathObject, Is.Not.Null);
            Assert.That(pathObject!.name, Does.Contain("RescuePath"));
            AssertRescuePathMarkerShape(pathObject);
        }

        [Test]
        public void BoardContentViewPresenter_AnimateRescuePathLockedOrientsMarkerBeforeSpawning()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new TargetTile("puppy-1", Extracted: false),
                    new EmptyTile())));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);
            harness.ContentPresenter.AnimateRescuePathLocked(new TargetRescuePathLocked(
                "puppy-1",
                ImmutableArray.Create(new TileCoord(0, 1))));

            GameObject? pathObject = GetRegisteredPieceObject(harness.ContentPresenter, "RescuePath", new TileCoord(0, 1));
            Assert.That(pathObject, Is.Not.Null);
            AssertRescuePathMarkerShape(pathObject!);
            AssertRescuePathDirection(pathObject, Vector3.right);
        }

        [Test]
        public void BoardContentViewPresenter_RescuePathRendersWithoutFallbackPrefab()
        {
            PresenterHarness harness = CreateHarness();
            SetPrivateField(harness.ContentPresenter, "fallbackContentPrefab", null);
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new RescuePathTile(ImmutableArray.Create("puppy-1")))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? pathObject = GetRegisteredPieceObject(harness.ContentPresenter, "RescuePath", new TileCoord(0, 0));
            Assert.That(pathObject, Is.Not.Null);
            Assert.That(pathObject!.name, Does.Contain("RescuePath"));
            AssertRescuePathMarkerShape(pathObject);
            Assert.That(pathObject.GetComponentsInChildren<Collider>(includeInactive: true), Is.Empty);
        }

        [TestCase(1, 1, 1, 2, 1f, 0f)]
        [TestCase(1, 1, 1, 0, -1f, 0f)]
        [TestCase(1, 1, 0, 1, 0f, 1f)]
        [TestCase(1, 1, 2, 1, 0f, -1f)]
        public void BoardContentViewPresenter_RescuePathChevronsPointOutwardFromTarget(
            int targetRow,
            int targetCol,
            int pathRow,
            int pathCol,
            float expectedX,
            float expectedZ)
        {
            PresenterHarness harness = CreateHarness();
            TileCoord targetCoord = new TileCoord(targetRow, targetCol);
            TileCoord pathCoord = new TileCoord(pathRow, pathCol);
            GameState state = CreateState(
                CreateRowsWithTargetAndPath(3, 3, targetCoord, pathCoord, ImmutableArray.Create("puppy-1")),
                ImmutableArray.Create(new TargetState("puppy-1", targetCoord, TargetReadiness.OneClearAway)));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? pathObject = GetRegisteredPieceObject(harness.ContentPresenter, "RescuePath", pathCoord);
            Assert.That(pathObject, Is.Not.Null);
            AssertRescuePathDirection(pathObject!, new Vector3(expectedX, 0f, expectedZ));
        }

        [Test]
        public void BoardContentViewPresenter_RescuePathChevronsUseFirstAdjacentResolvedTarget()
        {
            PresenterHarness harness = CreateHarness();
            TileCoord pathCoord = new TileCoord(1, 1);
            TileCoord adjacentTargetCoord = new TileCoord(1, 0);
            TileCoord nonAdjacentTargetCoord = new TileCoord(0, 0);
            GameState state = CreateState(
                CreateRowsWithTargetsAndPath(
                    3,
                    3,
                    pathCoord,
                    ImmutableArray.Create(
                        ("far", nonAdjacentTargetCoord),
                        ("puppy-1", adjacentTargetCoord)),
                    ImmutableArray.Create("missing", "far", "puppy-1")),
                ImmutableArray.Create(
                    new TargetState("far", nonAdjacentTargetCoord, TargetReadiness.OneClearAway),
                    new TargetState("puppy-1", adjacentTargetCoord, TargetReadiness.OneClearAway)));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? pathObject = GetRegisteredPieceObject(harness.ContentPresenter, "RescuePath", pathCoord);
            Assert.That(pathObject, Is.Not.Null);
            AssertRescuePathDirection(pathObject!, Vector3.right);
        }

        [Test]
        public void BoardContentViewPresenter_LastObstacleMarkerSkipsOpenRescuePathTiles()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(
                        new DebrisTile(DebrisType.A),
                        new TargetTile("puppy-1", Extracted: false),
                        new RescuePathTile(ImmutableArray.Create("puppy-1")))),
                ImmutableArray.Create(new TargetState(
                    "puppy-1",
                    new TileCoord(0, 1),
                    TargetReadiness.OneClearAway)));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? debrisObject = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(debrisObject, Is.Not.Null);
            Transform? marker = FindChildByName(debrisObject!.transform, "TargetLastObstacle");
            Assert.That(marker, Is.Not.Null);
            AssertNeutralCircleMarker(marker!.gameObject, maxDiameter: 0.65f);

            GameState progressedState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(
                        new DebrisTile(DebrisType.A),
                        new TargetTile("puppy-1", Extracted: false),
                        new RescuePathTile(ImmutableArray.Create("puppy-1")))),
                ImmutableArray.Create(new TargetState(
                    "puppy-1",
                    new TileCoord(0, 1),
                    TargetReadiness.Progressing)));

            harness.ContentPresenter.SyncImmediate(progressedState);

            Assert.That(FindChildByName(debrisObject.transform, "TargetLastObstacle"), Is.Null);
        }

        [Test]
        public void BoardContentViewPresenter_ClearContentRemovesGeneratedObjects()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);
            harness.ContentPresenter.ClearContent();

            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(0));
        }

        [Test]
        public void BoardContentViewPresenter_SyncImmediateDoesNotDuplicateContent()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new BlockerTile(BlockerType.Crate, 1, null))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(2));
        }

        [Test]
        public void BoardContentViewPresenter_RebuildContentStillPerformsImmediateSync()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.RebuildContent(state);

            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(harness.ContentRoot.GetChild(0).name, Does.Contain("Debris_A"));
        }

        [Test]
        public void BoardContentViewPresenter_ApplyPlaybackSettingsOverridesInternalDurations()
        {
            PresenterHarness harness = CreateHarness();
            ActionPlaybackSettings settings = new ActionPlaybackSettings();
            SetPrivateField(settings, "gravityDurationSeconds", 0.19f);
            SetPrivateField(settings, "breakBlockerOrRevealDurationSeconds", 0.09f);
            SetPrivateField(settings, "spawnDurationSeconds", 0.14f);
            SetPrivateField(settings, "targetExtractDurationSeconds", 0.17f);
            SetPrivateField(settings, "boardPieceLandingSquashXScale", 1.04f);
            SetPrivateField(settings, "boardPieceLandingSquashYScale", 0.94f);
            SetPrivateField(settings, "boardPieceLandingBounceDistance", 0.03f);

            harness.ContentPresenter.ApplyPlaybackSettings(settings);

            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "gravityDurationSeconds"), Is.EqualTo(0.19f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "blockerDamageDurationSeconds"), Is.EqualTo(0.09f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "blockerBreakDurationSeconds"), Is.EqualTo(0.09f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "iceRevealDurationSeconds"), Is.EqualTo(0.09f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "spawnDurationSeconds"), Is.EqualTo(0.14f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "targetExtractDurationSeconds"), Is.EqualTo(0.17f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "boardPieceLandingSquashXScale"), Is.EqualTo(1.04f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "boardPieceLandingSquashYScale"), Is.EqualTo(0.94f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "boardPieceLandingBounceDistance"), Is.EqualTo(0.03f));
        }

        [Test]
        public void BoardContentViewPresenter_SyncImmediateKeepsTrackedTargetInstanceStable()
        {
            PresenterHarness harness = CreateHarness();
            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.FallbackTargetPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? firstTarget), Is.True);
            Assert.That(firstTarget, Is.Not.Null);

            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? secondTarget), Is.True);
            Assert.That(secondTarget, Is.Not.Null);
            Assert.That(secondTarget, Is.SameAs(firstTarget));
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
        }

        [Test]
        public void BoardContentViewPresenter_RemoveDebrisGroupSafelyRemovesMatchingDebris()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.B))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            harness.ContentPresenter.RemoveDebrisGroup(new GroupRemoved(
                DebrisType.A,
                ImmutableArray.Create(new TileCoord(0, 0))));

            Assert.That(FindChildByName(harness.ContentRoot, "Debris_A"), Is.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Debris_B"), Is.Not.Null);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0)), Is.Null);
            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 1)), Is.Not.Null);
        }

        [Test]
        public void BoardContentViewPresenter_AnimateGravityMoveSafelyMovesExistingDebris()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile())));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);
            GameObject? originalDebris = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Vector3 targetPosition = harness.GridPresenter.GetCellWorldPosition(new TileCoord(1, 0)) + new Vector3(0f, 0.05f, 0f);

            harness.ContentPresenter.AnimateGravityMove(new GravitySettled(
                ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0)))));

            Assert.That(FindChildByName(harness.ContentRoot, "Content_01_00_Debris_A"), Is.Not.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Content_00_00_Debris_A"), Is.Null);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0)), Is.Null);
            GameObject? movedDebris = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(1, 0));
            Assert.That(movedDebris, Is.Not.Null);
            Assert.That(movedDebris, Is.SameAs(originalDebris));
            Assert.That(movedDebris!.transform.position, Is.EqualTo(targetPosition).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(movedDebris.transform.localScale, Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void BoardContentViewPresenter_AnimateGravityMoveLandingJuiceRunsWithoutThrowing()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile())));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateGravityMove(
                new GravitySettled(
                    ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0))))));

            GameObject? movedDebris = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(1, 0));
            Assert.That(movedDebris, Is.Not.Null);
            Assert.That(movedDebris!.transform.localScale, Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void BoardContentViewPresenter_AnimateSpawnSafelyAddsDebrisWhenMissing()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile())));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            harness.ContentPresenter.AnimateSpawn(new Spawned(
                ImmutableArray.Create((new TileCoord(0, 1), DebrisType.B))));

            Assert.That(FindChildByName(harness.ContentRoot, "Content_00_01_Debris_B"), Is.Not.Null);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            GameObject? spawnedDebris = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 1));
            Assert.That(spawnedDebris, Is.Not.Null);
            Assert.That(spawnedDebris!.transform.localScale, Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void BoardContentViewPresenter_AnimateSpawnLandingJuiceRunsWithoutThrowing()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile())));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateSpawn(new Spawned(
                ImmutableArray.Create((new TileCoord(0, 1), DebrisType.B)))));

            GameObject? spawnedDebris = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 1));
            Assert.That(spawnedDebris, Is.Not.Null);
            Assert.That(spawnedDebris!.transform.localScale, Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void BoardContentViewPresenter_RepeatedSyncAfterGravityDoesNotDuplicateVisuals()
        {
            PresenterHarness harness = CreateHarness();
            GameState previousState = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile())));
            GameState resultState = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new EmptyTile())));

            harness.GridPresenter.RebuildGrid(previousState);
            harness.ContentPresenter.SyncImmediate(previousState);

            harness.ContentPresenter.AnimateGravityMove(new GravitySettled(
                ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0)))));
            harness.ContentPresenter.SyncImmediate(resultState);
            harness.ContentPresenter.SyncImmediate(resultState);

            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(1, 0)), Is.Not.Null);
        }

        [Test]
        public void BoardContentViewPresenter_RepeatedSyncAfterSpawnDoesNotDuplicateVisuals()
        {
            PresenterHarness harness = CreateHarness();
            GameState previousState = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile())));
            GameState resultState = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new DebrisTile(DebrisType.B))));

            harness.GridPresenter.RebuildGrid(previousState);
            harness.ContentPresenter.SyncImmediate(previousState);

            harness.ContentPresenter.AnimateSpawn(new Spawned(
                ImmutableArray.Create((new TileCoord(0, 1), DebrisType.B))));
            harness.ContentPresenter.SyncImmediate(resultState);
            harness.ContentPresenter.SyncImmediate(resultState);

            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 1)), Is.Not.Null);
        }

        [Test]
        public void BoardContentViewPresenter_SyncImmediateRepairsMissingAndExtraVisualEntries()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? registeredDebris = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(registeredDebris, Is.Not.Null);

            if (registeredDebris is not null)
            {
                Object.DestroyImmediate(registeredDebris);
            }

            GameObject extraVisual = CreateTrackedGameObject("Content_00_00_Debris_Extra");
            extraVisual.transform.SetParent(harness.ContentRoot, false);
            GetSpawnedContentList(harness.ContentPresenter).Add(extraVisual);

            harness.ContentPresenter.SyncImmediate(state);

            GameObject? repairedDebris = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(repairedDebris, Is.Not.Null);
            Assert.That(repairedDebris, Is.Not.SameAs(extraVisual));
            Assert.That(FindChildByName(harness.ContentRoot, "Extra"), Is.Null);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(GetSpawnedContentList(harness.ContentPresenter).Count, Is.EqualTo(1));
        }

        [Test]
        public void BoardContentViewPresenter_SyncImmediateRepairsDebrisScaleAfterLandingJuice()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? registeredDebris = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(registeredDebris, Is.Not.Null);
            registeredDebris!.transform.localScale = new Vector3(1.35f, 0.65f, 1.35f);

            harness.ContentPresenter.SyncImmediate(state);

            GameObject? repairedDebris = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(repairedDebris, Is.SameAs(registeredDebris));
            Assert.That(repairedDebris!.transform.localScale, Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void BoardContentViewPresenter_SyncImmediateRepairsMismatchedDebrisVisuals()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.B))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? registeredDebris = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(registeredDebris, Is.Not.Null);

            if (registeredDebris is not null)
            {
                registeredDebris.name = "Content_00_00_Debris_Wrong";
            }

            harness.ContentPresenter.SyncImmediate(state);

            GameObject? repairedDebris = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(repairedDebris, Is.Not.Null);
            Assert.That(repairedDebris!.name, Does.Contain("Debris_B"));
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
        }

        [Test]
        public void BoardContentViewPresenter_SyncImmediateUsesPrimitiveRescuePathMarkerWhenFallbackPrefabIsUnassigned()
        {
            PresenterHarness harness = CreateHarness();
            SetPrivateField(harness.ContentPresenter, "fallbackContentPrefab", null);
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new RescuePathTile(ImmutableArray.Create("pup")))));

            harness.GridPresenter.RebuildGrid(state);

            Assert.DoesNotThrow(() => harness.ContentPresenter.SyncImmediate(state));
            GameObject? pathObject = GetRegisteredPieceObject(harness.ContentPresenter, "RescuePath", new TileCoord(0, 0));
            Assert.That(pathObject, Is.Not.Null);
            AssertRescuePathMarkerShape(pathObject!);
            Assert.That(pathObject!.GetComponentsInChildren<Collider>(includeInactive: true), Is.Empty);
        }

        [Test]
        public void BoardContentViewPresenter_AnimateTargetExtractSafelyRemovesTrackedTargetVisual()
        {
            PresenterHarness harness = CreateHarness();
            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.FallbackTargetPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? targetBeforeExtract), Is.True);
            Assert.That(targetBeforeExtract, Is.Not.Null);

            harness.ContentPresenter.AnimateTargetExtract(new TargetExtracted("puppy-1", new TileCoord(0, 0)));

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? targetAfterExtract), Is.False);
            Assert.That(targetAfterExtract, Is.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Target_puppy-1"), Is.Null);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(0));
        }

        [Test]
        public void BoardContentViewPresenter_AnimateTargetExtractRemovesOnlyMatchingTargetId()
        {
            PresenterHarness harness = CreateHarness();
            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.FallbackTargetPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new TargetTile("puppy-1", Extracted: false),
                    new TargetTile("puppy-2", Extracted: false))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? firstTarget), Is.True);
            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-2", out GameObject? secondTarget), Is.True);
            Assert.That(firstTarget, Is.Not.Null);
            Assert.That(secondTarget, Is.Not.Null);

            harness.ContentPresenter.AnimateTargetExtract(new TargetExtracted("puppy-1", new TileCoord(0, 0)));

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? removedTarget), Is.False);
            Assert.That(removedTarget, Is.Null);
            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-2", out GameObject? remainingTarget), Is.True);
            Assert.That(remainingTarget, Is.SameAs(secondTarget));
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(FindChildByName(harness.ContentRoot, "Target_puppy-2"), Is.Not.Null);
        }

        [Test]
        public void BoardContentViewPresenter_AnimateTargetExtractSkipsMissingTargetVisualSafely()
        {
            PresenterHarness harness = CreateHarness();

            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateTargetExtract(
                new TargetExtracted("missing-target", new TileCoord(0, 0))));
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(0));
        }

        [Test]
        public void BoardContentViewPresenter_NonTargetAnimationApisFailSoftWhenVisualsAreMissing()
        {
            PresenterHarness harness = CreateHarness();

            Assert.DoesNotThrow(() => harness.ContentPresenter.RemoveDebrisGroup(
                new GroupRemoved(
                    DebrisType.A,
                    ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))));
            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateGravityMove(
                new GravitySettled(
                    ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0))))));
            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateSpawn(
                new Spawned(
                    ImmutableArray.Create((new TileCoord(0, 0), DebrisType.B)))));
        }

        [Test]
        public void BoardContentViewPresenter_GravityLandingJuiceSkipsDestroyedVisualSafely()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile())));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);
            GameObject? registeredDebris = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(registeredDebris, Is.Not.Null);
            Object.DestroyImmediate(registeredDebris);

            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateGravityMove(
                new GravitySettled(
                    ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0))))));
        }

        [Test]
        public void BoardContentViewPresenter_AnimateBlockerDamageBreakAndIceRevealHandleValidVisualsSafely()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            blockerRegistry.FallbackBlockerPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.B)))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(FindChildByName(harness.ContentRoot, "Blocker_Ice"), Is.Not.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "HiddenDebris_B"), Is.Not.Null);

            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateBlockerDamage(
                new BlockerDamaged(new TileCoord(0, 0), BlockerType.Ice, RemainingHp: 0)));
            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateBlockerBreak(
                new BlockerBroken(new TileCoord(0, 0), BlockerType.Ice)));
            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateIceReveal(
                new IceRevealed(new TileCoord(0, 0), DebrisType.B)));

            Assert.That(FindChildByName(harness.ContentRoot, "Blocker_Ice"), Is.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Debris_B"), Is.Not.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "HiddenDebris_B"), Is.Null);
        }

        [Test]
        public void BoardContentViewPresenter_AnimateBlockerDamageUsesRegisteredBlockerVisual()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            blockerRegistry.FallbackBlockerPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new BlockerTile(BlockerType.Crate, 1, null))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? blockerBeforeDamage = GetRegisteredPieceObject(harness.ContentPresenter, "Blockers", new TileCoord(0, 0));
            Assert.That(blockerBeforeDamage, Is.Not.Null);

            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateBlockerDamage(
                new BlockerDamaged(new TileCoord(0, 0), BlockerType.Crate, RemainingHp: 0)));

            GameObject? blockerAfterDamage = GetRegisteredPieceObject(harness.ContentPresenter, "Blockers", new TileCoord(0, 0));
            Assert.That(blockerAfterDamage, Is.SameAs(blockerBeforeDamage));
        }

        [Test]
        public void BoardContentViewPresenter_AnimateBlockerBreakRemovesRegisteredBlockerVisual()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            blockerRegistry.FallbackBlockerPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new BlockerTile(BlockerType.Crate, 1, null))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "Blockers", new TileCoord(0, 0)), Is.Not.Null);

            harness.ContentPresenter.AnimateBlockerBreak(new BlockerBroken(new TileCoord(0, 0), BlockerType.Crate));

            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "Blockers", new TileCoord(0, 0)), Is.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Blocker_Crate"), Is.Null);
        }

        [Test]
        public void BoardContentViewPresenter_AnimateIceRevealPromotesRegisteredHiddenDebrisToDebris()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            blockerRegistry.FallbackBlockerPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.B)))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? hiddenBeforeReveal = GetRegisteredPieceObject(harness.ContentPresenter, "HiddenDebris", new TileCoord(0, 0));
            Assert.That(hiddenBeforeReveal, Is.Not.Null);

            harness.ContentPresenter.AnimateIceReveal(new IceRevealed(new TileCoord(0, 0), DebrisType.B));

            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "HiddenDebris", new TileCoord(0, 0)), Is.Null);
            GameObject? debrisAfterReveal = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(debrisAfterReveal, Is.SameAs(hiddenBeforeReveal));
            Assert.That(debrisAfterReveal!.name, Does.Contain("Debris_B"));
        }

        [Test]
        public void BoardContentViewPresenter_BlockerAndIceAnimationApisFailSoftWhenVisualsAreMissing()
        {
            PresenterHarness harness = CreateHarness();

            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateBlockerDamage(
                new BlockerDamaged(new TileCoord(0, 0), BlockerType.Crate, RemainingHp: 0)));
            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateBlockerBreak(
                new BlockerBroken(new TileCoord(0, 0), BlockerType.Crate)));
            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateIceReveal(
                new IceRevealed(new TileCoord(0, 0), DebrisType.B)));
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(0));
        }

        [Test]
        public void BoardContentViewPresenter_SyncImmediateRepairsTargetVisualAfterExtractionMismatch()
        {
            PresenterHarness harness = CreateHarness();
            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.FallbackTargetPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            harness.ContentPresenter.AnimateTargetExtract(new TargetExtracted("puppy-1", new TileCoord(0, 0)));

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out _), Is.False);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(0));

            harness.ContentPresenter.SyncImmediate(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? repairedTarget), Is.True);
            Assert.That(repairedTarget, Is.Not.Null);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(FindChildByName(harness.ContentRoot, "Target_puppy-1"), Is.Not.Null);
        }

        [Test]
        public void BoardContentViewPresenter_SyncImmediateRepairsBlockerAndHiddenDebrisMismatches()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            blockerRegistry.FallbackBlockerPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);

            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.B)))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            GameObject? blocker = GetRegisteredPieceObject(harness.ContentPresenter, "Blockers", new TileCoord(0, 0));
            GameObject? hiddenDebris = GetRegisteredPieceObject(harness.ContentPresenter, "HiddenDebris", new TileCoord(0, 0));
            Assert.That(blocker, Is.Not.Null);
            Assert.That(hiddenDebris, Is.Not.Null);

            if (blocker is not null)
            {
                blocker.name = "Content_00_00_Blocker_Wrong";
            }

            if (hiddenDebris is not null)
            {
                hiddenDebris.name = "Content_00_00_HiddenDebris_Wrong";
            }

            harness.ContentPresenter.SyncImmediate(state);

            GameObject? repairedBlocker = GetRegisteredPieceObject(harness.ContentPresenter, "Blockers", new TileCoord(0, 0));
            GameObject? repairedHiddenDebris = GetRegisteredPieceObject(harness.ContentPresenter, "HiddenDebris", new TileCoord(0, 0));
            Assert.That(repairedBlocker, Is.Not.Null);
            Assert.That(repairedHiddenDebris, Is.Not.Null);
            Assert.That(repairedBlocker!.name, Does.Contain("Blocker_Ice"));
            Assert.That(repairedHiddenDebris!.name, Does.Contain("HiddenDebris_B"));
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(2));
        }

        private PresenterHarness CreateHarness()
        {
            GameObject presenterObject = CreateTrackedGameObject("BoardPresenter");

            BoardGridViewPresenter gridPresenter = presenterObject.AddComponent<BoardGridViewPresenter>();
            Transform boardRoot = CreateTrackedGameObject("BoardRoot").transform;
            boardRoot.SetParent(presenterObject.transform, false);
            GameObject tileFallbackPrefab = CreateTrackedGameObject("FallbackTilePrefab");

            SetPrivateField(gridPresenter, "boardRoot", boardRoot);
            SetPrivateField(gridPresenter, "dryTilePrefab", null);
            SetPrivateField(gridPresenter, "fallbackTilePrefab", tileFallbackPrefab);

            BoardContentViewPresenter contentPresenter = presenterObject.AddComponent<BoardContentViewPresenter>();
            Transform contentRoot = CreateTrackedGameObject("BoardContentRoot").transform;
            contentRoot.SetParent(presenterObject.transform, false);
            GameObject fallbackPrefab = CreateTrackedGameObject("FallbackContentPrefab");

            PieceVisualRegistry pieceRegistry = CreateRegistry<PieceVisualRegistry>();

            SetPrivateField(contentPresenter, "gridView", gridPresenter);
            SetPrivateField(contentPresenter, "pieceRegistry", pieceRegistry);
            SetPrivateField(contentPresenter, "contentRoot", contentRoot);
            SetPrivateField(contentPresenter, "fallbackContentPrefab", fallbackPrefab);
            SetPrivateField(contentPresenter, "contentYOffset", 0.05f);

            return new PresenterHarness(gridPresenter, contentPresenter, boardRoot, contentRoot, fallbackPrefab);
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private Camera CreateTopDownCamera()
        {
            GameObject cameraObject = CreateTrackedGameObject("TestCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 10f, 0f), Quaternion.Euler(90f, 0f, 0f));
            return camera;
        }

        private T CreateRegistry<T>() where T : ScriptableObject
        {
            T registry = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(registry);
            return registry;
        }

        private static Transform? FindChildByName(Transform parent, string partialName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.Contains(partialName))
                {
                    return child;
                }
            }

            return null;
        }

        private static void SetPrivateField(object target, string fieldName, object? value)
        {
            System.Reflection.FieldInfo? field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}'.");
            if (field is null)
            {
                return;
            }

            field.SetValue(target, value);
        }

        private static GameObject? GetRegisteredPieceObject(
            BoardContentViewPresenter presenter,
            string registryName,
            TileCoord coord)
        {
            object? visualRegistry = GetPrivateFieldValue(presenter, "visualRegistry");
            Assert.That(visualRegistry, Is.Not.Null);
            if (visualRegistry is null)
            {
                return null;
            }

            System.Reflection.PropertyInfo? registryProperty = visualRegistry.GetType().GetProperty(
                registryName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            Assert.That(registryProperty, Is.Not.Null, $"Expected registry '{registryName}'.");
            if (registryProperty is null)
            {
                return null;
            }

            object? registry = registryProperty.GetValue(visualRegistry);
            Assert.That(registry, Is.Not.Null);
            if (registry is null)
            {
                return null;
            }

            object?[] arguments = { coord, null };
            System.Reflection.MethodInfo? tryGetMethod = registry.GetType().GetMethod(
                "TryGet",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            Assert.That(tryGetMethod, Is.Not.Null);
            if (tryGetMethod is null)
            {
                return null;
            }

            bool found = (bool)tryGetMethod.Invoke(registry, arguments)!;
            if (!found || arguments[1] is null)
            {
                return null;
            }

            System.Reflection.PropertyInfo? objectProperty = arguments[1]!.GetType().GetProperty(
                "Object",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            Assert.That(objectProperty, Is.Not.Null);
            return objectProperty?.GetValue(arguments[1]) as GameObject;
        }

        private static List<GameObject> GetSpawnedContentList(BoardContentViewPresenter presenter)
        {
            object? value = GetPrivateFieldValue(presenter, "spawnedContent");
            Assert.That(value, Is.Not.Null);
            return (List<GameObject>)value!;
        }

        private static void AssertRescuePathMarkerShape(GameObject pathObject)
        {
            Assert.That(pathObject.transform.Find("RescuePathWash"), Is.Not.Null);
            Transform? firstChevron = pathObject.transform.Find("RescuePathChevron_00");
            Transform? secondChevron = pathObject.transform.Find("RescuePathChevron_01");
            Assert.That(firstChevron, Is.Not.Null);
            Assert.That(secondChevron, Is.Not.Null);
            Assert.That(firstChevron!.Find("LeftArm"), Is.Not.Null);
            Assert.That(firstChevron.Find("RightArm"), Is.Not.Null);
            Assert.That(secondChevron!.Find("LeftArm"), Is.Not.Null);
            Assert.That(secondChevron.Find("RightArm"), Is.Not.Null);
            Assert.That(pathObject.GetComponentsInChildren<Collider>(includeInactive: true), Is.Empty);
            Assert.That(pathObject.GetComponentsInChildren<Renderer>(includeInactive: true), Has.Length.EqualTo(5));
        }

        private static void AssertUsesDefaultParticleSystemMaterial(GameObject markerObject)
        {
            Renderer? renderer = markerObject.GetComponent<Renderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer!.sharedMaterial, Is.Not.Null);
            Assert.That(renderer.sharedMaterial!.name, Does.Contain("Default-ParticleSystem"));
        }

        private static void AssertNeutralCircleMarker(GameObject markerObject, float maxDiameter)
        {
            AssertUsesDefaultParticleSystemMaterial(markerObject);
            Assert.That(markerObject.transform.localScale.x, Is.LessThanOrEqualTo(maxDiameter));
            Assert.That(markerObject.transform.localScale.z, Is.LessThanOrEqualTo(maxDiameter));
            Assert.That(markerObject.GetComponentsInChildren<Collider>(includeInactive: true), Is.Empty);

            MeshFilter? meshFilter = markerObject.GetComponent<MeshFilter>();
            Assert.That(meshFilter, Is.Not.Null);
            Assert.That(meshFilter!.sharedMesh, Is.Not.Null);
            Assert.That(meshFilter.sharedMesh!.name, Does.Contain("TargetMarkerCircleMesh"));
            Assert.That(meshFilter.sharedMesh.vertexCount, Is.GreaterThan(18));

            Vector3[] vertices = meshFilter.sharedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.That(vertices[i].y, Is.EqualTo(0f).Within(0.001f));
            }

            Renderer? renderer = markerObject.GetComponent<Renderer>();
            Assert.That(renderer, Is.Not.Null);
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            renderer!.GetPropertyBlock(propertyBlock);
            Color markerColor = propertyBlock.GetColor("_Color");
            Assert.That(markerColor.a, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(markerColor.b, Is.GreaterThan(0.7f));
        }

        private static void AssertTargetHasNoReadinessTint(GameObject targetObject)
        {
            Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            int targetRendererCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].name.StartsWith("TargetReadabilityMarker", System.StringComparison.Ordinal))
                {
                    continue;
                }

                targetRendererCount++;
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetColor("_Color").a, Is.EqualTo(0f));
            }

            Assert.That(targetRendererCount, Is.GreaterThan(0));
        }

        private static void AssertRescuePathDirection(GameObject pathObject, Vector3 expectedDirection)
        {
            Vector3 actualDirection = pathObject.transform.localRotation * Vector3.forward;
            actualDirection.y = 0f;
            actualDirection.Normalize();
            Vector3 normalizedExpected = expectedDirection.normalized;
            Assert.That(Vector3.Dot(actualDirection, normalizedExpected), Is.GreaterThan(0.99f));
        }

        private static object? GetPrivateFieldValue(object target, string fieldName)
        {
            System.Reflection.FieldInfo? field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}'.");
            return field?.GetValue(target);
        }

        private static ImmutableArray<ImmutableArray<Tile>> CreateRowsWithTargetAndPath(
            int width,
            int height,
            TileCoord targetCoord,
            TileCoord pathCoord,
            ImmutableArray<string> pathTargetIds)
        {
            return CreateRowsWithTargetsAndPath(
                width,
                height,
                pathCoord,
                ImmutableArray.Create(("puppy-1", targetCoord)),
                pathTargetIds);
        }

        private static ImmutableArray<ImmutableArray<Tile>> CreateRowsWithTargetsAndPath(
            int width,
            int height,
            TileCoord pathCoord,
            ImmutableArray<(string Id, TileCoord Coord)> targetCoords,
            ImmutableArray<string> pathTargetIds)
        {
            ImmutableArray<ImmutableArray<Tile>>.Builder rows = ImmutableArray.CreateBuilder<ImmutableArray<Tile>>(height);
            for (int row = 0; row < height; row++)
            {
                ImmutableArray<Tile>.Builder tiles = ImmutableArray.CreateBuilder<Tile>(width);
                for (int col = 0; col < width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (coord.Equals(pathCoord))
                    {
                        tiles.Add(new RescuePathTile(pathTargetIds));
                        continue;
                    }

                    string? targetId = FindTargetIdAt(targetCoords, coord);
                    tiles.Add(targetId is null ? new EmptyTile() : new TargetTile(targetId, Extracted: false));
                }

                rows.Add(tiles.ToImmutable());
            }

            return rows.ToImmutable();
        }

        private static string? FindTargetIdAt(ImmutableArray<(string Id, TileCoord Coord)> targetCoords, TileCoord coord)
        {
            for (int i = 0; i < targetCoords.Length; i++)
            {
                if (targetCoords[i].Coord.Equals(coord))
                {
                    return targetCoords[i].Id;
                }
            }

            return null;
        }

        private static GameState CreateState(
            ImmutableArray<ImmutableArray<Tile>> rows,
            ImmutableArray<TargetState>? targets = null,
            VineState? vine = null)
        {
            int height = rows.Length;
            int width = height > 0 ? rows[0].Length : 0;
            CoreBoard board = new CoreBoard(width, height, rows);

            return new GameState(
                Board: board,
                Dock: new CoreDock(ImmutableArray<DebrisType?>.Empty, Size: 7),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 10, RiseInterval: 10),
                Vine: vine ?? new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                Targets: targets ?? ImmutableArray<TargetState>.Empty,
                LevelConfig: new LevelConfig(
                    ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C),
                    null,
                    0.0d,
                    2),
                RngState: new RngState(1u, 2u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }

        private readonly struct PresenterHarness
        {
            public PresenterHarness(
                BoardGridViewPresenter gridPresenter,
                BoardContentViewPresenter contentPresenter,
                Transform boardRoot,
                Transform contentRoot,
                GameObject fallbackPrefab)
            {
                GridPresenter = gridPresenter;
                ContentPresenter = contentPresenter;
                BoardRoot = boardRoot;
                ContentRoot = contentRoot;
                FallbackPrefab = fallbackPrefab;
            }

            public BoardGridViewPresenter GridPresenter { get; }

            public BoardContentViewPresenter ContentPresenter { get; }

            public Transform BoardRoot { get; }

            public Transform ContentRoot { get; }

            public GameObject FallbackPrefab { get; }
        }
    }
}
