using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using UnityEngine;

namespace Rescue.Unity.Targets.Tests
{
    public sealed class TargetFeedbackPresenterTests
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
        public void TargetFeedbackPresenter_DoesNotThrowWithMissingArt()
        {
            PresenterHarness harness = CreateHarness();
            GameState baseState = CreateState(extracted: false, oneClearAway: false);
            GameState nearRescueState = CreateState(extracted: false, oneClearAway: true);
            GameState extractedState = CreateState(extracted: true, oneClearAway: true);

            harness.GridPresenter.RebuildGrid(baseState);
            harness.ContentPresenter.RebuildContent(baseState);

            harness.GridPresenter.RebuildGrid(nearRescueState);
            harness.ContentPresenter.RebuildContent(nearRescueState);
            Assert.DoesNotThrow(() => harness.FeedbackPresenter.Apply(baseState, nearRescueState));

            harness.GridPresenter.RebuildGrid(extractedState);
            harness.ContentPresenter.RebuildContent(extractedState);
            Assert.DoesNotThrow(() => harness.FeedbackPresenter.Apply(nearRescueState, extractedState));
        }

        private PresenterHarness CreateHarness()
        {
            GameObject root = CreateTrackedGameObject("TargetFeedbackPresenterRoot");

            BoardGridViewPresenter gridPresenter = root.AddComponent<BoardGridViewPresenter>();
            Transform boardRoot = CreateTrackedGameObject("BoardRoot").transform;
            boardRoot.SetParent(root.transform, false);
            GameObject fallbackTilePrefab = CreateTrackedGameObject("FallbackTilePrefab");
            SetPrivateField(gridPresenter, "boardRoot", boardRoot);
            SetPrivateField(gridPresenter, "dryTilePrefab", null);
            SetPrivateField(gridPresenter, "fallbackTilePrefab", fallbackTilePrefab);

            BoardContentViewPresenter contentPresenter = root.AddComponent<BoardContentViewPresenter>();
            Transform contentRoot = CreateTrackedGameObject("ContentRoot").transform;
            contentRoot.SetParent(root.transform, false);
            GameObject fallbackContentPrefab = CreateTrackedGameObject("FallbackContentPrefab");
            SetPrivateField(contentPresenter, "gridView", gridPresenter);
            SetPrivateField(contentPresenter, "contentRoot", contentRoot);
            SetPrivateField(contentPresenter, "fallbackContentPrefab", fallbackContentPrefab);

            TargetFeedbackPresenter feedbackPresenter = root.AddComponent<TargetFeedbackPresenter>();
            Transform feedbackRoot = CreateTrackedGameObject("FeedbackRoot").transform;
            feedbackRoot.SetParent(root.transform, false);
            GameObject placeholderPrefab = CreateTrackedGameObject("PlaceholderTargetPrefab");
            GameObject extractionPrefab = CreateTrackedGameObject("ExtractionFxPrefab");
            GameObject nearRescueFxPrefab = CreateTrackedGameObject("NearRescueFxPrefab");
            SetPrivateField(feedbackPresenter, "gridView", gridPresenter);
            SetPrivateField(feedbackPresenter, "contentView", contentPresenter);
            SetPrivateField(feedbackPresenter, "feedbackRoot", feedbackRoot);
            SetPrivateField(feedbackPresenter, "fallbackTargetPrefab", placeholderPrefab);
            SetPrivateField(feedbackPresenter, "extractionFxPrefab", extractionPrefab);
            SetPrivateField(feedbackPresenter, "nearRescueFxPrefab", nearRescueFxPrefab);

            return new PresenterHarness(gridPresenter, contentPresenter, feedbackPresenter);
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
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

        private static GameState CreateState(bool extracted, bool oneClearAway)
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile(),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile(),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new TargetTile("pup-1", extracted),
                    new EmptyTile()));

            return new GameState(
                Board: new Board(3, 3, rows),
                Dock: new Dock(ImmutableArray<DebrisType?>.Empty, Size: 7),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
                Vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                Targets: ImmutableArray.Create(new TargetState("pup-1", new TileCoord(2, 1), extracted, oneClearAway)),
                LevelConfig: new LevelConfig(
                    ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C),
                    BaseDistribution: null,
                    AssistanceChance: 0.0d,
                    ConsecutiveEmergencyCap: 2),
                RngState: new RngState(1u, 2u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: extracted ? ImmutableArray.Create("pup-1") : ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }

        private readonly struct PresenterHarness
        {
            public PresenterHarness(
                BoardGridViewPresenter gridPresenter,
                BoardContentViewPresenter contentPresenter,
                TargetFeedbackPresenter feedbackPresenter)
            {
                GridPresenter = gridPresenter;
                ContentPresenter = contentPresenter;
                FeedbackPresenter = feedbackPresenter;
            }

            public BoardGridViewPresenter GridPresenter { get; }

            public BoardContentViewPresenter ContentPresenter { get; }

            public TargetFeedbackPresenter FeedbackPresenter { get; }
        }
    }
}
