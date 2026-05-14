using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Presentation;
using Rescue.Unity.Presentation.Targets;
using UnityEngine;
using UnityEngine.TestTools.Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.Unity.BoardPresentation.Tests
{
    public sealed class BoardContentViewPresenterTests
    {
        private const string DaisyTargetPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Targets/PF_Target_Daisy_Puppy.prefab";
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
            Assert.That(targetObject!.GetComponentInChildren<TargetPuppyAnimator>(true), Is.Null);
        }

        [Test]
        public void BoardContentViewPresenter_CentersTargetRendererFootprintOnCell()
        {
            PresenterHarness harness = CreateHarness();
            GameObject targetPrefab = CreateTrackedGameObject("OffsetTargetPrefab");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(visual);
            visual.name = "Visual";
            visual.transform.SetParent(targetPrefab.transform, false);
            visual.transform.localPosition = new Vector3(-0.25f, 0f, 0.3f);
            visual.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.FallbackTargetPrefab = targetPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))),
                ImmutableArray.Create(new TargetState("puppy-1", new TileCoord(0, 0), TargetReadiness.OneClearAway)));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? targetObject), Is.True);
            Assert.That(targetObject, Is.Not.Null);
            Assert.That(harness.GridPresenter.TryGetCellAnchor(new TileCoord(0, 0), out Transform anchor), Is.True);
            Bounds worldBounds = CalculateWorldRendererBounds(targetObject!);
            Assert.That(Mathf.Abs(worldBounds.center.x - anchor.position.x), Is.LessThan(0.01f), $"center={worldBounds.center}, anchor={anchor.position}");
            Assert.That(Mathf.Abs(worldBounds.center.z - anchor.position.z), Is.LessThan(0.01f), $"center={worldBounds.center}, anchor={anchor.position}");
            Assert.That(Mathf.Abs(targetObject!.transform.position.x - anchor.position.x), Is.LessThan(0.01f), "Target root should remain on the logical cell anchor.");
            Assert.That(Mathf.Abs(targetObject.transform.position.z - anchor.position.z), Is.LessThan(0.01f), "Target root should remain on the logical cell anchor.");
            Assert.That(targetObject.GetComponent<BoardCellView>()!.Coord, Is.EqualTo(new TileCoord(0, 0)));
            Transform? marker = FindChildByName(targetObject.transform, "TargetReadabilityMarker");
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker!.localPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(marker.localPosition.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void BoardContentViewPresenter_DisablesNestedTargetPrefabCameras()
        {
            PresenterHarness harness = CreateHarness();
            GameObject targetPrefab = CreateTrackedGameObject("CameraBearingTargetPrefab");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(visual);
            visual.name = "Visual";
            visual.transform.SetParent(targetPrefab.transform, false);
            GameObject cameraObject = CreateTrackedGameObject("Camera");
            cameraObject.transform.SetParent(visual.transform, false);
            Camera nestedCamera = cameraObject.AddComponent<Camera>();
            nestedCamera.enabled = true;
            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.FallbackTargetPrefab = targetPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new TargetTile("puppy-camera", Extracted: false))),
                ImmutableArray.Create(new TargetState("puppy-camera", new TileCoord(0, 0), TargetReadiness.Trapped)));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-camera", out GameObject? targetObject), Is.True);
            Camera spawnedCamera = targetObject!.GetComponentInChildren<Camera>(true)!;
            Assert.That(spawnedCamera, Is.Not.Null);
            Assert.That(spawnedCamera.enabled, Is.False, "Imported cameras inside target art should not render over the gameplay camera.");
        }

#if UNITY_EDITOR
        [Test]
        public void BoardContentViewPresenter_DaisyTargetUsesBoardSurfacePoseAfterSync()
        {
            PresenterHarness harness = CreateHarness();
            GameObject daisyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DaisyTargetPrefabPath);
            Assert.That(daisyPrefab, Is.Not.Null, $"Expected Daisy target prefab at {DaisyTargetPrefabPath}.");
            if (daisyPrefab is null)
            {
                return;
            }

            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.PuppyPrefab = daisyPrefab;
            targetRegistry.FallbackTargetPrefab = daisyPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);
            GameState state = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))),
                ImmutableArray.Create(new TargetState("puppy-1", new TileCoord(0, 0), TargetReadiness.Trapped)));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? targetObject), Is.True);
            Assert.That(targetObject, Is.Not.Null);
            if (targetObject is null)
            {
                return;
            }

            Transform? visual = targetObject.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null, "Daisy prefab should keep the imported art under a single visual child.");
            if (visual is not null)
            {
                string visualDiagnostics = $"right={visual.right}, up={visual.up}, forward={visual.forward}, localEuler={visual.localEulerAngles}";
                Assert.That(Vector3.Dot(visual.up.normalized, Vector3.up), Is.GreaterThan(0.99f), $"Daisy's visual up axis should belong to the board surface normal. {visualDiagnostics}");
                Assert.That(Mathf.Abs(Vector3.Dot(visual.forward.normalized, Vector3.up)), Is.LessThan(0.01f), $"Daisy's visual forward axis should stay in the board plane. {visualDiagnostics}");
            }

            Camera[] nestedCameras = targetObject.GetComponentsInChildren<Camera>(includeInactive: true);
            Assert.That(nestedCameras.Length, Is.GreaterThan(0), "Daisy currently imports a nested camera, so the presenter should explicitly disable it.");
            for (int cameraIndex = 0; cameraIndex < nestedCameras.Length; cameraIndex++)
            {
                Assert.That(nestedCameras[cameraIndex].enabled, Is.False, "Imported target cameras must not render over the gameplay camera.");
            }

            Assert.That(harness.GridPresenter.TryGetCellAnchor(new TileCoord(0, 0), out Transform anchor), Is.True);
            Bounds worldBounds = CalculateWorldRendererBounds(targetObject);
            Assert.That(Mathf.Abs(worldBounds.center.x - anchor.position.x), Is.LessThan(0.01f), $"center={worldBounds.center}, anchor={anchor.position}");
            Assert.That(Mathf.Abs(worldBounds.center.z - anchor.position.z), Is.LessThan(0.01f), $"center={worldBounds.center}, anchor={anchor.position}");
            Assert.That(Mathf.Abs(targetObject.transform.position.x - anchor.position.x), Is.LessThan(0.01f), "Target root should remain on the logical cell anchor.");
            Assert.That(Mathf.Abs(targetObject.transform.position.z - anchor.position.z), Is.LessThan(0.01f), "Target root should remain on the logical cell anchor.");
        }

        [Test]
        public void BoardContentViewPresenter_DaisyTargetKeepsSurfacePoseThroughReadinessUpdatesAndLateUpdate()
        {
            PresenterHarness harness = CreateHarness();
            GameObject daisyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DaisyTargetPrefabPath);
            Assert.That(daisyPrefab, Is.Not.Null, $"Expected Daisy target prefab at {DaisyTargetPrefabPath}.");
            if (daisyPrefab is null)
            {
                return;
            }

            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.PuppyPrefab = daisyPrefab;
            targetRegistry.FallbackTargetPrefab = daisyPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);

            GameState trappedState = CreateSingleTargetState(TargetReadiness.Trapped);
            harness.GridPresenter.RebuildGrid(trappedState);
            harness.ContentPresenter.SyncImmediate(trappedState);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? targetObject), Is.True);
            Assert.That(targetObject, Is.Not.Null);
            if (targetObject is null)
            {
                return;
            }

            AssertDaisySurfacePoseAndCentered(harness, targetObject);

            TargetReadiness[] readinessStates =
            {
                TargetReadiness.Progressing,
                TargetReadiness.OneClearAway,
                TargetReadiness.ExtractableLatched,
                TargetReadiness.Distressed,
            };

            for (int stateIndex = 0; stateIndex < readinessStates.Length; stateIndex++)
            {
                GameState updatedState = CreateSingleTargetState(readinessStates[stateIndex]);
                harness.ContentPresenter.SyncImmediate(updatedState);

                Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? updatedTargetObject), Is.True);
                Assert.That(updatedTargetObject, Is.SameAs(targetObject));
                AssertDaisySurfacePoseAndCentered(harness, targetObject);
            }

            Transform visual = targetObject.transform.Find("Visual")
                ?? throw new AssertionException("Daisy prefab should keep the imported art under a single visual child.");
            visual.localRotation = Quaternion.Euler(32f, 57f, 91f);
            TargetSurfacePoseAdapter? poseAdapter = targetObject.GetComponent<TargetSurfacePoseAdapter>();
            Assert.That(poseAdapter, Is.Not.Null, "Daisy target should keep a surface-pose adapter after readiness sync.");

            InvokePrivateInstanceMethod(poseAdapter!, "LateUpdate");

            AssertDaisySurfacePoseAndCentered(harness, targetObject);
        }
#endif

        [Test]
        public void BoardContentViewPresenter_TargetAnimatorReceivesInitialTrappedReadiness()
        {
            PresenterHarness harness = CreateHarness();
            GameObject targetPrefab = CreateTrackedGameObject("AnimatedTargetPrefab");
            targetPrefab.AddComponent<TargetPuppyAnimator>();
            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.FallbackTargetPrefab = targetPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);
            GameState state = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))),
                ImmutableArray.Create(new TargetState("puppy-1", new TileCoord(0, 0), TargetReadiness.Trapped)));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? targetObject), Is.True);
            TargetPuppyAnimator? targetAnimator = targetObject!.GetComponentInChildren<TargetPuppyAnimator>(true);
            Assert.That(targetAnimator, Is.Not.Null);
            Assert.That(targetAnimator!.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.Trapped));
        }

        [Test]
        public void BoardContentViewPresenter_TargetAnimatorReceivesProgressingReadinessUpdate()
        {
            PresenterHarness harness = CreateHarness();
            GameObject targetPrefab = CreateTrackedGameObject("AnimatedTargetPrefab");
            targetPrefab.AddComponent<TargetPuppyAnimator>();
            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.FallbackTargetPrefab = targetPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);
            GameState trappedState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))),
                ImmutableArray.Create(new TargetState("puppy-1", new TileCoord(0, 0), TargetReadiness.Trapped)));
            GameState progressingState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))),
                ImmutableArray.Create(new TargetState("puppy-1", new TileCoord(0, 0), TargetReadiness.Progressing)));

            harness.GridPresenter.RebuildGrid(trappedState);
            harness.ContentPresenter.SyncImmediate(trappedState);
            harness.ContentPresenter.SyncImmediate(progressingState);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? targetObject), Is.True);
            TargetPuppyAnimator? targetAnimator = targetObject!.GetComponentInChildren<TargetPuppyAnimator>(true);
            Assert.That(targetAnimator, Is.Not.Null);
            Assert.That(targetAnimator!.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.Progressing));
        }

        [Test]
        public void BoardContentViewPresenter_TargetAnimatorReceivesOneClearAwayReadinessUpdate()
        {
            PresenterHarness harness = CreateHarness();
            GameObject targetPrefab = CreateTrackedGameObject("AnimatedTargetPrefab");
            targetPrefab.AddComponent<TargetPuppyAnimator>();
            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.FallbackTargetPrefab = targetPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);
            GameState progressingState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))),
                ImmutableArray.Create(new TargetState("puppy-1", new TileCoord(0, 0), TargetReadiness.Progressing)));
            GameState oneClearAwayState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))),
                ImmutableArray.Create(new TargetState("puppy-1", new TileCoord(0, 0), TargetReadiness.OneClearAway)));

            harness.GridPresenter.RebuildGrid(progressingState);
            harness.ContentPresenter.SyncImmediate(progressingState);
            harness.ContentPresenter.SyncImmediate(oneClearAwayState);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? targetObject), Is.True);
            TargetPuppyAnimator? targetAnimator = targetObject!.GetComponentInChildren<TargetPuppyAnimator>(true);
            Assert.That(targetAnimator, Is.Not.Null);
            Assert.That(targetAnimator!.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.OneClearAway));
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
        public void BoardContentViewPresenter_RendersPlannedVineOverlayBeforePendingPreview()
        {
            PresenterHarness harness = CreateHarness();
            GameState plannedState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile())),
                vine: new VineState(
                    ActionsSinceLastClear: 1,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: new TileCoord(0, 0)));

            harness.GridPresenter.RebuildGrid(plannedState);
            harness.ContentPresenter.SyncImmediate(plannedState);

            Transform? overlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay!.localScale.x, Is.LessThan(0.64f));
            Assert.That(harness.ContentPresenter.DescribeVisualAt(new TileCoord(0, 0)), Does.Contain("vinePreview"));
        }

        [Test]
        public void BoardContentViewPresenter_DoesNotRenderPlannedVineOverlayWithoutPlan()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new EmptyTile())));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Null);
        }

        [Test]
        public void BoardContentViewPresenter_PlannedVineOverlayClearsWhenPlanClears()
        {
            PresenterHarness harness = CreateHarness();
            GameState plannedState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile())),
                vine: new VineState(
                    ActionsSinceLastClear: 1,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: new TileCoord(0, 0)));
            GameState clearedState = plannedState with
            {
                Vine = plannedState.Vine with
                {
                    ActionsSinceLastClear = 0,
                    PendingGrowthTile = null,
                    PlannedGrowthTile = null,
                },
            };

            harness.GridPresenter.RebuildGrid(plannedState);
            harness.ContentPresenter.SyncImmediate(plannedState);
            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Not.Null);

            harness.ContentPresenter.SyncImmediate(clearedState);

            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Null);
        }

        [Test]
        public void BoardContentViewPresenter_PlannedVineOverlayMovesWhenPlanChanges()
        {
            PresenterHarness harness = CreateHarness();
            GameState firstState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile())),
                vine: new VineState(
                    ActionsSinceLastClear: 1,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: new TileCoord(0, 0)));
            GameState movedState = firstState with
            {
                Vine = firstState.Vine with { PlannedGrowthTile = new TileCoord(0, 1) },
            };

            harness.GridPresenter.RebuildGrid(firstState);
            harness.ContentPresenter.SyncImmediate(firstState);

            Transform? firstOverlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(firstOverlay, Is.Not.Null);

            harness.ContentPresenter.SyncImmediate(movedState);

            Transform? movedOverlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(movedOverlay, Is.Not.Null);
            Assert.That(harness.ContentPresenter.DescribeVisualAt(new TileCoord(0, 0)), Does.Not.Contain("vinePreview"));
            Assert.That(harness.ContentPresenter.DescribeVisualAt(new TileCoord(0, 1)), Does.Contain("vinePreview"));
        }

        [Test]
        public void BoardContentViewPresenter_PlannedVineOverlayRendersOverDebrisWithoutReplacingIt()
        {
            PresenterHarness harness = CreateHarness();
            GameState plannedState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A))),
                vine: new VineState(
                    ActionsSinceLastClear: 1,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: new TileCoord(0, 0)));

            harness.GridPresenter.RebuildGrid(plannedState);
            harness.ContentPresenter.SyncImmediate(plannedState);

            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0)), Is.Not.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Not.Null);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(2));
        }

        [Test]
        public void BoardContentViewPresenter_VinePreviewRendersAboveDebrisHeight()
        {
            PresenterHarness harness = CreateHarness();
            PieceVisualRegistry pieceRegistry = CreateRegistry<PieceVisualRegistry>();
            GameObject tallDebrisPrefab = CreateTrackedPrimitive(PrimitiveType.Cube, "TallDebrisPrefab");
            tallDebrisPrefab.transform.localScale = new Vector3(1f, 2f, 1f);
            pieceRegistry.DebrisAPrefab = tallDebrisPrefab;
            SetPrivateField(harness.ContentPresenter, "pieceRegistry", pieceRegistry);
            GameState plannedState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A))),
                vine: new VineState(
                    ActionsSinceLastClear: 1,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: new TileCoord(0, 0)));

            harness.GridPresenter.RebuildGrid(plannedState);
            harness.ContentPresenter.SyncImmediate(plannedState);

            GameObject? debrisObject = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Transform? overlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(debrisObject, Is.Not.Null);
            Assert.That(overlay, Is.Not.Null);
            Renderer debrisRenderer = debrisObject!.GetComponentInChildren<Renderer>()
                ?? throw new AssertionException("Expected tall debris renderer.");
            Assert.That(overlay!.position.y, Is.GreaterThan(debrisRenderer.bounds.max.y));
        }

        [Test]
        public void BoardContentViewPresenter_VinePreviewHeightUpdatesWhenDebrisUnderItChanges()
        {
            PresenterHarness harness = CreateHarness();
            PieceVisualRegistry pieceRegistry = CreateRegistry<PieceVisualRegistry>();
            GameObject shortDebrisPrefab = CreateTrackedPrimitive(PrimitiveType.Cube, "ShortDebrisPrefab");
            shortDebrisPrefab.transform.localScale = new Vector3(1f, 0.3f, 1f);
            GameObject tallDebrisPrefab = CreateTrackedPrimitive(PrimitiveType.Cube, "TallDebrisPrefab");
            tallDebrisPrefab.transform.localScale = new Vector3(1f, 2f, 1f);
            pieceRegistry.DebrisAPrefab = shortDebrisPrefab;
            pieceRegistry.DebrisBPrefab = tallDebrisPrefab;
            SetPrivateField(harness.ContentPresenter, "pieceRegistry", pieceRegistry);
            GameState shortState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A))),
                vine: new VineState(
                    ActionsSinceLastClear: 1,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: new TileCoord(0, 0)));
            GameState tallState = shortState with
            {
                Board = new CoreBoard(
                    1,
                    1,
                    ImmutableArray.Create(ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.B)))),
            };

            harness.GridPresenter.RebuildGrid(shortState);
            harness.ContentPresenter.SyncImmediate(shortState);
            Transform? shortOverlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(shortOverlay, Is.Not.Null);
            float shortOverlayY = shortOverlay!.position.y;

            harness.ContentPresenter.SyncImmediate(tallState);

            Transform? tallOverlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            GameObject? tallDebrisObject = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(tallOverlay, Is.Not.Null);
            Assert.That(tallDebrisObject, Is.Not.Null);
            Renderer debrisRenderer = tallDebrisObject!.GetComponentInChildren<Renderer>()
                ?? throw new AssertionException("Expected tall debris renderer.");
            Assert.That(tallOverlay!.position.y, Is.GreaterThan(shortOverlayY));
            Assert.That(tallOverlay.position.y, Is.GreaterThan(debrisRenderer.bounds.max.y));
        }

        [Test]
        public void BoardContentViewPresenter_PlannedVineOverlayRendersOverRescuePath()
        {
            PresenterHarness harness = CreateHarness();
            GameState plannedState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new RescuePathTile(ImmutableArray.Create("puppy-1")))),
                vine: new VineState(
                    ActionsSinceLastClear: 1,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: new TileCoord(0, 0)));

            harness.GridPresenter.RebuildGrid(plannedState);
            harness.ContentPresenter.SyncImmediate(plannedState);

            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "RescuePath", new TileCoord(0, 0)), Is.Not.Null);
            GameObject? rescuePathObject = GetRegisteredPieceObject(harness.ContentPresenter, "RescuePath", new TileCoord(0, 0));
            Transform? overlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(overlay, Is.Not.Null);
            Assert.That(rescuePathObject, Is.Not.Null);
            Assert.That(overlay!.position.y, Is.GreaterThan(rescuePathObject!.transform.position.y));
        }

        [Test]
        public void BoardContentViewPresenter_PlannedVineOverlayDoesNotRemainAfterRebuild()
        {
            PresenterHarness harness = CreateHarness();
            GameState plannedState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile())),
                vine: new VineState(
                    ActionsSinceLastClear: 1,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: new TileCoord(0, 1)));
            GameState rebuiltState = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new EmptyTile())));

            harness.GridPresenter.RebuildGrid(plannedState);
            harness.ContentPresenter.SyncImmediate(plannedState);
            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Not.Null);

            harness.GridPresenter.RebuildGrid(rebuiltState);
            harness.ContentPresenter.RebuildContent(rebuiltState);

            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Null);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(0));
        }

        [Test]
        public void BoardContentViewPresenter_PlannedVineOverlayClearsWhenTileBecomesFullVine()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            blockerRegistry.FallbackBlockerPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);
            GameState plannedState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile())),
                vine: new VineState(
                    ActionsSinceLastClear: 1,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: new TileCoord(0, 0)));
            GameState fullVineState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new BlockerTile(BlockerType.Vine, 1, null))),
                vine: plannedState.Vine);

            harness.GridPresenter.RebuildGrid(plannedState);
            harness.ContentPresenter.SyncImmediate(plannedState);
            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Not.Null);

            harness.ContentPresenter.SyncImmediate(fullVineState);

            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Blocker_Vine"), Is.Not.Null);
        }

        [Test]
        public void BoardContentViewPresenter_PlannedVineOverlayCoverageFollowsActionProgress()
        {
            PresenterHarness harness = CreateHarness();
            GameState earlyState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile())),
                vine: new VineState(
                    ActionsSinceLastClear: 0,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: new TileCoord(0, 0)));
            GameState progressedState = earlyState with
            {
                Vine = earlyState.Vine with { ActionsSinceLastClear = 2 },
            };

            harness.GridPresenter.RebuildGrid(earlyState);
            harness.ContentPresenter.SyncImmediate(earlyState);
            Transform? overlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(overlay, Is.Not.Null);
            float earlyScale = overlay!.localScale.x;

            harness.ContentPresenter.SyncImmediate(progressedState);

            Transform? progressedOverlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(progressedOverlay, Is.Not.Null);
            Assert.That(progressedOverlay!.localScale.x, Is.GreaterThan(earlyScale));
            Assert.That(progressedOverlay.localScale.x, Is.LessThan(0.64f));
        }

        [Test]
        public void BoardContentViewPresenter_PendingVinePreviewReadsStrongerThanEarlyProgress()
        {
            PresenterHarness harness = CreateHarness();
            GameState earlyState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile())),
                vine: new VineState(
                    ActionsSinceLastClear: 1,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: new TileCoord(0, 0)));
            GameState pendingState = earlyState with
            {
                Vine = earlyState.Vine with
                {
                    ActionsSinceLastClear = 3,
                    PendingGrowthTile = new TileCoord(0, 0),
                },
            };

            harness.GridPresenter.RebuildGrid(earlyState);
            harness.ContentPresenter.SyncImmediate(earlyState);
            Transform? earlyOverlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(earlyOverlay, Is.Not.Null);
            float earlyScale = earlyOverlay!.localScale.x;

            harness.ContentPresenter.SyncImmediate(pendingState);

            Transform? pendingOverlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(pendingOverlay, Is.Not.Null);
            Assert.That(pendingOverlay!.localScale.x, Is.GreaterThan(earlyScale));
            Assert.That(pendingOverlay.localScale.x, Is.LessThan(1f));
        }

        [Test]
        public void BoardContentViewPresenter_UsesSourceDirectionOnlyWhenSourceTileExists()
        {
            PresenterHarness harness = CreateHarness();
            GameState undirectedState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile())),
                vine: new VineState(
                    ActionsSinceLastClear: 1,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null,
                    PlannedGrowthTile: new TileCoord(0, 1)));
            GameState directedState = undirectedState with
            {
                Vine = undirectedState.Vine with { GrowthSourceTile = new TileCoord(0, 0) },
            };

            harness.GridPresenter.RebuildGrid(undirectedState);
            harness.ContentPresenter.SyncImmediate(undirectedState);
            Transform? undirectedOverlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(undirectedOverlay, Is.Not.Null);
            AssertVinePreviewDirection(undirectedOverlay!, Vector3.forward);
            Assert.That(undirectedOverlay.localScale.z, Is.EqualTo(undirectedOverlay.localScale.x).Within(0.001f));

            harness.ContentPresenter.SyncImmediate(directedState);

            Transform? directedOverlay = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(directedOverlay, Is.Not.Null);
            AssertVinePreviewDirection(directedOverlay!, Vector3.right);
            Assert.That(directedOverlay.localScale.z, Is.LessThan(directedOverlay.localScale.x));
        }

        [Test]
        public void BoardContentViewPresenter_VinePreviewUsesRegisteredVineBeforeOverlayFallback()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            GameObject vinePrefab = CreateTrackedGameObject("VinePrefab");
            GameObject overlayPrefab = CreateTrackedGameObject("VineOverlayPrefab");
            CreateTrackedGameObject("VinePrefabMarker").transform.SetParent(vinePrefab.transform, false);
            CreateTrackedGameObject("OverlayPrefabMarker").transform.SetParent(overlayPrefab.transform, false);
            blockerRegistry.VinePrefab = vinePrefab;
            blockerRegistry.VineOverlayPrefab = overlayPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);
            GameState previewState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile())),
                vine: new VineState(
                    ActionsSinceLastClear: 3,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray.Create(new TileCoord(0, 0)),
                    PriorityCursor: 0,
                    PendingGrowthTile: new TileCoord(0, 0)));

            harness.GridPresenter.RebuildGrid(previewState);
            harness.ContentPresenter.SyncImmediate(previewState);

            Transform? preview = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview!.Find("VinePrefabMarker"), Is.Not.Null);
            Assert.That(preview.Find("OverlayPrefabMarker"), Is.Null);
        }

        [Test]
        public void BoardContentViewPresenter_VinePreviewClearsWhenPendingTileBecomesBlocker()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            blockerRegistry.FallbackBlockerPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);
            GameState previewState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile())),
                vine: new VineState(
                    ActionsSinceLastClear: 3,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray.Create(new TileCoord(0, 0)),
                    PriorityCursor: 0,
                    PendingGrowthTile: new TileCoord(0, 0)));
            GameState grownState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new BlockerTile(BlockerType.Vine, 1, null))),
                vine: previewState.Vine with { ActionsSinceLastClear = 0, PendingGrowthTile = null });

            harness.GridPresenter.RebuildGrid(previewState);
            harness.ContentPresenter.SyncImmediate(previewState);
            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Not.Null);

            harness.ContentPresenter.ForceSyncToState(grownState);

            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Blocker_Vine"), Is.Not.Null);
        }

        [Test]
        public void BoardContentViewPresenter_AnimateVineGrowthCreatesOverlayUntilFinalSyncReplacesIt()
        {
            PresenterHarness harness = CreateHarness();
            BlockerVisualRegistry blockerRegistry = CreateRegistry<BlockerVisualRegistry>();
            blockerRegistry.FallbackBlockerPrefab = harness.FallbackPrefab;
            SetPrivateField(harness.ContentPresenter, "blockerRegistry", blockerRegistry);
            GameState emptyState = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new EmptyTile())));
            GameState grownState = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new BlockerTile(BlockerType.Vine, 1, null))));

            harness.GridPresenter.RebuildGrid(emptyState);
            harness.ContentPresenter.SyncImmediate(emptyState);
            harness.ContentPresenter.AnimateVineGrowth(new VineGrown(new TileCoord(0, 0)), durationSeconds: 0f);

            Transform? preview = FindChildByName(harness.ContentRoot, "VineGrowthPreview");
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview!.localScale.x, Is.GreaterThan(0.9f));

            harness.ContentPresenter.ForceSyncToState(grownState);

            Assert.That(FindChildByName(harness.ContentRoot, "VineGrowthPreview"), Is.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Blocker_Vine"), Is.Not.Null);
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
        public void BoardContentViewPresenter_AnimateRescuePathLockedKeepsMarkerTopOrientedBeforeSpawning()
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
            Assert.That(Quaternion.Angle(Quaternion.identity, pathObject.transform.localRotation), Is.LessThan(0.001f));
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
        public void BoardContentViewPresenter_RescuePathMarkerRemainsTopOriented(
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
            Assert.That(Quaternion.Angle(Quaternion.identity, pathObject!.transform.localRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void BoardContentViewPresenter_RescuePathMarkerIgnoresAdjacentTargetDirection()
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
            Assert.That(Quaternion.Angle(Quaternion.identity, pathObject!.transform.localRotation), Is.LessThan(0.001f));
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
            SetPrivateField(settings, "plannedVineProgressDurationSeconds", 0.11f);
            SetPrivateField(settings, "boardPieceLandingSquashXScale", 1.04f);
            SetPrivateField(settings, "boardPieceLandingSquashYScale", 0.94f);
            SetPrivateField(settings, "boardPieceLandingBounceDistance", 0.03f);

            harness.ContentPresenter.ApplyPlaybackSettings(settings);

            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "gravityDurationSeconds"), Is.EqualTo(0.095f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "blockerDamageDurationSeconds"), Is.EqualTo(0.18f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "blockerBreakDurationSeconds"), Is.EqualTo(0.18f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "iceRevealDurationSeconds"), Is.EqualTo(0.18f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "spawnDurationSeconds"), Is.EqualTo(0.07f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "targetExtractDurationSeconds"), Is.EqualTo(0.34f));
            Assert.That(GetPrivateFieldValue(harness.ContentPresenter, "plannedVineProgressDurationSeconds"), Is.EqualTo(0.22f));
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
        public void BoardContentViewPresenter_AnimateTargetExtractPlaysOptionalTargetAnimator()
        {
            PresenterHarness harness = CreateHarness();
            GameObject targetPrefab = CreateTrackedGameObject("AnimatedTargetPrefab");
            targetPrefab.AddComponent<TargetPuppyAnimator>();
            TargetVisualRegistry targetRegistry = CreateRegistry<TargetVisualRegistry>();
            targetRegistry.FallbackTargetPrefab = targetPrefab;
            SetPrivateField(harness.ContentPresenter, "targetRegistry", targetRegistry);
            TileCoord targetCoord = new TileCoord(0, 0);
            GameState state = CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))),
                ImmutableArray.Create(new TargetState("puppy-1", targetCoord, TargetReadiness.OneClearAway)));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("puppy-1", out GameObject? targetBeforeExtract), Is.True);
            TargetPuppyAnimator? targetAnimator = targetBeforeExtract!.GetComponentInChildren<TargetPuppyAnimator>(true);
            Assert.That(targetAnimator, Is.Not.Null);

            harness.ContentPresenter.AnimateTargetExtract(new TargetExtracted("puppy-1", targetCoord));

            Assert.That(targetAnimator!.IsExtracting, Is.True);
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
        public void BoardContentViewPresenter_TargetExtractPoseHoldsThenLiftsAndFades()
        {
            GameObject targetObject = CreateTrackedGameObject("TargetExtractPoseProbe");
            Transform targetTransform = targetObject.transform;
            Vector3 basePosition = targetTransform.localPosition;
            Vector3 baseScale = targetTransform.localScale;

            InvokeTargetExtractPose(targetTransform, 0.20f, basePosition, baseScale);

            Assert.That(targetTransform.localPosition.y, Is.EqualTo(basePosition.y).Within(0.001f));
            Assert.That(targetTransform.localPosition.x, Is.EqualTo(basePosition.x).Within(0.001f));
            Assert.That(targetTransform.localPosition.z, Is.EqualTo(basePosition.z).Within(0.001f));
            Assert.That(ResolveTargetExtractAlpha(0.20f), Is.EqualTo(1f).Within(0.001f));

            InvokeTargetExtractPose(targetTransform, 1.0f, basePosition, baseScale);

            Assert.That(targetTransform.localPosition.y - basePosition.y, Is.GreaterThan(0.18f));
            Assert.That(targetTransform.localPosition.x, Is.EqualTo(basePosition.x).Within(0.001f));
            Assert.That(targetTransform.localPosition.z, Is.EqualTo(basePosition.z).Within(0.001f));
            Assert.That(ResolveTargetExtractAlpha(1.0f), Is.EqualTo(0f).Within(0.001f));
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

        [Test]
        public void BoardContentViewPresenter_SyncImmediateDestroysUntrackedContentRootChildren()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A))));

            harness.GridPresenter.RebuildGrid(state);
            harness.ContentPresenter.SyncImmediate(state);
            GameObject orphan = CreateTrackedGameObject("Content_00_09_Debris_Orphan");
            orphan.transform.SetParent(harness.ContentRoot, false);

            harness.ContentPresenter.SyncImmediate(state);

            Assert.That(orphan == null, Is.True);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(harness.ContentRoot.GetChild(0).name, Does.Contain("Debris_A"));
            Assert.That(harness.ContentPresenter.DescribeStateMismatches(state), Is.EqualTo("none"));
        }

        [Test]
        public void BoardContentViewPresenter_ForceSyncToStateDestroysInFlightContentRemovedFromSpawnedRegistry()
        {
            PresenterHarness harness = CreateHarness();
            GameState debrisState = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A))));
            GameState emptyState = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new EmptyTile())));

            harness.GridPresenter.RebuildGrid(debrisState);
            harness.ContentPresenter.SyncImmediate(debrisState);
            GameObject? debrisObject = GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0));
            Assert.That(debrisObject, Is.Not.Null);
            GetSpawnedContentList(harness.ContentPresenter).Remove(debrisObject!);

            harness.ContentPresenter.ForceSyncToState(emptyState);

            Assert.That(debrisObject == null, Is.True);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(0));
            Assert.That(harness.ContentPresenter.DescribeStateMismatches(emptyState), Is.EqualTo("none"));
        }

        [Test]
        public void BoardContentViewPresenter_RebuildContentClearsContentRootBeforeRepopulating()
        {
            PresenterHarness harness = CreateHarness();
            GameState wideState = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.B))));
            GameState narrowState = CreateState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new DebrisTile(DebrisType.C))));

            harness.GridPresenter.RebuildGrid(wideState);
            harness.ContentPresenter.SyncImmediate(wideState);
            GameObject orphan = CreateTrackedGameObject("Content_00_02_Debris_Orphan");
            orphan.transform.SetParent(harness.ContentRoot, false);

            harness.GridPresenter.RebuildGrid(narrowState);
            harness.ContentPresenter.RebuildContent(narrowState);

            Assert.That(orphan == null, Is.True);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(harness.ContentRoot.GetChild(0).name, Does.Contain("Debris_C"));
            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 0)), Is.Null);
            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, "Debris", new TileCoord(0, 1)), Is.Not.Null);
            Assert.That(harness.ContentPresenter.DescribeStateMismatches(narrowState), Is.EqualTo("none"));
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

        private GameObject CreateTrackedPrimitive(PrimitiveType primitiveType, string name)
        {
            GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
            gameObject.name = name;
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

        private static void InvokePrivateInstanceMethod(object target, string methodName)
        {
            System.Reflection.MethodInfo? method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, $"Expected private method '{methodName}'.");
            method?.Invoke(target, null);
        }

        private static Bounds CalculateWorldRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0), "Expected target prefab to include renderers.");

            Bounds combined = renderers[0].bounds;

            for (int rendererIndex = 1; rendererIndex < renderers.Length; rendererIndex++)
            {
                combined.Encapsulate(renderers[rendererIndex].bounds);
            }

            return combined;
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
            Assert.That(pathObject.transform.Find("RescuePathWash"), Is.Null);
            Transform? paw = pathObject.transform.Find("RescuePathPaw");
            Assert.That(paw, Is.Not.Null);
            Assert.That(pathObject.transform.Find("RescuePathChevron_00"), Is.Null);
            Assert.That(pathObject.transform.Find("RescuePathChevron_01"), Is.Null);
            Transform? pawVisual = paw!.Find("Visual");
            Assert.That(pawVisual, Is.Not.Null);
            Assert.That(Quaternion.Angle(Quaternion.identity, pawVisual!.localRotation), Is.LessThan(0.001f));
            Assert.That(pathObject.GetComponentsInChildren<Collider>(includeInactive: true), Is.Empty);
            Renderer[] renderers = pathObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(1));
            for (int i = 0; i < renderers.Length; i++)
            {
                Material? material = renderers[i].sharedMaterial;
                Assert.That(material, Is.Not.Null);
                Assert.That(material!.color.a, Is.GreaterThan(0f));
                Assert.That(material.renderQueue, Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
                if (material.HasProperty("_ZWrite"))
                {
                    Assert.That(material.GetInt("_ZWrite"), Is.EqualTo(0));
                }

                Assert.That(renderers[i].shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                Assert.That(renderers[i].receiveShadows, Is.False);
            }
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

#if UNITY_EDITOR
        private static void AssertDaisySurfacePoseAndCentered(PresenterHarness harness, GameObject targetObject)
        {
            Transform? visual = targetObject.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null, "Daisy prefab should keep the imported art under a single visual child.");
            if (visual is null)
            {
                return;
            }

            string visualDiagnostics = $"right={visual.right}, up={visual.up}, forward={visual.forward}, localEuler={visual.localEulerAngles}";
            Assert.That(Vector3.Dot(visual.up.normalized, Vector3.up), Is.GreaterThan(0.99f), $"Daisy's visual up axis should belong to the board surface normal. {visualDiagnostics}");
            Assert.That(Mathf.Abs(Vector3.Dot(visual.forward.normalized, Vector3.up)), Is.LessThan(0.01f), $"Daisy's visual forward axis should stay in the board plane. {visualDiagnostics}");

            Assert.That(harness.GridPresenter.TryGetCellAnchor(new TileCoord(0, 0), out Transform anchor), Is.True);
            Bounds worldBounds = CalculateWorldRendererBounds(targetObject);
            Assert.That(Mathf.Abs(worldBounds.center.x - anchor.position.x), Is.LessThan(0.01f), $"center={worldBounds.center}, anchor={anchor.position}");
            Assert.That(Mathf.Abs(worldBounds.center.z - anchor.position.z), Is.LessThan(0.01f), $"center={worldBounds.center}, anchor={anchor.position}");
            Assert.That(Mathf.Abs(targetObject.transform.position.x - anchor.position.x), Is.LessThan(0.01f), "Target root should remain on the logical cell anchor.");
            Assert.That(Mathf.Abs(targetObject.transform.position.z - anchor.position.z), Is.LessThan(0.01f), "Target root should remain on the logical cell anchor.");
        }
#endif

        private static void AssertVinePreviewDirection(Transform overlay, Vector3 expectedDirection)
        {
            Vector3 actualDirection = overlay.localRotation * Vector3.forward;
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

        private static void InvokeTargetExtractPose(
            Transform targetTransform,
            float normalized,
            Vector3 baseLocalPosition,
            Vector3 baseLocalScale)
        {
            System.Reflection.MethodInfo? method = typeof(BoardContentViewPresenter).GetMethod(
                "ApplyTargetExtractPose",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                new[] { typeof(Transform), typeof(float), typeof(Vector3), typeof(Vector3) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "Expected private method 'ApplyTargetExtractPose'.");
            method?.Invoke(null, new object[] { targetTransform, normalized, baseLocalPosition, baseLocalScale });
        }

        private static float ResolveTargetExtractAlpha(float normalized)
        {
            System.Reflection.MethodInfo? method = typeof(BoardContentViewPresenter).GetMethod(
                "ResolveTargetExtractAlpha",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, "Expected private method 'ResolveTargetExtractAlpha'.");
            return (float)(method?.Invoke(null, new object[] { normalized }) ?? 0f);
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

        private static GameState CreateSingleTargetState(TargetReadiness readiness)
        {
            return CreateState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new TargetTile("puppy-1", Extracted: false))),
                ImmutableArray.Create(new TargetState("puppy-1", new TileCoord(0, 0), readiness)));
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
