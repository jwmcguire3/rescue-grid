using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using UnityEngine;
using UnityEngine.TestTools;
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class ActionPlaybackControllerTests
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
        public IEnumerator ActionPlaybackController_SetsIsPlayingDuringPlaybackAndClearsAfterCompletion()
        {
            ActionPlaybackController controller = CreateController(playbackEnabled: true, yieldBetweenSteps: true);
            ActionResult result = CreateResult(actionCount: 1);
            int finalSyncCalls = 0;

            bool handled = controller.TryPlayAction(CreateState(), new ActionInput(new TileCoord(0, 0)), result, _ => finalSyncCalls++);

            Assert.That(handled, Is.True);
            Assert.That(controller.IsPlaying, Is.True);
            Assert.That(finalSyncCalls, Is.EqualTo(0));

            yield return null;

            Assert.That(controller.IsPlaying, Is.False);
            Assert.That(finalSyncCalls, Is.EqualTo(1));
        }

        [Test]
        public void ActionPlaybackController_FinalSyncIsCalledAfterImmediatePlayback()
        {
            ActionPlaybackController controller = CreateController(playbackEnabled: true, yieldBetweenSteps: false);
            ActionResult result = CreateResult(actionCount: 2);
            int finalSyncCalls = 0;
            GameState? syncedState = null;

            bool handled = controller.TryPlayAction(CreateState(), new ActionInput(new TileCoord(0, 0)), result, syncedResult =>
            {
                finalSyncCalls++;
                syncedState = syncedResult.State;
                Assert.That(controller.IsPlaying, Is.True);
            });

            Assert.That(handled, Is.True);
            Assert.That(finalSyncCalls, Is.EqualTo(1));
            Assert.That(syncedState, Is.EqualTo(result.State));
            Assert.That(controller.IsPlaying, Is.False);
            Assert.That(controller.CurrentPlan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
        }

        private ActionPlaybackController CreateController(bool playbackEnabled, bool yieldBetweenSteps)
        {
            GameObject gameObject = CreateTrackedGameObject("ActionPlaybackController");
            ActionPlaybackController controller = gameObject.AddComponent<ActionPlaybackController>();
            SetPrivateField(controller, "settings", CreateSettings(playbackEnabled, yieldBetweenSteps));
            return controller;
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static ActionPlaybackSettings CreateSettings(bool playbackEnabled, bool yieldBetweenSteps)
        {
            ActionPlaybackSettings settings = new ActionPlaybackSettings();
            SetPrivateField(settings, "playbackEnabled", playbackEnabled);
            SetPrivateField(settings, "yieldBetweenSteps", yieldBetweenSteps);
            return settings;
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

        private static ActionResult CreateResult(int actionCount)
        {
            GameState state = CreateState() with { ActionCount = actionCount };
            return new ActionResult(
                state,
                ImmutableArray.Create<ActionEvent>(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1)))),
                ActionOutcome.Ok,
                Snapshot: null);
        }

        private static GameState CreateState()
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.A)),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
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
    }
}
