using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using TMPro;
using UnityEngine;
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.Unity.Water.Tests
{
    public sealed class WaterViewPresenterTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

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
        public void WaterViewPresenter_RebuildDoesNotThrowWithMissingArt()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            gridPresenter.RebuildGrid(CreateState(width: 6, height: 7, floodedRows: 2));

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: false);

            Assert.DoesNotThrow(() => presenter.RebuildWater(CreateState(width: 6, height: 7, floodedRows: 2)));
        }

        [Test]
        public void WaterFeedbackPresenter_DoesNotThrowWithMissingArt()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState previousState = CreateState(width: 6, height: 7, floodedRows: 1, actionsUntilRise: 2);
            GameState currentState = CreateState(width: 6, height: 7, floodedRows: 2, actionsUntilRise: 1);
            gridPresenter.RebuildGrid(currentState);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: false);

            presenter.RebuildWater(previousState);
            Assert.DoesNotThrow(() => presenter.RebuildWater(currentState));
        }

        [Test]
        public void WaterViewPresenter_RebuildCreatesExpectedOverlayCount()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState state = CreateState(width: 6, height: 7, floodedRows: 2);
            gridPresenter.RebuildGrid(state);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: true);

            presenter.RebuildWater(state);

            Assert.That(GetWaterRoot(presenter).childCount, Is.EqualTo(4));
        }

        [Test]
        public void WaterViewPresenter_ClearWaterRemovesGeneratedObjects()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState state = CreateState(width: 6, height: 7, floodedRows: 2);
            gridPresenter.RebuildGrid(state);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: true);
            presenter.RebuildWater(state);

            presenter.ClearWater();

            Assert.That(GetWaterRoot(presenter).childCount, Is.EqualTo(0));
        }

        [Test]
        public void WaterViewPresenter_RebuildWater_DoesNotThrowWithoutCounterLabel()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState state = CreateState(width: 6, height: 7, floodedRows: 2);
            gridPresenter.RebuildGrid(state);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: true);

            Assert.DoesNotThrow(() => presenter.RebuildWater(state));
        }

        [Test]
        public void WaterViewPresenter_RebuildWater_UpdatesTmpCounterLabelForCountdown()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState state = CreateState(width: 6, height: 7, floodedRows: 2);
            gridPresenter.RebuildGrid(state);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: true);
            TextMeshProUGUI label = CreateCounterLabel();
            SetPrivateField(presenter, "counterLabel", label);

            presenter.RebuildWater(state);

            Assert.That(label.text, Is.EqualTo("Water: 3/5 (40%)"));
        }

        [Test]
        public void WaterViewPresenter_RebuildWater_UpdatesTmpCounterLabelForPauseUntilFirstAction()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState state = CreateState(width: 6, height: 7, floodedRows: 2, pauseUntilFirstAction: true);
            gridPresenter.RebuildGrid(state);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: true);
            TextMeshProUGUI label = CreateCounterLabel();
            SetPrivateField(presenter, "counterLabel", label);

            presenter.RebuildWater(state);

            Assert.That(label.text, Is.EqualTo("Water: paused until first action"));
        }

        [Test]
        public void WaterViewPresenter_ClearWater_ClearsTmpCounterLabel()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState state = CreateState(width: 6, height: 7, floodedRows: 2);
            gridPresenter.RebuildGrid(state);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: true);
            TextMeshProUGUI label = CreateCounterLabel();
            label.text = "Water: stale";
            SetPrivateField(presenter, "counterLabel", label);

            presenter.ClearWater();

            Assert.That(label.text, Is.Empty);
        }

        [Test]
        public void WaterViewPresenter_AnimateWaterRiseHandlesValidRiseDataSafely()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState previousState = CreateState(width: 6, height: 7, floodedRows: 1, actionsUntilRise: 1);
            GameState currentState = CreateState(width: 6, height: 7, floodedRows: 2, actionsUntilRise: 5);
            gridPresenter.RebuildGrid(currentState);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: true);
            presenter.SyncImmediate(previousState);

            Assert.DoesNotThrow(() => presenter.AnimateWaterRise(previousState, currentState, preferredFloodedRow: 5, durationSeconds: 0.15f));
            Assert.That(GetWaterRoot(presenter).childCount, Is.EqualTo(4));
        }

        [Test]
        public void WaterViewPresenter_AnimateWaterRisePromotesForecastOverlayWithoutExtraOverlayPop()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState previousState = CreateState(width: 6, height: 7, floodedRows: 1, actionsUntilRise: 1);
            GameState currentState = CreateState(width: 6, height: 7, floodedRows: 2, actionsUntilRise: 5);
            gridPresenter.RebuildGrid(currentState);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: true);
            presenter.SyncImmediate(previousState);

            Transform waterRoot = GetWaterRoot(presenter);
            Transform forecastBeforeRise = GetNamedChild(waterRoot, "ForecastRow_05");

            presenter.AnimateRiseToRow(previousState, currentState, rowIndex: 5, durationSeconds: 0.15f, forecastTransitionDurationSeconds: 0.05f);

            Assert.That(waterRoot.childCount, Is.EqualTo(4));
            Assert.That(GetNamedChild(waterRoot, "FloodedRow_05"), Is.SameAs(forecastBeforeRise));
            Assert.That(GetNamedChild(waterRoot, "ForecastRow_04"), Is.Not.Null);
        }

        [Test]
        public void WaterViewPresenter_AnimateForecastTransitionHandlesValidStateData()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState previousState = CreateState(width: 6, height: 7, floodedRows: 1, actionsUntilRise: 2);
            GameState currentState = CreateState(width: 6, height: 7, floodedRows: 1, actionsUntilRise: 1);
            gridPresenter.RebuildGrid(currentState);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: true);
            presenter.SyncImmediate(previousState);

            Assert.DoesNotThrow(() => presenter.AnimateForecastTransition(previousState, currentState, durationSeconds: 0.05f));
            Assert.That(GetNamedChild(GetWaterRoot(presenter), "ForecastRow_05"), Is.Not.Null);
        }

        [Test]
        public void WaterViewPresenter_AnimateWaterRiseIgnoresInvalidPreferredRowAndStillRepairsAtFinalSync()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState previousState = CreateState(width: 6, height: 7, floodedRows: 1, actionsUntilRise: 1);
            GameState currentState = CreateState(width: 6, height: 7, floodedRows: 2, actionsUntilRise: 5);
            gridPresenter.RebuildGrid(currentState);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: true);
            presenter.SyncImmediate(previousState);

            Assert.DoesNotThrow(() => presenter.AnimateWaterRise(previousState, currentState, preferredFloodedRow: 99, durationSeconds: 0.15f));

            Transform waterRoot = GetWaterRoot(presenter);
            Object.DestroyImmediate(GetNamedChild(waterRoot, "Waterline_05").gameObject);

            presenter.ForceSyncToState(currentState);

            Assert.That(waterRoot.childCount, Is.EqualTo(4));
            Assert.That(GetNamedChild(waterRoot, "Waterline_05"), Is.Not.Null);
        }

        [Test]
        public void WaterViewPresenter_ForceSyncToStateRepairsWaterOverlaysAfterPlayback()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState previousState = CreateState(width: 6, height: 7, floodedRows: 1, actionsUntilRise: 1);
            GameState currentState = CreateState(width: 6, height: 7, floodedRows: 2, actionsUntilRise: 5);
            gridPresenter.RebuildGrid(currentState);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: true);
            presenter.SyncImmediate(previousState);
            presenter.AnimateWaterRise(previousState, currentState, preferredFloodedRow: 5, durationSeconds: 0.15f);

            Transform waterRoot = GetWaterRoot(presenter);
            Object.DestroyImmediate(waterRoot.GetChild(0).gameObject);

            presenter.ForceSyncToState(currentState);

            Assert.That(waterRoot.childCount, Is.EqualTo(4));
        }

        [Test]
        public void WaterViewPresenter_RebuildWater_PositionsWaterlineFromSharedRowGeometry()
        {
            BoardGridViewPresenter gridPresenter = CreateGridPresenter(out _);
            GameState state = CreateState(width: 6, height: 7, floodedRows: 2);
            gridPresenter.RebuildGrid(state);

            WaterViewPresenter presenter = CreateWaterPresenter(gridPresenter, useFallbackOverlay: true);
            presenter.RebuildWater(state);

            Assert.That(gridPresenter.TryGetRowWorldBounds(5, out BoardGridViewPresenter.RowWorldBounds rowBounds), Is.True);

            Transform waterline = GetNamedChild(GetWaterRoot(presenter), "Waterline_05");
            Vector3 expectedPosition = rowBounds.Center + new Vector3(0f, 0.1f, rowBounds.Depth * 0.5f);

            Assert.That(waterline.position, Is.EqualTo(expectedPosition));
            Assert.That(waterline.localScale.x, Is.EqualTo(rowBounds.Width).Within(0.001f));
        }

        private BoardGridViewPresenter CreateGridPresenter(out Transform boardRoot)
        {
            GameObject presenterObject = CreateTrackedObject("BoardPresenter");
            BoardGridViewPresenter presenter = presenterObject.AddComponent<BoardGridViewPresenter>();

            GameObject boardRootObject = CreateTrackedObject("BoardRoot");
            boardRoot = boardRootObject.transform;
            boardRoot.SetParent(presenterObject.transform, false);

            GameObject fallbackPrefab = CreateTrackedObject("FallbackTilePrefab");

            SetPrivateField(presenter, "boardRoot", boardRoot);
            SetPrivateField(presenter, "dryTilePrefab", null);
            SetPrivateField(presenter, "fallbackTilePrefab", fallbackPrefab);

            return presenter;
        }

        private WaterViewPresenter CreateWaterPresenter(BoardGridViewPresenter gridPresenter, bool useFallbackOverlay)
        {
            GameObject presenterObject = CreateTrackedObject("WaterPresenter");
            WaterViewPresenter presenter = presenterObject.AddComponent<WaterViewPresenter>();

            GameObject waterRootObject = CreateTrackedObject("WaterRoot");
            Transform waterRoot = waterRootObject.transform;
            waterRoot.SetParent(presenterObject.transform, false);

            GameObject? overlayPrefab = useFallbackOverlay ? CreateTrackedObject("OverlayPrefab") : null;
            GameObject? waterlinePrefab = useFallbackOverlay ? CreateTrackedObject("WaterlinePrefab") : null;

            SetPrivateField(presenter, "gridView", gridPresenter);
            SetPrivateField(presenter, "waterRoot", waterRoot);
            SetPrivateField(presenter, "floodedRowOverlayPrefab", null);
            SetPrivateField(presenter, "forecastRowOverlayPrefab", null);
            SetPrivateField(presenter, "waterlinePrefab", waterlinePrefab);
            SetPrivateField(presenter, "fallbackOverlayPrefab", overlayPrefab);

            return presenter;
        }

        private Transform GetWaterRoot(WaterViewPresenter presenter)
        {
            object? value = GetPrivateField(presenter, "waterRoot");
            Transform? waterRoot = value as Transform;
            Assert.That(waterRoot, Is.Not.Null);
            if (waterRoot is null)
            {
                throw new AssertionException("Expected a water root transform.");
            }

            return waterRoot;
        }

        private static Transform GetNamedChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }
            }

            throw new AssertionException($"Expected child '{childName}'.");
        }

        private GameObject CreateTrackedObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private TextMeshProUGUI CreateCounterLabel()
        {
            GameObject labelObject = CreateTrackedObject("CounterLabel");
            return labelObject.AddComponent<TextMeshProUGUI>();
        }

        private static object? GetPrivateField(object target, string fieldName)
        {
            System.Reflection.FieldInfo? field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}'.");
            return field?.GetValue(target);
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

        private static GameState CreateState(int width, int height, int floodedRows, int actionsUntilRise = 3, bool pauseUntilFirstAction = false)
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

            CoreBoard board = new CoreBoard(width, height, rows.ToImmutable());

            return new GameState(
                Board: board,
                Dock: new CoreDock(ImmutableArray<DebrisType?>.Empty, Size: 7),
                Water: new WaterState(FloodedRows: floodedRows, ActionsUntilRise: actionsUntilRise, RiseInterval: 5, PauseUntilFirstAction: pauseUntilFirstAction),
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
    }
}
