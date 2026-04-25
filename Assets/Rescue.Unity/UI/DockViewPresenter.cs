using System.Collections;
using System.Collections.Generic;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
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
        [SerializeField] private float insertDuration = 0.18f;

        [Header("Pressure")]
        [SerializeField] private float cautionPulseDuration = 0.5f;
        [SerializeField] private float acuteShakeAmount = 0.05f;
        [SerializeField] private float acuteShakeDuration = 0.4f;
        [SerializeField] private float failedPulseDuration = 0.7f;

        [Header("Curves")]
        [SerializeField] private AnimationCurve? insertCurve;
        [SerializeField] private AnimationCurve? pulseCurve;
        [SerializeField] private AnimationCurve? shakeCurve;

        private Coroutine? _activeFeedback;
        private Vector3 _baseLocalScale = Vector3.one;
        private Vector3 _baseLocalPosition = Vector3.zero;
        private float _baseAlpha = 1f;
        private bool _hasCachedBaseline;

        public float InsertPopScale => insertPopScale;

        public float InsertDuration => insertDuration;

        public float CautionPulseDuration => cautionPulseDuration;

        public float AcuteShakeAmount => acuteShakeAmount;

        public float AcuteShakeDuration => acuteShakeDuration;

        public float FailedPulseDuration => failedPulseDuration;

        public DockFeedbackType SelectFeedbackType(int occupancy, int dockSize)
        {
            return DockFeedbackTypeResolver.FromOccupancy(occupancy, dockSize);
        }

        public void PlayInsertFeedback()
        {
            PlayRoutine(CreatePulseRoutine(insertDuration, insertPopScale, ResolveInsertCurve()));
        }

        public void PlayCautionFeedback()
        {
            PlayRoutine(CreatePulseRoutine(cautionPulseDuration, DefaultPulseScaleMultiplier, ResolvePulseCurve()));
        }

        public void PlayAcuteFeedback()
        {
            PlayRoutine(CreateShakeRoutine(acuteShakeDuration, acuteShakeAmount, ResolveShakeCurve()));
        }

        public void PlayFailedFeedback()
        {
            PlayRoutine(CreatePulseRoutine(failedPulseDuration, FailedHoldScaleMultiplier, ResolvePulseCurve(), holdAtEnd: true));
        }

        public void PlayTripleClearFeedback()
        {
            PlayRoutine(CreateTripleClearRoutine());
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

        private IEnumerator CreateTripleClearRoutine()
        {
            if (!TryGetTarget(out Transform target))
            {
                yield break;
            }

            CanvasGroup? canvasGroup = ResolveFadeTarget();
            float duration = Mathf.Max(0.01f, insertDuration);
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
        private const string DefaultPieceContainerName = "DockPieces";
        private const string SharedDockInstanceName = "SharedDockVisualInstance";

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

        private readonly List<GameObject> _spawnedPieces = new List<GameObject>();
        private GameObject? _sharedDockInstance;

        public void Rebuild(GameState state)
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
            ResolveFeedbackPresenter().SyncToState(CountOccupiedSlots(state.Dock), state.Dock.Size);

            ClearSlots();
            Transform container = ResolvePieceContainer();

            if (anchors.Length == 0)
            {
                return;
            }

            int maxSlots = Mathf.Min(state.Dock.Slots.Length, anchors.Length);
            for (int slotIndex = 0; slotIndex < maxSlots; slotIndex++)
            {
                DebrisType? debrisType = state.Dock.Slots[slotIndex];
                if (!debrisType.HasValue)
                {
                    continue;
                }

                GameObject? piecePrefab = ResolvePiecePrefab(debrisType.Value);
                if (piecePrefab is null)
                {
                    Debug.LogWarning(
                        $"{nameof(DockViewPresenter)} could not resolve a prefab for dock slot {slotIndex} ({debrisType.Value}).",
                        this);
                    continue;
                }

                GameObject pieceObject = Instantiate(piecePrefab, container);
                pieceObject.name = $"DockPiece_{slotIndex:00}_{debrisType.Value}";

                Transform pieceTransform = pieceObject.transform;
                pieceTransform.position = anchors[slotIndex].position;
                pieceTransform.rotation = anchors[slotIndex].rotation;
                pieceTransform.localScale = piecePrefab.transform.localScale;

                _spawnedPieces.Add(pieceObject);
            }
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
                    case DockInserted:
                        feedback.PlayInsertFeedback();
                        break;
                    case DockWarningChanged dockWarningChanged when dockWarningChanged.After == DockWarningLevel.Caution:
                        feedback.PlayCautionFeedback();
                        break;
                    case DockWarningChanged dockWarningChanged when dockWarningChanged.After == DockWarningLevel.Acute:
                        feedback.PlayAcuteFeedback();
                        break;
                    case DockCleared:
                        feedback.PlayTripleClearFeedback();
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
            for (int i = _spawnedPieces.Count - 1; i >= 0; i--)
            {
                GameObject? spawnedPiece = _spawnedPieces[i];
                if (spawnedPiece is null)
                {
                    _spawnedPieces.RemoveAt(i);
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(spawnedPiece);
                }
                else
                {
                    DestroyImmediate(spawnedPiece);
                }

                _spawnedPieces.RemoveAt(i);
            }
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
    }
}
