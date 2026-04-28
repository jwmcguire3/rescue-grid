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
        public void ForceEmergencyOverrideAppliesEmergencyBonusWithoutNaturalTrigger()
        {
            GameState state = CreateSpawnState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                assistanceChance: 0.3d)
                with
                {
                    DebugSpawnOverride = new SpawnOverride(ForceEmergency: true, OverrideAssistanceChance: null),
                };

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig);

            Assert.That(bias.IsEmergency, Is.True);
            Assert.That(bias.EffectiveAssistanceChance, Is.EqualTo(0.5d).Within(1e-9));
        }

        [Test]
        public void AssistanceChanceOverrideReplacesLevelDefaultBeforeEmergencyBonus()
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
                    DebugSpawnOverride = new SpawnOverride(ForceEmergency: null, OverrideAssistanceChance: 1.0d),
                };

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig);

            Assert.That(bias.IsEmergency, Is.True);
            Assert.That(bias.EffectiveAssistanceChance, Is.EqualTo(1.0d).Within(1e-9));
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
                counts[SpawnOps.ChooseNextSpawn(state, state.Board, spawnCoord, rng)]++;
            }

            Assert.That(counts[DebrisType.B], Is.GreaterThan(counts[DebrisType.A]));
            Assert.That(counts[DebrisType.B], Is.GreaterThan(counts[DebrisType.C]));
        }

        [Test]
        public void SpawnIntegrity_RejectsNormalExactTriple()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile()),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)));
            GameState state = CreateSpawnState(board, assistanceChance: 0.0d);

            SpawnCandidate candidate = SpawnOps.EvaluateSpawnCandidate(
                state,
                board,
                new TileCoord(0, 0),
                DebrisType.A,
                weight: 1.0d);
            DebrisType chosen = SpawnOps.ChooseNextSpawn(state, board, new TileCoord(0, 0), new SeededRng(12u));

            Assert.That(candidate.GroupSize, Is.EqualTo(3));
            Assert.That(candidate.IsAllowed, Is.False);
            Assert.That(chosen, Is.Not.EqualTo(DebrisType.A));
        }

        [Test]
        public void SpawnIntegrity_AllowsGroupsOfFourAndFive()
        {
            Board fourBoard = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile()),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)));
            Board fiveBoard = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile()),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)));

            SpawnCandidate four = SpawnOps.EvaluateSpawnCandidate(
                CreateSpawnState(fourBoard, assistanceChance: 0.0d),
                fourBoard,
                new TileCoord(0, 0),
                DebrisType.A,
                weight: 1.0d);
            SpawnCandidate five = SpawnOps.EvaluateSpawnCandidate(
                CreateSpawnState(fiveBoard, assistanceChance: 0.0d),
                fiveBoard,
                new TileCoord(0, 0),
                DebrisType.A,
                weight: 1.0d);

            Assert.That(four.GroupSize, Is.EqualTo(4));
            Assert.That(four.IsAllowed, Is.True);
            Assert.That(five.GroupSize, Is.EqualTo(5));
            Assert.That(five.IsAllowed, Is.True);
        }

        [Test]
        public void SpawnIntegrity_RejectsFreshOversizedGroup()
        {
            Board currentBoard = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile()),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)));
            Board preSpawnBoard = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile()),
                Row(new EmptyTile()),
                Row(new EmptyTile()),
                Row(new EmptyTile()),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)));
            GameState state = CreateSpawnState(currentBoard, assistanceChance: 0.0d);

            SpawnCandidate candidate = SpawnOps.EvaluateSpawnCandidate(
                state,
                preSpawnBoard,
                new TileCoord(0, 0),
                DebrisType.A,
                weight: 1.0d);

            Assert.That(candidate.GroupSize, Is.EqualTo(6));
            Assert.That(candidate.ExistingGroupPieces, Is.EqualTo(2));
            Assert.That(candidate.IsAllowed, Is.False);
        }

        [Test]
        public void SpawnIntegrity_AllowsOversizedGroupWhenMajorityExistedBeforeSpawn()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile()),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)));
            GameState state = CreateSpawnState(board, assistanceChance: 0.0d);

            SpawnCandidate candidate = SpawnOps.EvaluateSpawnCandidate(
                state,
                board,
                new TileCoord(0, 0),
                DebrisType.A,
                weight: 1.0d);

            Assert.That(candidate.GroupSize, Is.EqualTo(6));
            Assert.That(candidate.ExistingGroupPieces, Is.EqualTo(5));
            Assert.That(candidate.IsAllowed, Is.True);
        }

        [Test]
        public void SpawnIntegrity_AllowsExactTripleForRuleTeachRecoveryAndExplicitPolicy()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile()),
                Row(new DebrisTile(DebrisType.A)),
                Row(new DebrisTile(DebrisType.A)));
            TileCoord spawnCoord = new TileCoord(0, 0);
            GameState ruleTeach = CreateSpawnState(board, assistanceChance: 0.0d) with
            {
                LevelConfig = PipelineTestFixtures.CreateLevelConfig() with { IsRuleTeach = true },
            };
            GameState recovery = CreateSpawnState(board, assistanceChance: 0.0d) with
            {
                SpawnRecoveryCounter = 2,
            };
            GameState explicitPolicy = CreateSpawnState(board, assistanceChance: 0.0d) with
            {
                LevelConfig = PipelineTestFixtures.CreateLevelConfig() with
                {
                    SpawnIntegrity = new SpawnIntegrityPolicy(AllowExactTripleSpawns: true),
                },
            };

            Assert.That(SpawnOps.EvaluateSpawnCandidate(ruleTeach, board, spawnCoord, DebrisType.A, 1.0d).IsAllowed, Is.True);
            Assert.That(SpawnOps.EvaluateSpawnCandidate(recovery, board, spawnCoord, DebrisType.A, 1.0d).IsAllowed, Is.True);
            Assert.That(SpawnOps.EvaluateSpawnCandidate(explicitPolicy, board, spawnCoord, DebrisType.A, 1.0d).IsAllowed, Is.True);
        }

        [Test]
        public void SpawnIntegrity_DoesNotSuppressGravityCreatedExactTriple()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new DebrisTile(DebrisType.A), new EmptyTile()),
                Row(new DebrisTile(DebrisType.A), new BlockerTile(BlockerType.Crate, 1, Hidden: null)),
                Row(new DebrisTile(DebrisType.A), new BlockerTile(BlockerType.Crate, 1, Hidden: null)));
            GameState state = CreateSpawnState(board, assistanceChance: 0.0d) with
            {
                LevelConfig = PipelineTestFixtures.CreateLevelConfig(
                    0.0d,
                    ImmutableDictionary<DebrisType, double>.Empty.Add(DebrisType.B, 1.0d),
                    DebrisType.A,
                    DebrisType.B),
            };

            StepResult result = Step08_Spawn.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            ImmutableArray<TileCoord>? existingGroup = GroupOps.FindGroup(result.State.Board, new TileCoord(0, 0));
            Spawned spawned = (Spawned)result.Events[0];
            Assert.That(existingGroup.HasValue, Is.True);
            Assert.That(existingGroup.GetValueOrDefault().Length, Is.EqualTo(3));
            Assert.That(spawned.Pieces[0].Type, Is.EqualTo(DebrisType.B));
        }

        [Test]
        public void SpawnIntegrity_SequentialSpawnsUseIncrementalBoard()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile(), new EmptyTile()),
                Row(new BlockerTile(BlockerType.Crate, 1, Hidden: null), new DebrisTile(DebrisType.A)));
            GameState state = CreateSpawnState(board, assistanceChance: 0.0d) with
            {
                LevelConfig = PipelineTestFixtures.CreateLevelConfig(
                    0.0d,
                    ImmutableDictionary<DebrisType, double>.Empty.Add(DebrisType.A, 1.0d),
                    DebrisType.A,
                    DebrisType.B),
            };

            StepResult result = Step08_Spawn.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            Spawned spawned = (Spawned)result.Events[0];

            Assert.That(spawned.Pieces.Length, Is.EqualTo(2));
            Assert.That(spawned.Pieces[0].Type, Is.EqualTo(DebrisType.A));
            Assert.That(spawned.Pieces[1].Type, Is.EqualTo(DebrisType.B));
            ImmutableArray<TileCoord>? avoidedTriple = GroupOps.FindGroup(
                BoardHelpers.SetTile(result.State.Board, new TileCoord(0, 1), new DebrisTile(DebrisType.A)),
                new TileCoord(0, 1));
            Assert.That(avoidedTriple.HasValue, Is.True);
            Assert.That(avoidedTriple.GetValueOrDefault().Length, Is.EqualTo(3));
        }

        [Test]
        public void UrgentTargetChoosesLowestWaterBlockedAndStableIndexTuple()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile(), new DebrisTile(DebrisType.A), new EmptyTile()),
                Row(new EmptyTile(), new TargetTile("safer", Extracted: false), new EmptyTile()),
                Row(new TargetTile("left-most", Extracted: false), new DebrisTile(DebrisType.B), new TargetTile("right-most", Extracted: false)),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile()));
            ImmutableArray<TargetState> targets = ImmutableArray.Create(
                new TargetState("safer", new TileCoord(1, 1), Extracted: false, OneClearAway: false),
                new TargetState("right-most", new TileCoord(2, 2), Extracted: false, OneClearAway: false),
                new TargetState("left-most", new TileCoord(2, 0), Extracted: false, OneClearAway: false));
            GameState state = CreateSpawnState(board, assistanceChance: 1.0d) with
            {
                Targets = targets,
            };

            UrgentRoute? urgent = SpawnOps.FindUrgentRoute(state);

            Assert.That(urgent.HasValue, Is.True);
            Assert.That(urgent!.Value.TargetCoord, Is.EqualTo(new TileCoord(2, 0)));
            Assert.That(urgent.Value.WaterRisesRemaining, Is.EqualTo(1));
            Assert.That(urgent.Value.BlockedRequiredNeighbors, Is.EqualTo(1));
        }

        [Test]
        public void NoUrgentTargetYieldsZeroRouteBonuses()
        {
            Board board = BuildSoftRouteBoard();
            GameState state = CreateSpawnState(board, assistanceChance: 1.0d) with
            {
                Targets = ImmutableArray.Create(
                    new TargetState("done", new TileCoord(1, 3), Extracted: true, OneClearAway: false)),
            };

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig, new TileCoord(0, 2));

            Assert.That(GetWeight(bias.Weights, DebrisType.A), Is.EqualTo(1.0d));
            Assert.That(GetWeight(bias.Weights, DebrisType.B), Is.EqualTo(1.0d));
        }

        [Test]
        public void HardAndSoftRouteRegionsAreBuiltCorrectlyForInteriorTarget()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile(), new DebrisTile(DebrisType.A), new EmptyTile(), new EmptyTile()),
                Row(new DebrisTile(DebrisType.B), new TargetTile("target", Extracted: false), new BlockerTile(BlockerType.Crate, 1, null), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()));
            GameState state = CreateSpawnState(board, assistanceChance: 1.0d) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 1), Extracted: false, OneClearAway: false)),
            };

            UrgentRoute route = SpawnOps.FindUrgentRoute(state)!.Value;

            Assert.That(route.HardRouteCells, Is.EqualTo(new[]
            {
                new TileCoord(0, 1),
                new TileCoord(1, 2),
                new TileCoord(1, 0),
            }).AsCollection);
            Assert.That(route.SoftRouteCells, Is.EqualTo(new[]
            {
                new TileCoord(0, 0),
                new TileCoord(0, 2),
                new TileCoord(1, 1),
                new TileCoord(1, 3),
                new TileCoord(2, 0),
                new TileCoord(2, 2),
            }).AsCollection);
        }

        [Test]
        public void HardAndSoftRouteRegionsAreBuiltCorrectlyForEdgeTarget()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new EmptyTile()),
                Row(new TargetTile("target", Extracted: false), new BlockerTile(BlockerType.Crate, 1, null), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile()));
            GameState state = CreateSpawnState(board, assistanceChance: 1.0d) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 0), Extracted: false, OneClearAway: false)),
            };

            UrgentRoute route = SpawnOps.FindUrgentRoute(state)!.Value;

            Assert.That(route.HardRouteCells, Is.EqualTo(new[]
            {
                new TileCoord(0, 0),
                new TileCoord(1, 1),
            }).AsCollection);
            Assert.That(route.SoftRouteCells, Is.EqualTo(new[]
            {
                new TileCoord(0, 1),
                new TileCoord(1, 0),
                new TileCoord(1, 2),
                new TileCoord(2, 1),
            }).AsCollection);
        }

        [Test]
        public void HardAndSoftRouteRegionsAreBuiltCorrectlyForCornerTarget()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new TargetTile("target", Extracted: false), new DebrisTile(DebrisType.A), new EmptyTile()),
                Row(new BlockerTile(BlockerType.Crate, 1, null), new FloodedTile(), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile()));
            GameState state = CreateSpawnState(board, assistanceChance: 1.0d) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(0, 0), Extracted: false, OneClearAway: false)),
            };

            UrgentRoute route = SpawnOps.FindUrgentRoute(state)!.Value;

            Assert.That(route.HardRouteCells, Is.EqualTo(new[]
            {
                new TileCoord(0, 1),
                new TileCoord(1, 0),
            }).AsCollection);
            Assert.That(route.SoftRouteCells, Is.EqualTo(new[]
            {
                new TileCoord(0, 0),
                new TileCoord(0, 2),
                new TileCoord(2, 0),
            }).AsCollection);
        }

        [Test]
        public void CandidateGetsReachablePairRouteBonusWhenSpawnCreatesLegalRouteGroup()
        {
            Board board = BuildHardRoutePairBoard();
            GameState state = CreateSpawnState(board, assistanceChance: 1.0d) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 2), Extracted: false, OneClearAway: false)),
            };

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig, new TileCoord(0, 1));

            Assert.That(GetWeight(bias.Weights, DebrisType.B), Is.EqualTo(41.0d));
            Assert.That(GetWeight(bias.Weights, DebrisType.A), Is.EqualTo(16.0d));
        }

        [Test]
        public void HardRouteQualificationWinsOverSoftOnlyQualification()
        {
            GameState hardRouteState = CreateSpawnState(BuildHardRoutePairBoard(), assistanceChance: 1.0d) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 2), Extracted: false, OneClearAway: false)),
            };
            GameState softRouteState = CreateSpawnState(BuildSoftRouteBoard(), assistanceChance: 1.0d) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 3), Extracted: false, OneClearAway: false)),
            };

            RouteAssistResult hardResult = SpawnOps.EvaluateRouteAssist(
                hardRouteState.Board,
                new TileCoord(0, 1),
                DebrisType.B,
                SpawnOps.FindUrgentRoute(hardRouteState)!.Value);
            RouteAssistResult softResult = SpawnOps.EvaluateRouteAssist(
                softRouteState.Board,
                new TileCoord(0, 2),
                DebrisType.B,
                SpawnOps.FindUrgentRoute(softRouteState)!.Value);

            Assert.That(hardResult.PairQuality, Is.EqualTo(RoutePairQuality.Hard));
            Assert.That(softResult.PairQuality, Is.EqualTo(RoutePairQuality.Soft));
        }

        [Test]
        public void CandidateGetsRouteAdjacencyBonusWhenFinalTileLandsInSoftRoute()
        {
            GameState state = CreateSpawnState(BuildSoftRouteBoard(), assistanceChance: 1.0d) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 3), Extracted: false, OneClearAway: false)),
            };

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig, new TileCoord(0, 2));

            Assert.That(GetWeight(bias.Weights, DebrisType.A), Is.EqualTo(16.0d));
            Assert.That(GetWeight(bias.Weights, DebrisType.B), Is.EqualTo(41.0d));
        }

        [Test]
        public void RouteAdjacencyDoesNotApplyForDiagonalOrNonUrgentTargetProximity()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new TargetTile("safe", Extracted: false)),
                Row(new TargetTile("urgent", Extracted: false), new EmptyTile(), new BlockerTile(BlockerType.Crate, 1, null), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()));
            ImmutableArray<TargetState> targets = ImmutableArray.Create(
                new TargetState("safe", new TileCoord(1, 3), Extracted: false, OneClearAway: false),
                new TargetState("urgent", new TileCoord(2, 0), Extracted: false, OneClearAway: false));
            GameState state = CreateSpawnState(board, assistanceChance: 1.0d) with
            {
                Targets = targets,
            };

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig, new TileCoord(1, 1));

            Assert.That(GetWeight(bias.Weights, DebrisType.A), Is.EqualTo(1.0d));
            Assert.That(GetWeight(bias.Weights, DebrisType.B), Is.EqualTo(1.0d));
        }

        [Test]
        public void DockBonusStillOutranksRouteAdjacency()
        {
            GameState state = CreateSpawnState(
                BuildSoftRouteBoard(),
                assistanceChance: 1.0d,
                dock: new Dock(
                    ImmutableArray.Create<DebrisType?>(DebrisType.C, DebrisType.C, null, null, null, null, null),
                    Size: 7)) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 3), Extracted: false, OneClearAway: false)),
            };

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig, new TileCoord(0, 2));

            Assert.That(GetWeight(bias.Weights, DebrisType.C), Is.GreaterThan(GetWeight(bias.Weights, DebrisType.A)));
        }

        [Test]
        public void SingletonRecoveryStillOutranksRouteBasedHelp()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new DebrisTile(DebrisType.B), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 1, null), new TargetTile("target", Extracted: false), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()));
            GameState state = CreateSpawnState(board, assistanceChance: 1.0d) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 2), Extracted: false, OneClearAway: false)),
                SpawnRecoveryCounter = 2,
            };

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig, new TileCoord(0, 1));

            Assert.That(GetWeight(bias.Weights, DebrisType.B), Is.EqualTo(141.0d));
            Assert.That(GetWeight(bias.Weights, DebrisType.A), Is.EqualTo(16.0d));
        }

        [Test]
        public void ThreatenedTargetOneRiseFromFloodIsSelectedAsUrgent()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                Row(new TargetTile("safe", Extracted: false), new EmptyTile(), new EmptyTile()),
                Row(new TargetTile("urgent", Extracted: false), new DebrisTile(DebrisType.A), new EmptyTile()),
                Row(new FloodedTile(), new FloodedTile(), new FloodedTile()));
            ImmutableArray<TargetState> targets = ImmutableArray.Create(
                new TargetState("safe", new TileCoord(1, 0), Extracted: false, OneClearAway: false),
                new TargetState("urgent", new TileCoord(2, 0), Extracted: false, OneClearAway: false));
            GameState state = CreateSpawnState(board, assistanceChance: 1.0d) with
            {
                Targets = targets,
                Water = new WaterState(FloodedRows: 1, ActionsUntilRise: 2, RiseInterval: 2),
            };

            UrgentRoute route = SpawnOps.FindUrgentRoute(state)!.Value;

            Assert.That(route.TargetCoord, Is.EqualTo(new TileCoord(2, 0)));
            Assert.That(route.WaterRisesRemaining, Is.EqualTo(0));
        }

        [Test]
        public void LastMoveRescueBoardGetsUsefulRouteBiasWithoutHardForcingSingleAnswer()
        {
            GameState state = CreateSpawnState(BuildSoftRouteBoard(), assistanceChance: 1.0d) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 3), Extracted: false, OneClearAway: false)),
            };

            SpawnBias bias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig, new TileCoord(0, 2));

            Assert.That(GetWeight(bias.Weights, DebrisType.B), Is.GreaterThan(GetWeight(bias.Weights, DebrisType.A)));
            Assert.That(GetWeight(bias.Weights, DebrisType.A), Is.GreaterThan(0.0d));
            Assert.That(GetWeight(bias.Weights, DebrisType.C), Is.GreaterThan(0.0d));
        }

        [Test]
        public void MultiTargetBoardKeepsStableTieBreakingAcrossRepeatedRuns()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                Row(new TargetTile("left", Extracted: false), new EmptyTile(), new TargetTile("right", Extracted: false)),
                Row(new EmptyTile(), new DebrisTile(DebrisType.A), new EmptyTile()));
            ImmutableArray<TargetState> targets = ImmutableArray.Create(
                new TargetState("right", new TileCoord(1, 2), Extracted: false, OneClearAway: false),
                new TargetState("left", new TileCoord(1, 0), Extracted: false, OneClearAway: false));
            GameState state = CreateSpawnState(board, assistanceChance: 1.0d) with
            {
                Targets = targets,
            };

            UrgentRoute first = SpawnOps.FindUrgentRoute(state)!.Value;
            UrgentRoute second = SpawnOps.FindUrgentRoute(state)!.Value;

            Assert.That(first.TargetCoord, Is.EqualTo(new TileCoord(1, 0)));
            Assert.That(second.TargetCoord, Is.EqualTo(first.TargetCoord));
        }

        [Test]
        public void SameSeedAndStateProduceSameSpawn()
        {
            GameState state = CreateSpawnState(
                BuildSoftRouteBoard(),
                assistanceChance: 1.0d)
                with
                {
                    Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 3), Extracted: false, OneClearAway: false)),
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

        [Test]
        public void DescribeSpawnedPiece_ReportsBaselineWhenNoSpecificBonusApplies()
        {
            GameState state = CreateSpawnState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                assistanceChance: 0.5d);

            SpawnedPiece piece = SpawnOps.DescribeSpawnedPiece(state, new TileCoord(0, 0), DebrisType.A, lineageId: 7);

            Assert.That(piece.LineageId, Is.EqualTo(7));
            Assert.That(piece.Reasons, Is.EqualTo(new[] { SpawnAssistReason.BaselineAssistance }).AsCollection);
        }

        [Test]
        public void DescribeSpawnedPiece_ReportsDockAndEmergencyReasons()
        {
            GameState state = CreateSpawnState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                assistanceChance: 0.3d,
                dock: new Dock(
                    ImmutableArray.Create<DebrisType?>(
                        DebrisType.C,
                        DebrisType.C,
                        DebrisType.A,
                        DebrisType.B,
                        DebrisType.D,
                        null,
                        null),
                    Size: 7));

            SpawnedPiece piece = SpawnOps.DescribeSpawnedPiece(state, new TileCoord(0, 0), DebrisType.C, lineageId: 1);

            Assert.That(piece.Reasons, Has.Member(SpawnAssistReason.DockCompletion));
            Assert.That(piece.Reasons, Has.Member(SpawnAssistReason.EmergencyDockPressure));
            Assert.That(piece.EmergencyRequested, Is.True);
            Assert.That(piece.EmergencyApplied, Is.True);
        }

        [Test]
        public void DescribeSpawnedPiece_ReportsWaterAndDebugReasons()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile(), new EmptyTile()),
                Row(new TargetTile("target", Extracted: false), new EmptyTile()));
            GameState state = CreateSpawnState(board, assistanceChance: 0.3d) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 0), Extracted: false, OneClearAway: false)),
                DebugSpawnOverride = new SpawnOverride(ForceEmergency: null, OverrideAssistanceChance: null),
            };

            SpawnedPiece piece = SpawnOps.DescribeSpawnedPiece(state, new TileCoord(0, 0), DebrisType.A, lineageId: 1);

            Assert.That(piece.Reasons, Has.Member(SpawnAssistReason.EmergencyWaterPressure));
            Assert.That(piece.Reasons, Has.Member(SpawnAssistReason.DebugOverride));
            Assert.That(piece.UrgentTargetId, Is.EqualTo("target"));
        }

        [Test]
        public void DescribeSpawnedPiece_ReportsRouteReasonPrecision()
        {
            GameState hardRouteState = CreateSpawnState(BuildHardRoutePairBoard(), assistanceChance: 1.0d) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 2), Extracted: false, OneClearAway: false)),
            };
            GameState softRouteState = CreateSpawnState(BuildSoftRouteBoard(), assistanceChance: 1.0d) with
            {
                Targets = ImmutableArray.Create(new TargetState("target", new TileCoord(1, 3), Extracted: false, OneClearAway: false)),
            };

            SpawnedPiece hard = SpawnOps.DescribeSpawnedPiece(hardRouteState, new TileCoord(0, 1), DebrisType.B, lineageId: 1);
            SpawnedPiece soft = SpawnOps.DescribeSpawnedPiece(softRouteState, new TileCoord(0, 2), DebrisType.B, lineageId: 2);
            SpawnedPiece adjacent = SpawnOps.DescribeSpawnedPiece(softRouteState, new TileCoord(0, 2), DebrisType.A, lineageId: 3);

            Assert.That(hard.Reasons, Has.Member(SpawnAssistReason.RouteHardPair));
            Assert.That(soft.Reasons, Has.Member(SpawnAssistReason.RouteSoftPair));
            Assert.That(adjacent.Reasons, Has.Member(SpawnAssistReason.RouteAdjacency));
        }

        [Test]
        public void DescribeSpawnedPiece_ReportsSingletonRecoveryReason()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile()),
                Row(new DebrisTile(DebrisType.B)));
            GameState state = CreateSpawnState(board, assistanceChance: 0.0d) with
            {
                SpawnRecoveryCounter = 2,
            };

            SpawnedPiece piece = SpawnOps.DescribeSpawnedPiece(state, new TileCoord(0, 0), DebrisType.B, lineageId: 1);

            Assert.That(piece.Reasons, Has.Member(SpawnAssistReason.SingletonRecovery));
        }

        [Test]
        public void Step08SpawnAssignsLineageSidecarWithoutChangingSpawnedType()
        {
            GameState state = CreateSpawnState(
                PipelineTestFixtures.CreateBoard(PipelineTestFixtures.EmptyRow(1)),
                assistanceChance: 1.0d)
                with
                {
                    RngState = new RngState(0x12345678u, 0x9ABCDEF0u),
                };

            StepResult result = Step08_Spawn.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            Spawned spawned = (Spawned)result.Events[0];
            SpawnedPiece piece = spawned.Pieces[0];

            Assert.That(piece.LineageId, Is.EqualTo(1));
            Assert.That(result.State.NextSpawnLineageId, Is.EqualTo(2));
            Assert.That(result.State.SpawnLineageByCoord[new TileCoord(0, 0)].LineageId, Is.EqualTo(1));
            Assert.That(result.State.SpawnLineageByCoord[new TileCoord(0, 0)].Type, Is.EqualTo(piece.Type));
        }

        [Test]
        public void GroupRemovalCarriesAndRemovesSpawnLineageIds()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A)));
            GameState state = CreateSpawnState(board, assistanceChance: 0.0d) with
            {
                SpawnLineageByCoord = ImmutableDictionary<TileCoord, SpawnLineage>.Empty
                    .Add(new TileCoord(0, 0), new SpawnLineage(4, DebrisType.A, new TileCoord(0, 0)))
                    .Add(new TileCoord(0, 1), new SpawnLineage(5, DebrisType.A, new TileCoord(0, 1))),
            };
            StepResult accepted = Step01_AcceptInput.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            StepResult removed = Step02_RemoveGroup.Run(accepted.State, accepted.Context);
            GroupRemoved ev = (GroupRemoved)removed.Events[0];

            Assert.That(ev.SpawnLineageIds, Is.EqualTo(new[] { 4, 5 }).AsCollection);
            Assert.That(removed.State.SpawnLineageByCoord, Is.Empty);
        }

        [Test]
        public void GravityMovesSpawnLineageWithDebris()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new DebrisTile(DebrisType.A)),
                Row(new EmptyTile()));
            GameState state = CreateSpawnState(board, assistanceChance: 0.0d) with
            {
                SpawnLineageByCoord = ImmutableDictionary<TileCoord, SpawnLineage>.Empty
                    .Add(new TileCoord(0, 0), new SpawnLineage(8, DebrisType.A, new TileCoord(0, 0))),
            };

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(result.State.SpawnLineageByCoord.ContainsKey(new TileCoord(0, 0)), Is.False);
            Assert.That(result.State.SpawnLineageByCoord[new TileCoord(1, 0)].LineageId, Is.EqualTo(8));
        }

        [Test]
        public void WaterOverwriteRemovesSpawnLineage()
        {
            Board board = PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile()),
                Row(new DebrisTile(DebrisType.A)));
            GameState state = CreateSpawnState(board, assistanceChance: 0.0d) with
            {
                SpawnLineageByCoord = ImmutableDictionary<TileCoord, SpawnLineage>.Empty
                    .Add(new TileCoord(1, 0), new SpawnLineage(9, DebrisType.A, new TileCoord(1, 0))),
            };
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0))) with
            {
                WaterRisePending = true,
            };

            StepResult result = Step12_ResolveHazards.Run(state, context);

            Assert.That(result.State.SpawnLineageByCoord.ContainsKey(new TileCoord(1, 0)), Is.False);
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

        private static Board BuildHardRoutePairBoard()
        {
            return PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile(), new EmptyTile(), new DebrisTile(DebrisType.B), new EmptyTile()),
                Row(new EmptyTile(), new DebrisTile(DebrisType.B), new TargetTile("target", Extracted: false), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()));
        }

        private static Board BuildSoftRouteBoard()
        {
            return PipelineTestFixtures.CreateBoard(
                Row(new EmptyTile(), new DebrisTile(DebrisType.B), new EmptyTile(), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new BlockerTile(BlockerType.Crate, 1, null), new TargetTile("target", Extracted: false)),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()));
        }

        private static ImmutableArray<Tile> Row(params Tile[] tiles)
        {
            return tiles.ToImmutableArray();
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
