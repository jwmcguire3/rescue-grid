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
    }
}
