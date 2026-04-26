using System;
using UnityEngine;

namespace Rescue.Unity.Presentation
{
    [Serializable]
    public sealed class ActionPlaybackSettings
    {
        [SerializeField] private bool playbackEnabled = true;
        [SerializeField] private bool yieldBetweenSteps;
        [SerializeField] private float removeDurationSeconds = 0.10f;
        [SerializeField] private float breakBlockerOrRevealDurationSeconds = 0.10f;
        [SerializeField] private float dockFeedbackDurationSeconds = 0.10f;
        [SerializeField] private float dockInsertFeedbackDurationSeconds = 0.08f;
        [SerializeField] private float dockClearFeedbackDurationSeconds = 0.08f;
        [SerializeField] private float dockWarningCautionDurationSeconds = 0.50f;
        [SerializeField] private float dockWarningAcuteDurationSeconds = 0.40f;
        [SerializeField] private float dockJamFeedbackDurationSeconds = 0.70f;
        [SerializeField] private float gravityDurationSeconds = 0.15f;
        [SerializeField] private float spawnDurationSeconds = 0.12f;
        [SerializeField] private float targetExtractDurationSeconds = 0.12f;
        [SerializeField] private float waterRiseDurationSeconds = 0.15f;
        [SerializeField] private float waterForecastTransitionDurationSeconds = 0.10f;
        [SerializeField] private float waterForecastPulseDurationSeconds = 0.25f;
        [SerializeField] private float waterlinePulseDurationSeconds = 0.20f;

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
