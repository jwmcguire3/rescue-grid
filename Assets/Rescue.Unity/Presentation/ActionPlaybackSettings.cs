using System;
using UnityEngine;

namespace Rescue.Unity.Presentation
{
    [Serializable]
    public sealed class ActionPlaybackSettings
    {
        [SerializeField] private bool playbackEnabled = true;
        [SerializeField] private bool yieldBetweenSteps;

        public bool PlaybackEnabled => playbackEnabled;

        public bool YieldBetweenSteps => yieldBetweenSteps;
    }
}
