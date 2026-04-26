using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;

namespace Rescue.Unity.Water.Tests
{
    public sealed class WaterFeedbackResolverTests
    {
        [Test]
        public void WaterFeedbackResolver_DetectsFloodRise()
        {
            WaterFeedbackResolution resolution = WaterFeedbackResolver.Resolve(
                boardHeight: 7,
                previous: new WaterState(FloodedRows: 1, ActionsUntilRise: 2, RiseInterval: 5),
                current: new WaterState(FloodedRows: 2, ActionsUntilRise: 5, RiseInterval: 5));

            Assert.That(resolution.HasWaterRise, Is.True);
            Assert.That(resolution.NewlyFloodedRowIndices, Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void WaterFeedbackResolver_DoesNotDetectRiseWhenSameRows()
        {
            WaterFeedbackResolution resolution = WaterFeedbackResolver.Resolve(
                boardHeight: 7,
                previous: new WaterState(FloodedRows: 1, ActionsUntilRise: 2, RiseInterval: 5),
                current: new WaterState(FloodedRows: 1, ActionsUntilRise: 1, RiseInterval: 5));

            Assert.That(resolution.HasWaterRise, Is.False);
            Assert.That(resolution.NewlyFloodedRowIndices, Is.Empty);
        }

        [Test]
        public void WaterFeedbackResolver_DetectsNearRiseWarning()
        {
            WaterFeedbackResolution resolution = WaterFeedbackResolver.Resolve(
                boardHeight: 7,
                previous: new WaterState(FloodedRows: 1, ActionsUntilRise: 2, RiseInterval: 5),
                current: new WaterState(FloodedRows: 1, ActionsUntilRise: 1, RiseInterval: 5));

            Assert.That(resolution.HasNearRiseWarning, Is.True);
            Assert.That(resolution.ShouldPulseWaterline, Is.True);
        }
    }
}
