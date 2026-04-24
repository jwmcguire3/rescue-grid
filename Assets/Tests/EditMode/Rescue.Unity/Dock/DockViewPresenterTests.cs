using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.UI;
using UnityEngine;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.Unity.UI.Tests
{
    public sealed class DockViewPresenterTests
    {
        private readonly GameObject[] _createdObjects = new GameObject[16];
        private int _createdObjectCount;

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjectCount - 1; i >= 0; i--)
            {
                if (_createdObjects[i] is not null)
                {
                    Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjectCount = 0;
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void DockVisualStateResolver_MapsOccupancyToSafe(int occupancy)
        {
            DockVisualState visualState = DockVisualStateResolver.FromOccupancy(occupancy, 7);

            Assert.That(visualState, Is.EqualTo(DockVisualState.Safe));
        }

        [Test]
        public void DockVisualStateResolver_MapsOccupancyToCaution()
        {
            DockVisualState visualState = DockVisualStateResolver.FromOccupancy(5, 7);

            Assert.That(visualState, Is.EqualTo(DockVisualState.Caution));
        }

        [Test]
        public void DockVisualStateResolver_MapsOccupancyToAcute()
        {
            DockVisualState visualState = DockVisualStateResolver.FromOccupancy(6, 7);

            Assert.That(visualState, Is.EqualTo(DockVisualState.Acute));
        }

        [Test]
        public void DockVisualStateResolver_MapsOccupancyToFailed()
        {
            DockVisualState visualState = DockVisualStateResolver.FromOccupancy(7, 7);

            Assert.That(visualState, Is.EqualTo(DockVisualState.Failed));
        }

        [Test]
        public void DockVisualStateResolver_ClampsOrHandlesInvalidOccupancy()
        {
            DockVisualState negative = DockVisualStateResolver.FromOccupancy(-3, 7);
            DockVisualState overflow = DockVisualStateResolver.FromOccupancy(12, 7);

            Assert.That(negative, Is.EqualTo(DockVisualState.Safe), "Negative occupancy is clamped to zero.");
            Assert.That(overflow, Is.EqualTo(DockVisualState.Failed), "Overflow occupancy is treated as failed deterministically.");
        }

        [Test]
        public void DockViewPresenter_RebuildDoesNotThrowWhenArtMissing()
        {
            GameObject presenterObject = CreateTrackedObject("DockPresenter");
            DockViewPresenter presenter = presenterObject.AddComponent<DockViewPresenter>();

            for (int i = 0; i < 7; i++)
            {
                CreateTrackedAnchor(presenterObject.transform, i);
            }

            Assert.DoesNotThrow(() => presenter.Rebuild(CreateState(null, null, null, null, null, null, null)));
        }

        [Test]
        public void DockViewPresenter_CreatesPieceObjectsForOccupiedSlots()
        {
            GameObject presenterObject = CreateTrackedObject("DockPresenter");
            DockViewPresenter presenter = presenterObject.AddComponent<DockViewPresenter>();
            Transform pieceContainer = new GameObject("DockPieces").transform;
            pieceContainer.SetParent(presenterObject.transform, false);
            Track(pieceContainer.gameObject);

            for (int i = 0; i < 7; i++)
            {
                CreateTrackedAnchor(presenterObject.transform, i);
            }

            GameObject fallbackPrefab = CreateTrackedObject("FallbackPiecePrefab");
            GameState state = CreateState(
                DebrisType.A,
                DebrisType.B,
                null,
                DebrisType.C,
                null,
                null,
                null);

            SetPrivateField(presenter, "pieceContainer", pieceContainer);
            SetPrivateField(presenter, "fallbackPiecePrefab", fallbackPrefab);

            presenter.Rebuild(state);

            Assert.That(pieceContainer.childCount, Is.EqualTo(3));
        }

        [Test]
        public void DockViewPresenter_ClearSlotsRemovesSpawnedPieces()
        {
            GameObject presenterObject = CreateTrackedObject("DockPresenter");
            DockViewPresenter presenter = presenterObject.AddComponent<DockViewPresenter>();
            Transform pieceContainer = new GameObject("DockPieces").transform;
            pieceContainer.SetParent(presenterObject.transform, false);
            Track(pieceContainer.gameObject);

            for (int i = 0; i < 7; i++)
            {
                CreateTrackedAnchor(presenterObject.transform, i);
            }

            GameObject fallbackPrefab = CreateTrackedObject("FallbackPiecePrefab");

            SetPrivateField(presenter, "pieceContainer", pieceContainer);
            SetPrivateField(presenter, "fallbackPiecePrefab", fallbackPrefab);

            presenter.Rebuild(CreateState(DebrisType.A, DebrisType.B, DebrisType.C, null, null, null, null));
            presenter.ClearSlots();

            Assert.That(pieceContainer.childCount, Is.EqualTo(0));
        }

        [Test]
        public void DockViewPresenter_UsesDockConfigSharedPrefabAndAnchors()
        {
            GameObject presenterObject = CreateTrackedObject("DockPresenter");
            DockViewPresenter presenter = presenterObject.AddComponent<DockViewPresenter>();
            Transform pieceContainer = new GameObject("DockPieces").transform;
            pieceContainer.SetParent(presenterObject.transform, false);
            Track(pieceContainer.gameObject);

            GameObject legacyDockVisual = CreateTrackedObject("LegacyDockVisual");
            MeshRenderer legacyRenderer = legacyDockVisual.AddComponent<MeshRenderer>();
            legacyDockVisual.transform.SetParent(presenterObject.transform, false);

            GameObject sharedDockPrefab = CreateTrackedObject("SharedDockPrefab");
            GameObject dockVisual = CreateTrackedObject("Visual");
            dockVisual.transform.SetParent(sharedDockPrefab.transform, false);
            MeshRenderer sharedRenderer = dockVisual.AddComponent<MeshRenderer>();

            for (int i = 0; i < 7; i++)
            {
                GameObject anchor = CreateTrackedObject($"Slot_{i:00}");
                anchor.transform.SetParent(sharedDockPrefab.transform, false);
                anchor.transform.localPosition = new Vector3(i * 0.5f, 0.125f, 0f);
            }

            DockVisualConfig config = ScriptableObject.CreateInstance<DockVisualConfig>();
            Material safeMaterial = new Material(Shader.Find("Standard"));

            config.SharedDockPrefab = sharedDockPrefab;
            config.SafeMaterial = safeMaterial;

            GameObject fallbackPrefab = CreateTrackedObject("FallbackPiecePrefab");

            SetPrivateField(presenter, "dockVisualConfig", config);
            SetPrivateField(presenter, "sharedDockRenderer", legacyRenderer);
            SetPrivateField(presenter, "pieceContainer", pieceContainer);
            SetPrivateField(presenter, "fallbackPiecePrefab", fallbackPrefab);

            presenter.Rebuild(CreateState(DebrisType.A, null, null, null, null, null, null));

            Transform? sharedDockInstance = presenterObject.transform.Find("SharedDockVisualInstance");
            Assert.That(sharedDockInstance, Is.Not.Null);

            MeshRenderer? instantiatedRenderer = sharedDockInstance?.GetComponentInChildren<MeshRenderer>(true);
            Assert.That(instantiatedRenderer, Is.Not.Null);

            Assert.That(legacyRenderer.enabled, Is.False);
            Assert.That(instantiatedRenderer!.sharedMaterial, Is.SameAs(safeMaterial));
            Assert.That(pieceContainer.childCount, Is.EqualTo(1));
            Assert.That(pieceContainer.GetChild(0).position.y, Is.EqualTo(0.125f).Within(0.001f));

            Object.DestroyImmediate(safeMaterial);
            Object.DestroyImmediate(config);
        }

        private GameObject CreateTrackedObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            Track(gameObject);
            return gameObject;
        }

        private Transform CreateTrackedAnchor(Transform parent, int slotIndex)
        {
            GameObject anchor = CreateTrackedObject($"Slot_{slotIndex:00}");
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = new Vector3(slotIndex, 0f, 0f);
            return anchor.transform;
        }

        private void Track(GameObject gameObject)
        {
            _createdObjects[_createdObjectCount++] = gameObject;
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

        private static GameState CreateState(
            DebrisType? slot0,
            DebrisType? slot1,
            DebrisType? slot2,
            DebrisType? slot3,
            DebrisType? slot4,
            DebrisType? slot5,
            DebrisType? slot6)
        {
            ImmutableArray<Tile> row0 = ImmutableArray.Create<Tile>(new EmptyTile());
            Board board = new Board(1, 1, ImmutableArray.Create(row0));

            return new GameState(
                Board: board,
                Dock: new CoreDock(
                    ImmutableArray.Create(slot0, slot1, slot2, slot3, slot4, slot5, slot6),
                    Size: 7),
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
