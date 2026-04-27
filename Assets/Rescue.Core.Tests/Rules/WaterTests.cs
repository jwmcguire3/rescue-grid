using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Pipeline.Steps;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Rules
{
    public sealed class WaterTests
    {
        [Test]
        public void CounterTicksDownByOnePerAction()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A)),
                    Row(new EmptyTile(), new EmptyTile())),
                actionsUntilRise: 3);

            StepResult result = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(result.State.Water.ActionsUntilRise, Is.EqualTo(2));
            Assert.That(result.State.Water.FloodedRows, Is.EqualTo(0));
            Assert.That(result.Context.WaterRisePending, Is.False);
            Assert.That(result.Events, Is.Empty);
        }

        [Test]
        public void AtCounterZeroWaterRisesOneRowAndCounterResets()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.B)),
                    Row(new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.D))),
                actionsUntilRise: 1,
                riseInterval: 4);

            StepResult tick = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            StepResult resolve = Step12_ResolveHazards.Run(tick.State, tick.Context);

            Assert.That(tick.Context.WaterRisePending, Is.True);
            Assert.That(resolve.State.Water.FloodedRows, Is.EqualTo(1));
            Assert.That(resolve.State.Water.ActionsUntilRise, Is.EqualTo(4));
            Assert.That(resolve.Events, Is.EqualTo(new ActionEvent[]
            {
                new WaterRose(1),
            }).AsCollection);
            AssertFloodedRow(resolve.State.Board, 1);
        }

        [Test]
        public void FloodedRowTilesBecomeFloodedAndUnextractedTargetsRemainAddressable()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(
                        new DebrisTile(DebrisType.A),
                        new BlockerTile(BlockerType.Crate, 1, Hidden: null),
                        new TargetTile("target", Extracted: false),
                        new EmptyTile())),
                targets: ImmutableArray.Create(new TargetState("target", new TileCoord(1, 2), Extracted: false, OneClearAway: false)),
                actionsUntilRise: 1);

            StepResult tick = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            StepResult resolve = Step12_ResolveHazards.Run(tick.State, tick.Context);

            Assert.That(BoardHelpers.GetTile(resolve.State.Board, new TileCoord(1, 0)), Is.TypeOf<FloodedTile>());
            Assert.That(BoardHelpers.GetTile(resolve.State.Board, new TileCoord(1, 1)), Is.TypeOf<FloodedTile>());
            Assert.That(BoardHelpers.GetTile(resolve.State.Board, new TileCoord(1, 2)), Is.EqualTo(new TargetTile("target", Extracted: false)));
            Assert.That(BoardHelpers.GetTile(resolve.State.Board, new TileCoord(1, 3)), Is.TypeOf<FloodedTile>());
        }

        [Test]
        public void WaterWarningFiresWhenNextActionWillRiseWater()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A)),
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile())),
                actionsUntilRise: 2);

            StepResult result = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(result.State.Water.ActionsUntilRise, Is.EqualTo(1));
            Assert.That(result.Context.WaterRisePending, Is.False);
            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new WaterWarning(1, 2),
            }).AsCollection);
        }

        [Test]
        public void WaterForecastReportsNextFloodRowUntilBoardIsFull()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile())),
                floodedRows: 1,
                actionsUntilRise: 2);

            Assert.That(WaterHelpers.GetNextFloodRow(state.Board, state.Water), Is.EqualTo(1));
            Assert.That(WaterHelpers.HasForecast(state.Board, state.Water), Is.True);

            WaterState fullyFlooded = state.Water with { FloodedRows = state.Board.Height };
            Assert.That(WaterHelpers.GetNextFloodRow(state.Board, fullyFlooded), Is.Null);
            Assert.That(WaterHelpers.HasForecast(state.Board, fullyFlooded), Is.False);
        }

        [Test]
        public void RuleTeachLevelStartsTickingOnFirstValidAction()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A)),
                    Row(new EmptyTile(), new EmptyTile())),
                actionsUntilRise: 1,
                riseInterval: 3) with
            {
                Water = new WaterState(
                    FloodedRows: 0,
                    ActionsUntilRise: 1,
                    RiseInterval: 3,
                    PauseUntilFirstAction: true),
                LevelConfig = PipelineTestFixtures.CreateLevelConfig() with { IsRuleTeach = true },
            };

            StepResult tick = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            StepResult resolve = Step12_ResolveHazards.Run(tick.State, tick.Context);

            Assert.That(tick.State.Water.PauseUntilFirstAction, Is.False);
            Assert.That(tick.Context.WaterRisePending, Is.True);
            Assert.That(resolve.State.Water.FloodedRows, Is.EqualTo(1));
            Assert.That(resolve.Events, Has.Some.EqualTo(new WaterRose(1)));
        }

        [Test]
        public void RuleTeachAfterFirstActionUsesNormalWaterTicking()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A)),
                    Row(new EmptyTile(), new EmptyTile())),
                actionsUntilRise: 3,
                riseInterval: 3) with
            {
                Water = new WaterState(
                    FloodedRows: 0,
                    ActionsUntilRise: 3,
                    RiseInterval: 3,
                    PauseUntilFirstAction: false),
                LevelConfig = PipelineTestFixtures.CreateLevelConfig() with { IsRuleTeach = true },
            };

            StepResult tick = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(tick.State.Water.ActionsUntilRise, Is.EqualTo(2));
            Assert.That(tick.Context.WaterRisePending, Is.False);
        }

        [Test]
        public void TargetInFloodedRowIsLost()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A)),
                    Row(new TargetTile("target", Extracted: false), new EmptyTile())),
                targets: ImmutableArray.Create(new TargetState("target", new TileCoord(1, 0), Extracted: false, OneClearAway: false)),
                actionsUntilRise: 1);

            StepResult tick = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            StepResult resolve = Step12_ResolveHazards.Run(tick.State, tick.Context);
            CheckLossResult loss = WaterTargetConsequence.Run(resolve.State, resolve.Context);

            Assert.That(loss.Outcome, Is.EqualTo(ActionOutcome.LossWaterOnTarget));
            Assert.That(loss.Events, Is.EqualTo(new ActionEvent[]
            {
                new Lost(ActionOutcome.LossWaterOnTarget),
            }).AsCollection);
        }

        [Test]
        public void TargetThatExtractsOnSameActionWaterRisesIsSaved()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(
                        new BlockerTile(BlockerType.Crate, 2, Hidden: null),
                        new BlockerTile(BlockerType.Crate, 2, Hidden: null),
                        new TargetTile("safe", Extracted: false),
                        new EmptyTile()),
                    Row(
                        new DebrisTile(DebrisType.A),
                        new DebrisTile(DebrisType.A),
                        new DebrisTile(DebrisType.B),
                        new DebrisTile(DebrisType.B)),
                    Row(
                        new TargetTile("urgent", Extracted: false),
                        new EmptyTile(),
                        new EmptyTile(),
                        new EmptyTile())),
                targets: ImmutableArray.Create(
                    new TargetState("safe", new TileCoord(0, 2), Extracted: false, OneClearAway: false),
                    new TargetState("urgent", new TileCoord(2, 0), Extracted: false, OneClearAway: false)),
                actionsUntilRise: 1);

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(1, 0)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Ok));
            Assert.That(result.State.Targets[1].Extracted, Is.True);
            Assert.That(result.Events, Has.Some.EqualTo(new TargetExtracted("urgent", new TileCoord(2, 0))));
            Assert.That(result.Events, Has.Some.EqualTo(new WaterRose(2)));
            Assert.That(result.Events, Has.None.TypeOf<Lost>());
        }

        [Test]
        public void InitialFloodedRowsOnLevelLoadAreRespected()
        {
            GameState state = CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.B)),
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new FloodedTile(), new FloodedTile())),
                floodedRows: 1,
                actionsUntilRise: 1,
                riseInterval: 3);

            StepResult tick = Step11_TickHazards.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            StepResult resolve = Step12_ResolveHazards.Run(tick.State, tick.Context);

            Assert.That(resolve.State.Water.FloodedRows, Is.EqualTo(2));
            AssertFloodedRow(resolve.State.Board, 1);
            AssertFloodedRow(resolve.State.Board, 2);
            Assert.That(resolve.Events, Is.EqualTo(new ActionEvent[]
            {
                new WaterRose(1),
            }).AsCollection);
        }

        private static GameState CreateState(
            Board board,
            ImmutableArray<TargetState>? targets = null,
            int floodedRows = 0,
            int actionsUntilRise = 3,
            int riseInterval = 3)
        {
            return PipelineTestFixtures.CreateState(board, targets) with
            {
                Water = new WaterState(
                    FloodedRows: floodedRows,
                    ActionsUntilRise: actionsUntilRise,
                    RiseInterval: riseInterval),
            };
        }

        private static void AssertFloodedRow(Board board, int row)
        {
            for (int col = 0; col < board.Width; col++)
            {
                Assert.That(BoardHelpers.GetTile(board, new TileCoord(row, col)), Is.TypeOf<FloodedTile>());
            }
        }

        private static ImmutableArray<Tile> Row(params Tile[] tiles)
        {
            return tiles.ToImmutableArray();
        }
    }
}
