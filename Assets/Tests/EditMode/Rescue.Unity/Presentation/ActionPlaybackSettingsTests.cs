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
            Assert.That(settings.BoardPieceLandingSquashXScale, Is.GreaterThanOrEqualTo(1f));
            Assert.That(settings.BoardPieceLandingSquashYScale, Is.LessThanOrEqualTo(1f));
            Assert.That(settings.BoardPieceLandingBounceDistance, Is.GreaterThanOrEqualTo(0f));
            Assert.That(settings.TargetExtractDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.WinFxDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.LossFxDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.WaterRiseDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.WaterForecastTransitionDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.WaterForecastPulseDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.WaterlinePulseDurationSeconds, Is.GreaterThan(0f));
            Assert.That(settings.PlaybackSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.DefaultPlaybackSpeedMultiplier));
            Assert.That(settings.BoardActionSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.DefaultGroupSpeedMultiplier));
            Assert.That(settings.DockSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.DefaultGroupSpeedMultiplier));
            Assert.That(settings.TargetSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.DefaultGroupSpeedMultiplier));
            Assert.That(settings.HazardSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.DefaultGroupSpeedMultiplier));
            Assert.That(settings.TerminalSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.DefaultGroupSpeedMultiplier));
            Assert.That(settings.GravitySpawnSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.DefaultGroupSpeedMultiplier));
        }

        [Test]
        public void ActionPlaybackSettings_DefaultsStayWithinIntendedRanges()
        {
            ActionPlaybackSettings settings = new ActionPlaybackSettings();

            Assert.That(settings.RemoveDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultRemoveDurationSeconds)));
            Assert.That(settings.BreakBlockerOrRevealDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultBreakBlockerOrRevealDurationSeconds)));
            Assert.That(settings.DockInsertFeedbackDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultDockInsertFeedbackDurationSeconds)));
            Assert.That(settings.DockClearFeedbackDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultDockClearFeedbackDurationSeconds)));
            Assert.That(settings.DockWarningCautionDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultDockWarningCautionDurationSeconds)));
            Assert.That(settings.DockWarningAcuteDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultDockWarningAcuteDurationSeconds)));
            Assert.That(settings.DockJamFeedbackDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultDockJamFeedbackDurationSeconds)));
            Assert.That(settings.GravityDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultGravityDurationSeconds));
            Assert.That(settings.SpawnDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultSpawnDurationSeconds));
            Assert.That(settings.BoardPieceLandingSquashXScale, Is.EqualTo(ActionPlaybackSettings.DefaultBoardPieceLandingSquashXScale));
            Assert.That(settings.BoardPieceLandingSquashYScale, Is.EqualTo(ActionPlaybackSettings.DefaultBoardPieceLandingSquashYScale));
            Assert.That(settings.BoardPieceLandingBounceDistance, Is.EqualTo(ActionPlaybackSettings.DefaultBoardPieceLandingBounceDistance));
            Assert.That(settings.TargetExtractDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultTargetExtractDurationSeconds)));
            Assert.That(settings.WinFxDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultWinFxDurationSeconds)));
            Assert.That(settings.LossFxDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultLossFxDurationSeconds)));
            Assert.That(settings.WaterRiseDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultWaterRiseDurationSeconds)));
            Assert.That(settings.WaterForecastTransitionDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultWaterForecastTransitionDurationSeconds)));
            Assert.That(settings.WaterForecastPulseDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultWaterForecastPulseDurationSeconds)));
            Assert.That(settings.WaterlinePulseDurationSeconds, Is.EqualTo(ScaleDefaultGameplay(ActionPlaybackSettings.DefaultWaterlinePulseDurationSeconds)));
            Assert.That(settings.PlaybackSpeedMultiplier, Is.EqualTo(0.5f));
            Assert.That(settings.RemoveDurationSeconds, Is.InRange(0.18f, 0.22f));
            Assert.That(settings.BreakBlockerOrRevealDurationSeconds, Is.InRange(0.18f, 0.22f));
            Assert.That(settings.DockInsertFeedbackDurationSeconds, Is.InRange(0.14f, 0.18f));
            Assert.That(settings.DockClearFeedbackDurationSeconds, Is.InRange(0.14f, 0.18f));
            Assert.That(settings.GravityDurationSeconds, Is.InRange(0.12f, 0.20f));
            Assert.That(settings.SpawnDurationSeconds, Is.InRange(0.10f, 0.16f));
            Assert.That(settings.TargetExtractDurationSeconds, Is.InRange(0.22f, 0.26f));
            Assert.That(settings.WinFxDurationSeconds, Is.InRange(1.15f, 1.25f));
            Assert.That(settings.LossFxDurationSeconds, Is.InRange(1.15f, 1.25f));
            Assert.That(settings.WaterRiseDurationSeconds, Is.InRange(0.28f, 0.32f));
        }

        [Test]
        public void ActionPlaybackSettings_SpeedMultiplierScalesGameplayDurationsButNotGravityOrSpawn()
        {
            ActionPlaybackSettings settings = new ActionPlaybackSettings();

            settings.SetPlaybackSpeedMultiplier(2.0f);

            Assert.That(settings.PlaybackSpeedMultiplier, Is.EqualTo(2.0f));
            Assert.That(settings.RemoveDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultRemoveDurationSeconds / 2.0f));
            Assert.That(settings.GravityDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultGravityDurationSeconds));
            Assert.That(settings.SpawnDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultSpawnDurationSeconds));
            Assert.That(settings.LossFxDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultLossFxDurationSeconds / 2.0f));
            Assert.That(settings.WaterForecastPulseDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultWaterForecastPulseDurationSeconds / 2.0f));
        }

        [Test]
        public void ActionPlaybackSettings_GroupSpeedMultipliersOnlyScaleTheirGroups()
        {
            ActionPlaybackSettings settings = new ActionPlaybackSettings();
            settings.SetPlaybackSpeedMultiplier(1.0f);

            settings.SetBoardActionSpeedMultiplier(2.0f);
            settings.SetDockSpeedMultiplier(4.0f);
            settings.SetTargetSpeedMultiplier(0.5f);
            settings.SetHazardSpeedMultiplier(2.0f);
            settings.SetTerminalSpeedMultiplier(4.0f);
            settings.SetGravitySpawnSpeedMultiplier(2.0f);

            Assert.That(settings.RemoveDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultRemoveDurationSeconds / 2.0f));
            Assert.That(settings.BreakBlockerOrRevealDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultBreakBlockerOrRevealDurationSeconds / 2.0f));
            Assert.That(settings.DockInsertFeedbackDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultDockInsertFeedbackDurationSeconds / 4.0f));
            Assert.That(settings.DockWarningAcuteDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultDockWarningAcuteDurationSeconds / 4.0f));
            Assert.That(settings.TargetReactionDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultTargetReactionDurationSeconds / 0.5f));
            Assert.That(settings.TargetExtractDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultTargetExtractDurationSeconds / 0.5f));
            Assert.That(settings.WaterRiseDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultWaterRiseDurationSeconds / 2.0f));
            Assert.That(settings.VineGrowthDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultVineGrowthDurationSeconds / 2.0f));
            Assert.That(settings.WinFxDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultWinFxDurationSeconds / 4.0f));
            Assert.That(settings.LossFxDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultLossFxDurationSeconds / 4.0f));
            Assert.That(settings.GravityDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultGravityDurationSeconds / 2.0f));
            Assert.That(settings.SpawnDurationSeconds, Is.EqualTo(ActionPlaybackSettings.DefaultSpawnDurationSeconds / 2.0f));
        }

        [Test]
        public void ActionPlaybackSettings_SpeedMultiplierIsClamped()
        {
            ActionPlaybackSettings settings = new ActionPlaybackSettings();

            settings.SetPlaybackSpeedMultiplier(0.0f);
            Assert.That(settings.PlaybackSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.MinPlaybackSpeedMultiplier));

            settings.SetPlaybackSpeedMultiplier(100.0f);
            Assert.That(settings.PlaybackSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.MaxPlaybackSpeedMultiplier));

            settings.SetBoardActionSpeedMultiplier(0.0f);
            settings.SetDockSpeedMultiplier(100.0f);
            settings.SetTargetSpeedMultiplier(0.0f);
            settings.SetHazardSpeedMultiplier(100.0f);
            settings.SetTerminalSpeedMultiplier(0.0f);
            settings.SetGravitySpawnSpeedMultiplier(100.0f);

            Assert.That(settings.BoardActionSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.MinPlaybackSpeedMultiplier));
            Assert.That(settings.DockSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.MaxPlaybackSpeedMultiplier));
            Assert.That(settings.TargetSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.MinPlaybackSpeedMultiplier));
            Assert.That(settings.HazardSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.MaxPlaybackSpeedMultiplier));
            Assert.That(settings.TerminalSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.MinPlaybackSpeedMultiplier));
            Assert.That(settings.GravitySpawnSpeedMultiplier, Is.EqualTo(ActionPlaybackSettings.MaxPlaybackSpeedMultiplier));
        }

        private static float ScaleDefaultGameplay(float seconds)
        {
            return seconds / ActionPlaybackSettings.DefaultPlaybackSpeedMultiplier;
        }
    }
}
