using System.Collections;
using System.Collections.Generic;
using Rescue.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace Rescue.Unity.BoardPresentation
{
    public interface ITargetFeedbackHook
    {
        void HandleTargetFeedback(TargetFeedbackEvent feedbackEvent, GameState? previousState, GameState currentState);
    }

    public sealed class TargetFeedbackPresenter : MonoBehaviour
    {
        private const string DefaultFeedbackRootName = "TargetFeedback";

        [SerializeField] private BoardGridViewPresenter? gridView;
        [SerializeField] private BoardContentViewPresenter? contentView;
        [SerializeField] private Transform? feedbackRoot;
        [SerializeField] private GameObject? nearRescueFxPrefab;
        [SerializeField] private GameObject? extractionFxPrefab;
        [SerializeField] private GameObject? fallbackTargetPrefab;
        [SerializeField] private MonoBehaviour? maeReactionHook;
        [SerializeField] private MonoBehaviour? aftercareCardHook;
        [SerializeField] private float feedbackYOffset = 0.1f;
        [SerializeField] private float nearRescuePulseDuration = 0.45f;
        [SerializeField] private float nearRescuePulseScale = 1.08f;
        [SerializeField] private float nearRescueAlphaMultiplier = 1.15f;
        [SerializeField] private float extractionHoldDuration = 0.55f;

        private readonly List<GameObject> transientObjects = new List<GameObject>();

        public void Apply(GameState? previousState, GameState currentState)
        {
            if (currentState is null)
            {
                Debug.LogWarning($"{nameof(TargetFeedbackPresenter)} requires a valid GameState.", this);
                return;
            }

            TargetFeedbackResolution resolution = TargetFeedbackResolver.Resolve(previousState, currentState);

            for (int i = 0; i < resolution.Events.Length; i++)
            {
                TargetFeedbackEvent feedbackEvent = resolution.Events[i];
                switch (feedbackEvent.Kind)
                {
                    case TargetFeedbackKind.NearRescue:
                        PlayNearRescue(feedbackEvent, previousState, currentState);
                        break;
                    case TargetFeedbackKind.Extraction:
                        PlayExtraction(feedbackEvent, previousState, currentState);
                        break;
                }
            }
        }

        public void ClearFeedback()
        {
            StopAllCoroutines();

            for (int i = transientObjects.Count - 1; i >= 0; i--)
            {
                GameObject? transientObject = transientObjects[i];
                if (transientObject == null)
                {
                    transientObjects.RemoveAt(i);
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(transientObject);
                }
                else
                {
                    DestroyImmediate(transientObject);
                }

                transientObjects.RemoveAt(i);
            }
        }

        private void PlayNearRescue(TargetFeedbackEvent feedbackEvent, GameState? previousState, GameState currentState)
        {
            GameObject? targetObject = ResolveTargetObject(feedbackEvent);
            if (targetObject != null)
            {
                PulseTarget(targetObject);
            }

            SpawnFeedbackPrefab(nearRescueFxPrefab, feedbackEvent, autoDestroyDelay: nearRescuePulseDuration * 1.5f);
            NotifyHook(maeReactionHook, feedbackEvent, previousState, currentState);
        }

        private void PlayExtraction(TargetFeedbackEvent feedbackEvent, GameState? previousState, GameState currentState)
        {
            SpawnFeedbackPrefab(extractionFxPrefab, feedbackEvent, autoDestroyDelay: extractionHoldDuration);
            NotifyHook(maeReactionHook, feedbackEvent, previousState, currentState);
            NotifyHook(aftercareCardHook, feedbackEvent, previousState, currentState);
        }

        private GameObject? ResolveTargetObject(TargetFeedbackEvent feedbackEvent)
        {
            if (contentView != null
                && contentView.TryGetTargetInstance(feedbackEvent.TargetId, out GameObject targetObject)
                && targetObject != null)
            {
                return targetObject;
            }

            return SpawnFeedbackPrefab(fallbackTargetPrefab, feedbackEvent, autoDestroyDelay: nearRescuePulseDuration * 1.5f);
        }

        private GameObject? SpawnFeedbackPrefab(GameObject? prefab, TargetFeedbackEvent feedbackEvent, float autoDestroyDelay)
        {
            if (prefab == null)
            {
                return null;
            }

            if (gridView == null || !gridView.TryGetCellAnchor(feedbackEvent.Coord, out Transform anchor))
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, ResolveFeedbackRoot());
            instance.name = $"{feedbackEvent.Kind}_{feedbackEvent.TargetId}";
            instance.transform.SetPositionAndRotation(
                anchor.position + new Vector3(0f, feedbackYOffset, 0f),
                anchor.rotation);
            transientObjects.Add(instance);

            if (Application.isPlaying && autoDestroyDelay > 0f)
            {
                StartCoroutine(DestroyAfterDelay(instance, autoDestroyDelay));
            }

            return instance;
        }

        private Transform ResolveFeedbackRoot()
        {
            if (feedbackRoot != null)
            {
                return feedbackRoot;
            }

            Transform existingRoot = transform.Find(DefaultFeedbackRootName);
            if (existingRoot != null)
            {
                feedbackRoot = existingRoot;
                return existingRoot;
            }

            GameObject rootObject = new GameObject(DefaultFeedbackRootName);
            Transform rootTransform = rootObject.transform;
            rootTransform.SetParent(transform, false);
            feedbackRoot = rootTransform;
            return rootTransform;
        }

        private void PulseTarget(GameObject targetObject)
        {
            Transform targetTransform = targetObject.transform;
            Vector3 baseScale = targetTransform.localScale;
            Vector3 pulseScale = baseScale * Mathf.Max(1f, nearRescuePulseScale);

            if (Application.isPlaying && isActiveAndEnabled)
            {
                StartCoroutine(AnimatePulse(targetTransform, baseScale, pulseScale, nearRescuePulseDuration));
            }

            PulseAlpha(targetObject, nearRescuePulseDuration, nearRescueAlphaMultiplier);
        }

        private void PulseAlpha(GameObject targetObject, float duration, float alphaMultiplier)
        {
            if (targetObject.TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                Color baseColor = spriteRenderer.color;
                Color pulseColor = ScaleAlpha(baseColor, alphaMultiplier);

                if (!Application.isPlaying || !isActiveAndEnabled)
                {
                    spriteRenderer.color = baseColor;
                    return;
                }

                StartCoroutine(AnimateSpriteAlpha(spriteRenderer, baseColor, pulseColor, duration));
                return;
            }

            if (targetObject.TryGetComponent(out Graphic graphic))
            {
                Color baseColor = graphic.color;
                Color pulseColor = ScaleAlpha(baseColor, alphaMultiplier);

                if (!Application.isPlaying || !isActiveAndEnabled)
                {
                    graphic.color = baseColor;
                    return;
                }

                StartCoroutine(AnimateGraphicAlpha(graphic, baseColor, pulseColor, duration));
            }
        }

        private static void NotifyHook(
            MonoBehaviour? hook,
            TargetFeedbackEvent feedbackEvent,
            GameState? previousState,
            GameState currentState)
        {
            if (hook is ITargetFeedbackHook targetFeedbackHook)
            {
                targetFeedbackHook.HandleTargetFeedback(feedbackEvent, previousState, currentState);
            }
        }

        private IEnumerator AnimatePulse(Transform target, Vector3 from, Vector3 peak, float duration)
        {
            if (target == null)
            {
                yield break;
            }

            float clampedDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            while (elapsed < clampedDuration)
            {
                if (target == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                float eased = Mathf.Sin(normalized * Mathf.PI);
                target.localScale = Vector3.LerpUnclamped(from, peak, eased);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = from;
            }
        }

        private IEnumerator AnimateSpriteAlpha(SpriteRenderer spriteRenderer, Color from, Color peak, float duration)
        {
            float clampedDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            while (elapsed < clampedDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                float eased = Mathf.Sin(normalized * Mathf.PI);
                spriteRenderer.color = Color.LerpUnclamped(from, peak, eased);
                yield return null;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = from;
            }
        }

        private IEnumerator AnimateGraphicAlpha(Graphic graphic, Color from, Color peak, float duration)
        {
            float clampedDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            while (elapsed < clampedDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                float eased = Mathf.Sin(normalized * Mathf.PI);
                graphic.color = Color.LerpUnclamped(from, peak, eased);
                yield return null;
            }

            if (graphic != null)
            {
                graphic.color = from;
            }
        }

        private IEnumerator DestroyAfterDelay(GameObject targetObject, float delay)
        {
            yield return delay <= 0f ? null : new WaitForSeconds(delay);

            if (targetObject == null)
            {
                yield break;
            }

            transientObjects.Remove(targetObject);
            Destroy(targetObject);
        }

        private static Color ScaleAlpha(Color color, float multiplier)
        {
            color.a = Mathf.Clamp01(color.a * Mathf.Max(0f, multiplier));
            return color;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            EnsureFeedbackRoot();
        }

        private void OnValidate()
        {
            EnsureFeedbackRoot();
        }

        private void EnsureFeedbackRoot()
        {
            if (feedbackRoot != null)
            {
                return;
            }

            Transform existingRoot = transform.Find(DefaultFeedbackRootName);
            if (existingRoot != null)
            {
                feedbackRoot = existingRoot;
                return;
            }

            GameObject rootObject = new GameObject(DefaultFeedbackRootName);
            Transform rootTransform = rootObject.transform;
            rootTransform.SetParent(transform, false);
            feedbackRoot = rootTransform;
        }
#endif
    }
}
