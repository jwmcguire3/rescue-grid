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
        public const float DefaultWaterRiseDurationSeconds = 0.15f;
        public const float DefaultWaterForecastTransitionDurationSeconds = 0.10f;
        public const float DefaultWaterForecastPulseDurationSeconds = 0.25f;
        public const float DefaultWaterlinePulseDurationSeconds = 0.20f;

        [SerializeField] private bool playbackEnabled = true;
        [SerializeField] private bool yieldBetweenSteps;
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
        [SerializeField] private float waterRiseDurationSeconds = DefaultWaterRiseDurationSeconds;
        [SerializeField] private float waterForecastTransitionDurationSeconds = DefaultWaterForecastTransitionDurationSeconds;
        [SerializeField] private float waterForecastPulseDurationSeconds = DefaultWaterForecastPulseDurationSeconds;
        [SerializeField] private float waterlinePulseDurationSeconds = DefaultWaterlinePulseDurationSeconds;

        public bool PlaybackEnabled => playbackEnabled;

        public bool YieldBetweenSteps => yieldBetweenSteps;

        public float RemoveDurationSeconds => removeDurationSeconds;

        public float BreakBlockerOrRevealDurationSeconds => breakBlockerOrRevealDurationSeconds;

        public float DockFeedbackDurationSeconds => dockFeedbackDurationSeconds;

        public float DockInsertFeedbackDurationSeconds => dockInsertFeedbackDurationSeconds;

        public float DockClearFeedbackDurationSeconds => dockClearFeedbackDurationSeconds;

        public float DockWarningCautionDurationSeconds => dockWarningCautionDurationSeconds;

        public float DockWarningAcuteDurationSeconds => dockWarningAcuteDurationSeconds;

        public float DockJamFeedbackDurationSeconds => dockJamFeedbackDurationSeconds;

        public float GravityDurationSeconds => gravityDurationSeconds;

        public float SpawnDurationSeconds => spawnDurationSeconds;

        public float TargetExtractDurationSeconds => targetExtractDurationSeconds;

        public float WaterRiseDurationSeconds => waterRiseDurationSeconds;

        public float WaterForecastTransitionDurationSeconds => waterForecastTransitionDurationSeconds;

        public float WaterForecastPulseDurationSeconds => waterForecastPulseDurationSeconds;

        public float WaterlinePulseDurationSeconds => waterlinePulseDurationSeconds;
    }
}
