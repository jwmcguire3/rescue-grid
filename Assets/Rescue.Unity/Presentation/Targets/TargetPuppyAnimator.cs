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
        [SerializeField] private bool playOneClearAwayBarkOnEntry = true;
        [SerializeField] private float progressingFidgetCooldownMinSeconds = 4f;
        [SerializeField] private float progressingFidgetCooldownMaxSeconds = 7f;
        [SerializeField] private float progressingFidgetDurationSeconds = 1.25f;
        [SerializeField] private float oneClearAwayBarkRepeatCooldownMinSeconds = 8f;
        [SerializeField] private float oneClearAwayBarkRepeatCooldownMaxSeconds = 14f;
        [SerializeField] private float oneClearAwayBarkDurationSeconds = 0.85f;

#if UNITY_EDITOR
        private bool warnedAboutAnimatorState;
#endif
        private int currentAppliedStateHash;
        private TargetPuppyLookAt? lookAt;
        private float progressingFidgetCooldownRemaining;
        private float progressingFidgetRemaining;
        private float oneClearAwayBarkCooldownRemaining;
        private float oneClearAwayBarkRemaining;

        public TargetReadiness? CurrentAppliedReadiness { get; private set; }

        public string CurrentAppliedStateName { get; private set; } = string.Empty;

        public bool IsExtracting { get; private set; }

        public TargetPuppyLookAt? ResolvedLookAt => lookAt;

        private void Awake()
        {
            ResolveAnimator();
            ResolveLookAt();
            DisableRootMotion();
        }

        private void Update()
        {
            AdvanceProceduralAnimation(Time.deltaTime);
        }

        public void ApplyReadiness(TargetReadiness readiness)
        {
            ResolveLookAt()?.ApplyReadiness(readiness);
            if (readiness == TargetReadiness.Extracted)
            {
                StopProceduralAnimation();
                return;
            }

            TargetReadiness? previousReadiness = CurrentAppliedReadiness;
            CurrentAppliedReadiness = readiness;

            if (readiness == TargetReadiness.ExtractableLatched)
            {
                StopProceduralAnimation();
                return;
            }

            IsExtracting = false;
            StopProceduralAnimation();

            if (readiness == TargetReadiness.OneClearAway &&
                previousReadiness != TargetReadiness.OneClearAway &&
                playOneClearAwayBarkOnEntry &&
                !string.IsNullOrWhiteSpace(oneClearAwayBarkState))
            {
                oneClearAwayBarkRemaining = Mathf.Max(0f, oneClearAwayBarkDurationSeconds);
                ScheduleOneClearAwayBark();
                PlayStateIntent(oneClearAwayBarkState);
                return;
            }

            string stateName = ResolveReadinessStateName(readiness);

            PlayStateIntent(stateName);
            ScheduleProceduralAnimation(readiness);
        }

        public void PlayExtract()
        {
            IsExtracting = true;
            StopProceduralAnimation();
            ResolveLookAt()?.PlayExtract();
            string stateName = string.IsNullOrWhiteSpace(extractStartState)
                ? extractAirState
                : extractStartState;

            PlayStateIntent(stateName);
        }

        public void AdvanceProceduralAnimationForTests(float deltaTime)
        {
            AdvanceProceduralAnimation(Mathf.Max(0f, deltaTime));
        }

        private string ResolveReadinessStateName(TargetReadiness readiness)
        {
            return readiness switch
            {
                TargetReadiness.Progressing => progressingIdleState,
                TargetReadiness.OneClearAway => oneClearAwayIdleState,
                TargetReadiness.Distressed => trappedIdleState,
                _ => trappedIdleState,
            };
        }

        private void AdvanceProceduralAnimation(float deltaTime)
        {
            if (IsExtracting || !CurrentAppliedReadiness.HasValue)
            {
                return;
            }

            if (CurrentAppliedReadiness.Value == TargetReadiness.Progressing)
            {
                AdvanceProgressingFidget(deltaTime);
                return;
            }

            if (CurrentAppliedReadiness.Value == TargetReadiness.OneClearAway)
            {
                AdvanceOneClearAwayBark(deltaTime);
            }
        }

        private void AdvanceProgressingFidget(float deltaTime)
        {
            if (progressingFidgetRemaining > 0f)
            {
                progressingFidgetRemaining = Mathf.Max(0f, progressingFidgetRemaining - deltaTime);
                if (progressingFidgetRemaining <= 0f)
                {
                    PlayStateIntent(progressingIdleState);
                    ScheduleProgressingFidget();
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(progressingFidgetState))
            {
                return;
            }

            progressingFidgetCooldownRemaining -= deltaTime;
            if (progressingFidgetCooldownRemaining > 0f)
            {
                return;
            }

            progressingFidgetRemaining = Mathf.Max(0f, progressingFidgetDurationSeconds);
            PlayStateIntent(progressingFidgetState);
        }

        private void AdvanceOneClearAwayBark(float deltaTime)
        {
            if (oneClearAwayBarkRemaining > 0f)
            {
                oneClearAwayBarkRemaining = Mathf.Max(0f, oneClearAwayBarkRemaining - deltaTime);
                if (oneClearAwayBarkRemaining <= 0f)
                {
                    PlayStateIntent(oneClearAwayIdleState);
                    ScheduleOneClearAwayBark();
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(oneClearAwayBarkState))
            {
                return;
            }

            oneClearAwayBarkCooldownRemaining -= deltaTime;
            if (oneClearAwayBarkCooldownRemaining > 0f)
            {
                return;
            }

            oneClearAwayBarkRemaining = Mathf.Max(0f, oneClearAwayBarkDurationSeconds);
            PlayStateIntent(oneClearAwayBarkState);
        }

        private void ScheduleProceduralAnimation(TargetReadiness readiness)
        {
            if (readiness == TargetReadiness.Progressing)
            {
                ScheduleProgressingFidget();
            }
            else if (readiness == TargetReadiness.OneClearAway)
            {
                ScheduleOneClearAwayBark();
            }
        }

        private void StopProceduralAnimation()
        {
            progressingFidgetCooldownRemaining = 0f;
            progressingFidgetRemaining = 0f;
            oneClearAwayBarkCooldownRemaining = 0f;
            oneClearAwayBarkRemaining = 0f;
        }

        private void ScheduleProgressingFidget()
        {
            progressingFidgetCooldownRemaining = Random.Range(
                progressingFidgetCooldownMinSeconds,
                Mathf.Max(progressingFidgetCooldownMinSeconds, progressingFidgetCooldownMaxSeconds));
        }

        private void ScheduleOneClearAwayBark()
        {
            oneClearAwayBarkCooldownRemaining = Random.Range(
                oneClearAwayBarkRepeatCooldownMinSeconds,
                Mathf.Max(oneClearAwayBarkRepeatCooldownMinSeconds, oneClearAwayBarkRepeatCooldownMaxSeconds));
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

        private TargetPuppyLookAt? ResolveLookAt()
        {
            if (lookAt == null)
            {
                lookAt = GetComponent<TargetPuppyLookAt>();
            }

            if (lookAt == null)
            {
                lookAt = GetComponentInChildren<TargetPuppyLookAt>(includeInactive: true);
            }

            return lookAt;
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
