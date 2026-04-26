using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.BoardPresentation;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.Unity.BoardPresentation.Tests
{
    public sealed class BoardGridViewPresenterTests
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
        public void BoardGridViewPresenter_BuildsCorrectTileCount_6x7()
        {
            BoardGridViewPresenter presenter = CreatePresenter(out Transform boardRoot);

            presenter.RebuildGrid(CreateState(width: 6, height: 7));

            Assert.That(boardRoot.childCount, Is.EqualTo(42));
            Assert.That(CountGeneratedTiles(boardRoot), Is.EqualTo(42));
            Assert.That(CountAnchors(presenter, width: 6, height: 7), Is.EqualTo(42));
        }

        [Test]
        public void BoardGridViewPresenter_BuildsCorrectTileCount_9x9()
        {
            BoardGridViewPresenter presenter = CreatePresenter(out Transform boardRoot);

            presenter.RebuildGrid(CreateState(width: 9, height: 9));

            Assert.That(boardRoot.childCount, Is.EqualTo(81));
            Assert.That(CountGeneratedTiles(boardRoot), Is.EqualTo(81));
            Assert.That(CountAnchors(presenter, width: 9, height: 9), Is.EqualTo(81));
        }

        [Test]
        public void BoardGridViewPresenter_ClearGridRemovesTiles()
        {
            BoardGridViewPresenter presenter = CreatePresenter(out Transform boardRoot);
            presenter.RebuildGrid(CreateState(width: 6, height: 7));

            presenter.ClearGrid();

            Assert.That(boardRoot.childCount, Is.EqualTo(0));
            Assert.That(CountGeneratedTiles(boardRoot), Is.EqualTo(0));
        }

        [Test]
        public void BoardGridViewPresenter_TryGetCellAnchorReturnsExpectedCells()
        {
            BoardGridViewPresenter presenter = CreatePresenter(out _);
            presenter.RebuildGrid(CreateState(width: 3, height: 3));

            Assert.That(presenter.TryGetCellAnchor(new TileCoord(0, 0), out Transform topLeft), Is.True);
            Assert.That(topLeft, Is.Not.Null);

            Assert.That(presenter.TryGetCellAnchor(new TileCoord(1, 1), out Transform center), Is.True);
            Assert.That(center, Is.Not.Null);

            Assert.That(presenter.TryGetCellAnchor(new TileCoord(2, 2), out Transform bottomRight), Is.True);
            Assert.That(bottomRight, Is.Not.Null);

            Assert.That(presenter.TryGetCellAnchor(new TileCoord(3, 0), out _), Is.False);
            Assert.That(presenter.TryGetCellAnchor(new TileCoord(0, 3), out _), Is.False);
            Assert.That(presenter.TryGetCellAnchor(new TileCoord(-1, 0), out _), Is.False);
        }

        [Test]
        public void BoardGridViewPresenter_RebuildDoesNotDuplicateTiles()
        {
            BoardGridViewPresenter presenter = CreatePresenter(out Transform boardRoot);
            GameState state = CreateState(width: 6, height: 7);

            presenter.RebuildGrid(state);
            presenter.RebuildGrid(state);

            Assert.That(boardRoot.childCount, Is.EqualTo(42));
            Assert.That(CountGeneratedTiles(boardRoot), Is.EqualTo(42));
            Assert.That(CountAnchors(presenter, width: 6, height: 7), Is.EqualTo(42));
        }

        [Test]
        public void BoardGridViewPresenter_CentersBoardWhenEnabled()
        {
            BoardGridViewPresenter presenter = CreatePresenter(out _);
            presenter.RebuildGrid(CreateState(width: 3, height: 3));

            Assert.That(presenter.TryGetCellAnchor(new TileCoord(0, 0), out Transform topLeft), Is.True);
            Assert.That(presenter.TryGetCellAnchor(new TileCoord(2, 2), out Transform bottomRight), Is.True);
            Assert.That(presenter.TryGetCellAnchor(new TileCoord(1, 1), out Transform center), Is.True);

            Vector3 centerPosition = center.localPosition;

            Assert.That(centerPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(centerPosition.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(topLeft.localPosition.x + bottomRight.localPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(topLeft.localPosition.z + bottomRight.localPosition.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void BoardGridViewPresenter_UsesTileRegistryBeforeSceneOverrides()
        {
            BoardGridViewPresenter presenter = CreatePresenter(out Transform boardRoot);
            TileVisualRegistry registry = CreateTrackedScriptableObject<TileVisualRegistry>();
            GameObject registryPrefab = CreateTrackedObject("RegistryTilePrefab");
            registryPrefab.transform.localScale = new Vector3(2f, 3f, 4f);
            GameObject sceneOverridePrefab = CreateTrackedObject("SceneOverrideTilePrefab");
            sceneOverridePrefab.transform.localScale = new Vector3(9f, 9f, 9f);

            registry.DryTilePrefab = registryPrefab;
            SetPrivateField(presenter, "tileRegistry", registry);
            SetPrivateField(presenter, "dryTilePrefab", sceneOverridePrefab);

            presenter.RebuildGrid(CreateState(width: 1, height: 1));

            Transform spawnedTile = boardRoot.GetChild(0).GetChild(0);
            Assert.That(spawnedTile.localScale, Is.EqualTo(registryPrefab.transform.localScale));
        }

        [Test]
        public void BoardGridViewPresenter_MapsColumnsToPositiveXAndRowsToNegativeZ()
        {
            BoardGridViewPresenter presenter = CreatePresenter(out _);
            presenter.RebuildGrid(CreateState(width: 3, height: 3));

            Assert.That(presenter.TryGetCellAnchor(new TileCoord(0, 0), out Transform topLeft), Is.True);
            Assert.That(presenter.TryGetCellAnchor(new TileCoord(0, 1), out Transform topMiddle), Is.True);
            Assert.That(presenter.TryGetCellAnchor(new TileCoord(1, 0), out Transform middleLeft), Is.True);

            Assert.That(topMiddle.localPosition.x, Is.GreaterThan(topLeft.localPosition.x));
            Assert.That(middleLeft.localPosition.z, Is.LessThan(topLeft.localPosition.z));
        }

        [Test]
        public void BoardGridViewPresenter_TryGetCellWorldPositionReturnsStableAnchorPosition()
        {
            BoardGridViewPresenter presenter = CreatePresenter(out _);
            presenter.RebuildGrid(CreateState(width: 3, height: 3));

            Assert.That(presenter.TryGetCellAnchor(new TileCoord(1, 2), out Transform anchor), Is.True);
            Assert.That(presenter.TryGetCellWorldPosition(new TileCoord(1, 2), out Vector3 worldPosition), Is.True);

            Assert.That(worldPosition, Is.EqualTo(anchor.position));
            Assert.That(presenter.GetCellWorldPosition(new TileCoord(1, 2)), Is.EqualTo(anchor.position));
        }

        [Test]
        public void BoardGridViewPresenter_TryGetCellWorldPositionFailsSafelyForInvalidCoord()
        {
            BoardGridViewPresenter presenter = CreatePresenter(out _);
            presenter.transform.position = new Vector3(5f, 6f, 7f);
            presenter.RebuildGrid(CreateState(width: 3, height: 3));

            Assert.That(presenter.IsCoordVisible(new TileCoord(4, 4)), Is.False);
            Assert.That(presenter.TryGetCellWorldPosition(new TileCoord(4, 4), out Vector3 worldPosition), Is.False);
            Assert.That(worldPosition, Is.EqualTo(presenter.transform.position));
            Assert.That(presenter.GetCellWorldPosition(new TileCoord(4, 4)), Is.EqualTo(presenter.transform.position));
        }

        [Test]
        public void BoardGridViewPresenter_GetColumnEntryWorldPositionReturnsTopCellPlusOneCellHeight()
        {
            BoardGridViewPresenter presenter = CreatePresenter(out _);
            presenter.RebuildGrid(CreateState(width: 3, height: 3));

            Assert.That(presenter.TryGetCellWorldPosition(new TileCoord(0, 1), out Vector3 topCellPosition), Is.True);

            Vector3 entryPosition = presenter.GetColumnEntryWorldPosition(1);

            Assert.That(entryPosition, Is.EqualTo(topCellPosition + Vector3.up).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void BoardGridViewPresenter_TryGetRowWorldBoundsReturnsStableCenteredGeometry()
        {
            BoardGridViewPresenter presenter = CreatePresenter(out _);
            presenter.RebuildGrid(CreateState(width: 3, height: 3));

            Assert.That(presenter.TryGetRowWorldBounds(1, out BoardGridViewPresenter.RowWorldBounds bounds), Is.True);
            Assert.That(presenter.TryGetCellWorldPosition(new TileCoord(1, 0), out Vector3 leftCellPosition), Is.True);
            Assert.That(presenter.TryGetCellWorldPosition(new TileCoord(1, 2), out Vector3 rightCellPosition), Is.True);

            Assert.That(bounds.Left, Is.EqualTo(leftCellPosition));
            Assert.That(bounds.Right, Is.EqualTo(rightCellPosition));
            Assert.That(bounds.Center, Is.EqualTo((leftCellPosition + rightCellPosition) * 0.5f));
            Assert.That(bounds.Width, Is.EqualTo(3f).Within(0.001f));
            Assert.That(bounds.Depth, Is.EqualTo(1f).Within(0.001f));
        }

        private BoardGridViewPresenter CreatePresenter(out Transform boardRoot)
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

        private GameObject CreateTrackedObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private T CreateTrackedScriptableObject<T>()
            where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(instance);
            return instance;
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

        private static int CountAnchors(BoardGridViewPresenter presenter, int width, int height)
        {
            int anchorCount = 0;
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    if (presenter.TryGetCellAnchor(new TileCoord(row, col), out _))
                    {
                        anchorCount++;
                    }
                }
            }

            return anchorCount;
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

        private static GameState CreateState(int width, int height)
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
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 10, RiseInterval: 10),
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
