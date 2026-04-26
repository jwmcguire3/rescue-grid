using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.BoardPresentation;
using UnityEngine;
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
        public void BoardContentViewPresenter_SyncImmediateRefreshesTrackedTargetInstance()
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
            Assert.That(secondTarget, Is.Not.SameAs(firstTarget));
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

            harness.ContentPresenter.AnimateGravityMove(new GravitySettled(
                ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0)))));

            Assert.That(FindChildByName(harness.ContentRoot, "Content_01_00_Debris_A"), Is.Not.Null);
            Assert.That(FindChildByName(harness.ContentRoot, "Content_00_00_Debris_A"), Is.Null);
            Assert.That(harness.ContentRoot.childCount, Is.EqualTo(1));
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
        }

        [Test]
        public void BoardContentViewPresenter_AnimationApisFailSoftWhenVisualsAreMissing()
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
            Assert.DoesNotThrow(() => harness.ContentPresenter.AnimateTargetExtract(
                new TargetExtracted("missing-target", new TileCoord(0, 0))));
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

        private static GameState CreateState(ImmutableArray<ImmutableArray<Tile>> rows)
        {
            int height = rows.Length;
            int width = height > 0 ? rows[0].Length : 0;
            CoreBoard board = new CoreBoard(width, height, rows);

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
