using System.Collections.Generic;
using System.Globalization;
using Rescue.Core.Pipeline;
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

        public void SyncImmediate(GameState state)
        {
            if (state is null)
            {
                Debug.LogWarning($"{nameof(BoardContentViewPresenter)} requires a valid GameState to sync.", this);
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

        public void RebuildContent(GameState state)
        {
            SyncImmediate(state);
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

                DestroyContentObject(contentObject);
                spawnedContent.RemoveAt(i);
            }

            spawnedTargetsById.Clear();
        }

        public void RemoveDebrisGroup(GroupRemoved removal)
        {
            for (int i = 0; i < removal.Coords.Length; i++)
            {
                if (!TryGetDebrisInstance(removal.Coords[i], out GameObject? debrisObject) || debrisObject is null)
                {
                    continue;
                }

                spawnedContent.Remove(debrisObject);
                DestroyContentObject(debrisObject);
            }
        }

        public void AnimateGravityMove(GravitySettled gravity)
        {
            for (int i = 0; i < gravity.Moves.Length; i++)
            {
                (TileCoord from, TileCoord to) = gravity.Moves[i];
                if (!TryGetDebrisInstance(from, out GameObject? debrisObject) || debrisObject is null)
                {
                    continue;
                }

                if (!TryGetAnchor(to, out Transform anchor))
                {
                    continue;
                }

                MoveContentObjectToAnchor(debrisObject, anchor, to, "Debris", contentYOffset);
            }
        }

        public void AnimateSpawn(Spawned spawned)
        {
            for (int i = 0; i < spawned.Pieces.Length; i++)
            {
                (TileCoord coord, DebrisType type) = spawned.Pieces[i];
                if (!TryGetAnchor(coord, out Transform anchor))
                {
                    continue;
                }

                if (TryGetDebrisInstance(coord, out _, includeHiddenDebris: false))
                {
                    continue;
                }

                SpawnAtAnchor(
                    coord,
                    $"Debris_{type}",
                    ResolveDebrisPrefab(type),
                    anchor,
                    contentYOffset,
                    Vector3.one);
            }
        }

        public void AnimateTargetExtract(TargetExtracted extraction)
        {
            _ = extraction;
            // Target extraction animation will be added later once playback owns target removal timing.
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

        private void DestroyContentObject(GameObject contentObject)
        {
            if (Application.isPlaying)
            {
                Destroy(contentObject);
            }
            else
            {
                DestroyImmediate(contentObject);
            }
        }

        private bool TryGetDebrisInstance(TileCoord coord, out GameObject? debrisObject, bool includeHiddenDebris = false)
        {
            string coordPrefix = GetCoordPrefix(coord);
            for (int i = spawnedContent.Count - 1; i >= 0; i--)
            {
                GameObject? contentObject = spawnedContent[i];
                if (contentObject is null)
                {
                    spawnedContent.RemoveAt(i);
                    continue;
                }

                if (!contentObject.name.StartsWith(coordPrefix, System.StringComparison.Ordinal))
                {
                    continue;
                }

                bool isDebris = contentObject.name.Contains("_Debris_");
                bool isHiddenDebris = includeHiddenDebris && contentObject.name.Contains("_HiddenDebris_");
                if (!isDebris && !isHiddenDebris)
                {
                    continue;
                }

                debrisObject = contentObject;
                return true;
            }

            debrisObject = null;
            return false;
        }

        private bool TryGetAnchor(TileCoord coord, out Transform anchor)
        {
            anchor = transform;
            if (gridView is null)
            {
                return false;
            }

            return gridView.TryGetCellAnchor(coord, out anchor);
        }

        private void MoveContentObjectToAnchor(GameObject contentObject, Transform anchor, TileCoord coord, string contentLabel, float yOffset)
        {
            Transform parent = ResolveContentParent(anchor);
            Transform contentTransform = contentObject.transform;
            if (contentTransform.parent != parent)
            {
                contentTransform.SetParent(parent, worldPositionStays: false);
            }

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

            contentObject.name = $"Content_{coord.Row.ToString("00", CultureInfo.InvariantCulture)}_{coord.Col.ToString("00", CultureInfo.InvariantCulture)}_{contentLabel}_{ExtractTypeSuffix(contentObject.name)}";
        }

        private static string GetCoordPrefix(TileCoord coord)
        {
            return $"Content_{coord.Row.ToString("00", CultureInfo.InvariantCulture)}_{coord.Col.ToString("00", CultureInfo.InvariantCulture)}_";
        }

        private static string ExtractTypeSuffix(string contentName)
        {
            int lastUnderscore = contentName.LastIndexOf('_');
            if (lastUnderscore < 0 || lastUnderscore >= contentName.Length - 1)
            {
                return "Unknown";
            }

            return contentName.Substring(lastUnderscore + 1);
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
