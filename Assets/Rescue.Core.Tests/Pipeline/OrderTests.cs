using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;

namespace Rescue.Core.Tests.Pipeline
{
    public sealed class OrderTests
    {
        [Test]
        public void RunActionInvokesStepsInExactSpecOrder()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.A),
                    PipelineTestFixtures.DebrisRow(DebrisType.B, DebrisType.C, DebrisType.D),
                    PipelineTestFixtures.TargetRow("target-1", 3)),
                targets: ImmutableArray.Create(new TargetState("target-1", new TileCoord(2, 0), Extracted: false, OneClearAway: false)));

            List<string> trace = new List<string>();
            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(
                state,
                new ActionInput(new TileCoord(0, 1)),
                options: null,
                observer: step => trace.Add(step.StepName));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(trace, Is.EqualTo(Rescue.Core.Pipeline.Pipeline.GetStepOrder()));
        }

        [Test]
        public void RemoveGroupLeavesTilesEmptyAfterStep02AndBeforeStep03()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.A),
                    PipelineTestFixtures.DebrisRow(DebrisType.B, DebrisType.C, DebrisType.D),
                    PipelineTestFixtures.TargetRow("target-1", 3)),
                targets: ImmutableArray.Create(new TargetState("target-1", new TileCoord(2, 0), Extracted: false, OneClearAway: false)));

            StepTrace? step02 = null;
            StepTrace? step03 = null;

            _ = Rescue.Core.Pipeline.Pipeline.RunAction(
                state,
                new ActionInput(new TileCoord(0, 0)),
                options: null,
                observer: step =>
                {
                    if (step.StepName == "Step02_RemoveGroup")
                    {
                        step02 = step;
                    }
                    else if (step.StepName == "Step03_DamageBlockers")
                    {
                        step03 = step;
                    }
                });

            Assert.That(step02.HasValue, Is.True);
            Assert.That(step03.HasValue, Is.True);

            TileCoord[] clearedCoords =
            {
                new TileCoord(0, 0),
                new TileCoord(0, 1),
                new TileCoord(0, 2),
            };

            for (int i = 0; i < clearedCoords.Length; i++)
            {
                Assert.That(BoardHelpers.GetTile(step02!.Value.State.Board, clearedCoords[i]), Is.TypeOf<EmptyTile>());
                Assert.That(BoardHelpers.GetTile(step03!.Value.State.Board, clearedCoords[i]), Is.TypeOf<EmptyTile>());
            }
        }

        [Test]
        public void SuccessfulRunActionIncrementsActionCountExactlyOnce()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A),
                    PipelineTestFixtures.DebrisRow(DebrisType.B, DebrisType.C),
                    PipelineTestFixtures.TargetRow("target-1", 2)),
                targets: ImmutableArray.Create(new TargetState("target-1", new TileCoord(2, 0), Extracted: false, OneClearAway: false)));

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(0, 0)));

            Assert.That(result.State.ActionCount, Is.EqualTo(state.ActionCount + 1));
        }

        [Test]
        public void InvalidInputShortCircuitsPipelineAfterStep01()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B)));

            List<string> trace = new List<string>();

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(
                state,
                new ActionInput(new TileCoord(0, 0)),
                options: null,
                observer: step => trace.Add(step.StepName));

            Assert.That(trace, Is.EqualTo(new[] { "Step01_AcceptInput" }));
            Assert.That(result.Events, Is.EqualTo(ImmutableArray.Create<ActionEvent>(
                new InvalidInput(new TileCoord(0, 0), InvalidInputReason.SingleTile))));
            Assert.That(result.State.ActionCount, Is.EqualTo(state.ActionCount));
        }

        [Test]
        public void RunActionReturnsNewGameStateInstanceWithoutMutatingInput()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A),
                PipelineTestFixtures.DebrisRow(DebrisType.B, DebrisType.C),
                PipelineTestFixtures.TargetRow("target-1", 2));
            GameState state = PipelineTestFixtures.CreateState(
                board,
                targets: ImmutableArray.Create(new TargetState("target-1", new TileCoord(2, 0), Extracted: false, OneClearAway: false)));

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(0, 0)));

            Assert.That(ReferenceEquals(result.State, state), Is.False);
            Assert.That(BoardHelpers.GetTile(state.Board, new TileCoord(0, 0)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(0, 0)), Is.TypeOf<EmptyTile>());
        }

        [Test]
        public void ReturnedEventsMatchPerStepEventsInOrder()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A),
                    PipelineTestFixtures.DebrisRow(DebrisType.B, DebrisType.C),
                    PipelineTestFixtures.TargetRow("target-1", 2)),
                targets: ImmutableArray.Create(new TargetState("target-1", new TileCoord(2, 0), Extracted: false, OneClearAway: false)));

            ImmutableArray<ActionEvent>.Builder fromSteps = ImmutableArray.CreateBuilder<ActionEvent>();

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(
                state,
                new ActionInput(new TileCoord(0, 0)),
                options: null,
                observer: step =>
                {
                    for (int i = 0; i < step.Events.Length; i++)
                    {
                        fromSteps.Add(step.Events[i]);
                    }
                });

            Assert.That(result.Events, Is.EqualTo(fromSteps.ToImmutable()));
        }
    }

    internal static class PipelineTestFixtures
    {
        public static GameState CreateState(
            Board board,
            ImmutableArray<TargetState>? targets = null,
            int actionCount = 0)
        {
            ImmutableArray<TargetState> resolvedTargets = targets ?? ImmutableArray<TargetState>.Empty;
            return new GameState(
                Board: board,
                Dock: new Dock(
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
                Vine: new VineState(
                    ActionsSinceLastClear: 0,
                    GrowthThreshold: 4,
                    GrowthPriorityList: ImmutableArray<TileCoord>.Empty,
                    PriorityCursor: 0,
                    PendingGrowthTile: null),
                Targets: resolvedTargets,
                RngState: new RngState(123u, 456u),
                ActionCount: actionCount,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }

        public static Board CreateBoard(params ImmutableArray<Tile>[] rows)
        {
            return new Board(rows[0].Length, rows.Length, rows.ToImmutableArray());
        }

        public static ImmutableArray<Tile> DebrisRow(params DebrisType[] debris)
        {
            ImmutableArray<Tile>.Builder row = ImmutableArray.CreateBuilder<Tile>(debris.Length);
            for (int i = 0; i < debris.Length; i++)
            {
                row.Add(new DebrisTile(debris[i]));
            }

            return row.MoveToImmutable();
        }

        public static ImmutableArray<Tile> EmptyRow(int width)
        {
            ImmutableArray<Tile>.Builder row = ImmutableArray.CreateBuilder<Tile>(width);
            for (int i = 0; i < width; i++)
            {
                row.Add(new EmptyTile());
            }

            return row.MoveToImmutable();
        }

        public static ImmutableArray<Tile> TargetRow(string targetId, int width)
        {
            ImmutableArray<Tile>.Builder row = ImmutableArray.CreateBuilder<Tile>(width);
            row.Add(new TargetTile(targetId, Extracted: false));
            for (int i = 1; i < width; i++)
            {
                row.Add(new EmptyTile());
            }

            return row.MoveToImmutable();
        }
    }
}
