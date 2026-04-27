using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Input;
using Rescue.Unity.Presentation;
using Rescue.Unity.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.PlayMode.Tests.Smoke
{
    public sealed class ActionPlaybackSmokeTests
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
        public System.Collections.IEnumerator TapPlaybackLocksInputAndFinalSyncRepairsRenderedState()
        {
            PlaybackHarness harness = CreateHarness();
            GameState initialState = CreatePlayableState();
            TileCoord tappedCoord = new TileCoord(0, 0);
            ActionInput input = new ActionInput(tappedCoord);
            ActionResult expectedResult = Pipeline.RunAction(initialState, input);

            Assert.That(expectedResult.Outcome, Is.Not.EqualTo(ActionOutcome.LossDockOverflow));
            Assert.That(expectedResult.Outcome, Is.Not.EqualTo(ActionOutcome.LossWaterOnTarget));
            Assert.That(expectedResult.Events, Has.Some.TypeOf<GroupRemoved>());
            Assert.That(expectedResult.Events, Has.Some.TypeOf<DockInserted>());
            Assert.That(expectedResult.Events, Has.Some.TypeOf<Spawned>());

            harness.ViewPresenter.Rebuild(initialState);
            harness.InputPresenter.SetCurrentState(initialState);

            bool firstTapHandled = harness.InputPresenter.TryRunActionAt(tappedCoord);

            Assert.That(firstTapHandled, Is.True);
            Assert.That(harness.ViewPresenter.IsPlaybackActive, Is.True);
            Assert.That(harness.PlaybackController.IsPlaying, Is.True);
            Assert.That(harness.ViewPresenter.CurrentState, Is.EqualTo(initialState));
            Assert.That(harness.InputPresenter.CurrentState, Is.EqualTo(initialState));
            Assert.That(harness.ViewPresenter.CurrentPlaybackPlan.Count, Is.GreaterThan(1));
            Assert.That(harness.ViewPresenter.CurrentPlaybackPlan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));

            bool secondTapHandled = harness.InputPresenter.TryRunActionAt(tappedCoord);

            Assert.That(secondTapHandled, Is.False, "Input should be locked while playback is active.");

            yield return null;

            while (harness.ViewPresenter.IsPlaybackActive)
            {
                yield return null;
            }

            yield return null;

            Assert.That(harness.PlaybackController.IsPlaying, Is.False);
            Assert.That(harness.ViewPresenter.CurrentState, Is.Not.Null);
            Assert.That(harness.InputPresenter.CurrentState, Is.Not.Null);
            Assert.That(
                SmokeTestHarness.Fingerprint(harness.ViewPresenter.CurrentState!),
                Is.EqualTo(SmokeTestHarness.Fingerprint(expectedResult.State)));
            Assert.That(
                SmokeTestHarness.Fingerprint(harness.InputPresenter.CurrentState!),
                Is.EqualTo(SmokeTestHarness.Fingerprint(expectedResult.State)));

            AssertBoardContentMatchesState(harness.ContentRoot, expectedResult.State);
            Assert.That(harness.DockPieceContainer.childCount, Is.EqualTo(CountDockPieces(expectedResult.State.Dock)));
            Assert.That(harness.WaterRoot.childCount, Is.EqualTo(ExpectedWaterVisualCount(expectedResult.State)));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public System.Collections.IEnumerator TargetStateFeedbackSmokeShowsOneClearAwayMarker()
        {
            PlaybackHarness harness = CreateHarness();
            GameState state = CreateTargetReadabilityState(TargetReadiness.OneClearAway);

            harness.ViewPresenter.Rebuild(state);
            yield return null;

            Transform target = FindRequiredChildContaining(harness.ContentRoot, "Target_pup-read");
            Assert.That(target.Find("TargetReadabilityMarker_OneClearAway"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public System.Collections.IEnumerator DockWarningSmokeShowsCautionAndAcuteMaterials()
        {
            PlaybackHarness harness = CreateHarness();
            GameState cautionState = CreateDockOccupancyState(5);
            GameState acuteState = CreateDockOccupancyState(6);

            harness.ViewPresenter.Rebuild(cautionState);
            yield return null;

            Assert.That(harness.DockRenderer.sharedMaterial, Is.SameAs(harness.CautionMaterial));

            harness.ViewPresenter.ForceSyncToState(acuteState);
            yield return null;

            Assert.That(harness.DockRenderer.sharedMaterial, Is.SameAs(harness.AcuteMaterial));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public System.Collections.IEnumerator WaterForecastSmokeShowsPersistentForecastRow()
        {
            PlaybackHarness harness = CreateHarness();
            GameState state = CreateWaterForecastState(floodedRows: 1, actionsUntilRise: 1);

            harness.ViewPresenter.Rebuild(state);
            yield return null;

            Assert.That(FindChildByName(harness.WaterRoot, "ForecastRow_00"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public System.Collections.IEnumerator VinePreviewAndGrowthSmokeShowsAuthoredTileThenGrowth()
        {
            PlaybackHarness harness = CreateHarness();
            GameState previewState = CreateVinePreviewState();
            GameState grownState = CreateVineGrownState();

            harness.ViewPresenter.Rebuild(previewState);
            yield return null;

            Assert.That(FindChildContaining(harness.ContentRoot, "VineGrowthPreview"), Is.Not.Null);

            harness.ViewPresenter.ForceSyncToState(grownState);
            yield return null;

            Assert.That(FindChildContaining(harness.ContentRoot, "VineGrowthPreview"), Is.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Content_00_00_Blocker_Vine"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public System.Collections.IEnumerator TerminalLossPlaybackShowsLossOverlayAfterFinalSync()
        {
            PlaybackHarness harness = CreateHarness();
            GameState initialState = CreateOverflowLossState();
            TileCoord tappedCoord = new TileCoord(0, 0);
            ActionInput input = new ActionInput(tappedCoord);
            ActionResult expectedResult = Pipeline.RunAction(initialState, input);

            Assert.That(expectedResult.Outcome, Is.EqualTo(ActionOutcome.LossDockOverflow));
            Assert.That(expectedResult.Events, Has.Some.TypeOf<Lost>());

            harness.ViewPresenter.Rebuild(initialState);
            harness.InputPresenter.SetCurrentState(initialState);

            bool firstTapHandled = harness.InputPresenter.TryRunActionAt(tappedCoord);

            Assert.That(firstTapHandled, Is.True);
            Assert.That(harness.ViewPresenter.IsPlaybackActive, Is.True);
            Assert.That(harness.LossScreen.IsVisible, Is.False);

            bool secondTapHandled = harness.InputPresenter.TryRunActionAt(tappedCoord);

            Assert.That(secondTapHandled, Is.False, "Input should be locked while terminal loss playback is active.");

            yield return null;

            while (harness.ViewPresenter.IsPlaybackActive)
            {
                yield return null;
            }

            yield return null;

            Assert.That(harness.LossScreen.IsVisible, Is.True);
            Assert.That(
                SmokeTestHarness.Fingerprint(harness.ViewPresenter.CurrentState!),
                Is.EqualTo(SmokeTestHarness.Fingerprint(expectedResult.State)));
            Assert.That(
                SmokeTestHarness.Fingerprint(harness.InputPresenter.CurrentState!),
                Is.EqualTo(SmokeTestHarness.Fingerprint(expectedResult.State)));
            AssertBoardContentMatchesState(harness.ContentRoot, expectedResult.State);
            Assert.That(harness.DockPieceContainer.childCount, Is.EqualTo(CountDockPieces(expectedResult.State.Dock)));
            LogAssert.NoUnexpectedReceived();
        }

        private PlaybackHarness CreateHarness()
        {
            GameObject root = CreateTrackedGameObject("ActionPlaybackSmokeHarness");

            Transform boardGridRoot = CreateTrackedGameObject("BoardGrid").transform;
            boardGridRoot.SetParent(root.transform, false);
            Transform boardContentRoot = CreateTrackedGameObject("BoardContent").transform;
            boardContentRoot.SetParent(root.transform, false);
            Transform waterOverlayRoot = CreateTrackedGameObject("WaterOverlay").transform;
            waterOverlayRoot.SetParent(root.transform, false);
            Transform targetFeedbackRoot = CreateTrackedGameObject("TargetFeedback").transform;
            targetFeedbackRoot.SetParent(root.transform, false);

            BoardGridViewPresenter boardGrid = root.AddComponent<BoardGridViewPresenter>();
            GameObject fallbackTilePrefab = CreateTrackedGameObject("FallbackTilePrefab");
            SetPrivateField(boardGrid, "boardRoot", boardGridRoot);
            SetPrivateField(boardGrid, "dryTilePrefab", null);
            SetPrivateField(boardGrid, "fallbackTilePrefab", fallbackTilePrefab);

            BoardContentViewPresenter boardContent = root.AddComponent<BoardContentViewPresenter>();
            GameObject fallbackContentPrefab = CreateTrackedGameObject("FallbackContentPrefab");
            SetPrivateField(boardContent, "gridView", boardGrid);
            SetPrivateField(boardContent, "contentRoot", boardContentRoot);
            SetPrivateField(boardContent, "fallbackContentPrefab", fallbackContentPrefab);

            WaterViewPresenter waterView = root.AddComponent<WaterViewPresenter>();
            GameObject overlayPrefab = CreateTrackedGameObject("OverlayPrefab");
            GameObject waterlinePrefab = CreateTrackedGameObject("WaterlinePrefab");
            SetPrivateField(waterView, "gridView", boardGrid);
            SetPrivateField(waterView, "waterRoot", waterOverlayRoot);
            SetPrivateField(waterView, "floodedRowOverlayPrefab", null);
            SetPrivateField(waterView, "forecastRowOverlayPrefab", null);
            SetPrivateField(waterView, "waterlinePrefab", waterlinePrefab);
            SetPrivateField(waterView, "fallbackOverlayPrefab", overlayPrefab);

            DockViewPresenter dockView = root.AddComponent<DockViewPresenter>();
            Transform dockPieceContainer = CreateTrackedGameObject("DockPieces").transform;
            dockPieceContainer.SetParent(root.transform, false);
            GameObject fallbackPiecePrefab = CreateTrackedGameObject("FallbackPiecePrefab");
            GameObject dockVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(dockVisual);
            dockVisual.name = "DockVisual";
            dockVisual.transform.SetParent(root.transform, false);
            MeshRenderer dockRenderer = dockVisual.GetComponent<MeshRenderer>();
            Material safeMaterial = new Material(Shader.Find("Standard"));
            Material cautionMaterial = new Material(Shader.Find("Standard"));
            Material acuteMaterial = new Material(Shader.Find("Standard"));
            Material failedMaterial = new Material(Shader.Find("Standard"));
            createdObjects.Add(safeMaterial);
            createdObjects.Add(cautionMaterial);
            createdObjects.Add(acuteMaterial);
            createdObjects.Add(failedMaterial);
            SetPrivateField(dockView, "sharedDockRenderer", dockRenderer);
            SetPrivateField(dockView, "safeMaterial", safeMaterial);
            SetPrivateField(dockView, "cautionMaterial", cautionMaterial);
            SetPrivateField(dockView, "acuteMaterial", acuteMaterial);
            SetPrivateField(dockView, "failedMaterial", failedMaterial);
            SetPrivateField(dockView, "pieceContainer", dockPieceContainer);
            SetPrivateField(dockView, "fallbackPiecePrefab", fallbackPiecePrefab);

            for (int i = 0; i < 7; i++)
            {
                Transform anchor = CreateTrackedGameObject($"Slot_{i:00}").transform;
                anchor.SetParent(root.transform, false);
                anchor.localPosition = new Vector3(i, 0f, 0f);
            }

            TargetFeedbackPresenter targetFeedback = root.AddComponent<TargetFeedbackPresenter>();
            GameObject fallbackTargetPrefab = CreateTrackedGameObject("FallbackTargetPrefab");
            SetPrivateField(targetFeedback, "gridView", boardGrid);
            SetPrivateField(targetFeedback, "contentView", boardContent);
            SetPrivateField(targetFeedback, "feedbackRoot", targetFeedbackRoot);
            SetPrivateField(targetFeedback, "fallbackTargetPrefab", fallbackTargetPrefab);

            ActionPlaybackController playbackController = root.AddComponent<ActionPlaybackController>();
            SetPrivateField(playbackController, "settings", CreateSettings());
            SetPrivateField(playbackController, "boardContent", boardContent);
            SetPrivateField(playbackController, "waterView", waterView);
            SetPrivateField(playbackController, "dockView", dockView);

            GameStateViewPresenter viewPresenter = root.AddComponent<GameStateViewPresenter>();
            SetPrivateField(viewPresenter, "boardGrid", boardGrid);
            SetPrivateField(viewPresenter, "boardContent", boardContent);
            SetPrivateField(viewPresenter, "waterView", waterView);
            SetPrivateField(viewPresenter, "dockView", dockView);
            SetPrivateField(viewPresenter, "targetFeedback", targetFeedback);
            SetPrivateField(viewPresenter, "playbackController", playbackController);

            GameObject lossScreenObject = CreateTrackedGameObject("LossScreen");
            lossScreenObject.AddComponent<UIDocument>();
            LossScreenPresenter lossScreen = lossScreenObject.AddComponent<LossScreenPresenter>();
            SetPrivateField(viewPresenter, "lossScreen", lossScreen);

            BoardInputPresenter inputPresenter = root.AddComponent<BoardInputPresenter>();
            SetPrivateField(inputPresenter, "gridView", boardGrid);
            SetPrivateField(inputPresenter, "gameStateView", viewPresenter);
            SetPrivateField(inputPresenter, "enableMouseInput", false);
            SetPrivateField(inputPresenter, "enableTouchInput", false);

            return new PlaybackHarness(
                inputPresenter,
                viewPresenter,
                playbackController,
                boardContentRoot,
                waterOverlayRoot,
                dockPieceContainer,
                lossScreen,
                dockRenderer,
                cautionMaterial,
                acuteMaterial);
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void AssertBoardContentMatchesState(Transform contentRoot, GameState state)
        {
            int expectedVisualCount = 0;
            for (int row = 0; row < state.Board.Height; row++)
            {
                for (int col = 0; col < state.Board.Width; col++)
                {
                    Tile tile = BoardHelpers.GetTile(state.Board, new TileCoord(row, col));
                    switch (tile)
                    {
                        case EmptyTile:
                        case FloodedTile:
                            break;
                        case DebrisTile debris:
                            expectedVisualCount++;
                            Assert.That(FindChildByName(contentRoot, $"Content_{row:00}_{col:00}_Debris_{debris.Type}"), Is.Not.Null);
                            break;
                        case BlockerTile blocker:
                            expectedVisualCount++;
                            Assert.That(FindChildByName(contentRoot, $"Content_{row:00}_{col:00}_Blocker_{blocker.Type}"), Is.Not.Null);
                            if (blocker.Type == BlockerType.Ice && blocker.Hidden is not null)
                            {
                                expectedVisualCount++;
                                Assert.That(FindChildByName(contentRoot, $"Content_{row:00}_{col:00}_HiddenDebris_{blocker.Hidden.Type}"), Is.Not.Null);
                            }

                            break;
                        case TargetTile target when !target.Extracted:
                            expectedVisualCount++;
                            Assert.That(FindChildByName(contentRoot, $"Content_{row:00}_{col:00}_Target_{target.TargetId.Replace(' ', '_')}"), Is.Not.Null);
                            break;
                    }
                }
            }

            Assert.That(contentRoot.childCount, Is.EqualTo(expectedVisualCount));
        }

        private static int CountDockPieces(CoreDock dock)
        {
            int occupiedSlots = 0;
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                if (dock.Slots[i].HasValue)
                {
                    occupiedSlots++;
                }
            }

            return occupiedSlots;
        }

        private static int ExpectedWaterVisualCount(GameState state)
        {
            int count = state.Water.FloodedRows;
            if (state.Water.FloodedRows > 0)
            {
                count++;
            }

            if (state.Water.FloodedRows < state.Board.Height)
            {
                count++;
            }

            return count;
        }

        private static Transform? FindChildByName(Transform parent, string exactName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == exactName)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform? FindChildContaining(Transform parent, string partialName)
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

        private static Transform FindRequiredChildContaining(Transform parent, string partialName)
        {
            Transform? child = FindChildContaining(parent, partialName);
            Assert.That(child, Is.Not.Null, $"Expected child containing '{partialName}'.");
            return child!;
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

        private static ActionPlaybackSettings CreateSettings()
        {
            ActionPlaybackSettings settings = new ActionPlaybackSettings();
            SetPrivateField(settings, "playbackEnabled", true);
            SetPrivateField(settings, "yieldBetweenSteps", true);
            SetPrivateField(settings, "removeDurationSeconds", 0f);
            SetPrivateField(settings, "breakBlockerOrRevealDurationSeconds", 0f);
            SetPrivateField(settings, "dockFeedbackDurationSeconds", 0f);
            SetPrivateField(settings, "gravityDurationSeconds", 0f);
            SetPrivateField(settings, "spawnDurationSeconds", 0f);
            SetPrivateField(settings, "targetExtractDurationSeconds", 0f);
            SetPrivateField(settings, "waterRiseDurationSeconds", 0f);
            SetPrivateField(settings, "lossFxDurationSeconds", 0f);
            return settings;
        }

        private static GameState CreatePlayableState()
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A)),
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.B),
                    new EmptyTile()));

            CoreBoard board = new CoreBoard(2, 2, rows);

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
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
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

        private static GameState CreateOverflowLossState()
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A)),
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.B),
                    new TargetTile("pup-loss", Extracted: false)));

            CoreBoard board = new CoreBoard(2, 2, rows);

            return new GameState(
                Board: board,
                Dock: new CoreDock(
                    ImmutableArray.Create<DebrisType?>(
                        DebrisType.B,
                        DebrisType.C,
                        DebrisType.D,
                        DebrisType.E,
                        DebrisType.B,
                        DebrisType.C,
                        null),
                    Size: 7),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
                Vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                Targets: ImmutableArray.Create(new TargetState("pup-loss", new TileCoord(1, 1), Extracted: false, OneClearAway: false)),
                LevelConfig: new LevelConfig(
                    ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E),
                    null,
                    0.0d,
                    2),
                RngState: new RngState(1u, 2u),
                ActionCount: 0,
                DockJamUsed: true,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }

        private static GameState CreateTargetReadabilityState(TargetReadiness readiness)
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new TargetTile("pup-read", Extracted: false)));

            return CreateStateFromRows(
                rows,
                dockSlots: EmptyDockSlots(),
                water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
                vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                targets: ImmutableArray.Create(new TargetState("pup-read", new TileCoord(0, 0), readiness)));
        }

        private static GameState CreateDockOccupancyState(int occupancy)
        {
            ImmutableArray<DebrisType?>.Builder slots = ImmutableArray.CreateBuilder<DebrisType?>(7);
            for (int i = 0; i < 7; i++)
            {
                slots.Add(i < occupancy ? DebrisType.A : null);
            }

            return CreateStateFromRows(
                ImmutableArray.Create(ImmutableArray.Create<Tile>(new EmptyTile())),
                dockSlots: slots.ToImmutable(),
                water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
                vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                targets: ImmutableArray<TargetState>.Empty);
        }

        private static GameState CreateWaterForecastState(int floodedRows, int actionsUntilRise)
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new EmptyTile()),
                ImmutableArray.Create<Tile>(new FloodedTile()));

            return CreateStateFromRows(
                rows,
                dockSlots: EmptyDockSlots(),
                water: new WaterState(floodedRows, actionsUntilRise, RiseInterval: 3),
                vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                targets: ImmutableArray<TargetState>.Empty);
        }

        private static GameState CreateVinePreviewState()
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new EmptyTile()));

            return CreateStateFromRows(
                rows,
                dockSlots: EmptyDockSlots(),
                water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
                vine: new VineState(
                    ActionsSinceLastClear: 3,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray.Create(new TileCoord(0, 0)),
                    PriorityCursor: 0,
                    PendingGrowthTile: new TileCoord(0, 0)),
                targets: ImmutableArray<TargetState>.Empty);
        }

        private static GameState CreateVineGrownState()
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(new BlockerTile(BlockerType.Vine, 1, Hidden: null)));

            return CreateStateFromRows(
                rows,
                dockSlots: EmptyDockSlots(),
                water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
                vine: new VineState(
                    ActionsSinceLastClear: 0,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray.Create(new TileCoord(0, 0)),
                    PriorityCursor: 1,
                    PendingGrowthTile: null),
                targets: ImmutableArray<TargetState>.Empty);
        }

        private static ImmutableArray<DebrisType?> EmptyDockSlots()
        {
            return ImmutableArray.Create<DebrisType?>(
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        private static GameState CreateStateFromRows(
            ImmutableArray<ImmutableArray<Tile>> rows,
            ImmutableArray<DebrisType?> dockSlots,
            WaterState water,
            VineState vine,
            ImmutableArray<TargetState> targets)
        {
            CoreBoard board = new CoreBoard(rows[0].Length, rows.Length, rows);

            return new GameState(
                Board: board,
                Dock: new CoreDock(dockSlots, Size: 7),
                Water: water,
                Vine: vine,
                Targets: targets,
                LevelConfig: new LevelConfig(
                    ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E),
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

        private readonly struct PlaybackHarness
        {
            public PlaybackHarness(
                BoardInputPresenter inputPresenter,
                GameStateViewPresenter viewPresenter,
                ActionPlaybackController playbackController,
                Transform contentRoot,
                Transform waterRoot,
                Transform dockPieceContainer,
                LossScreenPresenter lossScreen,
                MeshRenderer dockRenderer,
                Material cautionMaterial,
                Material acuteMaterial)
            {
                InputPresenter = inputPresenter;
                ViewPresenter = viewPresenter;
                PlaybackController = playbackController;
                ContentRoot = contentRoot;
                WaterRoot = waterRoot;
                DockPieceContainer = dockPieceContainer;
                LossScreen = lossScreen;
                DockRenderer = dockRenderer;
                CautionMaterial = cautionMaterial;
                AcuteMaterial = acuteMaterial;
            }

            public BoardInputPresenter InputPresenter { get; }

            public GameStateViewPresenter ViewPresenter { get; }

            public ActionPlaybackController PlaybackController { get; }

            public Transform ContentRoot { get; }

            public Transform WaterRoot { get; }

            public Transform DockPieceContainer { get; }

            public LossScreenPresenter LossScreen { get; }

            public MeshRenderer DockRenderer { get; }

            public Material CautionMaterial { get; }

            public Material AcuteMaterial { get; }
        }
    }
}
