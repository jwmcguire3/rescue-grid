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
        private int currentAppliedStateHash;

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
            string stateName = ResolveReadinessStateName(readiness);

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

        private string ResolveReadinessStateName(TargetReadiness readiness)
        {
            return readiness switch
            {
                TargetReadiness.Progressing => FirstConfiguredState(progressingFidgetState, progressingIdleState),
                TargetReadiness.OneClearAway => FirstConfiguredState(oneClearAwayBarkState, oneClearAwayIdleState),
                TargetReadiness.Distressed => trappedIdleState,
                _ => trappedIdleState,
            };
        }

        private static string FirstConfiguredState(string preferredState, string fallbackState)
        {
            return string.IsNullOrWhiteSpace(preferredState)
                ? fallbackState
                : preferredState;
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

            if (!TryResolveAnimatorState(targetAnimator, stateName, out int stateHash))
            {
                LogMissingStateOnce(targetAnimator, stateName);
                return;
            }

            if (currentAppliedStateHash == stateHash)
            {
                return;
            }

            currentAppliedStateHash = stateHash;
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

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(includeInactive: true);
            }
        }

        private static bool TryResolveAnimatorState(Animator targetAnimator, string stateName, out int stateHash)
        {
            stateHash = 0;
            if (targetAnimator.runtimeAnimatorController == null ||
                targetAnimator.layerCount <= BaseLayer)
            {
                return false;
            }

            int exactHash = Animator.StringToHash(stateName);
            if (targetAnimator.HasState(BaseLayer, exactHash))
            {
                stateHash = exactHash;
                return true;
            }

            string layerName = targetAnimator.GetLayerName(BaseLayer);
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return false;
            }

            int fullPathHash = Animator.StringToHash($"{layerName}.{stateName}");
            if (!targetAnimator.HasState(BaseLayer, fullPathHash))
            {
                return false;
            }

            stateHash = fullPathHash;
            return true;
        }

        private void LogMissingStateOnce(Animator targetAnimator, string stateName)
        {
#if UNITY_EDITOR
            if (warnedAboutAnimatorState)
            {
                return;
            }

            warnedAboutAnimatorState = true;
            string controllerName = targetAnimator.runtimeAnimatorController == null
                ? "<none>"
                : targetAnimator.runtimeAnimatorController.name;
            string layerName = targetAnimator.layerCount > BaseLayer
                ? targetAnimator.GetLayerName(BaseLayer)
                : "<missing>";
            string fullPathName = string.IsNullOrWhiteSpace(layerName)
                ? stateName
                : $"{layerName}.{stateName}";
            Debug.LogWarning(
                $"{nameof(TargetPuppyAnimator)} could not find animator state '{stateName}' " +
                $"on controller '{controllerName}' layer {BaseLayer} '{layerName}' " +
                $"with {targetAnimator.layerCount} layer(s). Tried '{stateName}' and '{fullPathName}'.",
                this);
#endif
        }
    }
}
