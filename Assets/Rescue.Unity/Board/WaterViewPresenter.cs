using System.Collections.Generic;
using System.Reflection;
using Rescue.Core.State;
using UnityEngine;

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
        [SerializeField] private Component? counterLabel;
        [SerializeField] private GameObject? fallbackOverlayPrefab;
        [SerializeField] private float overlayYOffset = 0.1f;

        private readonly List<GameObject> spawnedObjects = new List<GameObject>();

        public void RebuildWater(GameState state)
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

            WaterRowResolution resolution = WaterRowResolver.Resolve(state.Board.Height, state.Water);
            for (int i = 0; i < resolution.FloodedRowIndices.Length; i++)
            {
                SpawnRowOverlay(
                    rowIndex: resolution.FloodedRowIndices[i],
                    prefab: ResolveOverlayPrefab(floodedRowOverlayPrefab),
                    objectName: $"FloodedRow_{resolution.FloodedRowIndices[i]:00}",
                    width: state.Board.Width);
            }

            if (resolution.HasForecastRow)
            {
                SpawnRowOverlay(
                    rowIndex: resolution.ForecastRowIndex,
                    prefab: ResolveOverlayPrefab(forecastRowOverlayPrefab),
                    objectName: $"ForecastRow_{resolution.ForecastRowIndex:00}",
                    width: state.Board.Width);
            }

            if (state.Water.FloodedRows > 0)
            {
                SpawnWaterline(resolution, state.Board.Width);
            }

            UpdateCounterLabel(state.Water, resolution.NormalizedCounterProgress);
        }

        public void ClearWater()
        {
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
        }

        private void SpawnRowOverlay(int rowIndex, GameObject? prefab, string objectName, int width)
        {
            if (prefab is null || width <= 0)
            {
                return;
            }

            if (!TryGetRowEndpoints(rowIndex, width, out Transform leftAnchor, out Transform rightAnchor))
            {
                Debug.LogWarning(
                    $"{nameof(WaterViewPresenter)} could not resolve anchors for row {rowIndex}.",
                    this);
                return;
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
        }

        private void SpawnWaterline(WaterRowResolution resolution, int width)
        {
            GameObject? prefab = ResolveOverlayPrefab(waterlinePrefab);
            if (prefab is null || resolution.FloodedRowIndices.Length <= 0 || width <= 0)
            {
                return;
            }

            int topFloodedRow = resolution.FloodedRowIndices[0];
            if (!TryGetRowEndpoints(topFloodedRow, width, out Transform leftAnchor, out Transform rightAnchor))
            {
                return;
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

            PropertyInfo? textProperty = counterLabel.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            if (textProperty is null || !textProperty.CanWrite)
            {
                return;
            }

            string labelText = water is null
                ? string.Empty
                : water.PauseUntilFirstAction
                    ? "Water: paused until first action"
                    : $"Water: {water.ActionsUntilRise}/{water.RiseInterval} ({Mathf.RoundToInt(normalizedProgress * 100f)}%)";

            textProperty.SetValue(counterLabel, labelText);
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
