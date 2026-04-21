using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.State;

namespace Rescue.Core.Tests.Pipeline
{
    public sealed class InvalidInputTests
    {
        [Test]
        public void TapOnSingleTileLeavesStateUnchangedAndEmitsInvalidInput()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.B)));

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new Rescue.Core.Pipeline.ActionInput(new TileCoord(0, 0)));

            Assert.That(result.State, Is.EqualTo(state));
            Assert.That(result.Events, Is.EqualTo(ImmutableArray.Create<Rescue.Core.Pipeline.ActionEvent>(
                new Rescue.Core.Pipeline.InvalidInput(new TileCoord(0, 0), Rescue.Core.Pipeline.InvalidInputReason.SingleTile))));
            Assert.That(result.State.ActionCount, Is.EqualTo(state.ActionCount));
        }

        [Test]
        public void TapOnBlockerIsInvalid()
        {
            AssertInvalidTap(
                board: PipelineTestFixtures.CreateBoard(
                    ImmutableArray.Create<Tile>(
                        new BlockerTile(BlockerType.Crate, 1, Hidden: null),
                        new DebrisTile(DebrisType.A))),
                tappedCoord: new TileCoord(0, 0),
                expectedReason: Rescue.Core.Pipeline.InvalidInputReason.Blocker);
        }

        [Test]
        public void TapOnFloodedTileIsInvalid()
        {
            AssertInvalidTap(
                board: PipelineTestFixtures.CreateBoard(
                    ImmutableArray.Create<Tile>(
                        new DebrisTile(DebrisType.A),
                        new FloodedTile())),
                tappedCoord: new TileCoord(0, 1),
                expectedReason: Rescue.Core.Pipeline.InvalidInputReason.Flooded);
        }

        [Test]
        public void TapOnIceIsInvalid()
        {
            AssertInvalidTap(
                board: PipelineTestFixtures.CreateBoard(
                    ImmutableArray.Create<Tile>(
                        new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.A)),
                        new DebrisTile(DebrisType.A))),
                tappedCoord: new TileCoord(0, 0),
                expectedReason: Rescue.Core.Pipeline.InvalidInputReason.Ice);
        }

        [Test]
        public void TapOnTargetIsInvalid()
        {
            AssertInvalidTap(
                board: PipelineTestFixtures.CreateBoard(
                    ImmutableArray.Create<Tile>(
                        new TargetTile("target-1", Extracted: false),
                        new DebrisTile(DebrisType.A))),
                tappedCoord: new TileCoord(0, 0),
                expectedReason: Rescue.Core.Pipeline.InvalidInputReason.Target);
        }

        private static void AssertInvalidTap(
            Board board,
            TileCoord tappedCoord,
            Rescue.Core.Pipeline.InvalidInputReason expectedReason)
        {
            GameState state = PipelineTestFixtures.CreateState(board);
            List<string> trace = new List<string>();

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(
                state,
                new Rescue.Core.Pipeline.ActionInput(tappedCoord),
                options: null,
                observer: step => trace.Add(step.StepName));

            Assert.That(result.State, Is.EqualTo(state));
            Assert.That(result.Events, Is.EqualTo(ImmutableArray.Create<Rescue.Core.Pipeline.ActionEvent>(
                new Rescue.Core.Pipeline.InvalidInput(tappedCoord, expectedReason))));
            Assert.That(result.State.ActionCount, Is.EqualTo(state.ActionCount));
            Assert.That(trace, Is.EqualTo(new[] { "Step01_AcceptInput" }));
        }
    }
}
