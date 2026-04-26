using System.Collections.Generic;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using UnityEngine;

namespace Rescue.Unity.BoardPresentation
{
    public sealed class BoardContentViewPresenter : MonoBehaviour
    {
        private const string DefaultContentRootName = "BoardContent";
        private const float HiddenDebrisYOffsetRatio = 0.5f;
        private static readonly Vector3 HiddenDebrisScale = new Vector3(0.75f, 0.75f, 0.75f);

        [SerializeField] private BoardGridViewPresenter? gridView;
        [SerializeField] private PieceVisualRegistry? pieceRegistry;
        [SerializeField] private BlockerVisualRegistry? blockerRegistry;
        [SerializeField] private TargetVisualRegistry? targetRegistry;
        [SerializeField] private Transform? contentRoot;
        [SerializeField] private GameObject? fallbackContentPrefab;
        [SerializeField] private float contentYOffset = 0.05f;

        private readonly List<GameObject> spawnedContent = new List<GameObject>();
        private readonly Dictionary<string, GameObject> spawnedTargetsById = new Dictionary<string, GameObject>();

        public void RebuildContent(GameState state)
        {
            if (state is null)
            {
                Debug.LogWarning($"{nameof(BoardContentViewPresenter)} requires a valid GameState to rebuild.", this);
                return;
            }

            if (gridView is null)
            {
                Debug.LogWarning($"{nameof(BoardContentViewPresenter)} is missing {nameof(gridView)}.", this);
                ClearContent();
                return;
            }

            ClearContent();

            for (int row = 0; row < state.Board.Height; row++)
            {
                for (int col = 0; col < state.Board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (!gridView.TryGetCellAnchor(coord, out Transform anchor))
                    {
                        Debug.LogWarning(
                            $"{nameof(BoardContentViewPresenter)} could not find a cell anchor for tile ({row}, {col}).",
                            this);
                        continue;
                    }

                    Tile tile = state.Board.Tiles[row][col];
                    RenderTileContent(coord, tile, anchor);
                }
            }
        }

        public void ClearContent()
        {
            for (int i = spawnedContent.Count - 1; i >= 0; i--)
            {
                GameObject? contentObject = spawnedContent[i];
                if (contentObject is null)
                {
                    spawnedContent.RemoveAt(i);
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(contentObject);
                }
                else
                {
                    DestroyImmediate(contentObject);
                }

                spawnedContent.RemoveAt(i);
            }

            spawnedTargetsById.Clear();
        }

        public bool TryGetTargetInstance(string targetId, out GameObject? targetObject)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                targetObject = null;
                return false;
            }

            if (spawnedTargetsById.TryGetValue(targetId, out GameObject targetInstance) && targetInstance is not null)
            {
                targetObject = targetInstance;
                return true;
            }

            targetObject = null;
            return false;
        }

        private void RenderTileContent(TileCoord coord, Tile tile, Transform anchor)
        {
            switch (tile)
            {
                case EmptyTile:
                case FloodedTile:
                    return;
                case DebrisTile debrisTile:
                    SpawnAtAnchor(
                        coord,
                        $"Debris_{debrisTile.Type}",
                        ResolveDebrisPrefab(debrisTile.Type),
                        anchor,
                        contentYOffset,
                        Vector3.one);
                    return;
                case BlockerTile blockerTile:
                    RenderBlocker(coord, blockerTile, anchor);
                    return;
                case TargetTile targetTile when !targetTile.Extracted:
                    GameObject? targetObject = SpawnAtAnchor(
                        coord,
                        $"Target_{SanitizeName(targetTile.TargetId)}",
                        ResolveTargetPrefab(targetTile.TargetId),
                        anchor,
                        contentYOffset,
                        Vector3.one);

                    if (targetObject is not null && !string.IsNullOrWhiteSpace(targetTile.TargetId))
                    {
                        spawnedTargetsById[targetTile.TargetId] = targetObject;
                    }

                    return;
                default:
                    return;
            }
        }

        private void RenderBlocker(TileCoord coord, BlockerTile blockerTile, Transform anchor)
        {
            SpawnAtAnchor(
                coord,
                $"Blocker_{blockerTile.Type}",
                ResolveBlockerPrefab(blockerTile.Type),
                anchor,
                contentYOffset,
                Vector3.one);

            if (blockerTile.Type == BlockerType.Ice && blockerTile.Hidden is not null)
            {
                SpawnAtAnchor(
                    coord,
                    $"HiddenDebris_{blockerTile.Hidden.Type}",
                    ResolveDebrisPrefab(blockerTile.Hidden.Type),
                    anchor,
                    contentYOffset * HiddenDebrisYOffsetRatio,
                    HiddenDebrisScale);
            }
        }

        private GameObject? SpawnAtAnchor(
            TileCoord coord,
            string contentLabel,
            GameObject? prefab,
            Transform anchor,
            float yOffset,
            Vector3 scaleMultiplier)
        {
            if (prefab is null)
            {
                return null;
            }

            Transform parent = ResolveContentParent(anchor);
            GameObject contentObject = Instantiate(prefab, parent);
            contentObject.name = $"Content_{coord.Row:00}_{coord.Col:00}_{contentLabel}";

            Transform contentTransform = contentObject.transform;
            if (parent == anchor)
            {
                contentTransform.localPosition = new Vector3(0f, yOffset, 0f);
                contentTransform.localRotation = Quaternion.identity;
            }
            else
            {
                contentTransform.SetPositionAndRotation(
                    anchor.position + new Vector3(0f, yOffset, 0f),
                    anchor.rotation);
            }

            contentTransform.localScale = Vector3.Scale(prefab.transform.localScale, scaleMultiplier);
            spawnedContent.Add(contentObject);
            return contentObject;
        }

        private Transform ResolveContentParent(Transform anchor)
        {
            if (contentRoot is not null)
            {
                return contentRoot;
            }

            return anchor;
        }

        private GameObject? ResolveDebrisPrefab(DebrisType debrisType)
        {
            GameObject? registryPrefab = pieceRegistry?.GetPrefab(debrisType);
            if (registryPrefab is not null)
            {
                return registryPrefab;
            }

            return ResolveFallbackPrefab($"debris type {debrisType}");
        }

        private GameObject? ResolveBlockerPrefab(BlockerType blockerType)
        {
            GameObject? registryPrefab = blockerRegistry?.GetPrefab(blockerType);
            if (registryPrefab is not null)
            {
                return registryPrefab;
            }

            return ResolveFallbackPrefab($"blocker type {blockerType}");
        }

        private GameObject? ResolveTargetPrefab(string targetId)
        {
            GameObject? registryPrefab = targetRegistry?.GetTargetPrefab(targetId);
            if (registryPrefab is not null)
            {
                return registryPrefab;
            }

            return ResolveFallbackPrefab($"target '{targetId}'");
        }

        private GameObject? ResolveFallbackPrefab(string contentDescription)
        {
            if (fallbackContentPrefab is not null)
            {
                return fallbackContentPrefab;
            }

            Debug.LogWarning(
                $"{nameof(BoardContentViewPresenter)} could not resolve a prefab for {contentDescription} and is missing {nameof(fallbackContentPrefab)}.",
                this);
            return null;
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unknown";
            }

            return value.Replace(' ', '_');
        }

#if UNITY_EDITOR
        private void Reset()
        {
            EnsureContentRoot();
        }

        private void OnValidate()
        {
            EnsureContentRoot();
        }

        private void EnsureContentRoot()
        {
            if (contentRoot is not null)
            {
                return;
            }

            Transform existingRoot = transform.Find(DefaultContentRootName);
            if (existingRoot is not null)
            {
                contentRoot = existingRoot;
                return;
            }

            GameObject rootObject = new GameObject(DefaultContentRootName);
            Transform rootTransform = rootObject.transform;
            rootTransform.SetParent(transform, false);
            contentRoot = rootTransform;
        }
#endif
    }
}
