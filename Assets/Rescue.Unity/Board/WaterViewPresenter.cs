using System.Collections.Generic;
using Rescue.Core.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rescue.Unity.BoardPresentation
{
    public sealed class WaterViewPresenter : MonoBehaviour
    {
        private const string DefaultWaterRootName = "WaterOverlay";

        [SerializeField] private BoardGridViewPresenter? gridView;
        [SerializeField] private GameObject? floodedRowOverlayPrefab;
        [SerializeField] private GameObject? forecastRowOverlayPrefab;
        [SerializeField] private GameObject? waterlinePrefab;
        [SerializeField] private Transform? waterRoot;
        [SerializeField] private TextMeshProUGUI? counterLabel;
        [SerializeField] private GameObject? fallbackOverlayPrefab;
        [SerializeField] private float overlayYOffset = 0.1f;
        [SerializeField] private float forecastPulseDuration = 0.25f;
        [SerializeField] private float forecastPulseScale = 1.08f;
        [SerializeField] private float waterRiseDuration = 0.3f;
        [SerializeField] private float waterlinePulseDuration = 0.2f;

        private readonly List<GameObject> spawnedObjects = new List<GameObject>();
        private readonly Dictionary<int, GameObject> floodedRowOverlays = new Dictionary<int, GameObject>();
        private GameObject? forecastOverlayInstance;
        private GameObject? waterlineInstance;
        private WaterState? previousWaterState;

        public void RebuildWater(GameState state)
        {
            SyncImmediate(state);
        }

        public void SyncImmediate(GameState state)
        {
            if (state is null)
            {
                Debug.LogWarning($"{nameof(WaterViewPresenter)} requires a valid GameState to rebuild.", this);
                return;
            }

            if (gridView is null)
            {
                Debug.LogWarning($"{nameof(WaterViewPresenter)} is missing {nameof(gridView)}.", this);
                ClearWater();
                return;
            }

            ClearWater();
            ApplyStateVisuals(state, previousWaterState, animateRise: false, preferredFloodedRow: null, customRiseDuration: null);
        }

        public void ForceSyncToState(GameState state)
        {
            SyncImmediate(state);
        }

        public void AnimateWaterRise(
            GameState previousState,
            GameState state,
            int? preferredFloodedRow = null,
            float? durationSeconds = null)
        {
            if (previousState is null)
            {
                Debug.LogWarning($"{nameof(WaterViewPresenter)} requires a valid previous GameState to animate water rise.", this);
                return;
            }

            if (state is null)
            {
                Debug.LogWarning($"{nameof(WaterViewPresenter)} requires a valid GameState to animate water rise.", this);
                return;
            }

            if (gridView is null)
            {
                Debug.LogWarning($"{nameof(WaterViewPresenter)} is missing {nameof(gridView)}.", this);
                ClearWater();
                return;
            }

            if (state.Board.Height != previousState.Board.Height || state.Board.Width != previousState.Board.Width)
            {
                ForceSyncToState(state);
                return;
            }

            WaterState baselineWaterState = previousWaterState ?? previousState.Water;
            if (state.Water.FloodedRows <= baselineWaterState.FloodedRows)
            {
                ForceSyncToState(state);
                return;
            }

            ApplyStateVisuals(state, baselineWaterState, animateRise: true, preferredFloodedRow, durationSeconds);
        }

        public void ClearWater()
        {
            StopAllCoroutines();

            for (int i = spawnedObjects.Count - 1; i >= 0; i--)
            {
                GameObject? spawnedObject = spawnedObjects[i];
                if (spawnedObject is null)
                {
                    spawnedObjects.RemoveAt(i);
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(spawnedObject);
                }
                else
                {
                    DestroyImmediate(spawnedObject);
                }

                spawnedObjects.RemoveAt(i);
            }

            UpdateCounterLabel(null, 0f);
            floodedRowOverlays.Clear();
            forecastOverlayInstance = null;
            waterlineInstance = null;
            previousWaterState = null;
        }

        private void ApplyStateVisuals(
            GameState state,
            WaterState? baselineWaterState,
            bool animateRise,
            int? preferredFloodedRow,
            float? customRiseDuration)
        {
            WaterFeedbackResolution feedback = WaterFeedbackResolver.Resolve(
                state.Board.Height,
                baselineWaterState,
                state.Water);

            WaterRowResolution resolution = WaterRowResolver.Resolve(state.Board.Height, state.Water);
            SyncFloodedRows(state, resolution, animateRise, feedback, preferredFloodedRow, customRiseDuration);
            SyncForecastOverlay(state, resolution);
            SyncWaterline(state, resolution);
            UpdateCounterLabel(state.Water, resolution.NormalizedCounterProgress);
            ApplyFeedback(feedback, floodedRowOverlays, forecastOverlayInstance, waterlineInstance, customRiseDuration);
            previousWaterState = state.Water;
        }

        private void SyncFloodedRows(
            GameState state,
            WaterRowResolution resolution,
            bool animateRise,
            WaterFeedbackResolution feedback,
            int? preferredFloodedRow,
            float? customRiseDuration)
        {
            List<int> staleRows = new List<int>();
            foreach (KeyValuePair<int, GameObject> pair in floodedRowOverlays)
            {
                bool keep = false;
                for (int i = 0; i < resolution.FloodedRowIndices.Length; i++)
                {
                    if (resolution.FloodedRowIndices[i] == pair.Key)
                    {
                        keep = true;
                        break;
                    }
                }

                if (!keep)
                {
                    staleRows.Add(pair.Key);
                }
            }

            for (int i = 0; i < staleRows.Count; i++)
            {
                if (floodedRowOverlays.TryGetValue(staleRows[i], out GameObject staleOverlay))
                {
                    DestroySpawnedObject(staleOverlay);
                    floodedRowOverlays.Remove(staleRows[i]);
                }
            }

            for (int i = 0; i < resolution.FloodedRowIndices.Length; i++)
            {
                int rowIndex = resolution.FloodedRowIndices[i];
                if (!floodedRowOverlays.TryGetValue(rowIndex, out GameObject? overlay) || overlay is null)
                {
                    overlay = SpawnFloodedRowOverlay(rowIndex, state.Board.Width);
                    if (overlay is not null)
                    {
                        floodedRowOverlays[rowIndex] = overlay;
                    }
                }
            }

            if (!animateRise)
            {
                return;
            }

            int targetRow = ResolveAnimatedFloodedRow(feedback, preferredFloodedRow);
            if (targetRow < 0 || !floodedRowOverlays.TryGetValue(targetRow, out GameObject? targetOverlay) || targetOverlay is null)
            {
                return;
            }

            AnimateRiseOverlay(targetOverlay.transform, customRiseDuration ?? waterRiseDuration);
        }

        private GameObject? SpawnFloodedRowOverlay(int rowIndex, int width)
        {
            return SpawnRowOverlay(
                rowIndex,
                ResolveOverlayPrefab(floodedRowOverlayPrefab),
                $"FloodedRow_{rowIndex:00}",
                width);
        }

        private void SyncForecastOverlay(GameState state, WaterRowResolution resolution)
        {
            if (forecastOverlayInstance is not null)
            {
                DestroySpawnedObject(forecastOverlayInstance);
                forecastOverlayInstance = null;
            }

            if (resolution.HasForecastRow)
            {
                forecastOverlayInstance = SpawnRowOverlay(
                    resolution.ForecastRowIndex,
                    ResolveOverlayPrefab(forecastRowOverlayPrefab),
                    $"ForecastRow_{resolution.ForecastRowIndex:00}",
                    state.Board.Width);
            }
        }

        private void SyncWaterline(GameState state, WaterRowResolution resolution)
        {
            if (waterlineInstance is not null)
            {
                DestroySpawnedObject(waterlineInstance);
                waterlineInstance = null;
            }

            if (state.Water.FloodedRows > 0)
            {
                waterlineInstance = SpawnWaterline(resolution, state.Board.Width);
            }
        }

        private GameObject? SpawnRowOverlay(int rowIndex, GameObject? prefab, string objectName, int width)
        {
            if (prefab is null || width <= 0)
            {
                return null;
            }

            if (!TryGetRowEndpoints(rowIndex, width, out Transform leftAnchor, out Transform rightAnchor))
            {
                Debug.LogWarning(
                    $"{nameof(WaterViewPresenter)} could not resolve anchors for row {rowIndex}.",
                    this);
                return null;
            }

            GameObject overlay = Instantiate(prefab, ResolveWaterRoot());
            overlay.name = objectName;

            Transform overlayTransform = overlay.transform;
            Vector3 center = (leftAnchor.position + rightAnchor.position) * 0.5f;
            overlayTransform.SetPositionAndRotation(center + new Vector3(0f, overlayYOffset, 0f), leftAnchor.rotation);

            float widthScale = ResolveRowWidth(leftAnchor, rightAnchor, rowIndex, width);
            Vector3 localScale = prefab.transform.localScale;
            overlayTransform.localScale = new Vector3(localScale.x * widthScale, localScale.y, localScale.z);
            spawnedObjects.Add(overlay);
            return overlay;
        }

        private void DestroySpawnedObject(GameObject spawnedObject)
        {
            spawnedObjects.Remove(spawnedObject);

            if (Application.isPlaying)
            {
                Destroy(spawnedObject);
            }
            else
            {
                DestroyImmediate(spawnedObject);
            }
        }

        private GameObject? SpawnWaterline(WaterRowResolution resolution, int width)
        {
            GameObject? prefab = ResolveOverlayPrefab(waterlinePrefab);
            if (prefab is null || resolution.FloodedRowIndices.Length <= 0 || width <= 0)
            {
                return null;
            }

            int topFloodedRow = resolution.FloodedRowIndices[0];
            if (!TryGetRowEndpoints(topFloodedRow, width, out Transform leftAnchor, out Transform rightAnchor))
            {
                return null;
            }

            GameObject waterline = Instantiate(prefab, ResolveWaterRoot());
            waterline.name = $"Waterline_{topFloodedRow:00}";

            Transform waterlineTransform = waterline.transform;
            Vector3 center = (leftAnchor.position + rightAnchor.position) * 0.5f;
            float rowEdgeOffset = ResolveRowDepth(topFloodedRow) * 0.5f;
            waterlineTransform.SetPositionAndRotation(
                center + new Vector3(0f, overlayYOffset, rowEdgeOffset),
                leftAnchor.rotation);

            float widthScale = ResolveRowWidth(leftAnchor, rightAnchor, topFloodedRow, width);
            Vector3 localScale = prefab.transform.localScale;
            waterlineTransform.localScale = new Vector3(localScale.x * widthScale, localScale.y, localScale.z);
            spawnedObjects.Add(waterline);
            return waterline;
        }

        private bool TryGetRowEndpoints(int rowIndex, int width, out Transform leftAnchor, out Transform rightAnchor)
        {
            leftAnchor = transform;
            rightAnchor = transform;

            if (gridView is null)
            {
                return false;
            }

            return gridView.TryGetCellAnchor(new TileCoord(rowIndex, 0), out leftAnchor)
                && gridView.TryGetCellAnchor(new TileCoord(rowIndex, width - 1), out rightAnchor);
        }

        private float ResolveRowWidth(Transform leftAnchor, Transform rightAnchor, int rowIndex, int width)
        {
            float span = Vector3.Distance(leftAnchor.position, rightAnchor.position);
            if (width <= 1)
            {
                return Mathf.Max(1f, ResolveCellWidth(rowIndex));
            }

            float cellWidth = span / (width - 1);
            return Mathf.Max(1f, span + cellWidth);
        }

        private float ResolveCellWidth(int rowIndex)
        {
            if (gridView is null)
            {
                return 1f;
            }

            if (gridView.TryGetCellAnchor(new TileCoord(rowIndex, 0), out Transform firstAnchor)
                && gridView.TryGetCellAnchor(new TileCoord(rowIndex, 1), out Transform secondAnchor))
            {
                return Vector3.Distance(firstAnchor.position, secondAnchor.position);
            }

            return 1f;
        }

        private float ResolveRowDepth(int rowIndex)
        {
            if (gridView is null)
            {
                return 1f;
            }

            if (gridView.TryGetCellAnchor(new TileCoord(rowIndex, 0), out Transform currentAnchor))
            {
                if (gridView.TryGetCellAnchor(new TileCoord(rowIndex - 1, 0), out Transform aboveAnchor))
                {
                    return aboveAnchor.position.z - currentAnchor.position.z;
                }

                if (gridView.TryGetCellAnchor(new TileCoord(rowIndex + 1, 0), out Transform belowAnchor))
                {
                    return currentAnchor.position.z - belowAnchor.position.z;
                }
            }

            return 1f;
        }

        private GameObject? ResolveOverlayPrefab(GameObject? preferredPrefab)
        {
            if (preferredPrefab is not null)
            {
                return preferredPrefab;
            }

            return fallbackOverlayPrefab;
        }

        private Transform ResolveWaterRoot()
        {
            if (waterRoot is not null)
            {
                return waterRoot;
            }

            Transform existingRoot = transform.Find(DefaultWaterRootName);
            if (existingRoot is not null)
            {
                waterRoot = existingRoot;
                return existingRoot;
            }

            GameObject rootObject = new GameObject(DefaultWaterRootName);
            Transform rootTransform = rootObject.transform;
            rootTransform.SetParent(transform, false);
            waterRoot = rootTransform;
            return rootTransform;
        }

        private void UpdateCounterLabel(WaterState? water, float normalizedProgress)
        {
            if (counterLabel is null)
            {
                return;
            }

            string labelText = water is null
                ? string.Empty
                : water.PauseUntilFirstAction
                    ? "Water: paused until first action"
                    : $"Water: {water.ActionsUntilRise}/{water.RiseInterval} ({Mathf.RoundToInt(normalizedProgress * 100f)}%)";

            counterLabel.text = labelText;
        }

        private void ApplyFeedback(
            WaterFeedbackResolution feedback,
            IReadOnlyDictionary<int, GameObject> floodedRowOverlays,
            GameObject? forecastOverlay,
            GameObject? waterline,
            float? customRiseDuration)
        {
            if (feedback.HasNearRiseWarning && forecastOverlay is not null)
            {
                PulseTransform(forecastOverlay.transform, forecastPulseDuration, forecastPulseScale);
                PulseAlpha(forecastOverlay, forecastPulseDuration, targetAlphaMultiplier: 1.2f);
            }

            if (feedback.ShouldPulseWaterline && waterline is not null)
            {
                PulseTransform(waterline.transform, waterlinePulseDuration, forecastPulseScale);
                PulseAlpha(waterline, waterlinePulseDuration, targetAlphaMultiplier: 1.25f);
            }

            if (feedback.HasWaterRise)
            {
                for (int i = 0; i < feedback.NewlyFloodedRowIndices.Length; i++)
                {
                    if (floodedRowOverlays.TryGetValue(feedback.NewlyFloodedRowIndices[i], out GameObject overlay))
                    {
                        PulseAlpha(overlay, customRiseDuration ?? waterRiseDuration, targetAlphaMultiplier: 1.15f);
                    }
                }
            }

            if (feedback.ShouldEmphasizeCounter && counterLabel is not null)
            {
                PulseTransform(counterLabel.transform, forecastPulseDuration, forecastPulseScale);
            }
        }

        private int ResolveAnimatedFloodedRow(WaterFeedbackResolution feedback, int? preferredFloodedRow)
        {
            if (preferredFloodedRow.HasValue && floodedRowOverlays.ContainsKey(preferredFloodedRow.Value))
            {
                return preferredFloodedRow.Value;
            }

            if (feedback.NewlyFloodedRowIndices.Length > 0)
            {
                return feedback.NewlyFloodedRowIndices[0];
            }

            return -1;
        }

        private void AnimateRiseOverlay(Transform target, float durationSeconds)
        {
            if (target is null)
            {
                return;
            }

            Vector3 finalScale = target.localScale;
            Vector3 initialScale = new Vector3(finalScale.x, finalScale.y, 0.01f);

            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                target.localScale = finalScale;
                return;
            }

            StartCoroutine(AnimateTransformScale(target, initialScale, finalScale, durationSeconds));
        }

        private void PulseTransform(Transform target, float duration, float scaleMultiplier)
        {
            if (target is null)
            {
                return;
            }

            Vector3 baseScale = target.localScale;
            Vector3 pulseScale = baseScale * Mathf.Max(1f, scaleMultiplier);

            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                target.localScale = baseScale;
                return;
            }

            StartCoroutine(AnimatePulse(target, baseScale, pulseScale, duration));
        }

        private void PulseAlpha(GameObject target, float duration, float targetAlphaMultiplier)
        {
            if (target is null)
            {
                return;
            }

            if (target.TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                Color baseColor = spriteRenderer.color;
                Color peakColor = ScaleAlpha(baseColor, targetAlphaMultiplier);

                if (!Application.isPlaying || !isActiveAndEnabled)
                {
                    spriteRenderer.color = baseColor;
                    return;
                }

                StartCoroutine(AnimateSpriteAlpha(spriteRenderer, baseColor, peakColor, duration));
                return;
            }

            if (target.TryGetComponent(out Graphic graphic))
            {
                Color baseColor = graphic.color;
                Color peakColor = ScaleAlpha(baseColor, targetAlphaMultiplier);

                if (!Application.isPlaying || !isActiveAndEnabled)
                {
                    graphic.color = baseColor;
                    return;
                }

                StartCoroutine(AnimateGraphicAlpha(graphic, baseColor, peakColor, duration));
            }
        }

        private System.Collections.IEnumerator AnimatePulse(Transform target, Vector3 from, Vector3 peak, float duration)
        {
            if (target is null)
            {
                yield break;
            }

            float clampedDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            while (elapsed < clampedDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                float eased = Mathf.Sin(normalized * Mathf.PI);
                target.localScale = Vector3.LerpUnclamped(from, peak, eased);
                yield return null;
            }

            if (target is not null)
            {
                target.localScale = from;
            }
        }

        private System.Collections.IEnumerator AnimateTransformScale(Transform target, Vector3 from, Vector3 to, float duration)
        {
            if (target is null)
            {
                yield break;
            }

            float clampedDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            target.localScale = from;

            while (elapsed < clampedDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                float eased = 1f - Mathf.Pow(1f - normalized, 3f);
                target.localScale = Vector3.LerpUnclamped(from, to, eased);
                yield return null;
            }

            if (target is not null)
            {
                target.localScale = to;
            }
        }

        private System.Collections.IEnumerator AnimateSpriteAlpha(SpriteRenderer spriteRenderer, Color from, Color peak, float duration)
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

            if (spriteRenderer is not null)
            {
                spriteRenderer.color = from;
            }
        }

        private System.Collections.IEnumerator AnimateGraphicAlpha(Graphic graphic, Color from, Color peak, float duration)
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

            if (graphic is not null)
            {
                graphic.color = from;
            }
        }

        private static Color ScaleAlpha(Color color, float multiplier)
        {
            color.a = Mathf.Clamp01(color.a * Mathf.Max(0f, multiplier));
            return color;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            EnsureWaterRoot();
        }

        private void OnValidate()
        {
            EnsureWaterRoot();
        }

        private void EnsureWaterRoot()
        {
            if (waterRoot is not null)
            {
                return;
            }

            Transform existingRoot = transform.Find(DefaultWaterRootName);
            if (existingRoot is not null)
            {
                waterRoot = existingRoot;
                return;
            }

            GameObject rootObject = new GameObject(DefaultWaterRootName);
            Transform rootTransform = rootObject.transform;
            rootTransform.SetParent(transform, false);
            waterRoot = rootTransform;
        }
#endif
    }
}
