using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;

namespace Rescue.Unity.Water.Tests
{
    public sealed class WaterRowResolverTests
    {
        [Test]
        public void WaterRowResolver_ReturnsNoFloodedRowsWhenZero()
        {
            WaterRowResolution resolution = WaterRowResolver.Resolve(
                boardHeight: 7,
                new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 5));

            Assert.That(resolution.FloodedRowIndices, Is.Empty);
        }

        [Test]
        public void WaterRowResolver_ReturnsBottomRowsForFloodedCount()
        {
            WaterRowResolution resolution = WaterRowResolver.Resolve(
                boardHeight: 7,
                new WaterState(FloodedRows: 2, ActionsUntilRise: 3, RiseInterval: 5));

            Assert.That(resolution.FloodedRowIndices, Is.EqualTo(new[] { 5, 6 }));
        }

        [Test]
        public void WaterRowResolver_ReturnsForecastRowAboveFloodedRows()
        {
            WaterRowResolution resolution = WaterRowResolver.Resolve(
                boardHeight: 7,
                new WaterState(FloodedRows: 2, ActionsUntilRise: 3, RiseInterval: 5));

            Assert.That(resolution.HasForecastRow, Is.True);
            Assert.That(resolution.ForecastRowIndex, Is.EqualTo(4));
        }

        [Test]
        public void WaterRowResolver_NoForecastWhenBoardFullyFlooded()
        {
            WaterRowResolution resolution = WaterRowResolver.Resolve(
                boardHeight: 7,
                new WaterState(FloodedRows: 7, ActionsUntilRise: 0, RiseInterval: 5));

            Assert.That(resolution.HasForecastRow, Is.False);
            Assert.That(resolution.ForecastRowIndex, Is.EqualTo(-1));
        }

        [Test]
        public void WaterRowResolver_ForecastFillStartsAtOneStepForFullCountdown()
        {
            WaterRowResolution resolution = WaterRowResolver.Resolve(
                boardHeight: 7,
                new WaterState(FloodedRows: 0, ActionsUntilRise: 12, RiseInterval: 12));

            Assert.That(resolution.ForecastFillFraction, Is.EqualTo(1f / 12f).Within(0.0001f));
        }

        [Test]
        public void WaterRowResolver_ForecastFillAdvancesByElapsedActions()
        {
            WaterRowResolution resolution = WaterRowResolver.Resolve(
                boardHeight: 7,
                new WaterState(FloodedRows: 0, ActionsUntilRise: 7, RiseInterval: 12));

            Assert.That(resolution.ForecastFillFraction, Is.EqualTo(6f / 12f).Within(0.0001f));
        }

        [Test]
        public void WaterRowResolver_ForecastFillIsFullOnNextActionWarning()
        {
            WaterRowResolution resolution = WaterRowResolver.Resolve(
                boardHeight: 7,
                new WaterState(FloodedRows: 0, ActionsUntilRise: 1, RiseInterval: 12));

            Assert.That(resolution.ForecastFillFraction, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void WaterRowResolver_ForecastFillIsZeroWhenPausedDisabledOrUnavailable()
        {
            WaterRowResolution paused = WaterRowResolver.Resolve(
                boardHeight: 7,
                new WaterState(FloodedRows: 0, ActionsUntilRise: 12, RiseInterval: 12, PauseUntilFirstAction: true));
            WaterRowResolution disabled = WaterRowResolver.Resolve(
                boardHeight: 7,
                new WaterState(FloodedRows: 0, ActionsUntilRise: 0, RiseInterval: 0));
            WaterRowResolution unavailable = WaterRowResolver.Resolve(
                boardHeight: 7,
                new WaterState(FloodedRows: 7, ActionsUntilRise: 1, RiseInterval: 12));

            Assert.That(paused.ForecastFillFraction, Is.EqualTo(0f));
            Assert.That(disabled.ForecastFillFraction, Is.EqualTo(0f));
            Assert.That(unavailable.ForecastFillFraction, Is.EqualTo(0f));
        }
    }
}
