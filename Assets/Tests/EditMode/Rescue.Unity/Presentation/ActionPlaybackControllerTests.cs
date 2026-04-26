using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.UI;
using UnityEngine;
using UnityEngine.TestTools;
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class ActionPlaybackControllerTests
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

        [UnityTest]
        public IEnumerator ActionPlaybackController_SetsIsPlayingDuringPlaybackAndClearsAfterCompletion()
        {
            ActionPlaybackController controller = CreateController(playbackEnabled: true, yieldBetweenSteps: true);
            ActionResult result = CreateResult(actionCount: 1);
            int finalSyncCalls = 0;

            bool handled = controller.TryPlayAction(CreateState(), new ActionInput(new TileCoord(0, 0)), result, _ => finalSyncCalls++);

            Assert.That(handled, Is.True);
            Assert.That(controller.IsPlaying, Is.True);
            Assert.That(finalSyncCalls, Is.EqualTo(0));

            yield return null;

            Assert.That(controller.IsPlaying, Is.False);
            Assert.That(finalSyncCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ActionPlaybackController_CancelPlaybackClearsIsPlayingAndTriggersRecoveryFinalSync()
        {
            ActionPlaybackController controller = CreateController(playbackEnabled: true, yieldBetweenSteps: true);
            ActionResult result = CreateResult(actionCount: 2);
            int finalSyncCalls = 0;
            GameState? syncedState = null;

            bool handled = controller.TryPlayAction(CreateState(), new ActionInput(new TileCoord(0, 0)), result, syncedResult =>
            {
                finalSyncCalls++;
                syncedState = syncedResult.State;
            });

            Assert.That(handled, Is.True);
            Assert.That(controller.IsPlaying, Is.True);

            Assert.DoesNotThrow(() => controller.CancelPlayback());

            Assert.That(controller.IsPlaying, Is.False);
            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(syncedState, Is.EqualTo(result.State));

            yield return null;

            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(controller.IsPlaying, Is.False);
        }

        [Test]
        public void ActionPlaybackController_FinalSyncIsCalledAfterImmediatePlayback()
        {
            ActionPlaybackController controller = CreateController(playbackEnabled: true, yieldBetweenSteps: false);
            ActionResult result = CreateResult(actionCount: 2);
            int finalSyncCalls = 0;
            GameState? syncedState = null;

            bool handled = controller.TryPlayAction(CreateState(), new ActionInput(new TileCoord(0, 0)), result, syncedResult =>
            {
                finalSyncCalls++;
                syncedState = syncedResult.State;
                Assert.That(controller.IsPlaying, Is.True);
            });

            Assert.That(handled, Is.True);
            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(syncedState, Is.EqualTo(result.State));
            Assert.That(controller.IsPlaying, Is.False);
            Assert.That(controller.CurrentPlan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
        }

        [Test]
        public void ActionPlaybackController_FinalSyncRunsAfterRemoveGravityAndSpawnPlan()
        {
            ControllerHarness harness = CreateControllerHarness(playbackEnabled: true, yieldBetweenSteps: false);
            GameState previousState = CreateBoardState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(
                        new DebrisTile(DebrisType.A),
                        new EmptyTile()),
                    ImmutableArray.Create<Tile>(
                        new DebrisTile(DebrisType.B),
                        new EmptyTile())));

            harness.GridPresenter.RebuildGrid(previousState);
            harness.ContentPresenter.SyncImmediate(previousState);
            GameObject? gravitySourceDebris = GetRegisteredPieceObject(harness.ContentPresenter, new TileCoord(1, 0));

            ActionResult result = CreateResult(
                CreateBoardState(
                    ImmutableArray.Create(
                        ImmutableArray.Create<Tile>(
                            new DebrisTile(DebrisType.C),
                            new EmptyTile()),
                        ImmutableArray.Create<Tile>(
                            new EmptyTile(),
                            new DebrisTile(DebrisType.B)))),
                actionCount: 3,
                new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0))),
                new GravitySettled(ImmutableArray.Create((new TileCoord(1, 0), new TileCoord(1, 1)))),
                new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C))));

            int finalSyncCalls = 0;
            GameState? syncedState = null;

            bool handled = harness.Controller.TryPlayAction(previousState, new ActionInput(new TileCoord(0, 0)), result, syncedResult =>
            {
                finalSyncCalls++;
                syncedState = syncedResult.State;
                harness.ContentPresenter.SyncImmediate(syncedResult.State);
            });

            Assert.That(handled, Is.True);
            Assert.That(harness.Controller.CurrentPlan.Count, Is.EqualTo(4));
            Assert.That(harness.Controller.CurrentPlan[0].StepType, Is.EqualTo(ActionPlaybackStepType.RemoveGroup));
            Assert.That(harness.Controller.CurrentPlan[1].StepType, Is.EqualTo(ActionPlaybackStepType.Gravity));
            Assert.That(harness.Controller.CurrentPlan[2].StepType, Is.EqualTo(ActionPlaybackStepType.Spawn));

            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(syncedState, Is.EqualTo(result.State));
            Assert.That(harness.Controller.IsPlaying, Is.False);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(2));
            Assert.That(FindChildByName(harness.ContentRoot, "Content_00_00_Debris_C"), Is.Not.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Content_01_01_Debris_B"), Is.Not.Null);
            Assert.That(GetRegisteredPieceObject(harness.ContentPresenter, new TileCoord(1, 1)), Is.SameAs(gravitySourceDebris));
        }

        [Test]
        public void ActionPlaybackController_MissingVisualsDoNotPreventFinalSync()
        {
            ControllerHarness harness = CreateControllerHarness(playbackEnabled: true, yieldBetweenSteps: false);
            ActionResult result = CreateResult(
                CreateBoardState(ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.C)))),
                actionCount: 4,
                new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0))),
                new GravitySettled(ImmutableArray.Create((new TileCoord(1, 0), new TileCoord(1, 1)))),
                new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C))));

            int finalSyncCalls = 0;

            Assert.DoesNotThrow(() =>
            {
                bool handled = harness.Controller.TryPlayAction(CreateState(), new ActionInput(new TileCoord(0, 0)), result, _ => finalSyncCalls++);
                Assert.That(handled, Is.True);
            });

            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(harness.Controller.IsPlaying, Is.False);
        }

        [Test]
        public void ActionPlaybackController_FinalSyncRepairsMismatchedDebrisVisualsAfterGravityAndSpawn()
        {
            ControllerHarness harness = CreateControllerHarness(playbackEnabled: true, yieldBetweenSteps: false);
            GameState previousState = CreateBoardState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(
                        new DebrisTile(DebrisType.A),
                        new EmptyTile()),
                    ImmutableArray.Create<Tile>(
                        new DebrisTile(DebrisType.B),
                        new EmptyTile())));
            GameState resultState = CreateBoardState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(
                        new DebrisTile(DebrisType.C),
                        new EmptyTile()),
                    ImmutableArray.Create<Tile>(
                        new EmptyTile(),
                        new DebrisTile(DebrisType.B))));

            harness.GridPresenter.RebuildGrid(previousState);
            harness.ContentPresenter.SyncImmediate(previousState);

            ActionResult result = CreateResult(
                resultState,
                actionCount: 10,
                new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0))),
                new GravitySettled(ImmutableArray.Create((new TileCoord(1, 0), new TileCoord(1, 1)))),
                new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C))));

            bool handled = harness.Controller.TryPlayAction(previousState, new ActionInput(new TileCoord(0, 0)), result, syncedResult =>
            {
                GameObject? movedDebris = GetRegisteredPieceObject(harness.ContentPresenter, new TileCoord(1, 1));
                if (movedDebris is not null)
                {
                    movedDebris.name = "Content_01_01_Debris_Wrong";
                }

                GameObject? spawnedDebris = GetRegisteredPieceObject(harness.ContentPresenter, new TileCoord(0, 0));
                if (spawnedDebris is not null)
                {
                    spawnedDebris.name = "Content_00_00_Debris_Wrong";
                }

                harness.ContentPresenter.SyncImmediate(syncedResult.State);
            });

            Assert.That(handled, Is.True);
            Assert.That(FindChildByName(harness.ContentRoot, "Content_01_01_Debris_B"), Is.Not.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Content_00_00_Debris_C"), Is.Not.Null);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(2));
        }

        [Test]
        public void ActionPlaybackController_BreakBlockerAndRevealRouteBeforeFinalSyncAndSyncRepairsState()
        {
            ControllerHarness harness = CreateControllerHarness(playbackEnabled: true, yieldBetweenSteps: false);
            GameState previousState = CreateBoardState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.B)))));
            GameState resultState = CreateBoardState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.B))));

            harness.GridPresenter.RebuildGrid(previousState);
            harness.ContentPresenter.SyncImmediate(previousState);

            Assert.That(FindChildByName(harness.ContentRoot, "Blocker_Ice"), Is.Not.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "HiddenDebris_B"), Is.Not.Null);

            ActionResult result = CreateResult(
                resultState,
                actionCount: 6,
                new BlockerDamaged(new TileCoord(0, 0), BlockerType.Ice, RemainingHp: 0),
                new BlockerBroken(new TileCoord(0, 0), BlockerType.Ice),
                new IceRevealed(new TileCoord(0, 0), DebrisType.B),
                new GravitySettled(ImmutableArray<(TileCoord From, TileCoord To)>.Empty));

            int finalSyncCalls = 0;

            bool handled = harness.Controller.TryPlayAction(previousState, new ActionInput(new TileCoord(0, 0)), result, syncedResult =>
            {
                finalSyncCalls++;
                harness.ContentPresenter.SyncImmediate(syncedResult.State);
            });

            Assert.That(handled, Is.True);
            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(harness.Controller.CurrentPlan[0].StepType, Is.EqualTo(ActionPlaybackStepType.BreakBlockerOrReveal));
            Assert.That(harness.Controller.CurrentPlan[1].StepType, Is.EqualTo(ActionPlaybackStepType.BreakBlockerOrReveal));
            Assert.That(harness.Controller.CurrentPlan[2].StepType, Is.EqualTo(ActionPlaybackStepType.BreakBlockerOrReveal));
            Assert.That(harness.Controller.CurrentPlan[3].StepType, Is.EqualTo(ActionPlaybackStepType.Gravity));
            Assert.That(FindChildByName(harness.ContentRoot, "Blocker_Ice"), Is.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Debris_B"), Is.Not.Null);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
        }

        [Test]
        public void ActionPlaybackController_MissingBlockerAndIceVisualsDoNotCrashPlayback()
        {
            ControllerHarness harness = CreateControllerHarness(playbackEnabled: true, yieldBetweenSteps: false);
            GameState previousState = CreateBoardState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.B)))));
            GameState resultState = CreateBoardState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.B))));

            harness.GridPresenter.RebuildGrid(previousState);

            int finalSyncCalls = 0;

            Assert.DoesNotThrow(() =>
            {
                bool handled = harness.Controller.TryPlayAction(
                    previousState,
                    new ActionInput(new TileCoord(0, 0)),
                    CreateResult(
                        resultState,
                        actionCount: 7,
                        new BlockerDamaged(new TileCoord(0, 0), BlockerType.Ice, RemainingHp: 0),
                        new BlockerBroken(new TileCoord(0, 0), BlockerType.Ice),
                        new IceRevealed(new TileCoord(0, 0), DebrisType.B)),
                    syncedResult =>
                    {
                        finalSyncCalls++;
                        harness.ContentPresenter.SyncImmediate(syncedResult.State);
                    });

                Assert.That(handled, Is.True);
            });

            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(FindChildByName(harness.ContentRoot, "Debris_B"), Is.Not.Null);
        }

        [Test]
        public void ActionPlaybackController_TargetExtractRoutesThroughPlaybackAndFinalSyncRepairsState()
        {
            ControllerHarness harness = CreateControllerHarness(playbackEnabled: true, yieldBetweenSteps: false);
            GameState previousState = CreateBoardState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new TargetTile("pup-1", Extracted: false))));
            GameState resultState = CreateBoardState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new TargetTile("pup-1", Extracted: true))));

            harness.GridPresenter.RebuildGrid(previousState);
            harness.ContentPresenter.SyncImmediate(previousState);

            Assert.That(harness.ContentPresenter.TryGetTargetInstance("pup-1", out GameObject? targetBeforePlayback), Is.True);
            Assert.That(targetBeforePlayback, Is.Not.Null);

            ActionResult result = CreateResult(
                resultState,
                actionCount: 5,
                new TargetExtracted("pup-1", new TileCoord(0, 0)));

            int finalSyncCalls = 0;

            bool handled = harness.Controller.TryPlayAction(previousState, new ActionInput(new TileCoord(0, 0)), result, syncedResult =>
            {
                finalSyncCalls++;
                harness.ContentPresenter.SyncImmediate(syncedResult.State);
            });

            Assert.That(handled, Is.True);
            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(harness.Controller.CurrentPlan[0].StepType, Is.EqualTo(ActionPlaybackStepType.TargetExtract));
            Assert.That(harness.Controller.CurrentPlan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
            Assert.That(harness.ContentPresenter.TryGetTargetInstance("pup-1", out GameObject? targetAfterPlayback), Is.False);
            Assert.That(targetAfterPlayback, Is.Null);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(0));
        }

        [Test]
        public void ActionPlaybackController_WaterRiseRoutesThroughPresenterBeforeFinalSync()
        {
            ControllerHarness harness = CreateControllerHarness(playbackEnabled: true, yieldBetweenSteps: false);
            GameState previousState = CreateBoardState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                floodedRows: 1,
                actionsUntilRise: 1,
                riseInterval: 3);
            GameState resultState = previousState with
            {
                Water = new WaterState(FloodedRows: 2, ActionsUntilRise: 3, RiseInterval: 3),
                ActionCount = previousState.ActionCount + 1,
            };

            harness.GridPresenter.RebuildGrid(previousState);
            harness.WaterPresenter.SyncImmediate(previousState);

            ActionResult result = CreateResult(
                resultState,
                actionCount: resultState.ActionCount,
                new WaterRose(FloodedRow: 2));

            int finalSyncCalls = 0;
            int waterChildCountAtFinalSync = -1;

            bool handled = harness.Controller.TryPlayAction(previousState, new ActionInput(new TileCoord(0, 0)), result, syncedResult =>
            {
                finalSyncCalls++;
                waterChildCountAtFinalSync = harness.WaterRoot.childCount;
                harness.WaterPresenter.ForceSyncToState(syncedResult.State);
            });

            Assert.That(handled, Is.True);
            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(harness.Controller.CurrentPlan[0].StepType, Is.EqualTo(ActionPlaybackStepType.WaterRise));
            Assert.That(waterChildCountAtFinalSync, Is.EqualTo(4));
            Assert.That(harness.WaterRoot.childCount, Is.EqualTo(4));
        }

        [Test]
        public void ActionPlaybackController_DockFeedbackRoutesThroughPlaybackAndFinalSyncRepairsDockState()
        {
            ControllerHarness harness = CreateControllerHarness(playbackEnabled: true, yieldBetweenSteps: false);
            GameState previousState = CreateBoardState(
                ImmutableArray.Create(ImmutableArray.Create<Tile>(new EmptyTile())),
                dockSlots: ImmutableArray.Create<DebrisType?>(DebrisType.A, DebrisType.A, DebrisType.B, DebrisType.B, null, null, null));
            GameState resultState = CreateBoardState(
                ImmutableArray.Create(ImmutableArray.Create<Tile>(new EmptyTile())),
                dockSlots: ImmutableArray.Create<DebrisType?>(DebrisType.A, DebrisType.A, DebrisType.B, DebrisType.B, DebrisType.C, null, null));

            harness.DockPresenter.Rebuild(previousState);

            ActionResult result = CreateResult(
                resultState,
                actionCount: 8,
                new GroupRemoved(DebrisType.C, ImmutableArray.Create(new TileCoord(0, 0))),
                new DockInserted(ImmutableArray.Create(DebrisType.C), OccupancyAfterInsert: 5, OverflowCount: 0),
                new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution),
                new GravitySettled(ImmutableArray<(TileCoord From, TileCoord To)>.Empty));

            int finalSyncCalls = 0;
            Material? materialAtFinalSync = null;

            bool handled = harness.Controller.TryPlayAction(previousState, new ActionInput(new TileCoord(0, 0)), result, syncedResult =>
            {
                finalSyncCalls++;
                materialAtFinalSync = harness.DockRenderer.sharedMaterial;
                harness.DockPresenter.ForceSyncToState(syncedResult.State);
            });

            Assert.That(handled, Is.True);
            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(harness.Controller.CurrentPlan[1].StepType, Is.EqualTo(ActionPlaybackStepType.DockFeedback));
            Assert.That(harness.Controller.CurrentPlan[2].StepType, Is.EqualTo(ActionPlaybackStepType.DockFeedback));
            Assert.That(materialAtFinalSync, Is.SameAs(harness.CautionMaterial));
            Assert.That(harness.DockPieceContainer.childCount, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator ActionPlaybackController_CancelPlaybackRecoversBoardWaterAndDockToAuthoritativeState()
        {
            ControllerHarness harness = CreateControllerHarness(playbackEnabled: true, yieldBetweenSteps: true);
            GameState previousState = CreateBoardState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A), new EmptyTile(), new EmptyTile()),
                    ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                dockSlots: ImmutableArray.Create<DebrisType?>(DebrisType.A, DebrisType.B, null, null, null, null, null),
                floodedRows: 0,
                actionsUntilRise: 1,
                riseInterval: 2);
            GameState resultState = CreateBoardState(
                ImmutableArray.Create(
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.C), new EmptyTile(), new EmptyTile()),
                    ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    ImmutableArray.Create<Tile>(new FloodedTile(), new FloodedTile(), new FloodedTile())),
                dockSlots: ImmutableArray.Create<DebrisType?>(DebrisType.A, DebrisType.B, DebrisType.C, null, null, null, null),
                floodedRows: 1,
                actionsUntilRise: 2,
                riseInterval: 2);

            harness.GridPresenter.RebuildGrid(previousState);
            harness.ContentPresenter.SyncImmediate(previousState);
            harness.WaterPresenter.SyncImmediate(previousState);
            harness.DockPresenter.Rebuild(previousState);

            ActionResult result = CreateResult(
                resultState,
                actionCount: 9,
                new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0))),
                new DockInserted(ImmutableArray.Create(DebrisType.C), OccupancyAfterInsert: 3, OverflowCount: 0),
                new GravitySettled(ImmutableArray<(TileCoord From, TileCoord To)>.Empty),
                new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C))),
                new WaterRose(FloodedRow: 2));

            int finalSyncCalls = 0;

            bool handled = harness.Controller.TryPlayAction(previousState, new ActionInput(new TileCoord(0, 0)), result, syncedResult =>
            {
                finalSyncCalls++;
                harness.ContentPresenter.ForceSyncToState(syncedResult.State);
                harness.WaterPresenter.ForceSyncToState(syncedResult.State);
                harness.DockPresenter.ForceSyncToState(syncedResult.State);
            });

            Assert.That(handled, Is.True);
            Assert.That(harness.Controller.IsPlaying, Is.True);
            Assert.That(FindChildByName(harness.ContentRoot, "Debris_A"), Is.Null);

            Assert.DoesNotThrow(() => harness.Controller.CancelPlayback());

            Assert.That(harness.Controller.IsPlaying, Is.False);
            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(FindChildByName(harness.ContentRoot, "Content_00_00_Debris_C"), Is.Not.Null);
            Assert.That(harness.DockPieceContainer.childCount, Is.EqualTo(3));
            Assert.That(harness.WaterRoot.childCount, Is.EqualTo(3));

            yield return null;

            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(harness.Controller.IsPlaying, Is.False);
        }

        private ActionPlaybackController CreateController(bool playbackEnabled, bool yieldBetweenSteps)
        {
            GameObject gameObject = CreateTrackedGameObject("ActionPlaybackController");
            ActionPlaybackController controller = gameObject.AddComponent<ActionPlaybackController>();
            SetPrivateField(controller, "settings", CreateSettings(playbackEnabled, yieldBetweenSteps));
            return controller;
        }

        private ControllerHarness CreateControllerHarness(bool playbackEnabled, bool yieldBetweenSteps)
        {
            GameObject presenterObject = CreateTrackedGameObject("PlaybackHarness");
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
            SetPrivateField(contentPresenter, "gridView", gridPresenter);
            SetPrivateField(contentPresenter, "contentRoot", contentRoot);
            SetPrivateField(contentPresenter, "fallbackContentPrefab", fallbackPrefab);
            SetPrivateField(contentPresenter, "contentYOffset", 0.05f);

            WaterViewPresenter waterPresenter = presenterObject.AddComponent<WaterViewPresenter>();
            Transform waterRoot = CreateTrackedGameObject("WaterRoot").transform;
            waterRoot.SetParent(presenterObject.transform, false);
            GameObject overlayPrefab = CreateTrackedGameObject("OverlayPrefab");
            GameObject waterlinePrefab = CreateTrackedGameObject("WaterlinePrefab");
            SetPrivateField(waterPresenter, "gridView", gridPresenter);
            SetPrivateField(waterPresenter, "waterRoot", waterRoot);
            SetPrivateField(waterPresenter, "floodedRowOverlayPrefab", null);
            SetPrivateField(waterPresenter, "forecastRowOverlayPrefab", null);
            SetPrivateField(waterPresenter, "waterlinePrefab", waterlinePrefab);
            SetPrivateField(waterPresenter, "fallbackOverlayPrefab", overlayPrefab);

            DockViewPresenter dockPresenter = presenterObject.AddComponent<DockViewPresenter>();
            Transform dockPieceContainer = CreateTrackedGameObject("DockPieces").transform;
            dockPieceContainer.SetParent(presenterObject.transform, false);
            GameObject fallbackPiecePrefab = CreateTrackedGameObject("FallbackPiecePrefab");
            GameObject dockVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(dockVisual);
            dockVisual.name = "DockVisual";
            dockVisual.transform.SetParent(presenterObject.transform, false);
            MeshRenderer dockRenderer = dockVisual.GetComponent<MeshRenderer>();
            Material safeMaterial = new Material(Shader.Find("Standard"));
            Material cautionMaterial = new Material(Shader.Find("Standard"));
            createdObjects.Add(safeMaterial);
            createdObjects.Add(cautionMaterial);
            for (int i = 0; i < 7; i++)
            {
                Transform anchor = CreateTrackedGameObject($"Slot_{i:00}").transform;
                anchor.SetParent(presenterObject.transform, false);
                anchor.localPosition = new Vector3(i, 0f, 0f);
            }

            SetPrivateField(dockPresenter, "sharedDockRenderer", dockRenderer);
            SetPrivateField(dockPresenter, "safeMaterial", safeMaterial);
            SetPrivateField(dockPresenter, "cautionMaterial", cautionMaterial);
            SetPrivateField(dockPresenter, "pieceContainer", dockPieceContainer);
            SetPrivateField(dockPresenter, "fallbackPiecePrefab", fallbackPiecePrefab);

            ActionPlaybackController controller = presenterObject.AddComponent<ActionPlaybackController>();
            SetPrivateField(controller, "settings", CreateSettings(playbackEnabled, yieldBetweenSteps));
            SetPrivateField(controller, "boardContent", contentPresenter);
            SetPrivateField(controller, "waterView", waterPresenter);
            SetPrivateField(controller, "dockView", dockPresenter);

            return new ControllerHarness(
                controller,
                gridPresenter,
                contentPresenter,
                waterPresenter,
                dockPresenter,
                contentRoot,
                waterRoot,
                dockPieceContainer,
                dockRenderer,
                cautionMaterial);
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static ActionPlaybackSettings CreateSettings(bool playbackEnabled, bool yieldBetweenSteps)
        {
            ActionPlaybackSettings settings = new ActionPlaybackSettings();
            SetPrivateField(settings, "playbackEnabled", playbackEnabled);
            SetPrivateField(settings, "yieldBetweenSteps", yieldBetweenSteps);
            return settings;
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

        private static ActionResult CreateResult(int actionCount)
        {
            GameState state = CreateState() with { ActionCount = actionCount };
            return new ActionResult(
                state,
                ImmutableArray.Create<ActionEvent>(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))),
                ActionOutcome.Ok,
                Snapshot: null);
        }

        private static ActionResult CreateResult(GameState state, int actionCount, params ActionEvent[] events)
        {
            return new ActionResult(
                state with { ActionCount = actionCount },
                ImmutableArray.CreateRange(events),
                ActionOutcome.Ok,
                Snapshot: null);
        }

        private static GameState CreateState()
        {
            return CreateBoardState(ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A)),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile())));
        }

        private static GameState CreateBoardState(
            ImmutableArray<ImmutableArray<Tile>> rows,
            ImmutableArray<DebrisType?>? dockSlots = null,
            int floodedRows = 0,
            int actionsUntilRise = 3,
            int riseInterval = 3)
        {
            int height = rows.Length;
            int width = height > 0 ? rows[0].Length : 0;
            CoreBoard board = new CoreBoard(width, height, rows);

            return new GameState(
                Board: board,
                Dock: new CoreDock(
                    dockSlots ?? ImmutableArray.Create<DebrisType?>(
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null),
                    Size: 7),
                Water: new WaterState(FloodedRows: floodedRows, ActionsUntilRise: actionsUntilRise, RiseInterval: riseInterval),
                Vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                Targets: ImmutableArray<TargetState>.Empty,
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

        private static GameObject? GetRegisteredPieceObject(BoardContentViewPresenter presenter, TileCoord coord)
        {
            object? visualRegistry = GetPrivateFieldValue(presenter, "visualRegistry");
            Assert.That(visualRegistry, Is.Not.Null);
            if (visualRegistry is null)
            {
                return null;
            }

            System.Reflection.PropertyInfo? debrisRegistryProperty = visualRegistry.GetType().GetProperty(
                "Debris",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            Assert.That(debrisRegistryProperty, Is.Not.Null);
            if (debrisRegistryProperty is null)
            {
                return null;
            }

            object? debrisRegistry = debrisRegistryProperty.GetValue(visualRegistry);
            Assert.That(debrisRegistry, Is.Not.Null);
            if (debrisRegistry is null)
            {
                return null;
            }

            object?[] arguments = { coord, null };
            System.Reflection.MethodInfo? tryGetMethod = debrisRegistry.GetType().GetMethod(
                "TryGet",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            Assert.That(tryGetMethod, Is.Not.Null);
            if (tryGetMethod is null)
            {
                return null;
            }

            bool found = (bool)tryGetMethod.Invoke(debrisRegistry, arguments)!;
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

        private static object? GetPrivateFieldValue(object target, string fieldName)
        {
            System.Reflection.FieldInfo? field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}'.");
            return field?.GetValue(target);
        }

        private readonly struct ControllerHarness
        {
            public ControllerHarness(
                ActionPlaybackController controller,
                BoardGridViewPresenter gridPresenter,
                BoardContentViewPresenter contentPresenter,
                WaterViewPresenter waterPresenter,
                DockViewPresenter dockPresenter,
                Transform contentRoot,
                Transform waterRoot,
                Transform dockPieceContainer,
                MeshRenderer dockRenderer,
                Material cautionMaterial)
            {
                Controller = controller;
                GridPresenter = gridPresenter;
                ContentPresenter = contentPresenter;
                WaterPresenter = waterPresenter;
                DockPresenter = dockPresenter;
                ContentRoot = contentRoot;
                WaterRoot = waterRoot;
                DockPieceContainer = dockPieceContainer;
                DockRenderer = dockRenderer;
                CautionMaterial = cautionMaterial;
            }

            public ActionPlaybackController Controller { get; }

            public BoardGridViewPresenter GridPresenter { get; }

            public BoardContentViewPresenter ContentPresenter { get; }

            public WaterViewPresenter WaterPresenter { get; }

            public DockViewPresenter DockPresenter { get; }

            public Transform ContentRoot { get; }

            public Transform WaterRoot { get; }

            public Transform DockPieceContainer { get; }

            public MeshRenderer DockRenderer { get; }

            public Material CautionMaterial { get; }
        }
    }
}
