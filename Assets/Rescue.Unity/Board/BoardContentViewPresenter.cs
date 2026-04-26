using System.Collections.Generic;
using System.Globalization;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using UnityEngine;
using UnityEngine.UI;

namespace Rescue.Unity.BoardPresentation
{
    public sealed class BoardContentViewPresenter : MonoBehaviour
    {
        private const string DefaultContentRootName = "BoardContent";
        private const float HiddenDebrisYOffsetRatio = 0.5f;
        private const float DefaultTargetExtractLiftDistance = 0.18f;
        private const float DefaultTargetExtractPulseScale = 1.12f;
        private const float DefaultBlockerDamagePulseScale = 1.06f;
        private const float DefaultBlockerDamageAlphaFloor = 0.70f;
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

        public void AnimateBlockerDamage(BlockerDamaged damaged, float durationSeconds = 0.10f)
        {
            if (!TryGetBlockerInstance(damaged.Coord, out GameObject? blockerObject) || blockerObject is null)
            {
                return;
            }

            if (!Application.isPlaying || !isActiveAndEnabled || durationSeconds <= 0f)
            {
                return;
            }

            StartCoroutine(AnimateBlockerDamageRoutine(blockerObject, durationSeconds));
        }

        public void AnimateBlockerBreak(BlockerBroken broken, float durationSeconds = 0.10f)
        {
            if (!TryGetBlockerInstance(broken.Coord, out GameObject? blockerObject) || blockerObject is null)
            {
                return;
            }

            spawnedContent.Remove(blockerObject);

            if (!Application.isPlaying || !isActiveAndEnabled || durationSeconds <= 0f)
            {
                DestroyContentObject(blockerObject);
                return;
            }

            StartCoroutine(AnimateBlockerBreakRoutine(blockerObject, durationSeconds));
        }

        public void AnimateIceReveal(IceRevealed revealed, float durationSeconds = 0.10f)
        {
            if (!TryGetHiddenDebrisInstance(revealed.Coord, out GameObject? hiddenDebrisObject) || hiddenDebrisObject is null)
            {
                return;
            }

            if (!TryGetAnchor(revealed.Coord, out Transform anchor))
            {
                return;
            }

            MoveContentObjectToAnchor(hiddenDebrisObject, anchor, revealed.Coord, "Debris", contentYOffset);
            hiddenDebrisObject.transform.localScale = Vector3.one;

            if (!Application.isPlaying || !isActiveAndEnabled || durationSeconds <= 0f)
            {
                SetVisualAlpha(hiddenDebrisObject, 1f);
                return;
            }

            StartCoroutine(AnimateIceRevealRoutine(hiddenDebrisObject, durationSeconds));
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

        public void AnimateTargetExtract(TargetExtracted extraction, float durationSeconds = 0.12f)
        {
            if (!TryGetTargetInstance(extraction.TargetId, out GameObject? targetObject) || targetObject is null)
            {
                return;
            }

            spawnedTargetsById.Remove(extraction.TargetId);

            if (!Application.isPlaying || !isActiveAndEnabled || durationSeconds <= 0f)
            {
                ApplyTargetExtractPose(targetObject.transform, 1f);
                SetTargetVisualAlpha(targetObject, 0f);
                DestroyContentObject(targetObject);
                return;
            }

            StartCoroutine(AnimateTargetExtractRoutine(targetObject, durationSeconds));
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

        private System.Collections.IEnumerator AnimateTargetExtractRoutine(GameObject targetObject, float durationSeconds)
        {
            if (targetObject is null)
            {
                yield break;
            }

            Transform targetTransform = targetObject.transform;
            Vector3 baseLocalPosition = targetTransform.localPosition;
            Vector3 baseLocalScale = targetTransform.localScale;
            float clampedDuration = Mathf.Max(0.01f, durationSeconds);
            float elapsed = 0f;

            while (elapsed < clampedDuration)
            {
                if (targetObject is null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                ApplyTargetExtractPose(targetTransform, normalized, baseLocalPosition, baseLocalScale);
                SetTargetVisualAlpha(targetObject, 1f - normalized);
                yield return null;
            }

            if (targetObject is not null)
            {
                DestroyContentObject(targetObject);
            }
        }

        private System.Collections.IEnumerator AnimateBlockerDamageRoutine(GameObject blockerObject, float durationSeconds)
        {
            if (blockerObject is null)
            {
                yield break;
            }

            Transform blockerTransform = blockerObject.transform;
            Vector3 baseLocalScale = blockerTransform.localScale;
            float clampedDuration = Mathf.Max(0.01f, durationSeconds);
            float elapsed = 0f;

            while (elapsed < clampedDuration)
            {
                if (blockerObject is null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                float pulse = Mathf.Sin(normalized * Mathf.PI);
                blockerTransform.localScale = Vector3.LerpUnclamped(baseLocalScale, baseLocalScale * DefaultBlockerDamagePulseScale, pulse);
                SetVisualAlpha(blockerObject, Mathf.Lerp(1f, DefaultBlockerDamageAlphaFloor, pulse));
                yield return null;
            }

            if (blockerObject is not null)
            {
                blockerTransform.localScale = baseLocalScale;
                SetVisualAlpha(blockerObject, 1f);
            }
        }

        private System.Collections.IEnumerator AnimateBlockerBreakRoutine(GameObject blockerObject, float durationSeconds)
        {
            if (blockerObject is null)
            {
                yield break;
            }

            Transform blockerTransform = blockerObject.transform;
            Vector3 baseLocalScale = blockerTransform.localScale;
            float clampedDuration = Mathf.Max(0.01f, durationSeconds);
            float elapsed = 0f;

            while (elapsed < clampedDuration)
            {
                if (blockerObject is null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                blockerTransform.localScale = Vector3.Lerp(baseLocalScale, Vector3.zero, normalized);
                SetVisualAlpha(blockerObject, 1f - normalized);
                yield return null;
            }

            if (blockerObject is not null)
            {
                DestroyContentObject(blockerObject);
            }
        }

        private System.Collections.IEnumerator AnimateIceRevealRoutine(GameObject hiddenDebrisObject, float durationSeconds)
        {
            if (hiddenDebrisObject is null)
            {
                yield break;
            }

            Transform debrisTransform = hiddenDebrisObject.transform;
            Vector3 baseLocalScale = debrisTransform.localScale;
            float clampedDuration = Mathf.Max(0.01f, durationSeconds);
            float elapsed = 0f;

            SetVisualAlpha(hiddenDebrisObject, 0f);

            while (elapsed < clampedDuration)
            {
                if (hiddenDebrisObject is null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                debrisTransform.localScale = Vector3.Lerp(HiddenDebrisScale, baseLocalScale, normalized);
                SetVisualAlpha(hiddenDebrisObject, normalized);
                yield return null;
            }

            if (hiddenDebrisObject is not null)
            {
                debrisTransform.localScale = baseLocalScale;
                SetVisualAlpha(hiddenDebrisObject, 1f);
            }
        }

        private static void ApplyTargetExtractPose(Transform targetTransform, float normalized)
        {
            ApplyTargetExtractPose(targetTransform, normalized, targetTransform.localPosition, targetTransform.localScale);
        }

        private static void ApplyTargetExtractPose(
            Transform targetTransform,
            float normalized,
            Vector3 baseLocalPosition,
            Vector3 baseLocalScale)
        {
            float clamped = Mathf.Clamp01(normalized);
            float eased = 1f - Mathf.Pow(1f - clamped, 3f);
            float pulse = Mathf.Sin(clamped * Mathf.PI);

            targetTransform.localPosition = baseLocalPosition + new Vector3(0f, DefaultTargetExtractLiftDistance * eased, 0f);
            targetTransform.localScale = Vector3.LerpUnclamped(
                baseLocalScale,
                baseLocalScale * DefaultTargetExtractPulseScale,
                pulse);
        }

        private static void SetTargetVisualAlpha(GameObject targetObject, float alpha)
        {
            SetVisualAlpha(targetObject, alpha);
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

        private bool TryGetBlockerInstance(TileCoord coord, out GameObject? blockerObject)
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

                if (!contentObject.name.StartsWith(coordPrefix, System.StringComparison.Ordinal) ||
                    !contentObject.name.Contains("_Blocker_"))
                {
                    continue;
                }

                blockerObject = contentObject;
                return true;
            }

            blockerObject = null;
            return false;
        }

        private bool TryGetHiddenDebrisInstance(TileCoord coord, out GameObject? hiddenDebrisObject)
        {
            return TryGetDebrisInstance(coord, out hiddenDebrisObject, includeHiddenDebris: true) &&
                   hiddenDebrisObject is not null &&
                   hiddenDebrisObject.name.Contains("_HiddenDebris_");
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
