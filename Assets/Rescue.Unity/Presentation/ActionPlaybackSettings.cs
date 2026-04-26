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
        [SerializeField] private float gravityDurationSeconds = 0.15f;
        [SerializeField] private float spawnDurationSeconds = 0.12f;

        public bool PlaybackEnabled => playbackEnabled;

        public bool YieldBetweenSteps => yieldBetweenSteps;

        public float RemoveDurationSeconds => removeDurationSeconds;

        public float GravityDurationSeconds => gravityDurationSeconds;

        public float SpawnDurationSeconds => spawnDurationSeconds;
    }
}
