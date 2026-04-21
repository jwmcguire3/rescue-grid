using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;
using Rescue.Core.Undo;

namespace Rescue.Core.Tests.Undo
{
    public sealed class SnapshotTests
    {
        [Test]
        public void TakeThenApplyAfterPipelineMutationRestoresEqualOriginalState()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B),
                    PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.D, DebrisType.B),
                    PipelineTestFixtures.EmptyRow(3)));

            Snapshot snapshot = SnapshotHelpers.Take(original);
            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(original, new ActionInput(new TileCoord(0, 0)));
            GameState restored = SnapshotHelpers.Apply(snapshot);

            Assert.That(ReferenceEquals(snapshot.CapturedState, original), Is.True);
            Assert.That(result.State, Is.Not.EqualTo(original));
            Assert.That(restored, Is.EqualTo(original));
            Assert.That(ReferenceEquals(restored, original), Is.True);
            Assert.That(BoardHelpers.GetTile(original.Board, new TileCoord(0, 0)), Is.EqualTo(new DebrisTile(DebrisType.A)));
        }

        [Test]
        public void ApplyWithoutInterveningMutationIsNoOp()
        {
            GameState original = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A),
                    PipelineTestFixtures.DebrisRow(DebrisType.B, DebrisType.C)));

            Snapshot snapshot = SnapshotHelpers.Take(original);
            GameState restored = SnapshotHelpers.Apply(snapshot);

            Assert.That(restored, Is.EqualTo(original));
            Assert.That(ReferenceEquals(restored, original), Is.True);
        }
    }
}
