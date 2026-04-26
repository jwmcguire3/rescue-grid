using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Presentation;
using Rescue.Unity.UI;
using UnityEngine;
using UnityEngine.TestTools;
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class GameStateViewPresenterTests
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
        public void GameStateViewPresenter_RebuildDoesNotThrowWithNoPresenters()
        {
            GameObject presenterObject = CreateTrackedGameObject("GameStateViewPresenter");
            GameStateViewPresenter presenter = presenterObject.AddComponent<GameStateViewPresenter>();
            LogAssert.Expect(LogType.Warning, "GameStateViewPresenter is missing boardGrid.");
            LogAssert.Expect(LogType.Warning, "GameStateViewPresenter is missing boardContent.");
            LogAssert.Expect(LogType.Warning, "GameStateViewPresenter is missing waterView.");
            LogAssert.Expect(LogType.Warning, "GameStateViewPresenter is missing dockView.");
            LogAssert.Expect(LogType.Warning, "GameStateViewPresenter is missing targetFeedback.");

            Assert.DoesNotThrow(() => presenter.Rebuild(CreateState()));
        }

        [Test]
        public void GameStateViewPresenter_RebuildUpdatesAssignedPresenters()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState();

            harness.Presenter.Rebuild(state);

            Assert.That(CountGeneratedTiles(harness.BoardRoot), Is.EqualTo(6));
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(3));
            Assert.That(harness.WaterRoot.childCount, Is.EqualTo(3));
            Assert.That(harness.DockPieceContainer.childCount, Is.EqualTo(3));
        }

        [Test]
        public void GameStateViewPresenter_RebuildAutoResolvesCoLocatedTargetFeedbackPresenter()
        {
            PresenterHarness harness = CreateHarness(assignTargetFeedbackToPresenter: false);
            GameState state = CreateState();

            LogAssert.NoUnexpectedReceived();
            Assert.DoesNotThrow(() => harness.Presenter.Rebuild(state));
        }

        [Test]
        public void GameStateViewPresenter_ClearAllClearsGeneratedObjects()
        {
            PresenterHarness harness = CreateHarness();
            GameState state = CreateState();

            harness.Presenter.Rebuild(state);
            harness.Presenter.ClearAll();

            Assert.That(harness.BoardRoot.childCount, Is.EqualTo(0));
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(0));
            Assert.That(harness.WaterRoot.childCount, Is.EqualTo(0));
            Assert.That(harness.DockPieceContainer.childCount, Is.EqualTo(0));
        }

        [Test]
        public void GameStateViewPresenter_ApplyActionResultBuildsPlaybackPlanWithFinalSync()
        {
            PresenterHarness harness = CreateHarness();
            GameState previousState = CreateState();
            GameState resultState = previousState with { ActionCount = previousState.ActionCount + 1 };
            ActionInput input = new ActionInput(new TileCoord(0, 0));
            ActionResult result = new ActionResult(
                resultState,
                ImmutableArray.Create<ActionEvent>(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0)))),
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.B)))),
                ActionOutcome.Ok,
                Snapshot: null);

            harness.Presenter.Rebuild(previousState);
            harness.Presenter.ApplyActionResult(previousState, input, result);

            Assert.That(harness.Presenter.CurrentPlaybackPlan.Select(step => step.StepType), Is.EqualTo(new[]
            {
                ActionPlaybackStepType.RemoveGroup,
                ActionPlaybackStepType.Gravity,
                ActionPlaybackStepType.Spawn,
                ActionPlaybackStepType.FinalSync,
            }));
            Assert.That(harness.Presenter.CurrentState, Is.EqualTo(resultState));
            Assert.That(harness.Presenter.CurrentState!.ActionCount, Is.EqualTo(resultState.ActionCount));
        }

        [UnityTest]
        public System.Collections.IEnumerator GameStateViewPresenter_ApplyActionResultFinalSyncsAfterPlaybackControllerCompletes()
        {
            PresenterHarness harness = CreateHarness(withPlaybackController: true, yieldBetweenSteps: true);
            GameState previousState = CreateState();
            GameState resultState = previousState with { ActionCount = previousState.ActionCount + 1 };
            ActionInput input = new ActionInput(new TileCoord(0, 0));
            ActionResult result = new ActionResult(
                resultState,
                ImmutableArray.Create<ActionEvent>(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))),
                ActionOutcome.Ok,
                Snapshot: null);

            harness.Presenter.Rebuild(previousState);
            harness.Presenter.ApplyActionResult(previousState, input, result);

            Assert.That(harness.Presenter.IsPlaybackActive, Is.True);
            Assert.That(harness.Presenter.CurrentState, Is.EqualTo(previousState));

            yield return null;

            Assert.That(harness.Presenter.IsPlaybackActive, Is.False);
            Assert.That(harness.Presenter.CurrentState, Is.EqualTo(resultState));
        }

        [UnityTest]
        public System.Collections.IEnumerator GameStateViewPresenter_CancelPlaybackForceSyncsToAuthoritativeState()
        {
            PresenterHarness harness = CreateHarness(withPlaybackController: true, yieldBetweenSteps: true);
            GameState previousState = CreateState();
            GameState resultState = previousState with
            {
                Board = new CoreBoard(
                    3,
                    2,
                    ImmutableArray.Create(
                        ImmutableArray.Create<Tile>(
                            new DebrisTile(DebrisType.B),
                            new EmptyTile(),
                            new EmptyTile()),
                        ImmutableArray.Create<Tile>(
                            new EmptyTile(),
                            new EmptyTile(),
                            new EmptyTile()))),
                Dock = new CoreDock(
                    ImmutableArray.Create<DebrisType?>(
                        DebrisType.A,
                        DebrisType.B,
                        DebrisType.C,
                        DebrisType.A,
                        null,
                        null,
                        null),
                    Size: 7),
                Water = new WaterState(FloodedRows: 2, ActionsUntilRise: 4, RiseInterval: 4),
                ActionCount = previousState.ActionCount + 1,
            };

            ActionResult result = new ActionResult(
                resultState,
                ImmutableArray.Create<ActionEvent>(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0))),
                    new DockInserted(ImmutableArray.Create(DebrisType.A), OccupancyAfterInsert: 4, OverflowCount: 0),
                    new GravitySettled(ImmutableArray<(TileCoord From, TileCoord To)>.Empty),
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.B))),
                    new WaterRose(FloodedRow: 0)),
                ActionOutcome.Ok,
                Snapshot: null);

            harness.Presenter.Rebuild(previousState);
            harness.Presenter.ApplyActionResult(previousState, new ActionInput(new TileCoord(0, 0)), result);

            Assert.That(harness.Presenter.IsPlaybackActive, Is.True);

            ActionPlaybackController? playbackController = harness.Presenter.GetComponent<ActionPlaybackController>();
            Assert.That(playbackController, Is.Not.Null);
            Assert.DoesNotThrow(() => playbackController!.CancelPlayback());

            Assert.That(harness.Presenter.IsPlaybackActive, Is.False);
            Assert.That(harness.Presenter.CurrentState, Is.EqualTo(resultState));
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
            Assert.That(harness.WaterRoot.childCount, Is.EqualTo(3));
            Assert.That(harness.DockPieceContainer.childCount, Is.EqualTo(4));

            yield return null;

            Assert.That(harness.Presenter.CurrentState, Is.EqualTo(resultState));
        }

        [UnityTest]
        public System.Collections.IEnumerator GameStateViewPresenter_RebuildDuringPlaybackCancelsStalePlaybackAndKeepsReplacementState()
        {
            PresenterHarness harness = CreateHarness(withPlaybackController: true, yieldBetweenSteps: true);
            GameState previousState = CreateState();
            GameState playbackState = previousState with { ActionCount = previousState.ActionCount + 1 };
            GameState replacementState = previousState with
            {
                ActionCount = 99,
                Water = new WaterState(FloodedRows: 0, ActionsUntilRise: 4, RiseInterval: 4),
            };

            ActionResult result = new ActionResult(
                playbackState,
                ImmutableArray.Create<ActionEvent>(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0)))),
                ActionOutcome.Ok,
                Snapshot: null);

            harness.Presenter.Rebuild(previousState);
            harness.Presenter.ApplyActionResult(previousState, new ActionInput(new TileCoord(0, 0)), result);

            Assert.That(harness.Presenter.IsPlaybackActive, Is.True);

            harness.Presenter.Rebuild(replacementState);

            Assert.That(harness.Presenter.IsPlaybackActive, Is.False);
            Assert.That(harness.Presenter.CurrentState, Is.EqualTo(replacementState));
            Assert.That(harness.Presenter.CurrentPlaybackPlan.Count, Is.EqualTo(0));

            yield return null;

            Assert.That(harness.Presenter.CurrentState, Is.EqualTo(replacementState));
            Assert.That(harness.Presenter.IsPlaybackActive, Is.False);
        }

        [UnityTest]
        public System.Collections.IEnumerator GameStateViewPresenter_ForceSyncDuringPlaybackCancelsStalePlaybackAndKeepsReplacementState()
        {
            PresenterHarness harness = CreateHarness(withPlaybackController: true, yieldBetweenSteps: true);
            GameState previousState = CreateState();
            GameState playbackState = previousState with { ActionCount = previousState.ActionCount + 1 };
            GameState replacementState = previousState with
            {
                ActionCount = 42,
                Dock = new CoreDock(
                    ImmutableArray.Create<DebrisType?>(
                        DebrisType.B,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null),
                    Size: 7),
            };

            ActionResult result = new ActionResult(
                playbackState,
                ImmutableArray.Create<ActionEvent>(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0)))),
                ActionOutcome.Ok,
                Snapshot: null);

            harness.Presenter.Rebuild(previousState);
            harness.Presenter.ApplyActionResult(previousState, new ActionInput(new TileCoord(0, 0)), result);

            Assert.That(harness.Presenter.IsPlaybackActive, Is.True);

            harness.Presenter.ForceSyncToState(replacementState, "test undo sync");

            Assert.That(harness.Presenter.IsPlaybackActive, Is.False);
            Assert.That(harness.Presenter.CurrentState, Is.EqualTo(replacementState));
            Assert.That(harness.Presenter.CurrentPlaybackPlan.Count, Is.EqualTo(0));

            yield return null;

            Assert.That(harness.Presenter.CurrentState, Is.EqualTo(replacementState));
            Assert.That(harness.Presenter.IsPlaybackActive, Is.False);
        }

        [Test]
        public void GameStateViewPresenter_ApplyActionResultFallsBackToImmediateSyncWhenPlaybackControllerDisabled()
        {
            PresenterHarness harness = CreateHarness(withPlaybackController: true, playbackEnabled: false);
            GameState previousState = CreateState();
            GameState resultState = previousState with { ActionCount = previousState.ActionCount + 1 };
            ActionInput input = new ActionInput(new TileCoord(0, 0));
            ActionResult result = new ActionResult(
                resultState,
                ImmutableArray.Create<ActionEvent>(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))),
                ActionOutcome.Ok,
                Snapshot: null);

            harness.Presenter.Rebuild(previousState);
            harness.Presenter.ApplyActionResult(previousState, input, result);

            Assert.That(harness.Presenter.IsPlaybackActive, Is.False);
            Assert.That(harness.Presenter.CurrentState, Is.EqualTo(resultState));
        }

        [Test]
        public void GameStateViewPresenter_ForceSyncToStateRepairsVisibleStateAfterMismatch()
        {
            PresenterHarness harness = CreateHarness();
            GameState previousState = CreateState();
            GameState repairedState = previousState with
            {
                Board = new CoreBoard(
                    3,
                    2,
                    ImmutableArray.Create(
                        ImmutableArray.Create<Tile>(
                            new EmptyTile(),
                            new DebrisTile(DebrisType.B),
                            new TargetTile("puppy-1", Extracted: false)),
                        ImmutableArray.Create<Tile>(
                            new EmptyTile(),
                            new EmptyTile(),
                            new EmptyTile()))),
                Dock = new CoreDock(
                    ImmutableArray.Create<DebrisType?>(
                        DebrisType.B,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null),
                    Size: 7),
                Water = new WaterState(FloodedRows: 0, ActionsUntilRise: 4, RiseInterval: 4),
            };

            harness.Presenter.Rebuild(previousState);
            harness.ContentRoot.GetChild(0).name = "StaleVisual";
            Object.DestroyImmediate(harness.ContentRoot.GetChild(0).gameObject);
            harness.DockPieceContainer.GetChild(0).name = "StaleDockPiece";
            Object.DestroyImmediate(harness.DockPieceContainer.GetChild(0).gameObject);

            harness.Presenter.ForceSyncToState(repairedState, "test mismatch recovery");

            Assert.That(harness.Presenter.CurrentState, Is.EqualTo(repairedState));
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(2));
            Assert.That(harness.WaterRoot.childCount, Is.EqualTo(1));
            Assert.That(harness.DockPieceContainer.childCount, Is.EqualTo(1));
        }

        private PresenterHarness CreateHarness(
            bool assignTargetFeedbackToPresenter = true,
            bool withPlaybackController = false,
            bool playbackEnabled = true,
            bool yieldBetweenSteps = false)
        {
            GameObject presenterObject = CreateTrackedGameObject("GameStateViewRoot");
            GameStateViewPresenter presenter = presenterObject.AddComponent<GameStateViewPresenter>();

            BoardGridViewPresenter boardGrid = presenterObject.AddComponent<BoardGridViewPresenter>();
            Transform boardRoot = CreateTrackedGameObject("BoardRoot").transform;
            boardRoot.SetParent(presenterObject.transform, false);
            GameObject fallbackTilePrefab = CreateTrackedGameObject("FallbackTilePrefab");
            SetPrivateField(boardGrid, "boardRoot", boardRoot);
            SetPrivateField(boardGrid, "dryTilePrefab", null);
            SetPrivateField(boardGrid, "fallbackTilePrefab", fallbackTilePrefab);

            BoardContentViewPresenter boardContent = presenterObject.AddComponent<BoardContentViewPresenter>();
            Transform contentRoot = CreateTrackedGameObject("BoardContentRoot").transform;
            contentRoot.SetParent(presenterObject.transform, false);
            GameObject fallbackContentPrefab = CreateTrackedGameObject("FallbackContentPrefab");
            SetPrivateField(boardContent, "gridView", boardGrid);
            SetPrivateField(boardContent, "contentRoot", contentRoot);
            SetPrivateField(boardContent, "fallbackContentPrefab", fallbackContentPrefab);

            WaterViewPresenter waterView = presenterObject.AddComponent<WaterViewPresenter>();
            Transform waterRoot = CreateTrackedGameObject("WaterRoot").transform;
            waterRoot.SetParent(presenterObject.transform, false);
            GameObject overlayPrefab = CreateTrackedGameObject("OverlayPrefab");
            GameObject waterlinePrefab = CreateTrackedGameObject("WaterlinePrefab");
            SetPrivateField(waterView, "gridView", boardGrid);
            SetPrivateField(waterView, "waterRoot", waterRoot);
            SetPrivateField(waterView, "floodedRowOverlayPrefab", null);
            SetPrivateField(waterView, "forecastRowOverlayPrefab", null);
            SetPrivateField(waterView, "waterlinePrefab", waterlinePrefab);
            SetPrivateField(waterView, "fallbackOverlayPrefab", overlayPrefab);

            DockViewPresenter dockView = presenterObject.AddComponent<DockViewPresenter>();
            Transform dockPieceContainer = CreateTrackedGameObject("DockPieces").transform;
            dockPieceContainer.SetParent(presenterObject.transform, false);
            GameObject fallbackPiecePrefab = CreateTrackedGameObject("FallbackPiecePrefab");
            GameObject dockVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(dockVisual);
            dockVisual.name = "LegacyDockVisual";
            dockVisual.transform.SetParent(presenterObject.transform, false);
            MeshRenderer dockRenderer = dockVisual.GetComponent<MeshRenderer>();
            Material safeMaterial = new Material(Shader.Find("Standard"));
            createdObjects.Add(safeMaterial);
            for (int i = 0; i < 7; i++)
            {
                Transform anchor = CreateTrackedGameObject($"Slot_{i:00}").transform;
                anchor.SetParent(presenterObject.transform, false);
                anchor.localPosition = new Vector3(i, 0f, 0f);
            }

            SetPrivateField(dockView, "sharedDockRenderer", dockRenderer);
            SetPrivateField(dockView, "safeMaterial", safeMaterial);
            SetPrivateField(dockView, "pieceContainer", dockPieceContainer);
            SetPrivateField(dockView, "fallbackPiecePrefab", fallbackPiecePrefab);

            TargetFeedbackPresenter targetFeedback = presenterObject.AddComponent<TargetFeedbackPresenter>();
            Transform targetFeedbackRoot = CreateTrackedGameObject("TargetFeedbackRoot").transform;
            targetFeedbackRoot.SetParent(presenterObject.transform, false);
            GameObject fallbackTargetPrefab = CreateTrackedGameObject("FallbackTargetPrefab");
            SetPrivateField(targetFeedback, "gridView", boardGrid);
            SetPrivateField(targetFeedback, "contentView", boardContent);
            SetPrivateField(targetFeedback, "feedbackRoot", targetFeedbackRoot);
            SetPrivateField(targetFeedback, "fallbackTargetPrefab", fallbackTargetPrefab);

            SetPrivateField(presenter, "boardGrid", boardGrid);
            SetPrivateField(presenter, "boardContent", boardContent);
            SetPrivateField(presenter, "waterView", waterView);
            SetPrivateField(presenter, "dockView", dockView);
            if (assignTargetFeedbackToPresenter)
            {
                SetPrivateField(presenter, "targetFeedback", targetFeedback);
            }

            if (withPlaybackController)
            {
                ActionPlaybackController playbackController = presenterObject.AddComponent<ActionPlaybackController>();
                SetPrivateField(playbackController, "settings", CreateSettings(playbackEnabled, yieldBetweenSteps));
                SetPrivateField(presenter, "playbackController", playbackController);
            }

            return new PresenterHarness(presenter, boardRoot, contentRoot, waterRoot, dockPieceContainer);
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static int CountGeneratedTiles(Transform boardRoot)
        {
            int tileCount = 0;
            for (int i = 0; i < boardRoot.childCount; i++)
            {
                tileCount += boardRoot.GetChild(i).childCount;
            }

            return tileCount;
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

        private static ActionPlaybackSettings CreateSettings(bool playbackEnabled, bool yieldBetweenSteps)
        {
            ActionPlaybackSettings settings = new ActionPlaybackSettings();
            SetPrivateField(settings, "playbackEnabled", playbackEnabled);
            SetPrivateField(settings, "yieldBetweenSteps", yieldBetweenSteps);
            return settings;
        }

        private static GameState CreateState()
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new BlockerTile(BlockerType.Crate, 1, null),
                    new TargetTile("puppy-1", Extracted: false)),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile(),
                    new EmptyTile()));

            CoreBoard board = new CoreBoard(3, 2, rows);

            return new GameState(
                Board: board,
                Dock: new CoreDock(
                    ImmutableArray.Create<DebrisType?>(
                        DebrisType.A,
                        DebrisType.B,
                        DebrisType.C,
                        null,
                        null,
                        null,
                        null),
                    Size: 7),
                Water: new WaterState(FloodedRows: 1, ActionsUntilRise: 2, RiseInterval: 4),
                Vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                Targets: ImmutableArray.Create(new TargetState("puppy-1", new TileCoord(0, 2), Extracted: false, OneClearAway: false)),
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
                GameStateViewPresenter presenter,
                Transform boardRoot,
                Transform contentRoot,
                Transform waterRoot,
                Transform dockPieceContainer)
            {
                Presenter = presenter;
                BoardRoot = boardRoot;
                ContentRoot = contentRoot;
                WaterRoot = waterRoot;
                DockPieceContainer = dockPieceContainer;
            }

            public GameStateViewPresenter Presenter { get; }

            public Transform BoardRoot { get; }

            public Transform ContentRoot { get; }

            public Transform WaterRoot { get; }

            public Transform DockPieceContainer { get; }
        }
    }
}
