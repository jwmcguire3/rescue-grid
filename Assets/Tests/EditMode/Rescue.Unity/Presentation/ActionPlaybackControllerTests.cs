using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
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

            ActionPlaybackController controller = presenterObject.AddComponent<ActionPlaybackController>();
            SetPrivateField(controller, "settings", CreateSettings(playbackEnabled, yieldBetweenSteps));
            SetPrivateField(controller, "boardContent", contentPresenter);
            SetPrivateField(controller, "waterView", waterPresenter);

            return new ControllerHarness(controller, gridPresenter, contentPresenter, waterPresenter, contentRoot, waterRoot);
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
                    ImmutableArray.Create<DebrisType?>(
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

        private readonly struct ControllerHarness
        {
            public ControllerHarness(
                ActionPlaybackController controller,
                BoardGridViewPresenter gridPresenter,
                BoardContentViewPresenter contentPresenter,
                WaterViewPresenter waterPresenter,
                Transform contentRoot,
                Transform waterRoot)
            {
                Controller = controller;
                GridPresenter = gridPresenter;
                ContentPresenter = contentPresenter;
                WaterPresenter = waterPresenter;
                ContentRoot = contentRoot;
                WaterRoot = waterRoot;
            }

            public ActionPlaybackController Controller { get; }

            public BoardGridViewPresenter GridPresenter { get; }

            public BoardContentViewPresenter ContentPresenter { get; }

            public WaterViewPresenter WaterPresenter { get; }

            public Transform ContentRoot { get; }

            public Transform WaterRoot { get; }
        }
    }
}
