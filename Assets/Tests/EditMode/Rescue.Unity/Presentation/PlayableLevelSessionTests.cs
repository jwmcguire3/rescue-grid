using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.Input;
using UnityEngine;
using UnityEngine.UIElements;
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class PlayableLevelSessionTests
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
        public void PlayableLevelSession_SyncTerminalInputLockFollowsVisibleTerminalScreens()
        {
            PlayableLevelSession session = CreateSession(out BoardInputPresenter boardInput, out VictoryScreenPresenter victoryScreen, out LossScreenPresenter lossScreen);

            victoryScreen.Show();
            InvokePrivate(session, "SyncTerminalInputLock");

            Assert.That(session.IsTerminalInputLocked, Is.True);
            Assert.That(boardInput.IsTerminalInputLocked, Is.True);

            victoryScreen.Hide();
            lossScreen.Hide();
            InvokePrivate(session, "SyncTerminalInputLock");

            Assert.That(session.IsTerminalInputLocked, Is.False);
            Assert.That(boardInput.IsTerminalInputLocked, Is.False);
        }

        [Test]
        public void PlayableLevelSession_TryRunActionRejectsWhileTerminalScreenIsVisible()
        {
            PlayableLevelSession session = CreateSession(out BoardInputPresenter boardInput, out VictoryScreenPresenter victoryScreen, out _);
            GameState initialState = CreateValidPairState();
            SetCurrentState(session, initialState);
            boardInput.SetCurrentState(initialState, refreshView: false);

            victoryScreen.Show();

            bool handled = session.TryRunAction(new TileCoord(0, 0));

            Assert.That(handled, Is.False);
            Assert.That(session.IsTerminalInputLocked, Is.True);
            Assert.That(boardInput.IsTerminalInputLocked, Is.True);
            Assert.That(session.CurrentState, Is.EqualTo(initialState));
        }

        private PlayableLevelSession CreateSession(
            out BoardInputPresenter boardInput,
            out VictoryScreenPresenter victoryScreen,
            out LossScreenPresenter lossScreen)
        {
            GameObject sessionObject = CreateTrackedGameObject("PlayableLevelSession");
            PlayableLevelSession session = sessionObject.AddComponent<PlayableLevelSession>();

            GameObject viewObject = CreateTrackedGameObject("GameStateViewPresenter");
            GameStateViewPresenter gameStateView = viewObject.AddComponent<GameStateViewPresenter>();

            GameObject inputObject = CreateTrackedGameObject("BoardInputPresenter");
            boardInput = inputObject.AddComponent<BoardInputPresenter>();

            GameObject victoryObject = CreateTrackedGameObject("VictoryScreen");
            victoryObject.AddComponent<UIDocument>();
            victoryScreen = victoryObject.AddComponent<VictoryScreenPresenter>();

            GameObject lossObject = CreateTrackedGameObject("LossScreen");
            lossObject.AddComponent<UIDocument>();
            lossScreen = lossObject.AddComponent<LossScreenPresenter>();

            GameObject tutorialObject = CreateTrackedGameObject("TutorialCards");
            TutorialCardPresenter tutorialCards = tutorialObject.AddComponent<TutorialCardPresenter>();

            SetPrivateField(boardInput, "gameStateView", gameStateView);
            SetPrivateField(session, "gameStateView", gameStateView);
            SetPrivateField(session, "boardInput", boardInput);
            SetPrivateField(session, "victoryScreen", victoryScreen);
            SetPrivateField(session, "lossScreen", lossScreen);
            SetPrivateField(session, "tutorialCards", tutorialCards);
            return session;
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void SetCurrentState(PlayableLevelSession session, GameState state)
        {
            GameStateViewPresenter gameStateView = (GameStateViewPresenter)GetPrivateField(session, "gameStateView")!;
            SetPrivateField(gameStateView, "<CurrentState>k__BackingField", state);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            System.Reflection.MethodInfo? method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, $"Expected private method '{methodName}'.");
            method!.Invoke(target, null);
        }

        private static object? GetPrivateField(object target, string fieldName)
        {
            System.Reflection.FieldInfo? field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}'.");
            return field!.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object? value)
        {
            System.Reflection.FieldInfo? field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}'.");
            field!.SetValue(target, value);
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
