using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rescue.Unity.UI
{
    public static class DockVisualStateResolver
    {
        public static DockVisualState FromOccupancy(int occupiedSlots, int dockSize)
        {
            if (dockSize <= 0)
            {
                return DockVisualState.Failed;
            }

            int clampedOccupancy = Mathf.Max(0, occupiedSlots);

            if (clampedOccupancy >= dockSize)
            {
                return DockVisualState.Failed;
            }

            if (clampedOccupancy == dockSize - 1)
            {
                return DockVisualState.Acute;
            }

            if (clampedOccupancy == dockSize - 2)
            {
                return DockVisualState.Caution;
            }

            return DockVisualState.Safe;
        }
    }

    public enum DockFeedbackType
    {
        None,
        Caution,
        Acute,
        Full,
        Failed,
    }

    public static class DockFeedbackTypeResolver
    {
        public static DockFeedbackType FromOccupancy(int occupiedSlots, int dockSize)
        {
            if (dockSize <= 0)
            {
                return DockFeedbackType.Failed;
            }

            int clampedOccupancy = Mathf.Max(0, occupiedSlots);

            if (clampedOccupancy > dockSize)
            {
                return DockFeedbackType.Failed;
            }

            if (clampedOccupancy == dockSize)
            {
                return DockFeedbackType.Full;
            }

            if (clampedOccupancy == dockSize - 1)
            {
                return DockFeedbackType.Acute;
            }

            if (clampedOccupancy == dockSize - 2)
            {
                return DockFeedbackType.Caution;
            }

            return DockFeedbackType.None;
        }
    }

    public sealed class DockFeedbackPresenter : MonoBehaviour
    {
        private const float DefaultPulseScaleMultiplier = 1.08f;
        private const float FullPulseScaleMultiplier = 1.10f;
        private const float FailedHoldScaleMultiplier = 1.04f;
        private const float TripleClearMinScaleMultiplier = 0.9f;
        private const float TripleClearPopScaleMultiplier = 1.04f;

        [Header("Feedback Target")]
        [SerializeField] private Transform? feedbackTarget;
        [SerializeField] private CanvasGroup? fadeTarget;

        [Header("Insert")]
        [SerializeField] private float insertPopScale = 1.12f;

        [Header("Pressure")]
        [SerializeField] private float acuteShakeAmount = 0.05f;
        [SerializeField] private float failedShakeAmount = 0.04f;

        [Header("Curves")]
        [SerializeField] private AnimationCurve? insertCurve;
        [SerializeField] private AnimationCurve? pulseCurve;
        [SerializeField] private AnimationCurve? shakeCurve;

        private Coroutine? _activeFeedback;
        private Vector3 _baseLocalScale = Vector3.one;
        private Vector3 _baseLocalPosition = Vector3.zero;
        private float _baseAlpha = 1f;
        private bool _hasCachedBaseline;
        private float insertDurationSeconds = ActionPlaybackSettings.DefaultDockInsertFeedbackDurationSeconds;
        private float clearDurationSeconds = ActionPlaybackSettings.DefaultDockClearFeedbackDurationSeconds;
        private float cautionPulseDurationSeconds = ActionPlaybackSettings.DefaultDockWarningCautionDurationSeconds;
        private float acutePulseDurationSeconds = ActionPlaybackSettings.DefaultDockWarningAcuteDurationSeconds;
        private float fullPulseDurationSeconds = ActionPlaybackSettings.DefaultDockWarningAcuteDurationSeconds
            * ActionPlaybackSettings.DockWarningFullDurationMultiplier;
        private float failedPulseDurationSeconds = ActionPlaybackSettings.DefaultDockJamFeedbackDurationSeconds;

        public float InsertPopScale => insertPopScale;

        public float InsertDuration => insertDurationSeconds;

        public float CautionPulseDuration => cautionPulseDurationSeconds;

        public float AcuteShakeAmount => acuteShakeAmount;

        public float AcutePulseDuration => acutePulseDurationSeconds;

        public float AcuteShakeDuration => AcutePulseDuration;

        public float FullPulseDuration => fullPulseDurationSeconds;

        public float FailedPulseDuration => failedPulseDurationSeconds;

        public float ClearDuration => clearDurationSeconds;

        public void ApplyPlaybackSettings(ActionPlaybackSettings settings)
        {
            if (settings is null)
            {
                return;
            }

            insertDurationSeconds = settings.DockInsertFeedbackDurationSeconds;
            clearDurationSeconds = settings.DockClearFeedbackDurationSeconds;
            cautionPulseDurationSeconds = settings.DockWarningCautionDurationSeconds;
            acutePulseDurationSeconds = settings.DockWarningAcuteDurationSeconds;
            fullPulseDurationSeconds = settings.DockWarningFullDurationSeconds;
            failedPulseDurationSeconds = settings.DockJamFeedbackDurationSeconds;
        }

        public DockFeedbackType SelectFeedbackType(int occupancy, int dockSize)
        {
            return DockFeedbackTypeResolver.FromOccupancy(occupancy, dockSize);
        }

        public void PlayInsertFeedback()
        {
            PlayRoutine(CreatePulseRoutine(insertDurationSeconds, insertPopScale, ResolveInsertCurve()));
        }

        public void PlayCautionFeedback()
        {
            PlayRoutine(CreatePulseRoutine(cautionPulseDurationSeconds, DefaultPulseScaleMultiplier, ResolvePulseCurve()));
        }

        public void PlayAcuteFeedback()
        {
            PlayRoutine(CreatePulseRoutine(acutePulseDurationSeconds, DefaultPulseScaleMultiplier, ResolvePulseCurve()));
        }

        public void PlayFullFeedback()
        {
            PlayRoutine(CreatePulseRoutine(fullPulseDurationSeconds, FullPulseScaleMultiplier, ResolvePulseCurve()));
        }

        public void PlayFailedFeedback()
        {
            PlayRoutine(CreateFailedHoldRoutine(failedPulseDurationSeconds));
        }

        public void PlayTripleClearFeedback()
        {
            PlayRoutine(CreateTripleClearRoutine(clearDurationSeconds));
        }

        public void SyncToState(int occupancy, int dockSize)
        {
            DockFeedbackType feedbackType = SelectFeedbackType(occupancy, dockSize);
            if (feedbackType == DockFeedbackType.Failed)
            {
                PlayFailedFeedback();
                return;
            }

            if (feedbackType == DockFeedbackType.Full)
            {
                PlayFullFeedback();
                return;
            }

            ResetVisuals();
        }

        public void ForceSyncToState(int occupancy, int dockSize)
        {
            if (_activeFeedback != null)
            {
                StopCoroutine(_activeFeedback);
                _activeFeedback = null;
            }

            DockFeedbackType feedbackType = SelectFeedbackType(occupancy, dockSize);
            if (feedbackType is DockFeedbackType.Failed or DockFeedbackType.Full)
            {
                CacheBaseline();
                ResetVisuals();

                if (TryGetTarget(out Transform target))
                {
                    float scaleMultiplier = feedbackType == DockFeedbackType.Full
                        ? FullPulseScaleMultiplier
                        : FailedHoldScaleMultiplier;
                    target.localScale = _baseLocalScale * scaleMultiplier;
                }

                return;
            }

            ResetVisuals();
        }

        public void SetFeedbackTarget(Transform? target)
        {
            feedbackTarget = target;
            _hasCachedBaseline = false;
            CacheBaseline();
        }

        private void OnDisable()
        {
            ResetVisuals();
        }

        private void PlayRoutine(IEnumerator routine)
        {
            CacheBaseline();

            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                ResetVisuals();
                _activeFeedback = null;
                return;
            }

            if (_activeFeedback != null)
            {
                StopCoroutine(_activeFeedback);
            }

            ResetVisuals();
            _activeFeedback = StartCoroutine(RunAndRelease(routine));
        }

        private IEnumerator RunAndRelease(IEnumerator routine)
        {
            yield return routine;
            _activeFeedback = null;
        }

        private IEnumerator CreatePulseRoutine(
            float duration,
            float peakScaleMultiplier,
            AnimationCurve curve,
            bool holdAtEnd = false)
        {
            if (!TryGetTarget(out Transform target))
            {
                yield break;
            }

            float safeDuration = Mathf.Max(0.01f, duration);
            Vector3 baseScale = _baseLocalScale;
            Vector3 peakScale = baseScale * Mathf.Max(1f, peakScaleMultiplier);
            float elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / safeDuration);
                float curveValue = Mathf.Clamp01(curve.Evaluate(normalized));
                float scaleLerp = normalized <= 0.5f
                    ? curveValue
                    : 1f - curveValue;

                target.localScale = Vector3.LerpUnclamped(baseScale, peakScale, scaleLerp);
                yield return null;
            }

            target.localScale = holdAtEnd ? peakScale : baseScale;
        }

        private IEnumerator CreatePressureShakeRoutine(
            float duration,
            float amount,
            float peakScaleMultiplier,
            AnimationCurve curve)
        {
            if (!TryGetTarget(out Transform target))
            {
                yield break;
            }

            float safeDuration = Mathf.Max(0.01f, duration);
            float safeAmount = Mathf.Max(0f, amount);
            Vector3 baseScale = _baseLocalScale;
            Vector3 peakScale = baseScale * Mathf.Max(1f, peakScaleMultiplier);
            float elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / safeDuration);
                float strength = Mathf.Clamp01(1f - normalized);
                float wave = curve.Evaluate(normalized) * safeAmount * strength;
                float pulse = Mathf.Sin(normalized * Mathf.PI) * strength;

                target.localPosition = _baseLocalPosition + new Vector3(Mathf.Sin(normalized * Mathf.PI * 8f) * wave, 0f, 0f);
                target.localScale = Vector3.LerpUnclamped(baseScale, peakScale, pulse);
                yield return null;
            }

            target.localPosition = _baseLocalPosition;
            target.localScale = baseScale;
        }

        private IEnumerator CreateFailedHoldRoutine(float durationSeconds)
        {
            if (!TryGetTarget(out Transform target))
            {
                yield break;
            }

            float duration = Mathf.Max(0.01f, durationSeconds);
            float settleDuration = duration * 0.65f;
            float holdDuration = duration - settleDuration;
            float elapsed = 0f;
            Vector3 baseScale = _baseLocalScale;
            Vector3 holdScale = baseScale * FailedHoldScaleMultiplier;
            AnimationCurve curve = ResolveShakeCurve();

            while (elapsed < settleDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / settleDuration);
                float strength = Mathf.Clamp01(1f - normalized);
                float wave = curve.Evaluate(normalized) * Mathf.Max(0f, failedShakeAmount) * strength;
                float pulse = Mathf.Sin(normalized * Mathf.PI);

                target.localPosition = _baseLocalPosition + new Vector3(Mathf.Sin(normalized * Mathf.PI * 10f) * wave, 0f, 0f);
                target.localScale = Vector3.LerpUnclamped(baseScale, holdScale, Mathf.Max(normalized, pulse * 0.75f));
                yield return null;
            }

            target.localPosition = _baseLocalPosition;
            target.localScale = holdScale;

            if (holdDuration > 0f)
            {
                yield return new WaitForSeconds(holdDuration);
            }
        }

        private IEnumerator CreateTripleClearRoutine(float durationSeconds)
        {
            if (!TryGetTarget(out Transform target))
            {
                yield break;
            }

            CanvasGroup? canvasGroup = ResolveFadeTarget();
            float duration = Mathf.Max(0.01f, durationSeconds);
            float elapsed = 0f;
            Vector3 baseScale = _baseLocalScale;
            Vector3 minScale = baseScale * TripleClearMinScaleMultiplier;
            Vector3 popScale = baseScale * TripleClearPopScaleMultiplier;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = normalized < 0.55f
                    ? Mathf.SmoothStep(0f, 1f, normalized / 0.55f)
                    : Mathf.SmoothStep(1f, 0f, (normalized - 0.55f) / 0.45f);
                target.localScale = normalized < 0.55f
                    ? Vector3.LerpUnclamped(baseScale, minScale, eased)
                    : Vector3.LerpUnclamped(minScale, popScale, 1f - eased);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(_baseAlpha, _baseAlpha * 0.65f, Mathf.Sin(normalized * Mathf.PI));
                }

                yield return null;
            }

            ResetVisuals();
        }

        private bool TryGetTarget(out Transform target)
        {
            if (feedbackTarget != null)
            {
                target = feedbackTarget;
                return true;
            }

            target = transform;
            return target is not null;
        }

        private void CacheBaseline()
        {
            if (_hasCachedBaseline || !TryGetTarget(out Transform target))
            {
                return;
            }

            _baseLocalScale = target.localScale;
            _baseLocalPosition = target.localPosition;

            CanvasGroup? canvasGroup = ResolveFadeTarget();
            _baseAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            _hasCachedBaseline = true;
        }

        private void ResetVisuals()
        {
            if (!_hasCachedBaseline)
            {
                CacheBaseline();
            }

            if (!TryGetTarget(out Transform target))
            {
                return;
            }

            target.localScale = _baseLocalScale;
            target.localPosition = _baseLocalPosition;

            CanvasGroup? canvasGroup = ResolveFadeTarget();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = _baseAlpha;
            }
        }

        private CanvasGroup? ResolveFadeTarget()
        {
            if (fadeTarget != null)
            {
                return fadeTarget;
            }

            fadeTarget = GetComponent<CanvasGroup>();
            return fadeTarget;
        }

        private AnimationCurve ResolveInsertCurve()
        {
            return insertCurve ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        private AnimationCurve ResolvePulseCurve()
        {
            return pulseCurve ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        private AnimationCurve ResolveShakeCurve()
        {
            return shakeCurve ?? AnimationCurve.Linear(0f, 1f, 1f, 0f);
        }
    }

    public sealed class DockViewPresenter : MonoBehaviour
    {
        public const int Phase1SlotCount = 7;

        private const int Phase1DockSize = Phase1SlotCount;
        private const int MaxTrackedDockSlots = 96;
        private const string DefaultPieceContainerName = "DockPieces";
        private const string SharedDockInstanceName = "SharedDockVisualInstance";
        private const string OverflowAnchorPrefix = "OverflowSlot_";
        private const string JamCalloutObjectName = "DockJamCallout";
        private const string JamRecoveryCalloutText = "Dock Jam - clear a triple next move";
        private const int DockTripleSize = 3;
        private const float DockClearLiftFraction = 0.28f;
        private const float DockClearConvergeFraction = 0.39f;
        private const float DockClearCompactionDurationSeconds = 0.40f;

        [Header("Shared Dock")]
        [SerializeField] private DockVisualConfig? dockVisualConfig;
        [SerializeField] private MeshRenderer? sharedDockRenderer;
        [SerializeField] private Material? safeMaterial;
        [SerializeField] private Material? cautionMaterial;
        [SerializeField] private Material? acuteMaterial;
        [SerializeField] private Material? failedMaterial;

        [Header("Slot Layout")]
        [SerializeField] private Transform[]? slotAnchors;
        [SerializeField] private Transform? pieceContainer;

        [Header("Piece Visuals")]
        [SerializeField] private PieceVisualRegistry? pieceRegistry;
        [SerializeField] private GameObject? fallbackPiecePrefab;
        [SerializeField] private DockFeedbackPresenter? feedbackPresenter;

        [Header("Jam Callout")]
        [SerializeField] private TextMeshPro? jamCalloutLabel;
        [SerializeField] private Vector3 jamCalloutLocalOffset = new Vector3(0f, 0.65f, 0f);

        private readonly DockSlotVisualRegistry _trackedSlots = new DockSlotVisualRegistry(MaxTrackedDockSlots);
        private GameObject? _sharedDockInstance;
        private float dockInsertDurationSeconds = ActionPlaybackSettings.DefaultDockInsertFeedbackDurationSeconds;
        private float dockClearDurationSeconds = ActionPlaybackSettings.DefaultDockClearFeedbackDurationSeconds;
        private float dockJamFeedbackDurationSeconds = ActionPlaybackSettings.DefaultDockJamFeedbackDurationSeconds;
        private bool hasLastDockClearConvergenceWorldPosition;
        private Vector3 lastDockClearConvergenceWorldPosition;
        private Coroutine? activeJamCallout;
        private DockVisualState lastDockVisualState = DockVisualState.Safe;

        public DockVisualState LastDockVisualState => lastDockVisualState;

        public string CurrentJamCalloutText => jamCalloutLabel is null ? string.Empty : jamCalloutLabel.text;

        public bool IsJamCalloutVisible => jamCalloutLabel is not null && jamCalloutLabel.gameObject.activeSelf;

        public void Rebuild(GameState state)
        {
            SyncImmediate(state);
        }

        public void ApplyPlaybackSettings(ActionPlaybackSettings settings)
        {
            if (settings is null)
            {
                return;
            }

            dockInsertDurationSeconds = settings.DockInsertFeedbackDurationSeconds;
            dockClearDurationSeconds = settings.DockClearFeedbackDurationSeconds;
            dockJamFeedbackDurationSeconds = settings.DockJamFeedbackDurationSeconds;
            ResolveFeedbackPresenter().ApplyPlaybackSettings(settings);
        }

        public void SyncImmediate(GameState state)
        {
            if (state is null)
            {
                Debug.LogWarning($"{nameof(DockViewPresenter)} requires a valid GameState to rebuild.", this);
                return;
            }

            EnsureSharedDockVisual();
            HideJamCallout();

            Transform[] anchors = ResolveSlotAnchors();
            if (anchors.Length != Phase1DockSize)
            {
                Debug.LogWarning(
                    $"{nameof(DockViewPresenter)} expected exactly {Phase1DockSize} slot anchors but found {anchors.Length}. Verify the shared dock prefab exposes the Phase 1 slot anchors.",
                    this);
            }

            SetDockVisualState(DockVisualStateResolver.FromOccupancy(CountOccupiedSlots(state.Dock), state.Dock.Size));
            ResolveFeedbackPresenter().SetFeedbackTarget(ResolveFeedbackTarget());
            ResolveFeedbackPresenter().ForceSyncToState(CountOccupiedSlots(state.Dock), state.Dock.Size);

            if (anchors.Length == 0)
            {
                ClearSlots();
                return;
            }

            RepairTrackedSlots(state.Dock, anchors);
        }

        public void ForceSyncToState(GameState state)
        {
            SyncImmediate(state);
        }

        private void OnDisable()
        {
            HideJamCallout();
        }

        public void PlayInsertFeedback(DockInserted dockInserted)
        {
            if (dockInserted is null)
            {
                return;
            }

            DockFeedbackPresenter feedback = PrepareFeedbackPresenter();
            SetDockVisualState(DockVisualStateResolver.FromOccupancy(dockInserted.OccupancyAfterInsert, Phase1DockSize));
            ApplyInsertVisual(dockInserted);
            feedback.PlayInsertFeedback();
        }

        public void PlayInsertionTravelFeedback(
            ImmutableArray<ActionEvent> dockInsertedEvents,
            Vector3? sourceWorldPosition,
            float durationSeconds)
        {
            if (dockInsertedEvents.IsDefaultOrEmpty)
            {
                return;
            }

            int insertedPieceCount = CountInsertedPieces(dockInsertedEvents);
            if (insertedPieceCount <= 0)
            {
                return;
            }

            if (!sourceWorldPosition.HasValue)
            {
                for (int i = 0; i < dockInsertedEvents.Length; i++)
                {
                    if (dockInsertedEvents[i] is DockInserted inserted && inserted.Pieces.Length > 0)
                    {
                        PlayInsertFeedback(inserted);
                    }
                }

                return;
            }

            DockFeedbackPresenter feedback = PrepareFeedbackPresenter();
            int traveledPieceIndex = 0;
            for (int i = 0; i < dockInsertedEvents.Length; i++)
            {
                if (dockInsertedEvents[i] is not DockInserted inserted || inserted.Pieces.Length <= 0)
                {
                    continue;
                }

                SetDockVisualState(DockVisualStateResolver.FromOccupancy(inserted.OccupancyAfterInsert, Phase1DockSize));
                ApplyInsertionTravelVisual(
                    inserted,
                    sourceWorldPosition.Value,
                    durationSeconds,
                    ref traveledPieceIndex,
                    insertedPieceCount);
            }

            feedback.PlayInsertFeedback();
        }

        public void PlayClearFeedback(DockCleared dockCleared)
        {
            if (dockCleared is null)
            {
                return;
            }

            DockFeedbackPresenter feedback = PrepareFeedbackPresenter();
            SetDockVisualState(DockVisualStateResolver.FromOccupancy(dockCleared.OccupancyAfterClear, Phase1DockSize));
            ApplyClearVisual(dockCleared);
            feedback.PlayTripleClearFeedback();
        }

        public void PlayWarningFeedback(DockWarningChanged dockWarningChanged)
        {
            if (dockWarningChanged is null)
            {
                return;
            }

            DockFeedbackPresenter feedback = PrepareFeedbackPresenter();
            SetDockVisualState(MapVisualState(dockWarningChanged.After));

            switch (dockWarningChanged.After)
            {
                case DockWarningLevel.Caution:
                    feedback.PlayCautionFeedback();
                    break;
                case DockWarningLevel.Acute:
                    feedback.PlayAcuteFeedback();
                    break;
                case DockWarningLevel.Fail:
                    feedback.PlayFullFeedback();
                    break;
                default:
                    feedback.SyncToState(0, Phase1DockSize);
                    break;
            }
        }

        public void PlayJamFeedback(DockJamTriggered dockJamTriggered)
        {
            if (dockJamTriggered is null)
            {
                return;
            }

            DockFeedbackPresenter feedback = PrepareFeedbackPresenter();
            SetDockVisualState(DockVisualState.Jammed);
            ShowJamCallout();
            feedback.PlayFailedFeedback();
        }

        public void PlayOverflowFeedback(DockOverflowTriggered dockOverflowTriggered)
        {
            if (dockOverflowTriggered is null)
            {
                return;
            }

            DockFeedbackPresenter feedback = PrepareFeedbackPresenter();
            HideJamCallout();
            SetDockVisualState(DockVisualState.Failed);
            feedback.PlayFailedFeedback();
        }

        public void ApplyActionResult(ActionResult result)
        {
            if (result is null)
            {
                return;
            }

            DockFeedbackPresenter feedback = ResolveFeedbackPresenter();
            feedback.SetFeedbackTarget(ResolveFeedbackTarget());

            for (int i = 0; i < result.Events.Length; i++)
            {
                switch (result.Events[i])
                {
                    case DockInserted dockInserted:
                        PlayInsertFeedback(dockInserted);
                        break;
                    case DockWarningChanged dockWarningChanged:
                        PlayWarningFeedback(dockWarningChanged);
                        break;
                    case DockCleared dockCleared:
                        PlayClearFeedback(dockCleared);
                        break;
                    case DockJamTriggered dockJamTriggered:
                        PlayJamFeedback(dockJamTriggered);
                        break;
                    case DockOverflowTriggered dockOverflowTriggered:
                        PlayOverflowFeedback(dockOverflowTriggered);
                        break;
                    case Lost lost when lost.Outcome == ActionOutcome.LossDockOverflow:
                        feedback.PlayFailedFeedback();
                        break;
                }
            }

            feedback.SyncToState(CountOccupiedSlots(result.State.Dock), result.State.Dock.Size);
        }

        public void SetDockVisualState(DockVisualState state)
        {
            lastDockVisualState = state;
            MeshRenderer? renderer = ResolveSharedDockRenderer();
            if (renderer is null)
            {
                Debug.LogWarning($"{nameof(DockViewPresenter)} is missing {nameof(sharedDockRenderer)}.", this);
                return;
            }

            Material? material = dockVisualConfig?.GetMaterial(state) ?? ResolveLegacyMaterial(state);

            if (material is null)
            {
                Debug.LogWarning($"{nameof(DockViewPresenter)} is missing a material for dock state {state}.", this);
                return;
            }

            renderer.sharedMaterial = material;
        }

        public void ClearSlots()
        {
            _trackedSlots.ClearAll(DestroyTrackedObject);
        }

        public DebrisType? GetTrackedSlotType(int slotIndex)
        {
            return _trackedSlots.GetSlotType(slotIndex);
        }

        public GameObject? GetTrackedSlotObject(int slotIndex)
        {
            return _trackedSlots.GetSlotObject(slotIndex);
        }

        public bool TryGetSlotWorldPosition(int slotIndex, out Vector3 position)
        {
            Transform[] anchors = ResolveSlotAnchors();
            return DockAnchorResolver.TryGetSlotWorldPosition(anchors, slotIndex, out position);
        }

        public bool TryGetDockCenterWorldPosition(out Vector3 position)
        {
            Transform[] anchors = ResolveSlotAnchors();
            return DockAnchorResolver.TryGetCenterWorldPosition(anchors, out position);
        }

        public bool TryGetLastDockClearConvergenceWorldPosition(out Vector3 position)
        {
            position = lastDockClearConvergenceWorldPosition;
            return hasLastDockClearConvergenceWorldPosition;
        }

        public string DescribeTrackedSlots()
        {
            return DockPiecePoseHelper.DescribeTrackedSlots(_trackedSlots);
        }

        private static int CountOccupiedSlots(Dock dock)
        {
            int occupiedSlots = 0;
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                if (dock.Slots[i].HasValue)
                {
                    occupiedSlots++;
                }
            }

            return occupiedSlots;
        }

        private Transform ResolvePieceContainer()
        {
            if (pieceContainer != null)
            {
                return pieceContainer;
            }

            Transform existingContainer = transform.Find(DefaultPieceContainerName);
            if (existingContainer is not null)
            {
                pieceContainer = existingContainer;
                return existingContainer;
            }

            GameObject containerObject = new GameObject(DefaultPieceContainerName);
            Transform containerTransform = containerObject.transform;
            containerTransform.SetParent(transform, false);
            pieceContainer = containerTransform;

            return containerTransform;
        }

        private Transform[] ResolveSlotAnchors()
        {
            slotAnchors = DockAnchorResolver.ResolveSlotAnchors(
                transform,
                _sharedDockInstance?.transform,
                slotAnchors,
                Phase1DockSize,
                this,
                nameof(DockViewPresenter));
            return slotAnchors;
        }

        private void EnsureSharedDockVisual()
        {
            GameObject? sharedDockPrefab = dockVisualConfig?.GetSharedDockPrefab();
            if (sharedDockPrefab is null)
            {
                return;
            }

            if (_sharedDockInstance is null)
            {
                Transform? existingInstance = transform.Find(SharedDockInstanceName);
                if (existingInstance is not null)
                {
                    _sharedDockInstance = existingInstance.gameObject;
                }
            }

            if (_sharedDockInstance is null)
            {
                _sharedDockInstance = Instantiate(sharedDockPrefab, transform);
                _sharedDockInstance.name = SharedDockInstanceName;
            }

            if (_sharedDockInstance is not null)
            {
                Transform sharedDockTransform = _sharedDockInstance.transform;
                sharedDockTransform.SetParent(transform, false);
                sharedDockTransform.localPosition = Vector3.zero;
                sharedDockTransform.localRotation = Quaternion.identity;
                sharedDockTransform.localScale = Vector3.one;
            }

            if (sharedDockRenderer != null
                && _sharedDockInstance != null
                && !DockAnchorResolver.IsChildOf(sharedDockRenderer.transform, _sharedDockInstance.transform))
            {
                sharedDockRenderer.enabled = false;
            }
        }

        private MeshRenderer? ResolveSharedDockRenderer()
        {
            if (_sharedDockInstance is not null)
            {
                MeshRenderer? instanceRenderer = _sharedDockInstance.GetComponentInChildren<MeshRenderer>(true);
                if (instanceRenderer is not null)
                {
                    return instanceRenderer;
                }
            }

            return sharedDockRenderer;
        }

        private Transform ResolveFeedbackTarget()
        {
            return _sharedDockInstance is not null ? _sharedDockInstance.transform : transform;
        }

        private DockFeedbackPresenter ResolveFeedbackPresenter()
        {
            if (feedbackPresenter is not null)
            {
                return feedbackPresenter;
            }

            feedbackPresenter = GetComponent<DockFeedbackPresenter>();
            if (feedbackPresenter is not null)
            {
                return feedbackPresenter;
            }

            feedbackPresenter = gameObject.AddComponent<DockFeedbackPresenter>();
            return feedbackPresenter;
        }

        private DockFeedbackPresenter PrepareFeedbackPresenter()
        {
            DockFeedbackPresenter feedback = ResolveFeedbackPresenter();
            feedback.SetFeedbackTarget(ResolveFeedbackTarget());
            return feedback;
        }

        private void ShowJamCallout()
        {
            TextMeshPro label = ResolveJamCalloutLabel();
            label.text = JamRecoveryCalloutText;
            label.gameObject.SetActive(true);

            if (activeJamCallout is not null)
            {
                StopCoroutine(activeJamCallout);
                activeJamCallout = null;
            }

            if (Application.isPlaying && isActiveAndEnabled)
            {
                activeJamCallout = StartCoroutine(HideJamCalloutAfterDelay(dockJamFeedbackDurationSeconds));
            }
        }

        private void HideJamCallout()
        {
            if (activeJamCallout is not null)
            {
                StopCoroutine(activeJamCallout);
                activeJamCallout = null;
            }

            if (jamCalloutLabel is not null)
            {
                jamCalloutLabel.gameObject.SetActive(false);
            }
        }

        private IEnumerator HideJamCalloutAfterDelay(float durationSeconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, durationSeconds));
            activeJamCallout = null;
            HideJamCallout();
        }

        private TextMeshPro ResolveJamCalloutLabel()
        {
            if (jamCalloutLabel is not null)
            {
                return jamCalloutLabel;
            }

            Transform target = ResolveFeedbackTarget();
            Transform? existing = target.Find(JamCalloutObjectName);
            if (existing is not null && existing.TryGetComponent(out TextMeshPro existingLabel))
            {
                jamCalloutLabel = existingLabel;
                return existingLabel;
            }

            GameObject labelObject = new GameObject(JamCalloutObjectName);
            labelObject.transform.SetParent(target, false);
            labelObject.transform.localPosition = jamCalloutLocalOffset;
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one;

            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 0.28f;
            label.color = new Color(1f, 0.92f, 0.35f, 1f);
            label.text = JamRecoveryCalloutText;
            label.gameObject.SetActive(false);
            jamCalloutLabel = label;
            return label;
        }

        private static DockVisualState MapVisualState(DockWarningLevel warningLevel)
        {
            return warningLevel switch
            {
                DockWarningLevel.Caution => DockVisualState.Caution,
                DockWarningLevel.Acute => DockVisualState.Acute,
                DockWarningLevel.Fail => DockVisualState.Failed,
                _ => DockVisualState.Safe,
            };
        }

        private Material? ResolveLegacyMaterial(DockVisualState state)
        {
            return state switch
            {
                DockVisualState.Safe => safeMaterial,
                DockVisualState.Caution => cautionMaterial,
                DockVisualState.Acute => acuteMaterial,
                DockVisualState.Jammed => acuteMaterial ?? failedMaterial,
                DockVisualState.Failed => failedMaterial,
                _ => null,
            };
        }

        private GameObject? ResolvePiecePrefab(DebrisType debrisType)
        {
            GameObject? registryPrefab = pieceRegistry?.GetPrefab(debrisType);
            if (registryPrefab != null)
            {
                return registryPrefab;
            }

            if (fallbackPiecePrefab != null)
            {
                return fallbackPiecePrefab;
            }

            Debug.LogWarning(
                $"{nameof(DockViewPresenter)} is missing both a registry entry and {nameof(fallbackPiecePrefab)} for debris type {debrisType}.",
                this);
            return null;
        }

        private void ApplyInsertVisual(DockInserted dockInserted)
        {
            Transform[] anchors = ResolveSlotAnchors();
            if (anchors.Length == 0)
            {
                return;
            }

            int insertedCount = dockInserted.Pieces.Length;
            int firstInsertedSlot = Mathf.Max(0, dockInserted.OccupancyAfterInsert - insertedCount);
            int maxSlotCount = Mathf.Min(_trackedSlots.Capacity, anchors.Length);

            for (int pieceIndex = 0; pieceIndex < insertedCount; pieceIndex++)
            {
                int slotIndex = firstInsertedSlot + pieceIndex;
                if (slotIndex < 0 || slotIndex >= maxSlotCount)
                {
                    continue;
                }

                AssignTrackedSlot(slotIndex, dockInserted.Pieces[pieceIndex], anchors[slotIndex]);
                AnimateInsertedSlotLower(slotIndex);
            }
        }

        private void ApplyInsertionTravelVisual(
            DockInserted dockInserted,
            Vector3 sourceWorldPosition,
            float durationSeconds,
            ref int traveledPieceIndex,
            int totalTraveledPieces)
        {
            Transform[] anchors = ResolveSlotAnchors();
            if (anchors.Length == 0)
            {
                return;
            }

            int insertedCount = dockInserted.Pieces.Length;
            int firstInsertedSlot = Mathf.Max(0, dockInserted.OccupancyAfterInsert - insertedCount);
            int maxSlotCount = Mathf.Min(_trackedSlots.Capacity, anchors.Length);

            for (int pieceIndex = 0; pieceIndex < insertedCount; pieceIndex++)
            {
                int slotIndex = firstInsertedSlot + pieceIndex;
                if (slotIndex < 0 || slotIndex >= maxSlotCount)
                {
                    traveledPieceIndex++;
                    continue;
                }

                AssignTrackedSlot(slotIndex, dockInserted.Pieces[pieceIndex], anchors[slotIndex]);
                AnimateInsertedSlotTravel(
                    slotIndex,
                    sourceWorldPosition,
                    durationSeconds,
                    traveledPieceIndex,
                    totalTraveledPieces);
                traveledPieceIndex++;
            }
        }

        private void ApplyClearVisual(DockCleared dockCleared)
        {
            Transform[] anchors = ResolveSlotAnchors();
            if (anchors.Length == 0)
            {
                return;
            }

            int piecesToClear = Mathf.Max(0, dockCleared.SetsCleared * 3);
            List<int> selectedSlots = _trackedSlots.FindFirstMatchingSlotIndices(dockCleared.Type, piecesToClear);
            int completeTripleSlotCount = selectedSlots.Count - (selectedSlots.Count % DockTripleSize);
            if (completeTripleSlotCount < DockTripleSize)
            {
                hasLastDockClearConvergenceWorldPosition = false;
                return;
            }

            if (completeTripleSlotCount < selectedSlots.Count)
            {
                selectedSlots.RemoveRange(completeTripleSlotCount, selectedSlots.Count - completeTripleSlotCount);
            }

            lastDockClearConvergenceWorldPosition = ResolveDockClearConvergenceWorldPosition(selectedSlots);
            hasLastDockClearConvergenceWorldPosition = true;

            List<GameObject> clearedObjects = _trackedSlots.DetachSlots(selectedSlots);
            if (!Application.isPlaying || !isActiveAndEnabled || dockClearDurationSeconds <= 0f)
            {
                DestroyClearedDockPieces(clearedObjects);
                CompactTrackedSlots(anchors, animateMovedPieces: false);
                return;
            }

            StartCoroutine(AnimateDockTripleClearSequence(clearedObjects, anchors, dockClearDurationSeconds));
        }

        private void RepairTrackedSlots(Dock dock, Transform[] anchors)
        {
            int maxSlotCount = Mathf.Min(_trackedSlots.Capacity, dock.Slots.Length);

            for (int slotIndex = 0; slotIndex < maxSlotCount; slotIndex++)
            {
                DebrisType? expectedType = dock.Slots[slotIndex];

                if (!expectedType.HasValue)
                {
                    ClearTrackedSlot(slotIndex);
                    continue;
                }

                AssignTrackedSlot(slotIndex, expectedType.Value, ResolveAnchorForSlot(slotIndex, anchors));
            }

            for (int slotIndex = maxSlotCount; slotIndex < _trackedSlots.Capacity; slotIndex++)
            {
                ClearTrackedSlot(slotIndex);
            }
        }

        private void CompactTrackedSlots(Transform[] anchors)
        {
            CompactTrackedSlots(anchors, animateMovedPieces: false);
        }

        private void CompactTrackedSlots(Transform[] anchors, bool animateMovedPieces)
        {
            Dictionary<GameObject, Vector3>? startPositions = animateMovedPieces
                ? CaptureTrackedSlotPositions()
                : null;
            _trackedSlots.Compact(anchors, UpdateTrackedSlotTransform, DestroyTrackedObject);

            if (startPositions is not null)
            {
                AnimateCompactedTrackedSlots(startPositions);
            }
        }

        private void AssignTrackedSlot(int slotIndex, DebrisType debrisType, Transform anchor)
        {
            _trackedSlots.AssignSlot(
                slotIndex,
                debrisType,
                anchor,
                CreateTrackedSlotObject,
                RenameTrackedSlotObject,
                UpdateTrackedSlotTransform,
                DestroyTrackedObject);
        }

        private GameObject? CreateTrackedSlotObject(int slotIndex, DebrisType debrisType)
        {
            GameObject? piecePrefab = ResolvePiecePrefab(debrisType);
            if (piecePrefab == null)
            {
                Debug.LogWarning(
                    $"{nameof(DockViewPresenter)} could not resolve a prefab for dock slot {slotIndex} ({debrisType}).",
                    this);
                return null;
            }

            GameObject pieceObject = Instantiate(piecePrefab, ResolvePieceContainer());
            ApplyDockPieceScale(pieceObject.transform, piecePrefab, debrisType);
            return pieceObject;
        }

        private void UpdateTrackedSlotTransform(int slotIndex, Transform anchor)
        {
            GameObject? trackedObject = _trackedSlots.GetSlotObject(slotIndex);
            if (trackedObject is null)
            {
                return;
            }

            Transform pieceTransform = trackedObject.transform;
            pieceTransform.position = DockPiecePoseHelper.ResolveAnchoredPosition(anchor);
            DebrisType? debrisType = _trackedSlots.GetSlotType(slotIndex);
            pieceTransform.rotation = debrisType.HasValue
                ? DockPiecePoseHelper.ResolveAnchoredRotation(anchor, ResolveDockRotationOffset(debrisType.Value))
                : anchor.rotation;

            if (debrisType.HasValue)
            {
                GameObject? piecePrefab = ResolvePiecePrefab(debrisType.Value);
                if (piecePrefab != null)
                {
                    ApplyDockPieceScale(pieceTransform, piecePrefab, debrisType.Value);
                }
            }
        }

        private void AnimateInsertedSlotLower(int slotIndex)
        {
            GameObject? trackedObject = _trackedSlots.GetSlotObject(slotIndex);
            if (trackedObject is null)
            {
                return;
            }

            Transform pieceTransform = trackedObject.transform;
            Vector3 finalPosition = pieceTransform.position;
            Quaternion finalRotation = pieceTransform.rotation;
            Vector3 finalScale = pieceTransform.localScale;

            if (!Application.isPlaying || !isActiveAndEnabled || dockInsertDurationSeconds <= 0f)
            {
                SetVisualAlpha(trackedObject, 1f);
                return;
            }

            Vector3 startPosition = DockPiecePoseHelper.ResolveLiftedPosition(finalPosition, transform.up);
            pieceTransform.position = startPosition;
            pieceTransform.rotation = finalRotation;
            pieceTransform.localScale = DockPiecePoseHelper.ResolveRaisedScale(finalScale);
            SetVisualAlpha(trackedObject, 0f);
            StartCoroutine(AnimateDockPieceLowerRoutine(
                trackedObject,
                startPosition,
                finalPosition,
                finalRotation,
                finalScale,
                dockInsertDurationSeconds));
        }

        private void AnimateInsertedSlotTravel(
            int slotIndex,
            Vector3 sourceWorldPosition,
            float durationSeconds,
            int traveledPieceIndex,
            int totalTraveledPieces)
        {
            GameObject? trackedObject = _trackedSlots.GetSlotObject(slotIndex);
            if (trackedObject is null)
            {
                return;
            }

            Transform pieceTransform = trackedObject.transform;
            Vector3 finalPosition = pieceTransform.position;
            Quaternion finalRotation = pieceTransform.rotation;
            Vector3 finalScale = pieceTransform.localScale;

            if (!Application.isPlaying || !isActiveAndEnabled || durationSeconds <= 0f)
            {
                pieceTransform.SetPositionAndRotation(finalPosition, finalRotation);
                pieceTransform.localScale = finalScale;
                SetVisualAlpha(trackedObject, 1f);
                return;
            }

            float centeredIndex = traveledPieceIndex - ((Mathf.Max(1, totalTraveledPieces) - 1) * 0.5f);
            Vector3 startPosition = sourceWorldPosition + (transform.right * centeredIndex * 0.06f);
            pieceTransform.position = startPosition;
            pieceTransform.rotation = finalRotation;
            pieceTransform.localScale = DockPiecePoseHelper.ResolveRaisedScale(finalScale);
            SetVisualAlpha(trackedObject, 0.85f);

            StartCoroutine(AnimateDockPieceTravelRoutine(
                trackedObject,
                startPosition,
                finalPosition,
                finalRotation,
                finalScale,
                durationSeconds));
        }

        private Vector3 ResolveDockClearConvergenceWorldPosition(List<int> selectedSlots)
        {
            int firstTripleCount = Mathf.Min(DockTripleSize, selectedSlots.Count);
            int middleIndex = Mathf.Clamp(firstTripleCount / 2, 0, selectedSlots.Count - 1);
            int middleSlot = selectedSlots[middleIndex];
            GameObject? middleObject = _trackedSlots.GetSlotObject(middleSlot);
            if (middleObject is not null)
            {
                return middleObject.transform.position;
            }

            if (TryGetSlotWorldPosition(middleSlot, out Vector3 slotPosition))
            {
                return slotPosition;
            }

            return TryGetDockCenterWorldPosition(out Vector3 dockCenter)
                ? dockCenter
                : transform.position;
        }

        private Dictionary<GameObject, Vector3> CaptureTrackedSlotPositions()
        {
            Dictionary<GameObject, Vector3> startPositions = new Dictionary<GameObject, Vector3>();
            for (int slotIndex = 0; slotIndex < _trackedSlots.Capacity; slotIndex++)
            {
                GameObject? trackedObject = _trackedSlots.GetSlotObject(slotIndex);
                if (trackedObject is not null && !startPositions.ContainsKey(trackedObject))
                {
                    startPositions.Add(trackedObject, trackedObject.transform.position);
                }
            }

            return startPositions;
        }

        private void AnimateCompactedTrackedSlots(Dictionary<GameObject, Vector3> startPositions)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || DockClearCompactionDurationSeconds <= 0f)
            {
                return;
            }

            foreach (KeyValuePair<GameObject, Vector3> entry in startPositions)
            {
                GameObject trackedObject = entry.Key;
                if (trackedObject is null)
                {
                    continue;
                }

                Transform pieceTransform = trackedObject.transform;
                Vector3 startPosition = entry.Value;
                Vector3 finalPosition = pieceTransform.position;
                if ((finalPosition - startPosition).sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                Quaternion finalRotation = pieceTransform.rotation;
                Vector3 finalScale = pieceTransform.localScale;
                pieceTransform.position = startPosition;
                StartCoroutine(AnimateDockPieceSlideRoutine(
                    trackedObject,
                    startPosition,
                    finalPosition,
                    finalRotation,
                    finalScale,
                    DockClearCompactionDurationSeconds));
            }
        }

        private void DestroyClearedDockPieces(List<GameObject> clearedObjects)
        {
            for (int i = 0; i < clearedObjects.Count; i++)
            {
                DestroyTrackedObject(clearedObjects[i]);
            }
        }

        private System.Collections.IEnumerator AnimateDockPieceLowerRoutine(
            GameObject pieceObject,
            Vector3 startPosition,
            Vector3 finalPosition,
            Quaternion finalRotation,
            Vector3 finalScale,
            float durationSeconds)
        {
            float clampedDuration = Mathf.Max(0.01f, durationSeconds);
            float elapsed = 0f;

            while (elapsed < clampedDuration)
            {
                if (pieceObject is null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                float eased = Mathf.SmoothStep(0f, 1f, normalized);
                Transform pieceTransform = pieceObject.transform;
                pieceTransform.position = Vector3.LerpUnclamped(startPosition, finalPosition, eased);
                pieceTransform.rotation = finalRotation;
                pieceTransform.localScale = Vector3.LerpUnclamped(DockPiecePoseHelper.ResolveRaisedScale(finalScale), finalScale, eased);
                SetVisualAlpha(pieceObject, normalized);
                yield return null;
            }

            if (pieceObject is not null)
            {
                Transform pieceTransform = pieceObject.transform;
                pieceTransform.position = finalPosition;
                pieceTransform.rotation = finalRotation;
                pieceTransform.localScale = finalScale;
                SetVisualAlpha(pieceObject, 1f);
            }
        }

        private System.Collections.IEnumerator AnimateDockPieceTravelRoutine(
            GameObject pieceObject,
            Vector3 startPosition,
            Vector3 finalPosition,
            Quaternion finalRotation,
            Vector3 finalScale,
            float durationSeconds)
        {
            float clampedDuration = Mathf.Max(0.01f, durationSeconds);
            float elapsed = 0f;
            Vector3 raisedScale = DockPiecePoseHelper.ResolveRaisedScale(finalScale);

            while (elapsed < clampedDuration)
            {
                if (pieceObject is null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                float eased = 1f - Mathf.Pow(1f - normalized, 3f);
                float arc = Mathf.Sin(normalized * Mathf.PI) * 0.18f;
                Transform pieceTransform = pieceObject.transform;
                pieceTransform.position = Vector3.LerpUnclamped(startPosition, finalPosition, eased) + (transform.up * arc);
                pieceTransform.rotation = finalRotation;
                pieceTransform.localScale = Vector3.LerpUnclamped(raisedScale, finalScale, eased);
                SetVisualAlpha(pieceObject, Mathf.Lerp(0.85f, 1f, normalized));
                yield return null;
            }

            if (pieceObject is not null)
            {
                Transform pieceTransform = pieceObject.transform;
                pieceTransform.SetPositionAndRotation(finalPosition, finalRotation);
                pieceTransform.localScale = finalScale;
                SetVisualAlpha(pieceObject, 1f);
            }
        }

        private System.Collections.IEnumerator AnimateDockTripleClearSequence(
            List<GameObject> clearedObjects,
            Transform[] anchors,
            float durationSeconds)
        {
            if (clearedObjects.Count == 0)
            {
                yield break;
            }

            int tripleStart = 0;
            while (tripleStart < clearedObjects.Count)
            {
                int tripleCount = Mathf.Min(DockTripleSize, clearedObjects.Count - tripleStart);
                List<GameObject> tripleObjects = clearedObjects.GetRange(tripleStart, tripleCount);
                yield return AnimateDockTripleClearRoutine(tripleObjects, durationSeconds);
                tripleStart += DockTripleSize;
            }

            CompactTrackedSlots(anchors, animateMovedPieces: true);
        }

        private System.Collections.IEnumerator AnimateDockTripleClearRoutine(List<GameObject> tripleObjects, float durationSeconds)
        {
            if (tripleObjects.Count == 0)
            {
                yield break;
            }

            float safeDuration = Mathf.Max(0.01f, durationSeconds);
            float liftDuration = Mathf.Max(0.01f, safeDuration * DockClearLiftFraction);
            float convergeDuration = Mathf.Max(0.01f, safeDuration * DockClearConvergeFraction);
            float disappearDuration = Mathf.Max(0.01f, safeDuration - liftDuration - convergeDuration);

            int middleIndex = Mathf.Clamp(tripleObjects.Count / 2, 0, tripleObjects.Count - 1);
            GameObject? middleObject = tripleObjects[middleIndex];
            Vector3 convergencePosition = middleObject is not null
                ? DockPiecePoseHelper.ResolveLiftedPosition(middleObject.transform.position, transform.up)
                : transform.position;

            Vector3[] basePositions = new Vector3[tripleObjects.Count];
            Vector3[] liftedPositions = new Vector3[tripleObjects.Count];
            Vector3[] baseScales = new Vector3[tripleObjects.Count];
            Vector3[] raisedScales = new Vector3[tripleObjects.Count];
            Quaternion[] rotations = new Quaternion[tripleObjects.Count];

            for (int i = 0; i < tripleObjects.Count; i++)
            {
                GameObject pieceObject = tripleObjects[i];
                if (pieceObject is null)
                {
                    continue;
                }

                Transform pieceTransform = pieceObject.transform;
                basePositions[i] = pieceTransform.position;
                liftedPositions[i] = DockPiecePoseHelper.ResolveLiftedPosition(basePositions[i], transform.up);
                baseScales[i] = pieceTransform.localScale;
                raisedScales[i] = DockPiecePoseHelper.ResolveRaisedScale(baseScales[i]);
                rotations[i] = pieceTransform.rotation;
                SetVisualAlpha(pieceObject, 1f);
            }

            float elapsed = 0f;

            while (elapsed < liftDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / liftDuration);
                float eased = 1f - Mathf.Pow(1f - normalized, 3f);
                for (int i = 0; i < tripleObjects.Count; i++)
                {
                    GameObject pieceObject = tripleObjects[i];
                    if (pieceObject is null)
                    {
                        continue;
                    }

                    Transform pieceTransform = pieceObject.transform;
                    pieceTransform.SetPositionAndRotation(
                        Vector3.LerpUnclamped(basePositions[i], liftedPositions[i], eased),
                        rotations[i]);
                    pieceTransform.localScale = Vector3.LerpUnclamped(baseScales[i], raisedScales[i], eased);
                    SetVisualAlpha(pieceObject, 1f);
                }

                yield return null;
            }

            elapsed = 0f;
            while (elapsed < convergeDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / convergeDuration);
                float eased = Mathf.SmoothStep(0f, 1f, normalized);
                for (int i = 0; i < tripleObjects.Count; i++)
                {
                    GameObject pieceObject = tripleObjects[i];
                    if (pieceObject is null)
                    {
                        continue;
                    }

                    Vector3 targetPosition = i == middleIndex ? liftedPositions[i] : convergencePosition;
                    Transform pieceTransform = pieceObject.transform;
                    pieceTransform.SetPositionAndRotation(
                        Vector3.LerpUnclamped(liftedPositions[i], targetPosition, eased),
                        rotations[i]);
                    pieceTransform.localScale = raisedScales[i];
                    SetVisualAlpha(pieceObject, 1f);
                }

                yield return null;
            }

            elapsed = 0f;
            while (elapsed < disappearDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / disappearDuration);
                float eased = Mathf.SmoothStep(0f, 1f, normalized);
                for (int i = 0; i < tripleObjects.Count; i++)
                {
                    GameObject pieceObject = tripleObjects[i];
                    if (pieceObject is null)
                    {
                        continue;
                    }

                    Transform pieceTransform = pieceObject.transform;
                    pieceTransform.position = Vector3.LerpUnclamped(pieceTransform.position, convergencePosition, eased);
                    pieceTransform.rotation = rotations[i];
                    pieceTransform.localScale = Vector3.LerpUnclamped(raisedScales[i], baseScales[i] * 0.2f, eased);
                    SetVisualAlpha(pieceObject, 1f - eased);
                }

                yield return null;
            }

            DestroyClearedDockPieces(tripleObjects);
        }

        private System.Collections.IEnumerator AnimateDockPieceSlideRoutine(
            GameObject pieceObject,
            Vector3 startPosition,
            Vector3 finalPosition,
            Quaternion finalRotation,
            Vector3 finalScale,
            float durationSeconds)
        {
            float clampedDuration = Mathf.Max(0.01f, durationSeconds);
            float elapsed = 0f;

            while (elapsed < clampedDuration)
            {
                if (pieceObject is null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                float eased = Mathf.SmoothStep(0f, 1f, normalized);
                Transform pieceTransform = pieceObject.transform;
                pieceTransform.SetPositionAndRotation(Vector3.LerpUnclamped(startPosition, finalPosition, eased), finalRotation);
                pieceTransform.localScale = finalScale;
                SetVisualAlpha(pieceObject, 1f);
                yield return null;
            }

            if (pieceObject is not null)
            {
                Transform pieceTransform = pieceObject.transform;
                pieceTransform.SetPositionAndRotation(finalPosition, finalRotation);
                pieceTransform.localScale = finalScale;
                SetVisualAlpha(pieceObject, 1f);
            }
        }

        private void ApplyDockPieceScale(Transform pieceTransform, GameObject piecePrefab, DebrisType debrisType)
        {
            pieceTransform.localScale = DockPiecePoseHelper.ResolveDockScale(piecePrefab, ResolveDockScaleMultiplier(debrisType));
        }

        private static void SetVisualAlpha(GameObject contentObject, float alpha)
        {
            float clampedAlpha = Mathf.Clamp01(alpha);

            SpriteRenderer[] spriteRenderers = contentObject.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                Color color = spriteRenderer.color;
                color.a = clampedAlpha;
                spriteRenderer.color = color;
            }

            Graphic[] graphics = contentObject.GetComponentsInChildren<Graphic>(includeInactive: true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                Color color = graphic.color;
                color.a = clampedAlpha;
                graphic.color = color;
            }
        }

        private static int CountInsertedPieces(ImmutableArray<ActionEvent> dockInsertedEvents)
        {
            int count = 0;
            for (int i = 0; i < dockInsertedEvents.Length; i++)
            {
                if (dockInsertedEvents[i] is DockInserted inserted)
                {
                    count += inserted.Pieces.Length;
                }
            }

            return count;
        }

        private Quaternion ResolveDockRotationOffset(DebrisType debrisType)
        {
            return pieceRegistry is not null
                ? pieceRegistry.GetDockRotationOffset(debrisType)
                : Quaternion.identity;
        }

        private float ResolveDockScaleMultiplier(DebrisType debrisType)
        {
            return pieceRegistry is not null
                ? pieceRegistry.GetDockScaleMultiplier(debrisType)
                : 1f;
        }

        private void RenameTrackedSlotObject(int slotIndex, DebrisType debrisType)
        {
            GameObject? trackedObject = _trackedSlots.GetSlotObject(slotIndex);
            if (trackedObject is null)
            {
                return;
            }

            trackedObject.name = DockPiecePoseHelper.FormatPieceObjectName(slotIndex, debrisType);
        }

        private void ClearTrackedSlot(int slotIndex)
        {
            _trackedSlots.ClearSlot(slotIndex, DestroyTrackedObject);
        }

        private void DestroyTrackedObject(GameObject? trackedObject)
        {
            if (trackedObject is null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(trackedObject);
            }
            else
            {
                DestroyImmediate(trackedObject);
            }
        }

        private Transform ResolveAnchorForSlot(int slotIndex, Transform[] anchors)
        {
            return DockAnchorResolver.ResolveAnchorForSlot(slotIndex, anchors, transform, OverflowAnchorPrefix);
        }
    }
}
