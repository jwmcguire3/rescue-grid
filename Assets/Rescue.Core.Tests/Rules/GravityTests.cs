using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Pipeline.Steps;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Rules
{
    public sealed class GravityTests
    {
        [Test]
        public void SingleHoleFillsFromAbove()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A)),
                    ImmutableArray.Create<Tile>(new EmptyTile()),
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.B))));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(0, 0)), Is.TypeOf<EmptyTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 0)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 0)), Is.EqualTo(new DebrisTile(DebrisType.B)));
            AssertGravityEventsEqual(
                result.Events,
                (new TileCoord(0, 0), new TileCoord(1, 0)));
        }

        [Test]
        public void FullColumnOfDebrisAboveEmptyShiftsDownCorrectly()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A)),
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.B)),
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.C)),
                    ImmutableArray.Create<Tile>(new EmptyTile()),
                    ImmutableArray.Create<Tile>(new EmptyTile())));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(0, 0)), Is.TypeOf<EmptyTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 0)), Is.TypeOf<EmptyTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 0)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(3, 0)), Is.EqualTo(new DebrisTile(DebrisType.B)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(4, 0)), Is.EqualTo(new DebrisTile(DebrisType.C)));
            AssertGravityEventsEqual(
                result.Events,
                (new TileCoord(2, 0), new TileCoord(4, 0)),
                (new TileCoord(1, 0), new TileCoord(3, 0)),
                (new TileCoord(0, 0), new TileCoord(2, 0)));
        }

        [Test]
        public void FloodedTileActsAsFloorAndDryTilesSettleOnTop()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A)),
                    ImmutableArray.Create<Tile>(new EmptyTile()),
                    ImmutableArray.Create<Tile>(new FloodedTile()),
                    ImmutableArray.Create<Tile>(new EmptyTile())));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(0, 0)), Is.TypeOf<EmptyTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 0)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 0)), Is.TypeOf<FloodedTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(3, 0)), Is.TypeOf<EmptyTile>());
        }

        [Test]
        public void RescuePathTileAllowsDebrisToFallThroughButStaysReserved()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A)),
                    ImmutableArray.Create<Tile>(new RescuePathTile(ImmutableArray.Create("target"))),
                    ImmutableArray.Create<Tile>(new EmptyTile())));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(0, 0)), Is.TypeOf<EmptyTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 0)), Is.TypeOf<RescuePathTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 0)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            AssertGravityEventsEqual(
                result.Events,
                (new TileCoord(0, 0), new TileCoord(2, 0)));
        }

        [Test]
        public void RescuePathTileIsSkippedAsGravityDestinationInMixedColumn()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A)),
                    ImmutableArray.Create<Tile>(new RescuePathTile(ImmutableArray.Create("target"))),
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.B)),
                    ImmutableArray.Create<Tile>(new EmptyTile()),
                    ImmutableArray.Create<Tile>(new EmptyTile())));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(0, 0)), Is.TypeOf<EmptyTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 0)), Is.TypeOf<RescuePathTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 0)), Is.TypeOf<EmptyTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(3, 0)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(4, 0)), Is.EqualTo(new DebrisTile(DebrisType.B)));
            AssertGravityEventsEqual(
                result.Events,
                (new TileCoord(2, 0), new TileCoord(4, 0)),
                (new TileCoord(0, 0), new TileCoord(3, 0)));
        }

        [Test]
        public void GravityMovesSpawnLineageThroughRescuePathTile()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A)),
                    ImmutableArray.Create<Tile>(new RescuePathTile(ImmutableArray.Create("target"))),
                    ImmutableArray.Create<Tile>(new EmptyTile())))
                with
                {
                    SpawnLineageByCoord = ImmutableDictionary<TileCoord, SpawnLineage>.Empty
                        .Add(new TileCoord(0, 0), new SpawnLineage(8, DebrisType.A, new TileCoord(0, 0))),
                };

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(result.State.SpawnLineageByCoord.ContainsKey(new TileCoord(0, 0)), Is.False);
            Assert.That(result.State.SpawnLineageByCoord.ContainsKey(new TileCoord(1, 0)), Is.False);
            Assert.That(result.State.SpawnLineageByCoord[new TileCoord(2, 0)].LineageId, Is.EqualTo(8));
        }

        [Test]
        public void BlockedColumnHoleFillsDiagonally()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Crate, 1, null), new DebrisTile(DebrisType.A)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 1, null))));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 1)), Is.TypeOf<EmptyTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 0)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            AssertGravityEventsEqual(
                result.Events,
                (new TileCoord(1, 1), new TileCoord(2, 0)));
        }

        [Test]
        public void DiagonalSettlingRepeatsUntilStable()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), new DebrisTile(DebrisType.B), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Crate, 1, null), new DebrisTile(DebrisType.A), new BlockerTile(BlockerType.Crate, 1, null)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 1, null), new EmptyTile())));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(3, 0)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(3, 2)), Is.EqualTo(new DebrisTile(DebrisType.B)));
            AssertGravityEventsEqual(
                result.Events,
                (new TileCoord(2, 1), new TileCoord(3, 0)),
                (new TileCoord(1, 1), new TileCoord(2, 1)),
                (new TileCoord(2, 1), new TileCoord(3, 2)));
        }

        [Test]
        public void DiagonalSettlingUsesAboveLeftBeforeAboveRight()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 1, null), new EmptyTile()),
                    Row(new DebrisTile(DebrisType.A), new EmptyTile(), new DebrisTile(DebrisType.B)),
                    Row(new BlockerTile(BlockerType.Crate, 1, null), new EmptyTile(), new BlockerTile(BlockerType.Crate, 1, null))));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 0)), Is.TypeOf<EmptyTile>());
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 1)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 2)), Is.EqualTo(new DebrisTile(DebrisType.B)));
            AssertGravityEventsEqual(
                result.Events,
                (new TileCoord(1, 0), new TileCoord(2, 1)));
        }

        [Test]
        public void DiagonalSettlingDoesNotLandOnRescuePathTile()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Crate, 1, null), new DebrisTile(DebrisType.A)),
                    Row(new RescuePathTile(ImmutableArray.Create("target")), new BlockerTile(BlockerType.Crate, 1, null))));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 1)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 0)), Is.TypeOf<RescuePathTile>());
            Assert.That(result.Events, Is.Empty);
        }

        [Test]
        public void DiagonalSettlingDoesNotLandOnTarget()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Crate, 1, null), new DebrisTile(DebrisType.A)),
                    Row(new TargetTile("target", Extracted: false), new BlockerTile(BlockerType.Crate, 1, null))));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 1)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 0)), Is.TypeOf<TargetTile>());
            Assert.That(result.Events, Is.Empty);
        }

        [Test]
        public void DiagonalSettlingDoesNotLandOnBlocker()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Crate, 1, null), new DebrisTile(DebrisType.A)),
                    Row(new BlockerTile(BlockerType.Vine, 1, null), new BlockerTile(BlockerType.Crate, 1, null))));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 1)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 0)), Is.TypeOf<BlockerTile>());
            Assert.That(result.Events, Is.Empty);
        }

        [Test]
        public void DiagonalSettlingDoesNotLandInFloodedRow()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Crate, 1, null), new DebrisTile(DebrisType.A)),
                    Row(new FloodedTile(), new FloodedTile())))
                with
                {
                    Water = new WaterState(FloodedRows: 1, ActionsUntilRise: 3, RiseInterval: 3),
                };

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 1)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 0)), Is.TypeOf<FloodedTile>());
            Assert.That(result.Events, Is.Empty);
        }

        [Test]
        public void DiagonalSettlingDoesNotMoveHiddenIceContents()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Crate, 1, null), new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.A))),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 1, null))));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(
                BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 1)),
                Is.EqualTo(new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.A))));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 0)), Is.TypeOf<EmptyTile>());
            Assert.That(result.Events, Is.Empty);
        }

        [Test]
        public void DiagonalSettlingDoesNotFillCellVerticalGravityCanFill()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new DebrisTile(DebrisType.B)),
                    Row(new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Crate, 1, null), new EmptyTile())));

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 0)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(2, 1)), Is.EqualTo(new DebrisTile(DebrisType.B)));
            AssertGravityEventsEqual(
                result.Events,
                (new TileCoord(0, 1), new TileCoord(2, 1)));
        }

        [Test]
        public void DiagonalSettlingMovesSpawnLineageWithDebris()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Crate, 1, null), new DebrisTile(DebrisType.A)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 1, null))))
                with
                {
                    SpawnLineageByCoord = ImmutableDictionary<TileCoord, SpawnLineage>.Empty
                        .Add(new TileCoord(1, 1), new SpawnLineage(12, DebrisType.A, new TileCoord(1, 1))),
                };

            StepResult result = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(result.State.SpawnLineageByCoord.ContainsKey(new TileCoord(1, 1)), Is.False);
            Assert.That(result.State.SpawnLineageByCoord[new TileCoord(2, 0)].LineageId, Is.EqualTo(12));
        }

        [Test]
        public void DiagonalGravityIsIdempotentAfterSettling()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile()),
                    Row(new BlockerTile(BlockerType.Crate, 1, null), new DebrisTile(DebrisType.A)),
                    Row(new EmptyTile(), new BlockerTile(BlockerType.Crate, 1, null))));

            StepResult first = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            StepResult second = Step07_Gravity.Run(first.State, StepContext.Create(first.State, new ActionInput(new TileCoord(0, 0))));

            Assert.That(second.State.Board, Is.EqualTo(first.State.Board));
            Assert.That(second.Events, Is.Empty);
        }

        [Test]
        public void GravityIsIdempotent()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    ImmutableArray.Create<Tile>(new EmptyTile()),
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.A)),
                    ImmutableArray.Create<Tile>(new DebrisTile(DebrisType.B))));

            StepResult first = Step07_Gravity.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            StepResult second = Step07_Gravity.Run(first.State, StepContext.Create(first.State, new ActionInput(new TileCoord(0, 0))));

            Assert.That(second.State.Board, Is.EqualTo(first.State.Board));
            Assert.That(second.Events, Is.Empty);
        }

        private static void AssertGravityEventsEqual(
            ImmutableArray<ActionEvent> actual,
            params (TileCoord From, TileCoord To)[] expectedMoves)
        {
            Assert.That(actual.Length, Is.EqualTo(expectedMoves.Length));
            for (int i = 0; i < expectedMoves.Length; i++)
            {
                Assert.That(actual[i], Is.TypeOf<GravitySettled>());
                GravitySettled settled = (GravitySettled)actual[i];
                Assert.That(settled.Moves.Length, Is.EqualTo(1));
                Assert.That(settled.Moves[0].From, Is.EqualTo(expectedMoves[i].From));
                Assert.That(settled.Moves[0].To, Is.EqualTo(expectedMoves[i].To));
            }
        }

        private static ImmutableArray<Tile> Row(params Tile[] tiles)
        {
            return tiles.ToImmutableArray();
        }
    }
}
