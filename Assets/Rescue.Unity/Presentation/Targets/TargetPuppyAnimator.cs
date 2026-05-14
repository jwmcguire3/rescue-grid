using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.Presentation.Targets
{
    public sealed class TargetPuppyAnimator : MonoBehaviour
    {
        private const int BaseLayer = 0;
        private const float DefaultCrossFadeDuration = 0.1f;

        [SerializeField] private Animator? animator;
        [SerializeField] private string trappedIdleState = string.Empty;
        [SerializeField] private string progressingIdleState = string.Empty;
        [SerializeField] private string oneClearAwayIdleState = string.Empty;
        [SerializeField] private string extractStartState = string.Empty;
        [SerializeField] private string extractAirState = string.Empty;
        [SerializeField] private string oneClearAwayBarkState = string.Empty;
        [SerializeField] private string progressingFidgetState = string.Empty;

#if UNITY_EDITOR
        private bool warnedAboutAnimatorState;
#endif

        public TargetReadiness? CurrentAppliedReadiness { get; private set; }

        public string CurrentAppliedStateName { get; private set; } = string.Empty;

        public bool IsExtracting { get; private set; }

        private void Awake()
        {
            ResolveAnimator();
            DisableRootMotion();
        }

        public void ApplyReadiness(TargetReadiness readiness)
        {
            if (readiness == TargetReadiness.Extracted)
            {
                return;
            }

            CurrentAppliedReadiness = readiness;

            if (readiness == TargetReadiness.ExtractableLatched)
            {
                return;
            }

            IsExtracting = false;
            string stateName = readiness switch
            {
                TargetReadiness.Progressing => progressingIdleState,
                TargetReadiness.OneClearAway => oneClearAwayIdleState,
                TargetReadiness.Distressed => trappedIdleState,
                _ => trappedIdleState,
            };

            PlayStateIntent(stateName);
        }

        public void PlayExtract()
        {
            IsExtracting = true;
            string stateName = string.IsNullOrWhiteSpace(extractStartState)
                ? extractAirState
                : extractStartState;

            PlayStateIntent(stateName);
        }

        private void PlayStateIntent(string stateName)
        {
            CurrentAppliedStateName = stateName;
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            ResolveAnimator();
            DisableRootMotion();
            Animator? targetAnimator = animator;
            if (targetAnimator == null)
            {
                return;
            }

            int stateHash = Animator.StringToHash(stateName);
            if (!HasAnimatorState(targetAnimator, stateHash))
            {
                LogMissingStateOnce(stateName);
                return;
            }

            targetAnimator.CrossFade(stateHash, DefaultCrossFadeDuration, BaseLayer);
        }

        private void DisableRootMotion()
        {
            Animator? targetAnimator = animator;
            if (targetAnimator != null)
            {
                targetAnimator.applyRootMotion = false;
            }
        }

        private void ResolveAnimator()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private static bool HasAnimatorState(Animator targetAnimator, int stateHash)
        {
            return targetAnimator.runtimeAnimatorController != null
                && targetAnimator.layerCount > BaseLayer
                && targetAnimator.HasState(BaseLayer, stateHash);
        }

        private void LogMissingStateOnce(string stateName)
        {
#if UNITY_EDITOR
            if (warnedAboutAnimatorState)
            {
                return;
            }

            warnedAboutAnimatorState = true;
            Debug.LogWarning(
                $"{nameof(TargetPuppyAnimator)} could not find animator state '{stateName}'.",
                this);
#endif
        }
    }
}
