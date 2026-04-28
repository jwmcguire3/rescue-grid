using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.Presentation;
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
        public void DockFeedbackPresenter_SelectsCautionFeedbackAtFive()
        {
            GameObject presenterObject = CreateTrackedObject("DockFeedbackPresenter");
            DockFeedbackPresenter presenter = presenterObject.AddComponent<DockFeedbackPresenter>();

            DockFeedbackType feedbackType = presenter.SelectFeedbackType(5, 7);

            Assert.That(feedbackType, Is.EqualTo(DockFeedbackType.Caution));
        }

        [Test]
        public void DockFeedbackPresenter_SelectsAcuteFeedbackAtSix()
        {
            GameObject presenterObject = CreateTrackedObject("DockFeedbackPresenter");
            DockFeedbackPresenter presenter = presenterObject.AddComponent<DockFeedbackPresenter>();

            DockFeedbackType feedbackType = presenter.SelectFeedbackType(6, 7);

            Assert.That(feedbackType, Is.EqualTo(DockFeedbackType.Acute));
        }

        [Test]
        public void DockFeedbackPresenter_SelectsFailedFeedbackAtSeven()
        {
            GameObject presenterObject = CreateTrackedObject("DockFeedbackPresenter");
            DockFeedbackPresenter presenter = presenterObject.AddComponent<DockFeedbackPresenter>();

            DockFeedbackType feedbackType = presenter.SelectFeedbackType(7, 7);

            Assert.That(feedbackType, Is.EqualTo(DockFeedbackType.Failed));
        }

        [Test]
        public void DockFeedbackPresenter_DoesNotThrowWhenNoFxAssigned()
        {
            GameObject presenterObject = CreateTrackedObject("DockFeedbackPresenter");
            DockFeedbackPresenter presenter = presenterObject.AddComponent<DockFeedbackPresenter>();

            Assert.DoesNotThrow(() => presenter.PlayInsertFeedback());
            Assert.DoesNotThrow(() => presenter.PlayCautionFeedback());
            Assert.DoesNotThrow(() => presenter.PlayAcuteFeedback());
            Assert.DoesNotThrow(() => presenter.PlayFailedFeedback());
            Assert.DoesNotThrow(() => presenter.PlayTripleClearFeedback());
            Assert.DoesNotThrow(() => presenter.SyncToState(5, 7));
            Assert.DoesNotThrow(() => presenter.SetFeedbackTarget(null));
        }

        [Test]
        public void DockFeedbackPresenter_ForceSyncRepairsScaleAndPositionAfterFailedHold()
        {
            GameObject presenterObject = CreateTrackedObject("DockFeedbackPresenter");
            DockFeedbackPresenter presenter = presenterObject.AddComponent<DockFeedbackPresenter>();
            GameObject targetObject = CreateTrackedObject("DockFeedbackTarget");
            targetObject.transform.localScale = new Vector3(2f, 1f, 1f);
            targetObject.transform.localPosition = new Vector3(3f, 0f, 0f);

            presenter.SetFeedbackTarget(targetObject.transform);
            presenter.ForceSyncToState(7, 7);

            Assert.That(targetObject.transform.localScale.x, Is.GreaterThan(2f));

            targetObject.transform.localPosition = new Vector3(99f, 0f, 0f);
            presenter.ForceSyncToState(2, 7);

            Assert.That(targetObject.transform.localScale, Is.EqualTo(new Vector3(2f, 1f, 1f)));
            Assert.That(targetObject.transform.localPosition, Is.EqualTo(new Vector3(3f, 0f, 0f)));
        }

        [Test]
        public void DockFeedbackPresenter_ApplyPlaybackSettingsOverridesFeedbackDurations()
        {
            GameObject presenterObject = CreateTrackedObject("DockFeedbackPresenter");
            DockFeedbackPresenter presenter = presenterObject.AddComponent<DockFeedbackPresenter>();
            ActionPlaybackSettings settings = new ActionPlaybackSettings();
            SetPrivateField(settings, "dockInsertFeedbackDurationSeconds", 0.09f);
            SetPrivateField(settings, "dockClearFeedbackDurationSeconds", 0.07f);
            SetPrivateField(settings, "dockWarningCautionDurationSeconds", 0.42f);
            SetPrivateField(settings, "dockWarningAcuteDurationSeconds", 0.33f);
            SetPrivateField(settings, "dockJamFeedbackDurationSeconds", 0.61f);

            presenter.ApplyPlaybackSettings(settings);

            Assert.That(presenter.InsertDuration, Is.EqualTo(0.09f));
            Assert.That(presenter.ClearDuration, Is.EqualTo(0.07f));
            Assert.That(presenter.CautionPulseDuration, Is.EqualTo(0.42f));
            Assert.That(presenter.AcuteShakeDuration, Is.EqualTo(0.33f));
            Assert.That(presenter.FailedPulseDuration, Is.EqualTo(0.61f));
        }

        [Test]
        public void DockViewPresenter_DockFeedbackMethodsTolerateValidEventData()
        {
            GameObject presenterObject = CreateTrackedObject("DockPresenter");
            DockViewPresenter presenter = presenterObject.AddComponent<DockViewPresenter>();

            for (int i = 0; i < 7; i++)
            {
                CreateTrackedAnchor(presenterObject.transform, i);
            }

            Assert.DoesNotThrow(() => presenter.PlayInsertFeedback(new DockInserted(ImmutableArray.Create(DebrisType.A), OccupancyAfterInsert: 1, OverflowCount: 0)));
            Assert.DoesNotThrow(() => presenter.PlayClearFeedback(new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 0)));
            Assert.DoesNotThrow(() => presenter.PlayWarningFeedback(new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution)));
            Assert.DoesNotThrow(() => presenter.PlayJamFeedback(new DockJamTriggered(OverflowCount: 1)));
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
        public void DockViewPresenter_RebuildTracksSlotTypesAndObjectsByIndex()
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

            presenter.Rebuild(CreateState(DebrisType.A, null, DebrisType.C, null, null, null, null));

            Assert.That(presenter.GetTrackedSlotType(0), Is.EqualTo(DebrisType.A));
            Assert.That(presenter.GetTrackedSlotType(1), Is.Null);
            Assert.That(presenter.GetTrackedSlotType(2), Is.EqualTo(DebrisType.C));
            Assert.That(presenter.GetTrackedSlotObject(0), Is.Not.Null);
            Assert.That(presenter.GetTrackedSlotObject(1), Is.Null);
            Assert.That(presenter.GetTrackedSlotObject(2), Is.Not.Null);
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
        public void DockViewPresenter_PlayInsertFeedbackTracksExpectedInsertedSlot()
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

            presenter.Rebuild(CreateState(DebrisType.A, DebrisType.A, DebrisType.B, DebrisType.B, null, null, null));

            GameObject? originalSlotThree = presenter.GetTrackedSlotObject(3);

            presenter.PlayInsertFeedback(new DockInserted(ImmutableArray.Create(DebrisType.C), OccupancyAfterInsert: 5, OverflowCount: 0));

            Assert.That(presenter.GetTrackedSlotType(4), Is.EqualTo(DebrisType.C));
            Assert.That(presenter.GetTrackedSlotObject(4), Is.Not.Null);
            Assert.That(presenter.GetTrackedSlotObject(3), Is.SameAs(originalSlotThree));
            Assert.That(pieceContainer.childCount, Is.EqualTo(5));
        }

        [Test]
        public void DockViewPresenter_PlayClearFeedbackRemovesTriplesAndCompactsRemainingPieces()
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

            presenter.Rebuild(CreateState(DebrisType.A, DebrisType.A, DebrisType.A, DebrisType.B, DebrisType.B, null, null));

            GameObject? originalSlotThree = presenter.GetTrackedSlotObject(3);
            GameObject? originalSlotFour = presenter.GetTrackedSlotObject(4);

            presenter.PlayClearFeedback(new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 2));

            Assert.That(presenter.GetTrackedSlotType(0), Is.EqualTo(DebrisType.B));
            Assert.That(presenter.GetTrackedSlotType(1), Is.EqualTo(DebrisType.B));
            Assert.That(presenter.GetTrackedSlotType(2), Is.Null);
            Assert.That(presenter.GetTrackedSlotObject(0), Is.SameAs(originalSlotThree));
            Assert.That(presenter.GetTrackedSlotObject(1), Is.SameAs(originalSlotFour));
            Assert.That(pieceContainer.childCount, Is.EqualTo(2));
        }

        [Test]
        public void DockViewPresenter_WarningAndJamFeedbackDoNotCorruptTrackedSlots()
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

            presenter.Rebuild(CreateState(DebrisType.A, DebrisType.B, null, null, null, null, null));

            GameObject? slotZeroBefore = presenter.GetTrackedSlotObject(0);
            GameObject? slotOneBefore = presenter.GetTrackedSlotObject(1);

            presenter.PlayWarningFeedback(new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution));
            presenter.PlayJamFeedback(new DockJamTriggered(OverflowCount: 1));

            Assert.That(presenter.GetTrackedSlotType(0), Is.EqualTo(DebrisType.A));
            Assert.That(presenter.GetTrackedSlotType(1), Is.EqualTo(DebrisType.B));
            Assert.That(presenter.GetTrackedSlotObject(0), Is.SameAs(slotZeroBefore));
            Assert.That(presenter.GetTrackedSlotObject(1), Is.SameAs(slotOneBefore));
            Assert.That(pieceContainer.childCount, Is.EqualTo(2));
        }

        [Test]
        public void DockViewPresenter_FeedbackMethodsFailSoftWhenVisualReferencesAreMissing()
        {
            GameObject presenterObject = CreateTrackedObject("DockPresenter");
            DockViewPresenter presenter = presenterObject.AddComponent<DockViewPresenter>();

            Assert.DoesNotThrow(() => presenter.PlayInsertFeedback(new DockInserted(ImmutableArray.Create(DebrisType.A), OccupancyAfterInsert: 1, OverflowCount: 0)));
            Assert.DoesNotThrow(() => presenter.PlayClearFeedback(new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 0)));
            Assert.DoesNotThrow(() => presenter.PlayWarningFeedback(new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Acute)));
            Assert.DoesNotThrow(() => presenter.PlayJamFeedback(new DockJamTriggered(OverflowCount: 1)));
            Assert.DoesNotThrow(() => presenter.PlayOverflowFeedback(new DockOverflowTriggered(OverflowCount: 1)));
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
            Transform? instantiatedAnchor = sharedDockInstance?.Find("Slot_00");
            Assert.That(instantiatedRenderer, Is.Not.Null);
            Assert.That(instantiatedAnchor, Is.Not.Null);

            Assert.That(legacyRenderer.enabled, Is.False);
            Assert.That(sharedDockInstance!.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(sharedDockInstance.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(sharedDockInstance.localScale, Is.EqualTo(Vector3.one));
            Assert.That(instantiatedRenderer!.sharedMaterial, Is.SameAs(safeMaterial));
            Assert.That(pieceContainer.childCount, Is.EqualTo(1));
            Assert.That(pieceContainer.GetChild(0).position, Is.EqualTo(instantiatedAnchor!.position));

            Object.DestroyImmediate(safeMaterial);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void DockViewPresenter_ForceSyncToStateRepairsDockPieces()
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

            presenter.Rebuild(CreateState(DebrisType.A, null, null, null, null, null, null));
            presenter.PlayInsertFeedback(new DockInserted(ImmutableArray.Create(DebrisType.B, DebrisType.C), OccupancyAfterInsert: 3, OverflowCount: 0));
            presenter.ForceSyncToState(CreateState(DebrisType.A, DebrisType.B, DebrisType.C, null, null, null, null));

            Assert.That(pieceContainer.childCount, Is.EqualTo(3));
            Assert.That(presenter.GetTrackedSlotType(0), Is.EqualTo(DebrisType.A));
            Assert.That(presenter.GetTrackedSlotType(1), Is.EqualTo(DebrisType.B));
            Assert.That(presenter.GetTrackedSlotType(2), Is.EqualTo(DebrisType.C));
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
