using System.Collections;
using System.Collections.Generic;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.Presentation;
using UnityEngine;

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

            if (clampedOccupancy >= dockSize)
            {
                return DockFeedbackType.Failed;
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
        private const float FailedHoldScaleMultiplier = 1.04f;
        private const float TripleClearMinScaleMultiplier = 0.9f;

        [Header("Feedback Target")]
        [SerializeField] private Transform? feedbackTarget;
        [SerializeField] private CanvasGroup? fadeTarget;

        [Header("Insert")]
        [SerializeField] private float insertPopScale = 1.12f;

        [Header("Pressure")]
        [SerializeField] private float acuteShakeAmount = 0.05f;

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
        private float acuteShakeDurationSeconds = ActionPlaybackSettings.DefaultDockWarningAcuteDurationSeconds;
        private float failedPulseDurationSeconds = ActionPlaybackSettings.DefaultDockJamFeedbackDurationSeconds;

        public float InsertPopScale => insertPopScale;

        public float InsertDuration => insertDurationSeconds;

        public float CautionPulseDuration => cautionPulseDurationSeconds;

        public float AcuteShakeAmount => acuteShakeAmount;

        public float AcuteShakeDuration => acuteShakeDurationSeconds;

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
            acuteShakeDurationSeconds = settings.DockWarningAcuteDurationSeconds;
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
            PlayRoutine(CreateShakeRoutine(acuteShakeDurationSeconds, acuteShakeAmount, ResolveShakeCurve()));
        }

        public void PlayFailedFeedback()
        {
            PlayRoutine(CreatePulseRoutine(failedPulseDurationSeconds, FailedHoldScaleMultiplier, ResolvePulseCurve(), holdAtEnd: true));
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
            if (feedbackType == DockFeedbackType.Failed)
            {
                CacheBaseline();
                ResetVisuals();

                if (TryGetTarget(out Transform target))
                {
                    target.localScale = _baseLocalScale * FailedHoldScaleMultiplier;
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

        private IEnumerator CreateShakeRoutine(float duration, float amount, AnimationCurve curve)
        {
            if (!TryGetTarget(out Transform target))
            {
                yield break;
            }

            float safeDuration = Mathf.Max(0.01f, duration);
            float safeAmount = Mathf.Max(0f, amount);
            float elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / safeDuration);
                float strength = Mathf.Clamp01(1f - normalized);
                float wave = curve.Evaluate(normalized) * safeAmount * strength;
                target.localPosition = _baseLocalPosition + new Vector3(Mathf.Sin(normalized * Mathf.PI * 8f) * wave, 0f, 0f);
                yield return null;
            }

            target.localPosition = _baseLocalPosition;
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

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = normalized * normalized;
                target.localScale = Vector3.LerpUnclamped(baseScale, minScale, eased);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f - eased;
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
        private const int Phase1DockSize = 7;
        private const int MaxTrackedDockSlots = 96;
        private const string DefaultPieceContainerName = "DockPieces";
        private const string SharedDockInstanceName = "SharedDockVisualInstance";
        private const string OverflowAnchorPrefix = "OverflowSlot_";

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

        private readonly DebrisType?[] _trackedSlotTypes = new DebrisType?[MaxTrackedDockSlots];
        private readonly GameObject?[] _trackedSlotObjects = new GameObject?[MaxTrackedDockSlots];
        private GameObject? _sharedDockInstance;

        public void Rebuild(GameState state)
        {
            SyncImmediate(state);
        }

        public void ApplyPlaybackSettings(ActionPlaybackSettings settings)
        {
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
            SetDockVisualState(DockVisualState.Failed);
            feedback.PlayFailedFeedback();
        }

        public void PlayOverflowFeedback(DockOverflowTriggered dockOverflowTriggered)
        {
            if (dockOverflowTriggered is null)
            {
                return;
            }

            DockFeedbackPresenter feedback = PrepareFeedbackPresenter();
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
            for (int slotIndex = 0; slotIndex < _trackedSlotTypes.Length; slotIndex++)
            {
                ClearTrackedSlot(slotIndex);
            }
        }

        public DebrisType? GetTrackedSlotType(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _trackedSlotTypes.Length)
            {
                return null;
            }

            return _trackedSlotTypes[slotIndex];
        }

        public GameObject? GetTrackedSlotObject(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _trackedSlotObjects.Length)
            {
                return null;
            }

            return _trackedSlotObjects[slotIndex];
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
            Transform[] configAnchors = FindSharedDockAnchors();
            if (IsValidAnchorArray(configAnchors))
            {
                slotAnchors = configAnchors;
                return configAnchors;
            }

            if (IsValidAnchorArray(slotAnchors))
            {
                Transform[] assignedAnchors = slotAnchors ?? System.Array.Empty<Transform>();
                return assignedAnchors;
            }

            List<Transform> anchors = new List<Transform>(Phase1DockSize);
            for (int slotIndex = 0; slotIndex < Phase1DockSize; slotIndex++)
            {
                string anchorName = $"Slot_{slotIndex:00}";
                Transform? anchor = transform.Find(anchorName);
                if (anchor is null)
                {
                    GameObject anchorObject = new GameObject(anchorName);
                    anchor = anchorObject.transform;
                    anchor.SetParent(transform, false);
                    Debug.LogWarning(
                        $"{nameof(DockViewPresenter)} could not find anchor '{anchorName}'. Created a fallback anchor; prefer anchors provided by the shared dock prefab.",
                        this);
                }

                anchors.Add(anchor);
            }

            slotAnchors = anchors.ToArray();
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

            if (sharedDockRenderer != null && _sharedDockInstance != null && !IsChildOf(sharedDockRenderer.transform, _sharedDockInstance.transform))
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
                DockVisualState.Failed => failedMaterial,
                _ => null,
            };
        }

        private Transform[] FindSharedDockAnchors()
        {
            if (_sharedDockInstance is null)
            {
                return System.Array.Empty<Transform>();
            }

            List<Transform> anchors = new List<Transform>(Phase1DockSize);
            for (int slotIndex = 0; slotIndex < Phase1DockSize; slotIndex++)
            {
                Transform? anchor = FindChildRecursive(_sharedDockInstance.transform, $"Slot_{slotIndex:00}");
                if (anchor is null)
                {
                    return System.Array.Empty<Transform>();
                }

                anchors.Add(anchor);
            }

            return anchors.ToArray();
        }

        private static Transform? FindChildRecursive(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                Transform child = root.GetChild(childIndex);
                Transform? match = FindChildRecursive(child, name);
                if (match is not null)
                {
                    return match;
                }
            }

            return null;
        }

        private static bool IsChildOf(Transform child, Transform parent)
        {
            Transform? current = child;
            while (current is not null)
            {
                if (current == parent)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private GameObject? ResolvePiecePrefab(DebrisType debrisType)
        {
            GameObject? registryPrefab = pieceRegistry?.GetPrefab(debrisType);
            if (registryPrefab is not null)
            {
                return registryPrefab;
            }

            if (fallbackPiecePrefab is not null)
            {
                return fallbackPiecePrefab;
            }

            Debug.LogWarning(
                $"{nameof(DockViewPresenter)} is missing both a registry entry and {nameof(fallbackPiecePrefab)} for debris type {debrisType}.",
                this);
            return null;
        }

        private static bool IsValidAnchorArray(Transform[]? anchors)
        {
            if (anchors is null || anchors.Length != Phase1DockSize)
            {
                return false;
            }

            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] is null)
                {
                    return false;
                }
            }

            return true;
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
            int maxSlotCount = Mathf.Min(_trackedSlotTypes.Length, anchors.Length);

            for (int pieceIndex = 0; pieceIndex < insertedCount; pieceIndex++)
            {
                int slotIndex = firstInsertedSlot + pieceIndex;
                if (slotIndex < 0 || slotIndex >= maxSlotCount)
                {
                    continue;
                }

                AssignTrackedSlot(slotIndex, dockInserted.Pieces[pieceIndex], anchors[slotIndex]);
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
            for (int slotIndex = 0; slotIndex < _trackedSlotTypes.Length && piecesToClear > 0; slotIndex++)
            {
                if (_trackedSlotTypes[slotIndex] != dockCleared.Type)
                {
                    continue;
                }

                ClearTrackedSlot(slotIndex);
                piecesToClear--;
            }

            CompactTrackedSlots(anchors);
        }

        private void RepairTrackedSlots(Dock dock, Transform[] anchors)
        {
            int maxSlotCount = Mathf.Min(_trackedSlotTypes.Length, dock.Slots.Length);

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

            for (int slotIndex = maxSlotCount; slotIndex < _trackedSlotTypes.Length; slotIndex++)
            {
                ClearTrackedSlot(slotIndex);
            }
        }

        private void CompactTrackedSlots(Transform[] anchors)
        {
            int maxSlotCount = Mathf.Min(_trackedSlotTypes.Length, anchors.Length);
            int writeIndex = 0;

            for (int readIndex = 0; readIndex < maxSlotCount; readIndex++)
            {
                DebrisType? trackedType = _trackedSlotTypes[readIndex];
                if (!trackedType.HasValue)
                {
                    continue;
                }

                if (writeIndex != readIndex)
                {
                    _trackedSlotTypes[writeIndex] = trackedType;
                    _trackedSlotObjects[writeIndex] = _trackedSlotObjects[readIndex];
                    _trackedSlotTypes[readIndex] = null;
                    _trackedSlotObjects[readIndex] = null;
                }

                UpdateTrackedSlotTransform(writeIndex, anchors[writeIndex]);
                writeIndex++;
            }

            for (int slotIndex = writeIndex; slotIndex < _trackedSlotTypes.Length; slotIndex++)
            {
                _trackedSlotTypes[slotIndex] = null;
                if (slotIndex >= maxSlotCount)
                {
                    ClearTrackedSlot(slotIndex);
                    continue;
                }

                if (_trackedSlotObjects[slotIndex] is not null)
                {
                    DestroyTrackedObject(_trackedSlotObjects[slotIndex]);
                    _trackedSlotObjects[slotIndex] = null;
                }
            }
        }

        private void AssignTrackedSlot(int slotIndex, DebrisType debrisType, Transform anchor)
        {
            if (slotIndex < 0 || slotIndex >= _trackedSlotTypes.Length)
            {
                return;
            }

            if (_trackedSlotTypes[slotIndex] == debrisType && _trackedSlotObjects[slotIndex] is not null)
            {
                RenameTrackedSlotObject(slotIndex, debrisType);
                UpdateTrackedSlotTransform(slotIndex, anchor);
                return;
            }

            if (_trackedSlotTypes[slotIndex] != debrisType)
            {
                ClearTrackedSlot(slotIndex);
            }

            _trackedSlotTypes[slotIndex] = debrisType;

            if (_trackedSlotObjects[slotIndex] is null)
            {
                _trackedSlotObjects[slotIndex] = CreateTrackedSlotObject(slotIndex, debrisType);
            }

            RenameTrackedSlotObject(slotIndex, debrisType);
            UpdateTrackedSlotTransform(slotIndex, anchor);
        }

        private GameObject? CreateTrackedSlotObject(int slotIndex, DebrisType debrisType)
        {
            GameObject? piecePrefab = ResolvePiecePrefab(debrisType);
            if (piecePrefab is null)
            {
                Debug.LogWarning(
                    $"{nameof(DockViewPresenter)} could not resolve a prefab for dock slot {slotIndex} ({debrisType}).",
                    this);
                return null;
            }

            GameObject pieceObject = Instantiate(piecePrefab, ResolvePieceContainer());
            pieceObject.transform.localScale = piecePrefab.transform.localScale;
            return pieceObject;
        }

        private void UpdateTrackedSlotTransform(int slotIndex, Transform anchor)
        {
            GameObject? trackedObject = _trackedSlotObjects[slotIndex];
            if (trackedObject is null)
            {
                return;
            }

            Transform pieceTransform = trackedObject.transform;
            pieceTransform.position = anchor.position;
            pieceTransform.rotation = anchor.rotation;
        }

        private void RenameTrackedSlotObject(int slotIndex, DebrisType debrisType)
        {
            GameObject? trackedObject = _trackedSlotObjects[slotIndex];
            if (trackedObject is null)
            {
                return;
            }

            trackedObject.name = $"DockPiece_{slotIndex:00}_{debrisType}";
        }

        private void ClearTrackedSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _trackedSlotTypes.Length)
            {
                return;
            }

            _trackedSlotTypes[slotIndex] = null;

            if (_trackedSlotObjects[slotIndex] is null)
            {
                return;
            }

            DestroyTrackedObject(_trackedSlotObjects[slotIndex]);
            _trackedSlotObjects[slotIndex] = null;
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
            if (slotIndex >= 0 && slotIndex < anchors.Length)
            {
                return anchors[slotIndex];
            }

            string anchorName = OverflowAnchorPrefix + slotIndex.ToString("00");
            Transform? existingAnchor = transform.Find(anchorName);
            if (existingAnchor is not null)
            {
                return existingAnchor;
            }

            GameObject anchorObject = new GameObject(anchorName);
            Transform anchor = anchorObject.transform;
            anchor.SetParent(transform, false);

            if (anchors.Length > 0)
            {
                Transform lastAnchor = anchors[anchors.Length - 1];
                anchor.position = lastAnchor.position + new Vector3(slotIndex - anchors.Length + 1, 0f, 0f);
                anchor.rotation = lastAnchor.rotation;
                return anchor;
            }

            anchor.localPosition = new Vector3(slotIndex, 0f, 0f);
            return anchor;
        }
    }
}
