using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Pipeline.Steps;
using Rescue.Core.Rng;
using Rescue.Core.Rules;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Rules
{
    public sealed class SpawnTests
    {
        [Test]
        public void AssistanceChanceZeroKeepsDistributionNearBaseWeights()
        {
            GameState state = CreateSpawnState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                assistanceChance: 0.0d,
                baseDistribution: ImmutableDictionary<DebrisType, double>.Empty
                    .Add(DebrisType.A, 1.0d)
                    .Add(DebrisType.B, 3.0d));

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig);
            SeededRng rng = new SeededRng(20260421u);
            Dictionary<DebrisType, int> counts = Sample(rng, bias.Weights, sampleCount: 20000);

            AssertFrequency(counts[DebrisType.A], 20000, 0.25d, 0.03d);
            AssertFrequency(counts[DebrisType.B], 20000, 0.75d, 0.03d);
        }

        [Test]
        public void AssistanceChanceOnePrefersTypeWithTwoAlreadyInDock()
        {
            GameState state = CreateSpawnState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                assistanceChance: 1.0d,
                dock: new Dock(
                    ImmutableArray.Create<DebrisType?>(DebrisType.C, DebrisType.C, null, null, null, null, null),
                    Size: 7));

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig);

            Assert.That(GetWeight(bias.Weights, DebrisType.C), Is.GreaterThan(GetWeight(bias.Weights, DebrisType.A)));
            Assert.That(GetWeight(bias.Weights, DebrisType.C), Is.GreaterThan(GetWeight(bias.Weights, DebrisType.B)));
        }

        [Test]
        public void EmergencyTriggerAtDockOccupancyFiveRaisesAssistanceByExactlyTwentyPoints()
        {
            GameState state = CreateSpawnState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                assistanceChance: 0.3d,
                dock: new Dock(
                    ImmutableArray.Create<DebrisType?>(
                        DebrisType.A,
                        DebrisType.B,
                        DebrisType.C,
                        DebrisType.D,
                        DebrisType.E,
                        null,
                        null),
                    Size: 7));

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig);

            Assert.That(bias.IsEmergency, Is.True);
            Assert.That(bias.EffectiveAssistanceChance, Is.EqualTo(0.5d).Within(1e-9));
        }

        [Test]
        public void ThirdConsecutiveEmergencyRequestFallsBackToNonEmergencyBias()
        {
            GameState state = CreateSpawnState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                assistanceChance: 0.3d,
                dock: new Dock(
                    ImmutableArray.Create<DebrisType?>(
                        DebrisType.A,
                        DebrisType.B,
                        DebrisType.C,
                        DebrisType.D,
                        DebrisType.E,
                        null,
                        null),
                    Size: 7))
                with
                {
                    ConsecutiveEmergencySpawns = 2,
                };

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig);

            Assert.That(bias.IsEmergency, Is.False);
            Assert.That(bias.EffectiveAssistanceChance, Is.EqualTo(0.3d).Within(1e-9));
        }

        [Test]
        public void SpawnRecoveryBiasesNextTwoSpawnsTowardCreatingPair()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                ImmutableArray.Create<Tile>(new EmptyTile()),
                ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.B)));
            GameState state = CreateSpawnState(board, assistanceChance: 0.0d) with
            {
                SpawnRecoveryCounter = 2,
            };
            SeededRng rng = new SeededRng(12345u);
            Dictionary<DebrisType, int> counts = new Dictionary<DebrisType, int>
            {
                [DebrisType.A] = 0,
                [DebrisType.B] = 0,
                [DebrisType.C] = 0,
                [DebrisType.D] = 0,
                [DebrisType.E] = 0,
            };

            TileCoord spawnCoord = new TileCoord(0, 0);
            for (int i = 0; i < 20000; i++)
            {
                counts[SpawnOps.ChooseNextSpawn(state, spawnCoord, rng)]++;
            }

            Assert.That(counts[DebrisType.B], Is.GreaterThan(counts[DebrisType.A]));
            Assert.That(counts[DebrisType.B], Is.GreaterThan(counts[DebrisType.C]));
        }

        [Test]
        public void SameSeedAndStateProduceSameSpawn()
        {
            GameState state = CreateSpawnState(
                PipelineTestFixtures.CreateBoard(
                    ImmutableArray.Create<Tile>(new EmptyTile(), new EmptyTile()),
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.B))),
                assistanceChance: 0.0d)
                with
                {
                    RngState = new RngState(0x12345678u, 0x9ABCDEF0u),
                };
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0)));

            StepResult first = Step08_Spawn.Run(state, context);
            StepResult second = Step08_Spawn.Run(state, context);

            AssertBoardEqual(first.State.Board, second.State.Board);
            Assert.That(second.State.RngState, Is.EqualTo(first.State.RngState));
            Assert.That(second.State.ConsecutiveEmergencySpawns, Is.EqualTo(first.State.ConsecutiveEmergencySpawns));
            Assert.That(second.State.SpawnRecoveryCounter, Is.EqualTo(first.State.SpawnRecoveryCounter));
            AssertSpawnEventsEqual(first.Events, second.Events);
        }

        private static GameState CreateSpawnState(
            Board board,
            double assistanceChance,
            ImmutableDictionary<DebrisType, double>? baseDistribution = null,
            Dock? dock = null)
        {
            return PipelineTestFixtures.CreateState(board) with
            {
                Dock = dock ?? new Dock(ImmutableArray.Create<DebrisType?>(null, null, null, null, null, null, null), Size: 7),
                LevelConfig = PipelineTestFixtures.CreateLevelConfig(
                    assistanceChance,
                    baseDistribution,
                    DebrisType.A,
                    DebrisType.B,
                    DebrisType.C,
                    DebrisType.D,
                    DebrisType.E),
            };
        }

        private static Dictionary<DebrisType, int> Sample(
            SeededRng rng,
            ImmutableArray<(DebrisType Type, double Weight)> weights,
            int sampleCount)
        {
            List<(DebrisType item, double weight)> weightedItems = new List<(DebrisType item, double weight)>(weights.Length);
            for (int i = 0; i < weights.Length; i++)
            {
                weightedItems.Add((weights[i].Type, weights[i].Weight));
            }

            Dictionary<DebrisType, int> counts = new Dictionary<DebrisType, int>
            {
                [DebrisType.A] = 0,
                [DebrisType.B] = 0,
                [DebrisType.C] = 0,
                [DebrisType.D] = 0,
                [DebrisType.E] = 0,
            };

            for (int i = 0; i < sampleCount; i++)
            {
                counts[rng.WeightedPick(weightedItems)]++;
            }

            return counts;
        }

        private static double GetWeight(ImmutableArray<(DebrisType Type, double Weight)> weights, DebrisType type)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i].Type == type)
                {
                    return weights[i].Weight;
                }
            }

            Assert.Fail($"Missing weight for debris type {type}.");
            return 0.0d;
        }

        private static void AssertFrequency(int observedCount, int sampleCount, double expectedFrequency, double tolerance)
        {
            double actualFrequency = observedCount / (double)sampleCount;
            Assert.That(actualFrequency, Is.EqualTo(expectedFrequency).Within(tolerance));
        }

        private static void AssertSpawnEventsEqual(
            ImmutableArray<ActionEvent> expected,
            ImmutableArray<ActionEvent> actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i].GetType(), Is.EqualTo(expected[i].GetType()));
                Spawned expectedSpawned = (Spawned)expected[i];
                Spawned actualSpawned = (Spawned)actual[i];
                Assert.That(actualSpawned.Pieces.Length, Is.EqualTo(expectedSpawned.Pieces.Length));
                for (int j = 0; j < expectedSpawned.Pieces.Length; j++)
                {
                    Assert.That(actualSpawned.Pieces[j].Coord, Is.EqualTo(expectedSpawned.Pieces[j].Coord));
                    Assert.That(actualSpawned.Pieces[j].Type, Is.EqualTo(expectedSpawned.Pieces[j].Type));
                }
            }
        }

        private static void AssertBoardEqual(Board expected, Board actual)
        {
            Assert.That(actual.Width, Is.EqualTo(expected.Width));
            Assert.That(actual.Height, Is.EqualTo(expected.Height));
            Assert.That(actual.Tiles.Length, Is.EqualTo(expected.Tiles.Length));
            for (int row = 0; row < expected.Tiles.Length; row++)
            {
                Assert.That(actual.Tiles[row].Length, Is.EqualTo(expected.Tiles[row].Length));
                for (int col = 0; col < expected.Tiles[row].Length; col++)
                {
                    Assert.That(actual.Tiles[row][col], Is.EqualTo(expected.Tiles[row][col]));
                }
            }
        }
    }
}
