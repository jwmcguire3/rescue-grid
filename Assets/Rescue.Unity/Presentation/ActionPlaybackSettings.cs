using System;
using UnityEngine;

namespace Rescue.Unity.Presentation
{
    [Serializable]
    public sealed class ActionPlaybackSettings
    {
        public const float DefaultRemoveDurationSeconds = 0.10f;
        public const float DefaultBreakBlockerOrRevealDurationSeconds = 0.10f;
        public const float DefaultDockFeedbackDurationSeconds = 0.10f;
        public const float DefaultDockInsertFeedbackDurationSeconds = 0.08f;
        public const float DefaultDockClearFeedbackDurationSeconds = 0.08f;
        public const float DefaultDockWarningCautionDurationSeconds = 0.50f;
        public const float DefaultDockWarningAcuteDurationSeconds = 0.40f;
        public const float DefaultDockJamFeedbackDurationSeconds = 0.70f;
        public const float DefaultGravityDurationSeconds = 0.15f;
        public const float DefaultSpawnDurationSeconds = 0.12f;
        public const float DefaultTargetExtractDurationSeconds = 0.12f;
        public const float DefaultWinFxDurationSeconds = 0.60f;
        public const float DefaultWaterRiseDurationSeconds = 0.15f;
        public const float DefaultWaterForecastTransitionDurationSeconds = 0.10f;
        public const float DefaultWaterForecastPulseDurationSeconds = 0.25f;
        public const float DefaultWaterlinePulseDurationSeconds = 0.20f;
        public const float DefaultPlaybackSpeedMultiplier = 1.0f;
        public const float MinPlaybackSpeedMultiplier = 0.10f;
        public const float MaxPlaybackSpeedMultiplier = 8.0f;

        [SerializeField] private bool playbackEnabled = true;
        [SerializeField] private bool yieldBetweenSteps;
        [SerializeField] private float playbackSpeedMultiplier = DefaultPlaybackSpeedMultiplier;
        [SerializeField] private float removeDurationSeconds = DefaultRemoveDurationSeconds;
        [SerializeField] private float breakBlockerOrRevealDurationSeconds = DefaultBreakBlockerOrRevealDurationSeconds;
        [SerializeField] private float dockFeedbackDurationSeconds = DefaultDockFeedbackDurationSeconds;
        [SerializeField] private float dockInsertFeedbackDurationSeconds = DefaultDockInsertFeedbackDurationSeconds;
        [SerializeField] private float dockClearFeedbackDurationSeconds = DefaultDockClearFeedbackDurationSeconds;
        [SerializeField] private float dockWarningCautionDurationSeconds = DefaultDockWarningCautionDurationSeconds;
        [SerializeField] private float dockWarningAcuteDurationSeconds = DefaultDockWarningAcuteDurationSeconds;
        [SerializeField] private float dockJamFeedbackDurationSeconds = DefaultDockJamFeedbackDurationSeconds;
        [SerializeField] private float gravityDurationSeconds = DefaultGravityDurationSeconds;
        [SerializeField] private float spawnDurationSeconds = DefaultSpawnDurationSeconds;
        [SerializeField] private float targetExtractDurationSeconds = DefaultTargetExtractDurationSeconds;
        [SerializeField] private float winFxDurationSeconds = DefaultWinFxDurationSeconds;
        [SerializeField] private float waterRiseDurationSeconds = DefaultWaterRiseDurationSeconds;
        [SerializeField] private float waterForecastTransitionDurationSeconds = DefaultWaterForecastTransitionDurationSeconds;
        [SerializeField] private float waterForecastPulseDurationSeconds = DefaultWaterForecastPulseDurationSeconds;
        [SerializeField] private float waterlinePulseDurationSeconds = DefaultWaterlinePulseDurationSeconds;

        public bool PlaybackEnabled => playbackEnabled;

        public bool YieldBetweenSteps => yieldBetweenSteps;

        public float PlaybackSpeedMultiplier => Mathf.Clamp(
            playbackSpeedMultiplier,
            MinPlaybackSpeedMultiplier,
            MaxPlaybackSpeedMultiplier);

        public float RemoveDurationSeconds => ScaleDuration(removeDurationSeconds);

        public float BreakBlockerOrRevealDurationSeconds => ScaleDuration(breakBlockerOrRevealDurationSeconds);

        public float DockFeedbackDurationSeconds => ScaleDuration(dockFeedbackDurationSeconds);

        public float DockInsertFeedbackDurationSeconds => ScaleDuration(dockInsertFeedbackDurationSeconds);

        public float DockClearFeedbackDurationSeconds => ScaleDuration(dockClearFeedbackDurationSeconds);

        public float DockWarningCautionDurationSeconds => ScaleDuration(dockWarningCautionDurationSeconds);

        public float DockWarningAcuteDurationSeconds => ScaleDuration(dockWarningAcuteDurationSeconds);

        public float DockJamFeedbackDurationSeconds => ScaleDuration(dockJamFeedbackDurationSeconds);

        public float GravityDurationSeconds => ScaleDuration(gravityDurationSeconds);

        public float SpawnDurationSeconds => ScaleDuration(spawnDurationSeconds);

        public float TargetExtractDurationSeconds => ScaleDuration(targetExtractDurationSeconds);

        public float WinFxDurationSeconds => ScaleDuration(winFxDurationSeconds);

        public float WaterRiseDurationSeconds => ScaleDuration(waterRiseDurationSeconds);

        public float WaterForecastTransitionDurationSeconds => ScaleDuration(waterForecastTransitionDurationSeconds);

        public float WaterForecastPulseDurationSeconds => ScaleDuration(waterForecastPulseDurationSeconds);

        public float WaterlinePulseDurationSeconds => ScaleDuration(waterlinePulseDurationSeconds);

        public void SetPlaybackEnabled(bool enabled)
        {
            playbackEnabled = enabled;
        }

        public void SetPlaybackSpeedMultiplier(float multiplier)
        {
            playbackSpeedMultiplier = Mathf.Clamp(
                multiplier,
                MinPlaybackSpeedMultiplier,
                MaxPlaybackSpeedMultiplier);
        }

        private float ScaleDuration(float seconds)
        {
            return seconds / PlaybackSpeedMultiplier;
        }
    }
}
