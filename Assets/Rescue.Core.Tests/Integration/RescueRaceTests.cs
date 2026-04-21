using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Integration
{
    public sealed class RescueRaceTests
    {
        [Test]
        public void LastPossibleActionExtractsTargetBeforeWaterLossAndWinsLevel()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(
                        new BlockerTile(BlockerType.Crate, 2, Hidden: null),
                        new BlockerTile(BlockerType.Crate, 2, Hidden: null),
                        new DebrisTile(DebrisType.B)),
                    Row(
                        new DebrisTile(DebrisType.A),
                        new DebrisTile(DebrisType.A),
                        new DebrisTile(DebrisType.C)),
                    Row(
                        new TargetTile("urgent", Extracted: false),
                        new EmptyTile(),
                        new EmptyTile())),
                targets: ImmutableArray.Create(new TargetState("urgent", new TileCoord(2, 0), Extracted: false, OneClearAway: false)))
                with
                {
                    Water = new WaterState(FloodedRows: 0, ActionsUntilRise: 1, RiseInterval: 3),
                };

            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(state, new ActionInput(new TileCoord(1, 0)));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(result.State.Targets[0].Extracted, Is.True);
            Assert.That(result.Events, Has.Some.EqualTo(new TargetExtracted("urgent", new TileCoord(2, 0))));
            Assert.That(result.Events, Has.Some.TypeOf<Won>());
            Assert.That(result.Events, Has.None.TypeOf<WaterRose>());
            Assert.That(result.Events, Has.None.TypeOf<Lost>());
        }

        private static ImmutableArray<Tile> Row(params Tile[] tiles)
        {
            return tiles.ToImmutableArray();
        }
    }
}
