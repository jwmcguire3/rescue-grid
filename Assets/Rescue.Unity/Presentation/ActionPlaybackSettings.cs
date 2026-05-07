using System;
using UnityEngine;

namespace Rescue.Unity.Presentation
{
    [Serializable]
    public sealed class ActionPlaybackSettings
    {
        public const float DefaultRemoveDurationSeconds = 0.10f;
        public const float DefaultBreakBlockerOrRevealDurationSeconds = 0.10f;
        public const float DefaultBlockerBreakCascadeStaggerSeconds = 0.08f;
        public const float DefaultDockFeedbackDurationSeconds = 0.10f;
        public const float DefaultDockInsertionTravelDurationSeconds = 0.12f;
        public const float DefaultDockInsertFeedbackDurationSeconds = 0.08f;
        public const float DefaultDockClearFeedbackDurationSeconds = 0.18f;
        public const float DefaultDockWarningCautionDurationSeconds = 0.50f;
        public const float DefaultDockWarningAcuteDurationSeconds = 0.40f;
        public const float DefaultDockJamFeedbackDurationSeconds = 0.70f;
        public const float DefaultGravityDurationSeconds = 0.15f;
        public const float DefaultSpawnDurationSeconds = 0.12f;
        public const float DefaultBoardPieceLandingSquashXScale = 1.06f;
        public const float DefaultBoardPieceLandingSquashYScale = 0.92f;
        public const float DefaultBoardPieceLandingBounceDistance = 0.04f;
        public const float DefaultTargetReactionDurationSeconds = 0.12f;
        public const float DefaultTargetExtractDurationSeconds = 0.20f;
        public const float DefaultWinFxDurationSeconds = 0.60f;
        public const float DefaultLossFxDurationSeconds = 0.60f;
        public const float DefaultWaterRiseDurationSeconds = 0.15f;
        public const float DefaultWaterForecastTransitionDurationSeconds = 0.10f;
        public const float DefaultWaterForecastPulseDurationSeconds = 0.25f;
        public const float DefaultWaterlinePulseDurationSeconds = 0.20f;
        public const float DefaultPlannedVineProgressDurationSeconds = 0.10f;
        public const float DefaultVinePreviewDurationSeconds = 0.18f;
        public const float DefaultVineGrowthDurationSeconds = 0.30f;
        public const float DefaultPlaybackSpeedMultiplier = 0.5f;
        public const float DefaultGroupSpeedMultiplier = 1.0f;
        public const float DefaultGravitySpawnSpeedMultiplier = 4.0f;
        public const float MinPlaybackSpeedMultiplier = 0.10f;
        public const float MaxPlaybackSpeedMultiplier = 8.0f;

        [SerializeField] private bool playbackEnabled = true;
        [SerializeField] private bool yieldBetweenSteps;
        [SerializeField] private float playbackSpeedMultiplier = DefaultPlaybackSpeedMultiplier;
        [SerializeField] private float boardActionSpeedMultiplier = DefaultGroupSpeedMultiplier;
        [SerializeField] private float dockSpeedMultiplier = DefaultGroupSpeedMultiplier;
        [SerializeField] private float targetSpeedMultiplier = DefaultGroupSpeedMultiplier;
        [SerializeField] private float hazardSpeedMultiplier = DefaultGroupSpeedMultiplier;
        [SerializeField] private float terminalSpeedMultiplier = DefaultGroupSpeedMultiplier;
        [SerializeField] private float gravitySpawnSpeedMultiplier = DefaultGravitySpawnSpeedMultiplier;
        [SerializeField] private float removeDurationSeconds = DefaultRemoveDurationSeconds;
        [SerializeField] private float breakBlockerOrRevealDurationSeconds = DefaultBreakBlockerOrRevealDurationSeconds;
        [SerializeField] private float blockerBreakCascadeStaggerSeconds = DefaultBlockerBreakCascadeStaggerSeconds;
        [SerializeField] private float dockFeedbackDurationSeconds = DefaultDockFeedbackDurationSeconds;
        [SerializeField] private float dockInsertionTravelDurationSeconds = DefaultDockInsertionTravelDurationSeconds;
        [SerializeField] private float dockInsertFeedbackDurationSeconds = DefaultDockInsertFeedbackDurationSeconds;
        [SerializeField] private float dockClearFeedbackDurationSeconds = DefaultDockClearFeedbackDurationSeconds;
        [SerializeField] private float dockWarningCautionDurationSeconds = DefaultDockWarningCautionDurationSeconds;
        [SerializeField] private float dockWarningAcuteDurationSeconds = DefaultDockWarningAcuteDurationSeconds;
        [SerializeField] private float dockJamFeedbackDurationSeconds = DefaultDockJamFeedbackDurationSeconds;
        [SerializeField] private float gravityDurationSeconds = DefaultGravityDurationSeconds;
        [SerializeField] private float spawnDurationSeconds = DefaultSpawnDurationSeconds;
        [SerializeField] private float boardPieceLandingSquashXScale = DefaultBoardPieceLandingSquashXScale;
        [SerializeField] private float boardPieceLandingSquashYScale = DefaultBoardPieceLandingSquashYScale;
        [SerializeField] private float boardPieceLandingBounceDistance = DefaultBoardPieceLandingBounceDistance;
        [SerializeField] private float targetReactionDurationSeconds = DefaultTargetReactionDurationSeconds;
        [SerializeField] private float targetExtractDurationSeconds = DefaultTargetExtractDurationSeconds;
        [SerializeField] private float winFxDurationSeconds = DefaultWinFxDurationSeconds;
        [SerializeField] private float lossFxDurationSeconds = DefaultLossFxDurationSeconds;
        [SerializeField] private float waterRiseDurationSeconds = DefaultWaterRiseDurationSeconds;
        [SerializeField] private float waterForecastTransitionDurationSeconds = DefaultWaterForecastTransitionDurationSeconds;
        [SerializeField] private float waterForecastPulseDurationSeconds = DefaultWaterForecastPulseDurationSeconds;
        [SerializeField] private float waterlinePulseDurationSeconds = DefaultWaterlinePulseDurationSeconds;
        [SerializeField] private float plannedVineProgressDurationSeconds = DefaultPlannedVineProgressDurationSeconds;
        [SerializeField] private float vinePreviewDurationSeconds = DefaultVinePreviewDurationSeconds;
        [SerializeField] private float vineGrowthDurationSeconds = DefaultVineGrowthDurationSeconds;

        public bool PlaybackEnabled => playbackEnabled;

        public bool YieldBetweenSteps => yieldBetweenSteps;

        public float PlaybackSpeedMultiplier => Mathf.Clamp(
            playbackSpeedMultiplier,
            MinPlaybackSpeedMultiplier,
            MaxPlaybackSpeedMultiplier);

        public float BoardActionSpeedMultiplier => ClampSpeedMultiplier(boardActionSpeedMultiplier);

        public float DockSpeedMultiplier => ClampSpeedMultiplier(dockSpeedMultiplier);

        public float TargetSpeedMultiplier => ClampSpeedMultiplier(targetSpeedMultiplier);

        public float HazardSpeedMultiplier => ClampSpeedMultiplier(hazardSpeedMultiplier);

        public float TerminalSpeedMultiplier => ClampSpeedMultiplier(terminalSpeedMultiplier);

        public float GravitySpawnSpeedMultiplier => ClampSpeedMultiplier(gravitySpawnSpeedMultiplier);

        public float RemoveDurationSeconds => ScaleGameplayDuration(removeDurationSeconds, BoardActionSpeedMultiplier);

        public float BreakBlockerOrRevealDurationSeconds => ScaleGameplayDuration(
            breakBlockerOrRevealDurationSeconds,
            BoardActionSpeedMultiplier);

        public float BlockerBreakCascadeStaggerSeconds => ScaleGameplayDuration(
            blockerBreakCascadeStaggerSeconds,
            BoardActionSpeedMultiplier);

        public float DockFeedbackDurationSeconds => ScaleGameplayDuration(dockFeedbackDurationSeconds, DockSpeedMultiplier);

        public float DockInsertionTravelDurationSeconds => ScaleGameplayDuration(
            dockInsertionTravelDurationSeconds,
            DockSpeedMultiplier);

        public float DockInsertFeedbackDurationSeconds => ScaleGameplayDuration(
            dockInsertFeedbackDurationSeconds,
            DockSpeedMultiplier);

        public float DockClearFeedbackDurationSeconds => ScaleGameplayDuration(
            dockClearFeedbackDurationSeconds,
            DockSpeedMultiplier);

        public float DockWarningCautionDurationSeconds => ScaleGameplayDuration(
            dockWarningCautionDurationSeconds,
            DockSpeedMultiplier);

        public float DockWarningAcuteDurationSeconds => ScaleGameplayDuration(
            dockWarningAcuteDurationSeconds,
            DockSpeedMultiplier);

        public float DockJamFeedbackDurationSeconds => ScaleGameplayDuration(
            dockJamFeedbackDurationSeconds,
            DockSpeedMultiplier);

        public float GravityDurationSeconds => ScaleGravitySpawnDuration(gravityDurationSeconds);

        public float SpawnDurationSeconds => ScaleGravitySpawnDuration(spawnDurationSeconds);

        public float BoardPieceLandingSquashXScale => Mathf.Max(1.0f, boardPieceLandingSquashXScale);

        public float BoardPieceLandingSquashYScale => Mathf.Clamp(boardPieceLandingSquashYScale, 0.75f, 1.0f);

        public float BoardPieceLandingBounceDistance => Mathf.Max(0.0f, boardPieceLandingBounceDistance);

        public float TargetReactionDurationSeconds => ScaleGameplayDuration(
            targetReactionDurationSeconds,
            TargetSpeedMultiplier);

        public float TargetExtractDurationSeconds => ScaleGameplayDuration(
            targetExtractDurationSeconds,
            TargetSpeedMultiplier);

        public float WinFxDurationSeconds => ScaleGameplayDuration(winFxDurationSeconds, TerminalSpeedMultiplier);

        public float LossFxDurationSeconds => ScaleGameplayDuration(lossFxDurationSeconds, TerminalSpeedMultiplier);

        public float WaterRiseDurationSeconds => ScaleGameplayDuration(waterRiseDurationSeconds, HazardSpeedMultiplier);

        public float WaterForecastTransitionDurationSeconds => ScaleGameplayDuration(
            waterForecastTransitionDurationSeconds,
            HazardSpeedMultiplier);

        public float WaterForecastPulseDurationSeconds => ScaleGameplayDuration(
            waterForecastPulseDurationSeconds,
            HazardSpeedMultiplier);

        public float WaterlinePulseDurationSeconds => ScaleGameplayDuration(
            waterlinePulseDurationSeconds,
            HazardSpeedMultiplier);

        public float PlannedVineProgressDurationSeconds => ScaleGameplayDuration(
            plannedVineProgressDurationSeconds,
            HazardSpeedMultiplier);

        public float VinePreviewDurationSeconds => ScaleGameplayDuration(vinePreviewDurationSeconds, HazardSpeedMultiplier);

        public float VineGrowthDurationSeconds => ScaleGameplayDuration(vineGrowthDurationSeconds, HazardSpeedMultiplier);

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

        public void SetBoardActionSpeedMultiplier(float multiplier)
        {
            boardActionSpeedMultiplier = ClampSpeedMultiplier(multiplier);
        }

        public void SetDockSpeedMultiplier(float multiplier)
        {
            dockSpeedMultiplier = ClampSpeedMultiplier(multiplier);
        }

        public void SetTargetSpeedMultiplier(float multiplier)
        {
            targetSpeedMultiplier = ClampSpeedMultiplier(multiplier);
        }

        public void SetHazardSpeedMultiplier(float multiplier)
        {
            hazardSpeedMultiplier = ClampSpeedMultiplier(multiplier);
        }

        public void SetTerminalSpeedMultiplier(float multiplier)
        {
            terminalSpeedMultiplier = ClampSpeedMultiplier(multiplier);
        }

        public void SetGravitySpawnSpeedMultiplier(float multiplier)
        {
            gravitySpawnSpeedMultiplier = ClampSpeedMultiplier(multiplier);
        }

        private float ScaleGameplayDuration(float seconds, float groupMultiplier)
        {
            return seconds / (PlaybackSpeedMultiplier * groupMultiplier);
        }

        private float ScaleGravitySpawnDuration(float seconds)
        {
            return seconds / (PlaybackSpeedMultiplier * GravitySpawnSpeedMultiplier);
        }

        private static float ClampSpeedMultiplier(float multiplier)
        {
            return Mathf.Clamp(
                multiplier,
                MinPlaybackSpeedMultiplier,
                MaxPlaybackSpeedMultiplier);
        }
    }
}
