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
        private readonly BoardContentVisualRegistry visualRegistry = new BoardContentVisualRegistry();
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

            CleanupDestroyedVisualReferences();

            HashSet<TileCoord> expectedDebris = new HashSet<TileCoord>();
            HashSet<TileCoord> expectedBlockers = new HashSet<TileCoord>();
            HashSet<TileCoord> expectedHiddenDebris = new HashSet<TileCoord>();
            HashSet<string> expectedTargets = new HashSet<string>();

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
                    ReconcileTileContent(
                        coord,
                        tile,
                        anchor,
                        expectedDebris,
                        expectedBlockers,
                        expectedHiddenDebris,
                        expectedTargets);
                }
            }

            RemoveUnexpectedPieces(visualRegistry.Debris, expectedDebris);
            RemoveUnexpectedPieces(visualRegistry.Blockers, expectedBlockers);
            RemoveUnexpectedPieces(visualRegistry.HiddenDebris, expectedHiddenDebris);
            RemoveUnexpectedTargets(expectedTargets);
            RemoveUntrackedContentObjects();
        }

        public void ForceSyncToState(GameState state)
        {
            StopAllCoroutines();
            SyncImmediate(state);
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
                if (contentObject == null)
                {
                    spawnedContent.RemoveAt(i);
                    continue;
                }

                DestroyContentObject(contentObject);
                spawnedContent.RemoveAt(i);
            }

            visualRegistry.Clear();
            spawnedTargetsById.Clear();
        }

        public void RemoveDebrisGroup(GroupRemoved removal)
        {
            for (int i = 0; i < removal.Coords.Length; i++)
            {
                if (!visualRegistry.Debris.TryGet(removal.Coords[i], out BoardPieceView? debrisView) ||
                    debrisView is null ||
                    debrisView.Object == null)
                {
                    continue;
                }

                RemoveAndDestroyPiece(visualRegistry.Debris, removal.Coords[i]);
            }
        }

        public void AnimateGravityMove(GravitySettled gravity)
        {
            for (int i = 0; i < gravity.Moves.Length; i++)
            {
                (TileCoord from, TileCoord to) = gravity.Moves[i];
                if (!visualRegistry.Debris.TryGet(from, out BoardPieceView? debrisView) ||
                    debrisView is null ||
                    debrisView.Object == null)
                {
                    continue;
                }

                if (!TryGetAnchor(to, out Transform anchor))
                {
                    continue;
                }

                visualRegistry.Debris.Remove(from);
                debrisView.Coord = to;
                visualRegistry.Debris.Set(to, debrisView);
                MoveContentObjectToAnchor(debrisView.Object, anchor, to, debrisView.ContentLabel, contentYOffset);
            }
        }

        public void AnimateBlockerDamage(BlockerDamaged damaged, float durationSeconds = 0.10f)
        {
            if (!visualRegistry.Blockers.TryGet(damaged.Coord, out BoardPieceView? blockerView) ||
                blockerView is null ||
                blockerView.Object == null)
            {
                return;
            }

            if (!Application.isPlaying || !isActiveAndEnabled || durationSeconds <= 0f)
            {
                return;
            }

            StartCoroutine(AnimateBlockerDamageRoutine(blockerView.Object, durationSeconds));
        }

        public void AnimateBlockerBreak(BlockerBroken broken, float durationSeconds = 0.10f)
        {
            if (!visualRegistry.Blockers.TryGet(broken.Coord, out BoardPieceView? blockerView) ||
                blockerView is null ||
                blockerView.Object == null)
            {
                return;
            }

            visualRegistry.Blockers.Remove(broken.Coord);

            if (!Application.isPlaying || !isActiveAndEnabled || durationSeconds <= 0f)
            {
                RemoveSpawnedContentReference(blockerView.Object);
                DestroyContentObject(blockerView.Object);
                return;
            }

            StartCoroutine(AnimateBlockerBreakRoutine(blockerView.Object, durationSeconds));
        }

        public void AnimateIceReveal(IceRevealed revealed, float durationSeconds = 0.10f)
        {
            if (!visualRegistry.HiddenDebris.TryGet(revealed.Coord, out BoardPieceView? hiddenDebrisView) ||
                hiddenDebrisView is null ||
                hiddenDebrisView.Object == null)
            {
                return;
            }

            if (!TryGetAnchor(revealed.Coord, out Transform anchor))
            {
                return;
            }

            visualRegistry.HiddenDebris.Remove(revealed.Coord);
            hiddenDebrisView.ContentLabel = $"Debris_{revealed.RevealedType}";
            visualRegistry.Debris.Set(revealed.Coord, hiddenDebrisView);

            MoveContentObjectToAnchor(hiddenDebrisView.Object, anchor, revealed.Coord, hiddenDebrisView.ContentLabel, contentYOffset);
            hiddenDebrisView.Object.transform.localScale = Vector3.one;

            if (!Application.isPlaying || !isActiveAndEnabled || durationSeconds <= 0f)
            {
                SetVisualAlpha(hiddenDebrisView.Object, 1f);
                return;
            }

            StartCoroutine(AnimateIceRevealRoutine(hiddenDebrisView.Object, durationSeconds));
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

                if (visualRegistry.Debris.Contains(coord))
                {
                    continue;
                }

                GameObject? debrisObject = SpawnAtAnchor(
                    coord,
                    $"Debris_{type}",
                    ResolveDebrisPrefab(type),
                    anchor,
                    contentYOffset,
                    Vector3.one);

                if (debrisObject is not null)
                {
                    visualRegistry.Debris.Set(coord, new BoardPieceView(coord, $"Debris_{type}", debrisObject));
                }
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
                RemoveSpawnedContentReference(targetObject);
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

            if (spawnedTargetsById.TryGetValue(targetId, out GameObject targetInstance) && targetInstance != null)
            {
                targetObject = targetInstance;
                return true;
            }

            spawnedTargetsById.Remove(targetId);

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

        private void ReconcileTileContent(
            TileCoord coord,
            Tile tile,
            Transform anchor,
            HashSet<TileCoord> expectedDebris,
            HashSet<TileCoord> expectedBlockers,
            HashSet<TileCoord> expectedHiddenDebris,
            HashSet<string> expectedTargets)
        {
            switch (tile)
            {
                case EmptyTile:
                case FloodedTile:
                    return;
                case DebrisTile debrisTile:
                    expectedDebris.Add(coord);
                    EnsurePieceVisual(
                        visualRegistry.Debris,
                        coord,
                        $"Debris_{debrisTile.Type}",
                        ResolveDebrisPrefab(debrisTile.Type),
                        anchor,
                        contentYOffset,
                        Vector3.one);
                    return;
                case BlockerTile blockerTile:
                    expectedBlockers.Add(coord);
                    EnsurePieceVisual(
                        visualRegistry.Blockers,
                        coord,
                        $"Blocker_{blockerTile.Type}",
                        ResolveBlockerPrefab(blockerTile.Type),
                        anchor,
                        contentYOffset,
                        Vector3.one);

                    if (blockerTile.Type == BlockerType.Ice && blockerTile.Hidden is not null)
                    {
                        expectedHiddenDebris.Add(coord);
                        EnsurePieceVisual(
                            visualRegistry.HiddenDebris,
                            coord,
                            $"HiddenDebris_{blockerTile.Hidden.Type}",
                            ResolveDebrisPrefab(blockerTile.Hidden.Type),
                            anchor,
                            contentYOffset * HiddenDebrisYOffsetRatio,
                            HiddenDebrisScale);
                    }

                    return;
                case TargetTile targetTile when !targetTile.Extracted:
                    expectedTargets.Add(targetTile.TargetId);
                    EnsureTargetVisual(coord, targetTile.TargetId, anchor);
                    return;
                default:
                    return;
            }
        }

        private void EnsurePieceVisual(
            BoardPieceRegistry registry,
            TileCoord coord,
            string contentLabel,
            GameObject? prefab,
            Transform anchor,
            float yOffset,
            Vector3 scaleMultiplier)
        {
            if (registry.TryGet(coord, out BoardPieceView? existingView) &&
                existingView is not null &&
                existingView.Object != null)
            {
                if (existingView.ContentLabel != contentLabel)
                {
                    RemoveAndDestroyPiece(registry, coord);
                }
                else
                {
                    existingView.Coord = coord;
                    MoveContentObjectToAnchor(existingView.Object, anchor, coord, contentLabel, yOffset);
                    return;
                }
            }

            GameObject? spawnedObject = SpawnAtAnchor(coord, contentLabel, prefab, anchor, yOffset, scaleMultiplier);
            if (spawnedObject is null)
            {
                return;
            }

            registry.Set(coord, new BoardPieceView(coord, contentLabel, spawnedObject));
        }

        private void EnsureTargetVisual(TileCoord coord, string targetId, Transform anchor)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return;
            }

            string contentLabel = $"Target_{SanitizeName(targetId)}";
            if (spawnedTargetsById.TryGetValue(targetId, out GameObject targetObject) && targetObject != null)
            {
                MoveContentObjectToAnchor(targetObject, anchor, coord, contentLabel, contentYOffset);
                return;
            }

            GameObject? spawnedObject = SpawnAtAnchor(
                coord,
                contentLabel,
                ResolveTargetPrefab(targetId),
                anchor,
                contentYOffset,
                Vector3.one);

            if (spawnedObject is not null)
            {
                spawnedTargetsById[targetId] = spawnedObject;
            }
        }

        private void RemoveUnexpectedPieces(BoardPieceRegistry registry, HashSet<TileCoord> expectedCoords)
        {
            foreach (TileCoord coord in registry.GetCoordsSnapshot())
            {
                if (!expectedCoords.Contains(coord))
                {
                    RemoveAndDestroyPiece(registry, coord);
                }
            }
        }

        private void RemoveUnexpectedTargets(HashSet<string> expectedTargetIds)
        {
            List<string> targetIds = new List<string>(spawnedTargetsById.Keys);
            for (int i = 0; i < targetIds.Count; i++)
            {
                string targetId = targetIds[i];
                if (expectedTargetIds.Contains(targetId))
                {
                    continue;
                }

                if (spawnedTargetsById.TryGetValue(targetId, out GameObject targetObject) && targetObject != null)
                {
                    RemoveSpawnedContentReference(targetObject);
                    DestroyContentObject(targetObject);
                }

                spawnedTargetsById.Remove(targetId);
            }
        }

        private void RemoveAndDestroyPiece(BoardPieceRegistry registry, TileCoord coord)
        {
            if (!registry.Remove(coord, out BoardPieceView? removedView) ||
                removedView is null ||
                removedView.Object == null)
            {
                return;
            }

            RemoveSpawnedContentReference(removedView.Object);
            DestroyContentObject(removedView.Object);
        }

        private void CleanupDestroyedVisualReferences()
        {
            CleanupDestroyedPieceReferences(visualRegistry.Debris);
            CleanupDestroyedPieceReferences(visualRegistry.Blockers);
            CleanupDestroyedPieceReferences(visualRegistry.HiddenDebris);

            List<string> targetIds = new List<string>(spawnedTargetsById.Keys);
            for (int i = 0; i < targetIds.Count; i++)
            {
                string targetId = targetIds[i];
                if (spawnedTargetsById.TryGetValue(targetId, out GameObject targetObject) && targetObject != null)
                {
                    continue;
                }

                spawnedTargetsById.Remove(targetId);
            }

            for (int i = spawnedContent.Count - 1; i >= 0; i--)
            {
                if (spawnedContent[i] != null)
                {
                    continue;
                }

                spawnedContent.RemoveAt(i);
            }
        }

        private static void CleanupDestroyedPieceReferences(BoardPieceRegistry registry)
        {
            foreach (TileCoord coord in registry.GetCoordsSnapshot())
            {
                if (registry.TryGet(coord, out BoardPieceView? view) &&
                    view is not null &&
                    view.Object != null)
                {
                    continue;
                }

                registry.Remove(coord);
            }
        }

        private void RemoveUntrackedContentObjects()
        {
            HashSet<GameObject> trackedObjects = new HashSet<GameObject>();
            visualRegistry.AddTrackedObjects(trackedObjects);

            foreach (KeyValuePair<string, GameObject> entry in spawnedTargetsById)
            {
                if (entry.Value != null)
                {
                    trackedObjects.Add(entry.Value);
                }
            }

            for (int i = spawnedContent.Count - 1; i >= 0; i--)
            {
                GameObject? contentObject = spawnedContent[i];
                if (contentObject == null)
                {
                    spawnedContent.RemoveAt(i);
                    continue;
                }

                if (trackedObjects.Contains(contentObject))
                {
                    continue;
                }

                spawnedContent.RemoveAt(i);
                DestroyContentObject(contentObject);
            }
        }

        private void RemoveSpawnedContentReference(GameObject contentObject)
        {
            for (int i = spawnedContent.Count - 1; i >= 0; i--)
            {
                if (spawnedContent[i] != contentObject)
                {
                    continue;
                }

                spawnedContent.RemoveAt(i);
                return;
            }
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

            contentObject.name =
                $"Content_{coord.Row.ToString("00", CultureInfo.InvariantCulture)}_{coord.Col.ToString("00", CultureInfo.InvariantCulture)}_{contentLabel}";
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

        private sealed class BoardPieceView
        {
            public BoardPieceView(TileCoord coord, string contentLabel, GameObject contentObject)
            {
                Coord = coord;
                ContentLabel = contentLabel;
                Object = contentObject;
            }

            public TileCoord Coord { get; set; }

            public string ContentLabel { get; set; }

            public GameObject Object { get; }
        }

        private sealed class BoardPieceRegistry
        {
            private readonly Dictionary<TileCoord, BoardPieceView> viewsByCoord = new Dictionary<TileCoord, BoardPieceView>();

            public bool TryGet(TileCoord coord, out BoardPieceView? view)
            {
                if (viewsByCoord.TryGetValue(coord, out BoardPieceView existingView))
                {
                    view = existingView;
                    return true;
                }

                view = null;
                return false;
            }

            public bool Contains(TileCoord coord)
            {
                return viewsByCoord.ContainsKey(coord);
            }

            public void Set(TileCoord coord, BoardPieceView view)
            {
                viewsByCoord[coord] = view;
            }

            public bool Remove(TileCoord coord)
            {
                return viewsByCoord.Remove(coord);
            }

            public bool Remove(TileCoord coord, out BoardPieceView? view)
            {
                if (viewsByCoord.TryGetValue(coord, out BoardPieceView existingView))
                {
                    viewsByCoord.Remove(coord);
                    view = existingView;
                    return true;
                }

                view = null;
                return false;
            }

            public List<TileCoord> GetCoordsSnapshot()
            {
                return new List<TileCoord>(viewsByCoord.Keys);
            }

            public void AddTrackedObjects(ISet<GameObject> trackedObjects)
            {
                foreach (BoardPieceView view in viewsByCoord.Values)
                {
                    if (view.Object != null)
                    {
                        trackedObjects.Add(view.Object);
                    }
                }
            }

            public void Clear()
            {
                viewsByCoord.Clear();
            }
        }

        private sealed class BoardContentVisualRegistry
        {
            public BoardPieceRegistry Debris { get; } = new BoardPieceRegistry();

            public BoardPieceRegistry Blockers { get; } = new BoardPieceRegistry();

            public BoardPieceRegistry HiddenDebris { get; } = new BoardPieceRegistry();

            public void AddTrackedObjects(ISet<GameObject> trackedObjects)
            {
                Debris.AddTrackedObjects(trackedObjects);
                Blockers.AddTrackedObjects(trackedObjects);
                HiddenDebris.AddTrackedObjects(trackedObjects);
            }

            public void Clear()
            {
                Debris.Clear();
                Blockers.Clear();
                HiddenDebris.Clear();
            }
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
