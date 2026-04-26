using NUnit.Framework;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class ActionPlaybackSettingsTests
    {
        [Test]
        public void ActionPlaybackSettings_ExposesCentralizedDurations()
        {
            ActionPlaybackSettings settings = new ActionPlaybackSettings();

            Assert.That(settings.RemoveDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.BreakBlockerOrRevealDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.DockFeedbackDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.DockInsertFeedbackDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.DockClearFeedbackDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.DockWarningCautionDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.DockWarningAcuteDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.DockJamFeedbackDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.GravityDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.SpawnDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.TargetExtractDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.WaterRiseDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.WaterForecastTransitionDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.WaterForecastPulseDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.WaterlinePulseDurationSeconds, Is.GreaterThan(0f));
        }

        [Test]
        public void ActionPlaybackSettings_DefaultsStayWithinIntendedRanges()
        {
            ActionPlaybackSettings settings = new ActionPlaybackSettings();

            Assert.That(settings.RemoveDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultRemoveDurationSeconds));
            Assert.That(settings.BreakBlockerOrRevealDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultBreakBlockerOrRevealDurationSeconds));
            Assert.That(settings.DockInsertFeedbackDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultDockInsertFeedbackDurationSeconds));
            Assert.That(settings.DockClearFeedbackDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultDockClearFeedbackDurationSeconds));
            Assert.That(settings.DockWarningCautionDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultDockWarningCautionDurationSeconds));
            Assert.That(settings.DockWarningAcuteDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultDockWarningAcuteDurationSeconds));
            Assert.That(settings.DockJamFeedbackDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultDockJamFeedbackDurationSeconds));
            Assert.That(settings.GravityDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultGravityDurationSeconds));
            Assert.That(settings.SpawnDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultSpawnDurationSeconds));
            Assert.That(settings.TargetExtractDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultTargetExtractDurationSeconds));
            Assert.That(settings.WaterRiseDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultWaterRiseDurationSeconds));
            Assert.That(settings.WaterForecastTransitionDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultWaterForecastTransitionDurationSeconds));
            Assert.That(settings.WaterForecastPulseDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultWaterForecastPulseDurationSeconds));
            Assert.That(settings.WaterlinePulseDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultWaterlinePulseDurationSeconds));
            Assert.That(settings.RemoveDurationSeconds, Is.InRange(0.08f, 0.12f));
            Assert.That(settings.BreakBlockerOrRevealDurationSeconds, Is.InRange(0.08f, 0.12f));
            Assert.That(settings.DockInsertFeedbackDurationSeconds, Is.InRange(0.06f, 0.10f));
            Assert.That(settings.DockClearFeedbackDurationSeconds, Is.InRange(0.06f, 0.10f));
            Assert.That(settings.GravityDurationSeconds, Is.InRange(0.12f, 0.20f));
            Assert.That(settings.SpawnDurationSeconds, Is.InRange(0.10f, 0.16f));
            Assert.That(settings.TargetExtractDurationSeconds, Is.InRange(0.12f, 0.20f));
            Assert.That(settings.WaterRiseDurationSeconds, Is.InRange(0.12f, 0.20f));
        }
    }
}
