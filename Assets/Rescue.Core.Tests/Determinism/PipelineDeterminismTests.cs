using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Determinism
{
    public sealed class PipelineDeterminismTests
    {
        [Test]
        public void RepeatedRunsProduceIdenticalFinalStateAndEventStream()
        {
            const uint seed = 424242u;
            IReadOnlyList<ActionInput> inputs = CreateInputSequence();

            IReadOnlyList<PipelineRunState> firstRun = DeterminismHarness.RecordStateStream(
                seed,
                inputs,
                stepFn: static (run, input) =>
                {
                    ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(run.State, input);
                    return run with { State = result.State, Events = run.Events.AddRange(result.Events) };
                },
                initialStateFromSeed: CreateInitialRunState);

            IReadOnlyList<PipelineRunState> secondRun = DeterminismHarness.RecordStateStream(
                seed,
                inputs,
                stepFn: static (run, input) =>
                {
                    ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(run.State, input);
                    return run with { State = result.State, Events = run.Events.AddRange(result.Events) };
                },
                initialStateFromSeed: CreateInitialRunState);

            Assert.That(secondRun.Count, Is.EqualTo(firstRun.Count));

            for (int i = 0; i < firstRun.Count; i++)
            {
                string label = i == 0 ? "initial state" : $"step {i}";
                AssertRunStatesEqual(firstRun[i], secondRun[i], label);
            }
        }

        [Test]
        public void AssistedSpawnAfterDiagonalSettlingIsDeterministic()
        {
            GameState state = CreateDiagonalAssistedSpawnState(0xBADC0DEu);
            ActionInput input = new ActionInput(new TileCoord(0, 2));

            ActionResult first = Rescue.Core.Pipeline.Pipeline.RunAction(state, input);
            ActionResult second = Rescue.Core.Pipeline.Pipeline.RunAction(state, input);

            Assert.That(first.Events, Has.Some.TypeOf<GravitySettled>());
            Assert.That(first.Events, Has.Some.TypeOf<Spawned>());
            AssertRunStatesEqual(
                new PipelineRunState(first.State, first.Events),
                new PipelineRunState(second.State, second.Events),
                "assisted spawn after diagonal settling");

            Spawned firstSpawn = GetSingleSpawnedEvent(first.Events);
            Spawned secondSpawn = GetSingleSpawnedEvent(second.Events);
            Assert.That(firstSpawn.Pieces[0].Reasons, Is.Not.Empty);
            AssertSpawnAssistReasonsEqual(firstSpawn.Pieces[0].Reasons, secondSpawn.Pieces[0].Reasons);
            AssertStringSequenceEqual(firstSpawn.Pieces[0].TriggerContext, secondSpawn.Pieces[0].TriggerContext);
        }

        [Test]
        public void RepeatedDiagonalSettlingRunsFromSameSeedProduceIdenticalBoardsAndEvents()
        {
            GameState state = CreateDiagonalAssistedSpawnState(0xBADC0DEu);
            ActionInput input = new ActionInput(new TileCoord(0, 2));

            ActionResult first = Rescue.Core.Pipeline.Pipeline.RunAction(state, input);
            ActionResult second = Rescue.Core.Pipeline.Pipeline.RunAction(state, input);

            Assert.That(first.Events, Has.Some.TypeOf<DiagonalSettlingApplied>());
            AssertBoardEqual(first.State.Board, second.State.Board, "diagonal final board");
            AssertActionEventSequenceEqual(first.Events, second.Events, "diagonal event stream");
        }

        [Test]
        public void HardNoMoveRepairAfterFullRefillIsDeterministic()
        {
            GameState state = CreateHardNoMoveRepairPipelineState(0xFEED1234u);
            ActionInput input = new ActionInput(new TileCoord(2, 0));

            ActionResult first = Rescue.Core.Pipeline.Pipeline.RunAction(state, input);
            ActionResult second = Rescue.Core.Pipeline.Pipeline.RunAction(state, input);

            Assert.That(first.Events, Has.Some.TypeOf<DeadboardMinimalShuffleApplied>());
            Assert.That(first.Events, Has.Some.TypeOf<Spawned>());
            Assert.That(first.State.RngState, Is.Not.EqualTo(state.RngState));
            AssertBoardEqual(first.State.Board, second.State.Board, "hard no-move repair final board");
            AssertActionEventSequenceEqual(first.Events, second.Events, "hard no-move repair event stream");
        }

        private static PipelineRunState CreateInitialRunState(uint seed)
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B, DebrisType.B),
                    PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.C, DebrisType.D, DebrisType.D),
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.C, DebrisType.C),
                    PipelineTestFixtures.EmptyRow(4)));

            state = state with
            {
                RngState = new Rescue.Core.Rng.RngState(seed, seed ^ 0xA5A5A5A5u),
            };

            return new PipelineRunState(state, ImmutableArray<ActionEvent>.Empty);
        }

        private static GameState CreateDiagonalAssistedSpawnState(uint seed)
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.Row(new EmptyTile(), new EmptyTile(), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A)),
                    PipelineTestFixtures.Row(new BlockerTile(BlockerType.Crate, 1, null), new DebrisTile(DebrisType.B), new EmptyTile(), new EmptyTile()),
                    PipelineTestFixtures.Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 1, null), new EmptyTile(), new EmptyTile()),
                    PipelineTestFixtures.Row(new BlockerTile(BlockerType.Crate, 1, null), new BlockerTile(BlockerType.Crate, 1, null), new EmptyTile(), new EmptyTile())));

            return state with
            {
                LevelConfig = PipelineTestFixtures.CreateLevelConfig(1.0d),
                RngState = new Rescue.Core.Rng.RngState(seed, seed ^ 0xA5A5A5A5u),
            };
        }

        private static GameState CreateHardNoMoveRepairPipelineState(uint seed)
        {
            return PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B, DebrisType.C),
                    PipelineTestFixtures.Row(
                        new BlockerTile(BlockerType.Crate, 2, null),
                        new BlockerTile(BlockerType.Crate, 2, null),
                        new BlockerTile(BlockerType.Crate, 2, null)),
                    PipelineTestFixtures.DebrisRow(DebrisType.D, DebrisType.D, DebrisType.E)))
                with
                {
                    Water = new WaterState(FloodedRows: 0, ActionsUntilRise: 1, RiseInterval: 3),
                    RngState = new Rescue.Core.Rng.RngState(seed, seed ^ 0xA5A5A5A5u),
                };
        }

        private static IReadOnlyList<ActionInput> CreateInputSequence()
        {
            TileCoord[] taps =
            {
                new TileCoord(0, 0),
                new TileCoord(0, 2),
                new TileCoord(1, 0),
                new TileCoord(1, 2),
                new TileCoord(2, 0),
                new TileCoord(2, 2),
                new TileCoord(3, 0),
                new TileCoord(0, 0),
                new TileCoord(0, 1),
                new TileCoord(0, 2),
                new TileCoord(1, 0),
                new TileCoord(1, 1),
                new TileCoord(1, 2),
                new TileCoord(2, 0),
                new TileCoord(2, 1),
                new TileCoord(2, 2),
                new TileCoord(3, 1),
                new TileCoord(3, 2),
                new TileCoord(3, 3),
                new TileCoord(0, 3),
            };

            List<ActionInput> inputs = new List<ActionInput>(taps.Length);
            for (int i = 0; i < taps.Length; i++)
            {
                inputs.Add(new ActionInput(taps[i]));
            }

            return inputs;
        }

        private static void AssertRunStatesEqual(PipelineRunState expected, PipelineRunState actual, string label)
        {
            AssertGameStatesEqual(expected.State, actual.State, label);
            AssertActionEventSequenceEqual(expected.Events, actual.Events, label);
        }

        private sealed record PipelineRunState(GameState State, ImmutableArray<ActionEvent> Events);

        private static void AssertGameStatesEqual(GameState expected, GameState actual, string label)
        {
            AssertBoardEqual(expected.Board, actual.Board, label);
            Assert.That(actual.Dock.Size, Is.EqualTo(expected.Dock.Size), $"Dock size mismatch at {label}.");
            AssertNullableDebrisSequenceEqual(expected.Dock.Slots, actual.Dock.Slots, $"Dock slots mismatch at {label}.");
            Assert.That(actual.Water, Is.EqualTo(expected.Water), $"Water mismatch at {label}.");
            Assert.That(actual.Vine.ActionsSinceLastClear, Is.EqualTo(expected.Vine.ActionsSinceLastClear), $"Vine action counter mismatch at {label}.");
            Assert.That(actual.Vine.GrowthThreshold, Is.EqualTo(expected.Vine.GrowthThreshold), $"Vine threshold mismatch at {label}.");
            Assert.That(actual.Vine.GrowthPriorityList, Is.EqualTo(expected.Vine.GrowthPriorityList).AsCollection, $"Vine growth list mismatch at {label}.");
            Assert.That(actual.Vine.PriorityCursor, Is.EqualTo(expected.Vine.PriorityCursor), $"Vine cursor mismatch at {label}.");
            Assert.That(actual.Vine.PendingGrowthTile, Is.EqualTo(expected.Vine.PendingGrowthTile), $"Vine pending tile mismatch at {label}.");
            Assert.That(actual.Targets, Is.EqualTo(expected.Targets).AsCollection, $"Targets mismatch at {label}.");
            AssertLevelConfigEqual(expected.LevelConfig, actual.LevelConfig, label);
            Assert.That(actual.RngState, Is.EqualTo(expected.RngState), $"RngState mismatch at {label}.");
            Assert.That(actual.ActionCount, Is.EqualTo(expected.ActionCount), $"ActionCount mismatch at {label}.");
            Assert.That(actual.DockJamUsed, Is.EqualTo(expected.DockJamUsed), $"DockJamUsed mismatch at {label}.");
            Assert.That(actual.UndoAvailable, Is.EqualTo(expected.UndoAvailable), $"UndoAvailable mismatch at {label}.");
            Assert.That(actual.ExtractedTargetOrder, Is.EqualTo(expected.ExtractedTargetOrder).AsCollection, $"Extracted target order mismatch at {label}.");
            Assert.That(actual.Frozen, Is.EqualTo(expected.Frozen), $"Frozen mismatch at {label}.");
            Assert.That(actual.ConsecutiveEmergencySpawns, Is.EqualTo(expected.ConsecutiveEmergencySpawns), $"ConsecutiveEmergencySpawns mismatch at {label}.");
            Assert.That(actual.SpawnRecoveryCounter, Is.EqualTo(expected.SpawnRecoveryCounter), $"SpawnRecoveryCounter mismatch at {label}.");
            Assert.That(actual.DockJamEnabled, Is.EqualTo(expected.DockJamEnabled), $"DockJamEnabled mismatch at {label}.");
            Assert.That(actual.DockJamActive, Is.EqualTo(expected.DockJamActive), $"DockJamActive mismatch at {label}.");
            Assert.That(actual.DebugSpawnOverride, Is.EqualTo(expected.DebugSpawnOverride), $"DebugSpawnOverride mismatch at {label}.");
            AssertSpawnLineageEqual(expected.SpawnLineageByCoord, actual.SpawnLineageByCoord, label);
            Assert.That(actual.NextSpawnLineageId, Is.EqualTo(expected.NextSpawnLineageId), $"NextSpawnLineageId mismatch at {label}.");
        }

        private static void AssertBoardEqual(Board expected, Board actual, string label)
        {
            Assert.That(actual.Width, Is.EqualTo(expected.Width), $"Board width mismatch at {label}.");
            Assert.That(actual.Height, Is.EqualTo(expected.Height), $"Board height mismatch at {label}.");
            Assert.That(actual.Tiles.Length, Is.EqualTo(expected.Tiles.Length), $"Board row count mismatch at {label}.");

            for (int row = 0; row < expected.Tiles.Length; row++)
            {
                Assert.That(actual.Tiles[row].Length, Is.EqualTo(expected.Tiles[row].Length), $"Board row length mismatch at {label}, row {row}.");
                for (int col = 0; col < expected.Tiles[row].Length; col++)
                {
                    Assert.That(actual.Tiles[row][col], Is.EqualTo(expected.Tiles[row][col]), $"Board tile mismatch at {label}, row {row}, col {col}.");
                }
            }
        }

        private static void AssertActionEventSequenceEqual(
            ImmutableArray<ActionEvent> expected,
            ImmutableArray<ActionEvent> actual,
            string label)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"Event count mismatch at {label}.");
            for (int i = 0; i < expected.Length; i++)
            {
                AssertActionEventEqual(expected[i], actual[i], label, i);
            }
        }

        private static void AssertActionEventEqual(ActionEvent expected, ActionEvent actual, string label, int index)
        {
            Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()), $"Event type mismatch at {label}, index {index}.");

            switch (expected)
            {
                case GroupRemoved expectedGroupRemoved:
                    GroupRemoved actualGroupRemoved = (GroupRemoved)actual;
                    Assert.That(actualGroupRemoved.Type, Is.EqualTo(expectedGroupRemoved.Type), $"GroupRemoved type mismatch at {label}, index {index}.");
                    AssertTileCoordSequenceEqual(expectedGroupRemoved.Coords, actualGroupRemoved.Coords, $"GroupRemoved coords mismatch at {label}, index {index}.");
                    AssertIntSequenceEqual(expectedGroupRemoved.SpawnLineageIds, actualGroupRemoved.SpawnLineageIds, $"GroupRemoved lineage mismatch at {label}, index {index}.");
                    return;
                case DockInserted expectedDockInserted:
                    DockInserted actualDockInserted = (DockInserted)actual;
                    AssertDebrisSequenceEqual(expectedDockInserted.Pieces, actualDockInserted.Pieces, $"DockInserted pieces mismatch at {label}, index {index}.");
                    Assert.That(actualDockInserted.OccupancyAfterInsert, Is.EqualTo(expectedDockInserted.OccupancyAfterInsert), $"DockInserted occupancy mismatch at {label}, index {index}.");
                    Assert.That(actualDockInserted.OverflowCount, Is.EqualTo(expectedDockInserted.OverflowCount), $"DockInserted overflow mismatch at {label}, index {index}.");
                    return;
                case GravitySettled expectedGravitySettled:
                    GravitySettled actualGravitySettled = (GravitySettled)actual;
                    AssertMoveSequenceEqual(expectedGravitySettled.Moves, actualGravitySettled.Moves, $"GravitySettled moves mismatch at {label}, index {index}.");
                    return;
                case DiagonalSettlingApplied expectedDiagonalSettling:
                    DiagonalSettlingApplied actualDiagonalSettling = (DiagonalSettlingApplied)actual;
                    AssertMoveSequenceEqual(expectedDiagonalSettling.Moves, actualDiagonalSettling.Moves, $"DiagonalSettlingApplied moves mismatch at {label}, index {index}.");
                    return;
                case Spawned expectedSpawned:
                    Spawned actualSpawned = (Spawned)actual;
                    AssertSpawnSequenceEqual(expectedSpawned.Pieces, actualSpawned.Pieces, $"Spawned pieces mismatch at {label}, index {index}.");
                    return;
                case Won expectedWon:
                    Won actualWon = (Won)actual;
                    Assert.That(actualWon.ExtractedTargetOrder, Is.EqualTo(expectedWon.ExtractedTargetOrder).AsCollection, $"Won extracted order mismatch at {label}, index {index}.");
                    Assert.That(actualWon.FinalExtractedTargetId, Is.EqualTo(expectedWon.FinalExtractedTargetId), $"Won final target mismatch at {label}, index {index}.");
                    Assert.That(actualWon.TotalActions, Is.EqualTo(expectedWon.TotalActions), $"Won total actions mismatch at {label}, index {index}.");
                    return;
                default:
                    Assert.That(actual, Is.EqualTo(expected), $"Event mismatch at {label}, index {index}.");
                    return;
            }
        }

        private static void AssertNullableDebrisSequenceEqual(
            ImmutableArray<DebrisType?> expected,
            ImmutableArray<DebrisType?> actual,
            string messagePrefix)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{messagePrefix} length.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"{messagePrefix} slot {i}.");
            }
        }

        private static void AssertTileCoordSequenceEqual(
            ImmutableArray<TileCoord> expected,
            ImmutableArray<TileCoord> actual,
            string messagePrefix)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{messagePrefix} length.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"{messagePrefix} coord {i}.");
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

        private static void AssertLevelConfigEqual(LevelConfig expected, LevelConfig actual, string label)
        {
            Assert.That(actual.DebrisTypePool.Length, Is.EqualTo(expected.DebrisTypePool.Length), $"LevelConfig pool length mismatch at {label}.");
            for (int i = 0; i < expected.DebrisTypePool.Length; i++)
            {
                Assert.That(actual.DebrisTypePool[i], Is.EqualTo(expected.DebrisTypePool[i]), $"LevelConfig pool item mismatch at {label}, index {i}.");
            }
            Assert.That(actual.AssistanceChance, Is.EqualTo(expected.AssistanceChance), $"LevelConfig assistance mismatch at {label}.");
            Assert.That(actual.ConsecutiveEmergencyCap, Is.EqualTo(expected.ConsecutiveEmergencyCap), $"LevelConfig cap mismatch at {label}.");
            Assert.That(actual.IsRuleTeach, Is.EqualTo(expected.IsRuleTeach), $"LevelConfig rule teach mismatch at {label}.");
            Assert.That(actual.WaterContactMode, Is.EqualTo(expected.WaterContactMode), $"LevelConfig water contact mode mismatch at {label}.");
            Assert.That(actual.BaseDistribution?.Count ?? 0, Is.EqualTo(expected.BaseDistribution?.Count ?? 0), $"LevelConfig distribution size mismatch at {label}.");

            if (expected.BaseDistribution is null || actual.BaseDistribution is null)
            {
                Assert.That(actual.BaseDistribution is null, Is.EqualTo(expected.BaseDistribution is null), $"LevelConfig distribution null mismatch at {label}.");
                return;
            }

            foreach ((DebrisType key, double value) in expected.BaseDistribution)
            {
                Assert.That(actual.BaseDistribution.TryGetValue(key, out double actualValue), Is.True, $"LevelConfig missing weight for {key} at {label}.");
                Assert.That(actualValue, Is.EqualTo(value), $"LevelConfig weight mismatch for {key} at {label}.");
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
                Assert.That(actual[i].LineageId, Is.EqualTo(expected[i].LineageId), $"{messagePrefix} lineage {i}.");
                AssertSpawnAssistReasonsEqual(expected[i].Reasons, actual[i].Reasons, $"{messagePrefix} reasons {i}.");
                AssertStringSequenceEqual(expected[i].TriggerContext, actual[i].TriggerContext, $"{messagePrefix} trigger context {i}.");
                Assert.That(actual[i].UrgentTargetId, Is.EqualTo(expected[i].UrgentTargetId), $"{messagePrefix} urgent target {i}.");
                Assert.That(actual[i].UrgentTargetCoord, Is.EqualTo(expected[i].UrgentTargetCoord), $"{messagePrefix} urgent target coord {i}.");
                Assert.That(actual[i].WaterRisesRemaining, Is.EqualTo(expected[i].WaterRisesRemaining), $"{messagePrefix} water rises remaining {i}.");
                Assert.That(actual[i].DockOccupancy, Is.EqualTo(expected[i].DockOccupancy), $"{messagePrefix} dock occupancy {i}.");
                Assert.That(actual[i].RecoveryCounterBefore, Is.EqualTo(expected[i].RecoveryCounterBefore), $"{messagePrefix} recovery counter {i}.");
                Assert.That(actual[i].EmergencyRequested, Is.EqualTo(expected[i].EmergencyRequested), $"{messagePrefix} emergency requested {i}.");
                Assert.That(actual[i].EmergencyApplied, Is.EqualTo(expected[i].EmergencyApplied), $"{messagePrefix} emergency applied {i}.");
                Assert.That(actual[i].EffectiveAssistanceChance, Is.EqualTo(expected[i].EffectiveAssistanceChance), $"{messagePrefix} assistance chance {i}.");
            }
        }

        private static void AssertIntSequenceEqual(
            ImmutableArray<int> expected,
            ImmutableArray<int> actual,
            string messagePrefix)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{messagePrefix} length.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"{messagePrefix} item {i}.");
            }
        }

        private static void AssertSpawnLineageEqual(
            ImmutableDictionary<TileCoord, SpawnLineage> expected,
            ImmutableDictionary<TileCoord, SpawnLineage> actual,
            string label)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), $"Spawn lineage count mismatch at {label}.");
            foreach ((TileCoord coord, SpawnLineage lineage) in expected)
            {
                Assert.That(actual.TryGetValue(coord, out SpawnLineage actualLineage), Is.True, $"Missing spawn lineage at {label}, coord {coord}.");
                Assert.That(actualLineage, Is.EqualTo(lineage), $"Spawn lineage mismatch at {label}, coord {coord}.");
            }
        }

        private static Spawned GetSingleSpawnedEvent(ImmutableArray<ActionEvent> events)
        {
            Spawned? spawned = null;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i] is Spawned candidate)
                {
                    Assert.That(spawned, Is.Null, "Expected exactly one Spawned event.");
                    spawned = candidate;
                }
            }

            Assert.That(spawned, Is.Not.Null, "Expected a Spawned event.");
            return spawned!;
        }

        private static void AssertSpawnAssistReasonsEqual(
            ImmutableArray<SpawnAssistReason> expected,
            ImmutableArray<SpawnAssistReason> actual,
            string messagePrefix = "Spawn assist reason mismatch")
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{messagePrefix} count.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"{messagePrefix} at index {i}.");
            }
        }

        private static void AssertStringSequenceEqual(
            ImmutableArray<string> expected,
            ImmutableArray<string> actual,
            string messagePrefix = "Trigger context mismatch")
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{messagePrefix} count.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"{messagePrefix} at index {i}.");
            }
        }
    }
}
