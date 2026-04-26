using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.Unity.Input.Tests
{
    public sealed class BoardInputPresenterTests
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
        public void BoardInputPresenter_MapsCellObjectToTileCoord()
        {
            BoardGridViewPresenter gridView = CreateGridPresenter(out _);
            gridView.RebuildGrid(CreateEmptyState(width: 3, height: 2));

            Assert.That(gridView.TryGetCellAnchor(new TileCoord(1, 2), out Transform anchor), Is.True);

            BoardInputPresenter presenter = CreateInputPresenter(gridView, gameStateView: null);

            Assert.That(presenter.TryGetTileCoordFromObject(anchor.GetChild(0).gameObject, out TileCoord coord), Is.True);
            Assert.That(coord, Is.EqualTo(new TileCoord(1, 2)));
        }

        [Test]
        public void BoardInputPresenter_InvalidCoordDoesNotRunAction()
        {
            BoardInputPresenter presenter = CreateInputPresenter(gridView: null, gameStateView: null);
            GameState initialState = CreateValidPairState();
            presenter.SetCurrentState(initialState, refreshView: false);

            bool handled = presenter.TryRunActionAt(new TileCoord(-1, 0));

            Assert.That(handled, Is.False);
            Assert.That(presenter.CurrentState, Is.EqualTo(initialState));
            Assert.That(presenter.CurrentState!.ActionCount, Is.EqualTo(initialState.ActionCount));
        }

        [Test]
        public void BoardInputPresenter_ValidActionUpdatesCurrentState()
        {
            GameStateViewPresenter viewPresenter = CreateViewPresenter();
            BoardInputPresenter presenter = CreateInputPresenter(gridView: null, viewPresenter);
            GameState initialState = CreateValidPairState();
            TileCoord tappedCoord = new TileCoord(0, 0);
            ActionInput input = new ActionInput(tappedCoord);
            ActionResult expectedResult = Pipeline.RunAction(initialState, input);
            presenter.SetCurrentState(initialState);

            bool handled = presenter.TryRunActionAt(tappedCoord);

            Assert.That(handled, Is.True);
            Assert.That(presenter.CurrentState, Is.Not.Null);
            Assert.That(presenter.CurrentState, Is.Not.EqualTo(initialState));
            Assert.That(presenter.CurrentState!.ActionCount, Is.EqualTo(expectedResult.State.ActionCount));
            Assert.That(presenter.CurrentState.Board.Width, Is.EqualTo(expectedResult.State.Board.Width));
            Assert.That(presenter.CurrentState.Board.Height, Is.EqualTo(expectedResult.State.Board.Height));
            Assert.That(presenter.CurrentState.Dock.Slots.Length, Is.EqualTo(expectedResult.State.Dock.Slots.Length));
            Assert.That(presenter.CurrentState.Frozen, Is.EqualTo(expectedResult.State.Frozen));
            Assert.That(viewPresenter.CurrentState, Is.EqualTo(presenter.CurrentState));
            Assert.That(viewPresenter.CurrentPlaybackPlan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
        }

        [Test]
        public void BoardInputPresenter_InvalidSingleTileDoesNotIncrementActionCount()
        {
            BoardInputPresenter presenter = CreateInputPresenter(gridView: null, gameStateView: null);
            GameState initialState = CreateSingleTileState();
            presenter.SetCurrentState(initialState, refreshView: false);

            bool handled = presenter.TryRunActionAt(new TileCoord(0, 0));

            Assert.That(handled, Is.True);
            Assert.That(presenter.CurrentState, Is.EqualTo(initialState));
            Assert.That(presenter.CurrentState!.ActionCount, Is.EqualTo(initialState.ActionCount));
        }

        [UnityTest]
        public System.Collections.IEnumerator BoardInputPresenter_IgnoresInputWhilePlaybackIsActive()
        {
            GameStateViewPresenter viewPresenter = CreateViewPresenterWithPlaybackController(yieldBetweenSteps: true);
            BoardInputPresenter presenter = CreateInputPresenter(gridView: null, viewPresenter);
            GameState initialState = CreateValidPairState();
            GameState resultState = initialState with { ActionCount = initialState.ActionCount + 1 };
            ActionResult playbackResult = new ActionResult(
                resultState,
                ImmutableArray.Create<ActionEvent>(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))),
                ActionOutcome.Ok,
                Snapshot: null);

            presenter.SetCurrentState(initialState);
            viewPresenter.ApplyActionResult(initialState, new ActionInput(new TileCoord(0, 0)), playbackResult);

            Assert.That(viewPresenter.IsPlaybackActive, Is.True);

            bool handled = presenter.TryRunActionAt(new TileCoord(0, 0));

            Assert.That(handled, Is.False);
            Assert.That(presenter.CurrentState, Is.EqualTo(initialState));

            yield return null;
            Assert.That(viewPresenter.CurrentState, Is.EqualTo(resultState));
        }

        private BoardInputPresenter CreateInputPresenter(BoardGridViewPresenter? gridView, GameStateViewPresenter? gameStateView)
        {
            GameObject presenterObject = CreateTrackedGameObject("BoardInputPresenter");
            BoardInputPresenter presenter = presenterObject.AddComponent<BoardInputPresenter>();
            SetPrivateField(presenter, "gridView", gridView);
            SetPrivateField(presenter, "gameStateView", gameStateView);
            return presenter;
        }

        private BoardGridViewPresenter CreateGridPresenter(out Transform boardRoot)
        {
            GameObject presenterObject = CreateTrackedGameObject("BoardGridPresenter");
            BoardGridViewPresenter presenter = presenterObject.AddComponent<BoardGridViewPresenter>();
            boardRoot = CreateTrackedGameObject("BoardRoot").transform;
            boardRoot.SetParent(presenterObject.transform, false);

            GameObject fallbackTilePrefab = CreateTrackedGameObject("FallbackTilePrefab");
            SetPrivateField(presenter, "boardRoot", boardRoot);
            SetPrivateField(presenter, "dryTilePrefab", null);
            SetPrivateField(presenter, "fallbackTilePrefab", fallbackTilePrefab);
            return presenter;
        }

        private GameStateViewPresenter CreateViewPresenter()
        {
            GameObject presenterObject = CreateTrackedGameObject("GameStateViewPresenter");
            return presenterObject.AddComponent<GameStateViewPresenter>();
        }

        private GameStateViewPresenter CreateViewPresenterWithPlaybackController(bool yieldBetweenSteps)
        {
            GameObject presenterObject = CreateTrackedGameObject("GameStateViewPresenter");
            GameStateViewPresenter presenter = presenterObject.AddComponent<GameStateViewPresenter>();
            ActionPlaybackController playbackController = presenterObject.AddComponent<ActionPlaybackController>();
            SetPrivateField(playbackController, "settings", CreateSettings(playbackEnabled: true, yieldBetweenSteps));
            SetPrivateField(presenter, "playbackController", playbackController);
            return presenter;
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

        private static ActionPlaybackSettings CreateSettings(bool playbackEnabled, bool yieldBetweenSteps)
        {
            ActionPlaybackSettings settings = new ActionPlaybackSettings();
            SetPrivateField(settings, "playbackEnabled", playbackEnabled);
            SetPrivateField(settings, "yieldBetweenSteps", yieldBetweenSteps);
            return settings;
        }

        private static GameState CreateEmptyState(int width, int height)
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

            return CreateState(rows.ToImmutable());
        }

        private static GameState CreateValidPairState()
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A)),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile()));

            return CreateState(rows);
        }

        private static GameState CreateSingleTileState()
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.B)),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile()));

            return CreateState(rows);
        }

        private static GameState CreateState(ImmutableArray<ImmutableArray<Tile>> rows)
        {
            CoreBoard board = new CoreBoard(rows[0].Length, rows.Length, rows);

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
    }
}
