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

            Assert.That(trace, Is.EqualTo(Rescue.Core.Pipeline.Pipeline.GetNonShortCircuitedStepOrder()));
            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Ok));
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
            AssertInvalidInputEvent(
                result.Events,
                new TileCoord(0, 0),
                InvalidInputReason.SingleTile);
            Assert.That(result.State, Is.EqualTo(state));
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
            Assert.That(result.State, Is.Not.EqualTo(state));
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

            AssertActionEventSequenceEqual(fromSteps.ToImmutable(), result.Events);
        }

        [Test]
        public void WinShortCircuitsSteps11And12AndSkipsLossCheck()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new DebrisTile(DebrisType.B)),
                    PipelineTestFixtures.Row(new EmptyTile(), new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.D)),
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.A)),
                targets: ImmutableArray.Create(new TargetState("target-1", new TileCoord(0, 0), Extracted: true, OneClearAway: false)))
                with
                {
                    ExtractedTargetOrder = ImmutableArray.Create("target-1"),
                    Dock = new Dock(
                        ImmutableArray.Create<DebrisType?>(
                            DebrisType.B,
                            DebrisType.C,
                            DebrisType.D,
                            DebrisType.E,
                            DebrisType.B,
                            null,
                            null),
                        Size: 7),
                };

            List<string> trace = new List<string>();
            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(
                state,
                new ActionInput(new TileCoord(2, 0)),
                options: null,
                observer: step => trace.Add(step.StepName));

            Assert.That(trace, Is.EqualTo(new[]
            {
                "Step01_AcceptInput",
                "Step02_RemoveGroup",
                "Step03_DamageBlockers",
                "Step04_ResolveBreaks",
                "Step05_UpdateTargets",
                "Step06_InsertDock",
                "Step07_ClearDock",
                "Step08_Extract",
                "Step09_CheckWin",
            }));
            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(result.State.Frozen, Is.True);
            Assert.That(result.Events, Has.Some.TypeOf<Won>());
            Assert.That(result.Events, Has.None.TypeOf<Lost>());
        }

        [Test]
        public void ClearingFinalNeighborLatchesExtractionBeforeDockInsertion()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new DebrisTile(DebrisType.B), new DebrisTile(DebrisType.C)),
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new DebrisTile(DebrisType.D)),
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    PipelineTestFixtures.Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new TargetTile("pup", Extracted: false))),
                targets: ImmutableArray.Create(new TargetState("pup", new TileCoord(3, 3), Extracted: false, OneClearAway: true)))
                with
                {
                    LevelConfig = PipelineTestFixtures.CreateLevelConfig(0.0d, null, DebrisType.A),
                };

            StepTrace? updateTargets = null;
            StepTrace? insertDock = null;

            _ = Rescue.Core.Pipeline.Pipeline.RunAction(
                state,
                new ActionInput(new TileCoord(3, 0)),
                options: null,
                observer: step =>
                {
                    if (step.StepName == "Step05_UpdateTargets")
                    {
                        updateTargets = step;
                    }
                    else if (step.StepName == "Step06_InsertDock")
                    {
                        insertDock = step;
                    }
                });

            Assert.That(updateTargets.HasValue, Is.True);
            Assert.That(insertDock.HasValue, Is.True);
            Assert.That(updateTargets!.Value.State.Targets[0].ExtractableLatched, Is.True);
            Assert.That(updateTargets.Value.Context.LatchedTargetIdsThisAction, Is.EqualTo(new[] { "pup" }).AsCollection);
            Assert.That(DockHelpers.Occupancy(updateTargets.Value.State.Dock), Is.EqualTo(0));
            Assert.That(insertDock!.Value.State.Targets[0].ExtractableLatched, Is.True);
            Assert.That(DockHelpers.Occupancy(insertDock.Value.State.Dock), Is.EqualTo(3));
        }

        private static void AssertInvalidInputEvent(
            ImmutableArray<ActionEvent> events,
            TileCoord tappedCoord,
            InvalidInputReason expectedReason)
        {
            Assert.That(events.Length, Is.EqualTo(1));
            Assert.That(events[0], Is.TypeOf<InvalidInput>());

            InvalidInput invalidInput = (InvalidInput)events[0];
            Assert.That(invalidInput.TappedCoord, Is.EqualTo(tappedCoord));
            Assert.That(invalidInput.Reason, Is.EqualTo(expectedReason));
        }

        private static void AssertActionEventSequenceEqual(
            ImmutableArray<ActionEvent> expected,
            ImmutableArray<ActionEvent> actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                AssertActionEventEqual(expected[i], actual[i], i);
            }
        }

        private static void AssertActionEventEqual(ActionEvent expected, ActionEvent actual, int index)
        {
            Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()), $"Event type mismatch at index {index}.");

            switch (expected)
            {
                case GroupRemoved expectedGroupRemoved:
                    GroupRemoved actualGroupRemoved = (GroupRemoved)actual;
                    Assert.That(actualGroupRemoved.Type, Is.EqualTo(expectedGroupRemoved.Type), $"GroupRemoved type mismatch at index {index}.");
                    Assert.That(actualGroupRemoved.Coords, Is.EqualTo(expectedGroupRemoved.Coords).AsCollection, $"GroupRemoved coords mismatch at index {index}.");
                    return;
                case DockInserted expectedDockInserted:
                    DockInserted actualDockInserted = (DockInserted)actual;
                    AssertDebrisSequenceEqual(expectedDockInserted.Pieces, actualDockInserted.Pieces, $"DockInserted pieces mismatch at index {index}.");
                    Assert.That(actualDockInserted.OccupancyAfterInsert, Is.EqualTo(expectedDockInserted.OccupancyAfterInsert), $"DockInserted occupancy mismatch at index {index}.");
                    Assert.That(actualDockInserted.OverflowCount, Is.EqualTo(expectedDockInserted.OverflowCount), $"DockInserted overflow mismatch at index {index}.");
                    return;
                case GravitySettled expectedGravitySettled:
                    GravitySettled actualGravitySettled = (GravitySettled)actual;
                    AssertMoveSequenceEqual(expectedGravitySettled.Moves, actualGravitySettled.Moves, $"GravitySettled moves mismatch at index {index}.");
                    return;
                case Spawned expectedSpawned:
                    Spawned actualSpawned = (Spawned)actual;
                    AssertSpawnSequenceEqual(expectedSpawned.Pieces, actualSpawned.Pieces, $"Spawned pieces mismatch at index {index}.");
                    return;
                case Won expectedWon:
                    Won actualWon = (Won)actual;
                    Assert.That(actualWon.ExtractedTargetOrder, Is.EqualTo(expectedWon.ExtractedTargetOrder).AsCollection, $"Won extracted order mismatch at index {index}.");
                    Assert.That(actualWon.FinalExtractedTargetId, Is.EqualTo(expectedWon.FinalExtractedTargetId), $"Won final target mismatch at index {index}.");
                    Assert.That(actualWon.TotalActions, Is.EqualTo(expectedWon.TotalActions), $"Won total actions mismatch at index {index}.");
                    return;
                default:
                    Assert.That(actual, Is.EqualTo(expected), $"Event mismatch at index {index}.");
                    return;
            }
        }

        private static void AssertDebrisSequenceEqual(
            ImmutableArray<DebrisType> expected,
            ImmutableArray<DebrisType> actual,
            string messagePrefix)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{messagePrefix} length.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"{messagePrefix} item {i}.");
            }
        }

        private static void AssertMoveSequenceEqual(
            ImmutableArray<(TileCoord From, TileCoord To)> expected,
            ImmutableArray<(TileCoord From, TileCoord To)> actual,
            string messagePrefix)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{messagePrefix} length.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i].From, Is.EqualTo(expected[i].From), $"{messagePrefix} from {i}.");
                Assert.That(actual[i].To, Is.EqualTo(expected[i].To), $"{messagePrefix} to {i}.");
            }
        }

        private static void AssertSpawnSequenceEqual(
            ImmutableArray<SpawnedPiece> expected,
            ImmutableArray<SpawnedPiece> actual,
            string messagePrefix)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{messagePrefix} length.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i].Coord, Is.EqualTo(expected[i].Coord), $"{messagePrefix} coord {i}.");
                Assert.That(actual[i].Type, Is.EqualTo(expected[i].Type), $"{messagePrefix} type {i}.");
            }
        }
    }

    internal static class PipelineTestFixtures
    {
        public static GameState CreateState(
            Board board,
            ImmutableArray<TargetState>? targets = null,
            int actionCount = 0,
            bool dockJamEnabled = false,
            bool dockJamActive = false)
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
                LevelConfig: CreateLevelConfig(),
                RngState: new RngState(123u, 456u),
                ActionCount: actionCount,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0,
                DockJamEnabled: dockJamEnabled,
                DockJamActive: dockJamActive,
                DebugSpawnOverride: null);
        }

        public static LevelConfig CreateLevelConfig(
            double assistanceChance = 0.0d,
            ImmutableDictionary<DebrisType, double>? baseDistribution = null,
            params DebrisType[] pool)
        {
            ImmutableArray<DebrisType> debrisPool = pool is { Length: > 0 }
                ? pool.ToImmutableArray()
                : ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E);

            return new LevelConfig(
                DebrisTypePool: debrisPool,
                BaseDistribution: baseDistribution,
                AssistanceChance: assistanceChance,
                ConsecutiveEmergencyCap: 2);
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

        public static ImmutableArray<Tile> Row(params Tile[] tiles)
        {
            return tiles.ToImmutableArray();
        }
    }
}
