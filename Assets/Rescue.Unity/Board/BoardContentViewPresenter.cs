using System.Collections.Generic;
using System.Collections.Immutable;
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
        private const float MinimumTimelineDurationSeconds = 0.01f;
        private const float MoveLandingPhaseRatio = 0.35f;
        private const string VinePreviewLabel = "VineGrowthPreview";
        private const string LastObstacleLabel = "TargetLastObstacle";
        private static readonly Vector3 HiddenDebrisScale = new Vector3(0.75f, 0.75f, 0.75f);
        private static readonly Color TargetTrappedColor = new Color(0.62f, 0.72f, 0.92f, 0.82f);
        private static readonly Color TargetProgressingColor = new Color(0.86f, 0.95f, 0.76f, 1f);
        private static readonly Color TargetOneClearAwayColor = new Color(1f, 0.88f, 0.36f, 1f);
        private static readonly Color TargetExtractableColor = new Color(1f, 0.64f, 0.24f, 1f);
        private static readonly Color TargetDistressedColor = new Color(0.35f, 0.72f, 1f, 1f);
        private static readonly Color VinePreviewColor = new Color(0.52f, 0.95f, 0.48f, 0.72f);
        private static readonly Vector3 TargetTrappedScale = new Vector3(0.92f, 0.92f, 0.92f);
        private static readonly Vector3 TargetProgressingScale = new Vector3(1f, 1f, 1f);
        private static readonly Vector3 TargetOneClearAwayScale = new Vector3(1.08f, 1.08f, 1.08f);
        private static readonly Vector3 TargetExtractableScale = new Vector3(1.16f, 1.16f, 1.16f);
        private static readonly Vector3 TargetDistressedScale = new Vector3(1.08f, 1.08f, 1.08f);

        [SerializeField] private BoardGridViewPresenter? gridView;
        [SerializeField] private PieceVisualRegistry? pieceRegistry;
        [SerializeField] private BlockerVisualRegistry? blockerRegistry;
        [SerializeField] private TargetVisualRegistry? targetRegistry;
        [SerializeField] private Transform? contentRoot;
        [SerializeField] private GameObject? fallbackContentPrefab;
        [SerializeField] private float contentYOffset = 0.05f;

        private readonly List<GameObject> spawnedContent = new List<GameObject>();
        private readonly Dictionary<GameObject, int> moveAnimationTokens = new Dictionary<GameObject, int>();
        private readonly BoardContentVisualRegistry visualRegistry = new BoardContentVisualRegistry();
        private readonly Dictionary<string, TargetVisualView> spawnedTargetsById = new Dictionary<string, TargetVisualView>();
        private readonly List<GameObject> targetObstacleMarkers = new List<GameObject>();
        private GameObject? vinePreviewObject;
        private TileCoord? vinePreviewCoord;
        private float gravityDurationSeconds = Presentation.ActionPlaybackSettings.DefaultGravityDurationSeconds;
        private float blockerDamageDurationSeconds = Presentation.ActionPlaybackSettings.DefaultBreakBlockerOrRevealDurationSeconds;
        private float blockerBreakDurationSeconds = Presentation.ActionPlaybackSettings.DefaultBreakBlockerOrRevealDurationSeconds;
        private float iceRevealDurationSeconds = Presentation.ActionPlaybackSettings.DefaultBreakBlockerOrRevealDurationSeconds;
        private float spawnDurationSeconds = Presentation.ActionPlaybackSettings.DefaultSpawnDurationSeconds;
        private float targetExtractDurationSeconds = Presentation.ActionPlaybackSettings.DefaultTargetExtractDurationSeconds;
        private float boardPieceLandingSquashXScale = Presentation.ActionPlaybackSettings.DefaultBoardPieceLandingSquashXScale;
        private float boardPieceLandingSquashYScale = Presentation.ActionPlaybackSettings.DefaultBoardPieceLandingSquashYScale;
        private float boardPieceLandingBounceDistance = Presentation.ActionPlaybackSettings.DefaultBoardPieceLandingBounceDistance;

        public void ApplyPlaybackSettings(Presentation.ActionPlaybackSettings settings)
        {
            if (settings is null)
            {
                return;
            }

            gravityDurationSeconds = settings.GravityDurationSeconds;
            blockerDamageDurationSeconds = settings.BreakBlockerOrRevealDurationSeconds;
            blockerBreakDurationSeconds = settings.BreakBlockerOrRevealDurationSeconds;
            iceRevealDurationSeconds = settings.BreakBlockerOrRevealDurationSeconds;
            spawnDurationSeconds = settings.SpawnDurationSeconds;
            targetExtractDurationSeconds = settings.TargetExtractDurationSeconds;
            boardPieceLandingSquashXScale = settings.BoardPieceLandingSquashXScale;
            boardPieceLandingSquashYScale = settings.BoardPieceLandingSquashYScale;
            boardPieceLandingBounceDistance = settings.BoardPieceLandingBounceDistance;
        }

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
            Dictionary<string, TargetState> targetsById = CreateTargetsById(state);
            ClearTargetObstacleMarkers();

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
                        expectedTargets,
                        targetsById);
                }
            }

            SyncVinePreview(state);
            SyncLastObstacleMarkers(state);
            RemoveUnexpectedPieces(visualRegistry.Debris, expectedDebris);
            RemoveUnexpectedPieces(visualRegistry.Blockers, expectedBlockers);
            RemoveUnexpectedPieces(visualRegistry.HiddenDebris, expectedHiddenDebris);
            RemoveUnexpectedTargets(expectedTargets);
            RemoveUntrackedContentObjects();
        }

        public void ForceSyncToState(GameState state)
        {
            StopAllCoroutines();
            moveAnimationTokens.Clear();
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
            moveAnimationTokens.Clear();
            spawnedTargetsById.Clear();
            targetObstacleMarkers.Clear();
            vinePreviewObject = null;
            vinePreviewCoord = null;
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

        public void AnimateGravityMove(GravitySettled gravity, float? durationSeconds = null)
        {
            float effectiveDurationSeconds = durationSeconds ?? gravityDurationSeconds;
            List<(BoardPieceView View, TileCoord To)> movesToAnimate = new List<(BoardPieceView View, TileCoord To)>();

            for (int i = 0; i < gravity.Moves.Length; i++)
            {
                (TileCoord from, TileCoord to) = gravity.Moves[i];
                if (!visualRegistry.Debris.TryGet(from, out BoardPieceView? debrisView) ||
                    debrisView is null ||
                    debrisView.Object == null)
                {
                    continue;
                }

                if (!TryGetAnchor(to, out _))
                {
                    continue;
                }

                visualRegistry.Debris.Remove(from);
                debrisView.Coord = to;
                visualRegistry.Debris.Set(to, debrisView);
                movesToAnimate.Add((debrisView, to));
            }

            for (int i = 0; i < movesToAnimate.Count; i++)
            {
                (BoardPieceView debrisView, TileCoord to) = movesToAnimate[i];
                if (!TryGetAnchor(to, out Transform anchor))
                {
                    continue;
                }

                MovePieceToCoord(
                    debrisView.Object,
                    anchor,
                    to,
                    debrisView.ContentLabel,
                    contentYOffset,
                    effectiveDurationSeconds,
                    debrisView.BaseLocalScale,
                    applyLandingFeedback: true);
            }
        }

        public void AnimateBlockerDamage(BlockerDamaged damaged, float? durationSeconds = null)
        {
            float effectiveDurationSeconds = durationSeconds ?? blockerDamageDurationSeconds;
            CleanupDestroyedVisualReferences();

            if (!TryGetLivePieceView(visualRegistry.Blockers, damaged.Coord, out BoardPieceView? blockerView) ||
                blockerView is null)
            {
                return;
            }

            if (!Application.isPlaying || !isActiveAndEnabled || effectiveDurationSeconds <= 0f)
            {
                return;
            }

            StartCoroutine(AnimateBlockerDamageRoutine(blockerView.Object, effectiveDurationSeconds));
        }

        public void AnimateBlockerBreak(BlockerBroken broken, float? durationSeconds = null)
        {
            float effectiveDurationSeconds = durationSeconds ?? blockerBreakDurationSeconds;
            CleanupDestroyedVisualReferences();

            if (!RemoveLivePieceView(visualRegistry.Blockers, broken.Coord, out BoardPieceView? blockerView) ||
                blockerView is null)
            {
                return;
            }

            if (!Application.isPlaying || !isActiveAndEnabled || effectiveDurationSeconds <= 0f)
            {
                RemoveSpawnedContentReference(blockerView.Object);
                DestroyContentObject(blockerView.Object);
                return;
            }

            StartCoroutine(AnimateBlockerBreakRoutine(blockerView.Object, effectiveDurationSeconds));
        }

        public void AnimateIceReveal(IceRevealed revealed, float? durationSeconds = null)
        {
            float effectiveDurationSeconds = durationSeconds ?? iceRevealDurationSeconds;
            CleanupDestroyedVisualReferences();

            if (!RemoveLivePieceView(visualRegistry.HiddenDebris, revealed.Coord, out BoardPieceView? hiddenDebrisView) ||
                hiddenDebrisView is null)
            {
                return;
            }

            if (!TryGetAnchor(revealed.Coord, out Transform anchor))
            {
                return;
            }

            RemoveAndDestroyPiece(visualRegistry.Debris, revealed.Coord);

            hiddenDebrisView.Coord = revealed.Coord;
            hiddenDebrisView.ContentLabel = $"Debris_{revealed.RevealedType}";
            hiddenDebrisView.BaseLocalScale = Vector3.one;
            visualRegistry.Debris.Set(revealed.Coord, hiddenDebrisView);

            MoveContentObjectToAnchor(hiddenDebrisView.Object, anchor, revealed.Coord, hiddenDebrisView.ContentLabel, contentYOffset);
            hiddenDebrisView.Object.transform.localScale = Vector3.one;

            if (!Application.isPlaying || !isActiveAndEnabled || effectiveDurationSeconds <= 0f)
            {
                SetVisualAlpha(hiddenDebrisView.Object, 1f);
                return;
            }

            StartCoroutine(AnimateIceRevealRoutine(hiddenDebrisView.Object, effectiveDurationSeconds));
        }

        public void AnimateSpawn(Spawned spawned, float? durationSeconds = null)
        {
            float effectiveDurationSeconds = durationSeconds ?? spawnDurationSeconds;
            for (int i = 0; i < spawned.Pieces.Length; i++)
            {
                TileCoord coord = spawned.Pieces[i].Coord;
                DebrisType type = spawned.Pieces[i].Type;
                if (!TryGetAnchor(coord, out Transform anchor))
                {
                    continue;
                }

                if (visualRegistry.Debris.Contains(coord))
                {
                    continue;
                }

                string contentLabel = $"Debris_{type}";
                GameObject? debrisObject = SpawnAtAnchor(
                    coord,
                    contentLabel,
                    ResolveDebrisPrefab(type),
                    anchor,
                    contentYOffset,
                    Vector3.one);

                if (debrisObject is null)
                {
                    continue;
                }

                BoardPieceView debrisView = new BoardPieceView(coord, contentLabel, debrisObject, debrisObject.transform.localScale);
                visualRegistry.Debris.Set(coord, debrisView);

                Vector3 entryPosition = GetSpawnEntryWorldPosition(coord);
                PositionContentObjectAtWorldPose(debrisObject, anchor, entryPosition, anchor.rotation);
                MovePieceToCoord(
                    debrisObject,
                    anchor,
                    coord,
                    contentLabel,
                    contentYOffset,
                    effectiveDurationSeconds,
                    debrisView.BaseLocalScale,
                    applyLandingFeedback: true);
            }
        }

        public void AnimateTargetExtract(TargetExtracted extraction, float? durationSeconds = null)
        {
            float effectiveDurationSeconds = durationSeconds ?? targetExtractDurationSeconds;
            CleanupDestroyedVisualReferences();

            if (!TryGetLiveTargetView(extraction.TargetId, out TargetVisualView? targetView) ||
                targetView is null)
            {
                return;
            }

            GameObject targetObject = targetView.Object;
            targetView.IsExtracting = true;

            if (!Application.isPlaying || !isActiveAndEnabled || effectiveDurationSeconds <= 0f)
            {
                ApplyTargetExtractPose(targetObject.transform, 1f);
                SetTargetVisualAlpha(targetObject, 0f);
                UnregisterAndDestroyTarget(extraction.TargetId, targetObject);
                return;
            }

            StartCoroutine(AnimateTargetExtractRoutine(extraction.TargetId, targetObject, effectiveDurationSeconds));
        }

        public bool TryGetTargetInstance(string targetId, out GameObject? targetObject)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                targetObject = null;
                return false;
            }

            if (TryGetLiveTargetView(targetId, out TargetVisualView? targetView) &&
                targetView is not null)
            {
                targetObject = targetView.Object;
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

        private System.Collections.IEnumerator AnimateTargetExtractRoutine(string targetId, GameObject targetObject, float durationSeconds)
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
                UnregisterAndDestroyTarget(targetId, targetObject);
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
            HashSet<string> expectedTargets,
            IReadOnlyDictionary<string, TargetState> targetsById)
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
                    TargetState targetState = targetsById.TryGetValue(targetTile.TargetId, out TargetState resolvedTarget)
                        ? resolvedTarget
                        : new TargetState(targetTile.TargetId, coord, TargetReadiness.Trapped);
                    EnsureTargetVisual(coord, targetState, anchor);
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
                    existingView.Object.transform.localScale = existingView.BaseLocalScale;
                    return;
                }
            }

            GameObject? spawnedObject = SpawnAtAnchor(coord, contentLabel, prefab, anchor, yOffset, scaleMultiplier);
            if (spawnedObject is null)
            {
                return;
            }

            registry.Set(coord, new BoardPieceView(coord, contentLabel, spawnedObject, spawnedObject.transform.localScale));
        }

        private void EnsureTargetVisual(TileCoord coord, TargetState targetState, Transform anchor)
        {
            string targetId = targetState.TargetId;
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return;
            }

            string contentLabel = $"Target_{SanitizeName(targetId)}";
            if (TryGetLiveTargetView(targetId, out TargetVisualView? existingView) &&
                existingView is not null)
            {
                if (!existingView.IsExtracting)
                {
                    MoveContentObjectToAnchor(existingView.Object, anchor, coord, contentLabel, contentYOffset);
                    ApplyTargetVisualState(existingView.Object, targetState.Readiness);
                    return;
                }

                RemoveRegisteredTarget(targetId);
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
                spawnedTargetsById[targetId] = new TargetVisualView(spawnedObject);
                ApplyTargetVisualState(spawnedObject, targetState.Readiness);
            }
        }

        private void SyncVinePreview(GameState state)
        {
            TileCoord? pendingTile = state.Vine.PendingGrowthTile;
            if (pendingTile is null
                || !BoardHelpers.InBounds(state.Board, pendingTile.Value)
                || BoardHelpers.GetTile(state.Board, pendingTile.Value) is not EmptyTile
                || !TryGetAnchor(pendingTile.Value, out Transform anchor))
            {
                ClearVinePreview();
                return;
            }

            if (vinePreviewObject is null)
            {
                vinePreviewObject = SpawnAtAnchor(
                    pendingTile.Value,
                    VinePreviewLabel,
                    ResolveFallbackPrefab("vine growth preview"),
                    anchor,
                    contentYOffset * 0.5f,
                    new Vector3(0.64f, 0.08f, 0.64f));
            }

            if (vinePreviewObject is null)
            {
                return;
            }

            vinePreviewCoord = pendingTile.Value;
            MoveContentObjectToAnchor(vinePreviewObject, anchor, pendingTile.Value, VinePreviewLabel, contentYOffset * 0.5f);
            ApplyTint(vinePreviewObject, VinePreviewColor);
        }

        private void ClearVinePreview()
        {
            if (vinePreviewObject is null)
            {
                vinePreviewCoord = null;
                return;
            }

            RemoveSpawnedContentReference(vinePreviewObject);
            DestroyContentObject(vinePreviewObject);
            vinePreviewObject = null;
            vinePreviewCoord = null;
        }

        private void SyncLastObstacleMarkers(GameState state)
        {
            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                if (target.Readiness != TargetReadiness.OneClearAway)
                {
                    continue;
                }

                if (!TryFindBlockedRequiredNeighbor(state.Board, target.Coord, out TileCoord blockedCoord)
                    || !TryGetObstacleObject(blockedCoord, out GameObject? obstacleObject)
                    || obstacleObject is null)
                {
                    continue;
                }

                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"{LastObstacleLabel}_{SanitizeName(target.TargetId)}";
                marker.transform.SetParent(obstacleObject.transform, false);
                marker.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                marker.transform.localScale = new Vector3(1.16f, 0.08f, 1.16f);
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                ApplyTint(marker, TargetOneClearAwayColor);
                targetObstacleMarkers.Add(marker);
            }
        }

        private bool TryGetObstacleObject(TileCoord coord, out GameObject? obstacleObject)
        {
            if (TryGetLivePieceView(visualRegistry.Debris, coord, out BoardPieceView? debrisView)
                && debrisView is not null)
            {
                obstacleObject = debrisView.Object;
                return true;
            }

            if (TryGetLivePieceView(visualRegistry.Blockers, coord, out BoardPieceView? blockerView)
                && blockerView is not null)
            {
                obstacleObject = blockerView.Object;
                return true;
            }

            if (TryGetLivePieceView(visualRegistry.HiddenDebris, coord, out BoardPieceView? hiddenView)
                && hiddenView is not null)
            {
                obstacleObject = hiddenView.Object;
                return true;
            }

            obstacleObject = null;
            return false;
        }

        private void ClearTargetObstacleMarkers()
        {
            for (int i = targetObstacleMarkers.Count - 1; i >= 0; i--)
            {
                GameObject? marker = targetObstacleMarkers[i];
                if (marker is null)
                {
                    targetObstacleMarkers.RemoveAt(i);
                    continue;
                }

                DestroyContentObject(marker);
                targetObstacleMarkers.RemoveAt(i);
            }
        }

        private static bool TryFindBlockedRequiredNeighbor(Board board, TileCoord targetCoord, out TileCoord blockedCoord)
        {
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, targetCoord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                TileCoord coord = neighbors[i];
                if (BoardHelpers.GetTile(board, coord) is EmptyTile)
                {
                    continue;
                }

                blockedCoord = coord;
                return true;
            }

            blockedCoord = default;
            return false;
        }

        private static Dictionary<string, TargetState> CreateTargetsById(GameState state)
        {
            Dictionary<string, TargetState> targetsById = new Dictionary<string, TargetState>(System.StringComparer.Ordinal);
            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                targetsById[target.TargetId] = target;
            }

            return targetsById;
        }

        private static void ApplyTargetVisualState(GameObject targetObject, TargetReadiness readiness)
        {
            Color tint = readiness switch
            {
                TargetReadiness.Progressing => TargetProgressingColor,
                TargetReadiness.OneClearAway => TargetOneClearAwayColor,
                TargetReadiness.ExtractableLatched => TargetExtractableColor,
                TargetReadiness.Distressed => TargetDistressedColor,
                _ => TargetTrappedColor,
            };
            Vector3 scale = readiness switch
            {
                TargetReadiness.Progressing => TargetProgressingScale,
                TargetReadiness.OneClearAway => TargetOneClearAwayScale,
                TargetReadiness.ExtractableLatched => TargetExtractableScale,
                TargetReadiness.Distressed => TargetDistressedScale,
                _ => TargetTrappedScale,
            };

            targetObject.transform.localScale = scale;
            ApplyTint(targetObject, tint);
            SyncReadabilityMarker(targetObject, readiness, tint);
        }

        private static void SyncReadabilityMarker(GameObject targetObject, TargetReadiness readiness, Color tint)
        {
            const string markerName = "TargetReadabilityMarker";
            Transform? marker = FindChildByNamePrefix(targetObject.transform, markerName);
            bool needsMarker = readiness != TargetReadiness.Trapped;
            if (!needsMarker)
            {
                if (marker is not null)
                {
                    DestroyMarker(marker.gameObject);
                }

                return;
            }

            if (marker is null)
            {
                GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                markerObject.name = markerName;
                markerObject.transform.SetParent(targetObject.transform, false);
                markerObject.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                markerObject.transform.localScale = new Vector3(1.18f, 0.04f, 1.18f);
                Object.DestroyImmediate(markerObject.GetComponent<Collider>());
                marker = markerObject.transform;
            }

            marker.gameObject.name = $"{markerName}_{readiness}";
            ApplyTint(marker.gameObject, tint);
        }

        private static Transform? FindChildByNamePrefix(Transform parent, string namePrefix)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith(namePrefix, System.StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static void DestroyMarker(GameObject markerObject)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(markerObject);
            }
            else
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        private static void ApplyTint(GameObject contentObject, Color tint)
        {
            SpriteRenderer[] spriteRenderers = contentObject.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                spriteRenderers[i].color = tint;
            }

            Graphic[] graphics = contentObject.GetComponentsInChildren<Graphic>(includeInactive: true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].color = tint;
            }

            Renderer[] renderers = contentObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", tint);
                renderers[i].SetPropertyBlock(propertyBlock);
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

                RemoveRegisteredTarget(targetId);
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

        private static bool TryGetLivePieceView(BoardPieceRegistry registry, TileCoord coord, out BoardPieceView? view)
        {
            if (!registry.TryGet(coord, out view) || view is null)
            {
                return false;
            }

            if (view.Object != null)
            {
                return true;
            }

            registry.Remove(coord);
            view = null;
            return false;
        }

        private static bool RemoveLivePieceView(BoardPieceRegistry registry, TileCoord coord, out BoardPieceView? view)
        {
            if (!registry.Remove(coord, out view) || view is null)
            {
                return false;
            }

            if (view.Object != null)
            {
                return true;
            }

            view = null;
            return false;
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
                if (spawnedTargetsById.TryGetValue(targetId, out TargetVisualView? targetView) &&
                    targetView is not null &&
                    targetView.Object != null)
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

            foreach (KeyValuePair<string, TargetVisualView> entry in spawnedTargetsById)
            {
                if (entry.Value.Object != null)
                {
                    trackedObjects.Add(entry.Value.Object);
                }
            }

            if (vinePreviewObject != null)
            {
                trackedObjects.Add(vinePreviewObject);
            }

            for (int i = 0; i < targetObstacleMarkers.Count; i++)
            {
                if (targetObstacleMarkers[i] != null)
                {
                    trackedObjects.Add(targetObstacleMarkers[i]);
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
            moveAnimationTokens.Remove(contentObject);

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

            if (!gridView.IsCoordVisible(coord))
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
                    ResolveCellWorldPositionWithYOffset(coord, yOffset),
                    anchor.rotation);
            }

            contentObject.name =
                $"Content_{coord.Row.ToString("00", CultureInfo.InvariantCulture)}_{coord.Col.ToString("00", CultureInfo.InvariantCulture)}_{contentLabel}";
        }

        private void MovePieceToCoord(
            GameObject contentObject,
            Transform anchor,
            TileCoord coord,
            string contentLabel,
            float yOffset,
            float durationSeconds,
            Vector3 baseLocalScale,
            bool applyLandingFeedback)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || durationSeconds <= 0f)
            {
                MoveContentObjectToAnchor(contentObject, anchor, coord, contentLabel, yOffset);
                contentObject.transform.localScale = baseLocalScale;
                return;
            }

            Transform parent = ResolveContentParent(anchor);
            Transform contentTransform = contentObject.transform;
            if (contentTransform.parent != parent)
            {
                Vector3 currentWorldPosition = contentTransform.position;
                Quaternion currentWorldRotation = contentTransform.rotation;
                contentTransform.SetParent(parent, worldPositionStays: false);
                contentTransform.SetPositionAndRotation(currentWorldPosition, currentWorldRotation);
            }

            Vector3 targetWorldPosition = ResolveCellWorldPositionWithYOffset(coord, yOffset);
            Quaternion targetWorldRotation = anchor.rotation;
            contentObject.name =
                $"Content_{coord.Row.ToString("00", CultureInfo.InvariantCulture)}_{coord.Col.ToString("00", CultureInfo.InvariantCulture)}_{contentLabel}";

            int token = RegisterMoveAnimation(contentObject);
            StartCoroutine(AnimateWorldMoveRoutine(
                contentObject,
                targetWorldPosition,
                targetWorldRotation,
                durationSeconds,
                baseLocalScale,
                applyLandingFeedback,
                token));
        }

        private Vector3 GetSpawnEntryWorldPosition(TileCoord coord)
        {
            if (gridView is null)
            {
                return transform.position;
            }

            return gridView.GetColumnEntryWorldPosition(coord.Col) + new Vector3(0f, contentYOffset, 0f);
        }

        private static void PositionContentObjectAtWorldPose(
            GameObject contentObject,
            Transform anchor,
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            Transform parent = contentObject.transform.parent;
            if (parent != anchor && parent is not null)
            {
                contentObject.transform.SetPositionAndRotation(worldPosition, worldRotation);
                return;
            }

            contentObject.transform.SetPositionAndRotation(worldPosition, worldRotation);
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
                    ResolveCellWorldPositionWithYOffset(coord, yOffset),
                    anchor.rotation);
            }

            contentTransform.localScale = Vector3.Scale(prefab.transform.localScale, scaleMultiplier);
            spawnedContent.Add(contentObject);
            return contentObject;
        }

        private Vector3 ResolveCellWorldPositionWithYOffset(TileCoord coord, float yOffset)
        {
            Vector3 basePosition = gridView is not null
                ? gridView.GetCellWorldPosition(coord)
                : transform.position;

            return basePosition + new Vector3(0f, yOffset, 0f);
        }

        private System.Collections.IEnumerator AnimateWorldMoveRoutine(
            GameObject contentObject,
            Vector3 targetWorldPosition,
            Quaternion targetWorldRotation,
            float durationSeconds,
            Vector3 baseLocalScale,
            bool applyLandingFeedback,
            int animationToken)
        {
            if (contentObject is null)
            {
                yield break;
            }

            Transform contentTransform = contentObject.transform;
            Vector3 startWorldPosition = contentTransform.position;
            Quaternion startWorldRotation = contentTransform.rotation;
            float clampedDuration = Mathf.Max(MinimumTimelineDurationSeconds, durationSeconds);
            float movePhaseDuration = applyLandingFeedback
                ? Mathf.Max(MinimumTimelineDurationSeconds, clampedDuration * (1.0f - MoveLandingPhaseRatio))
                : clampedDuration;
            float elapsed = 0f;

            while (elapsed < movePhaseDuration)
            {
                if (contentObject is null || !IsCurrentMoveAnimation(contentObject, animationToken))
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / movePhaseDuration);
                contentTransform.SetPositionAndRotation(
                    Vector3.Lerp(startWorldPosition, targetWorldPosition, normalized),
                    Quaternion.Lerp(startWorldRotation, targetWorldRotation, normalized));
                contentTransform.localScale = baseLocalScale;
                yield return null;
            }

            if (contentObject is null || !IsCurrentMoveAnimation(contentObject, animationToken))
            {
                yield break;
            }

            contentTransform.SetPositionAndRotation(targetWorldPosition, targetWorldRotation);

            if (applyLandingFeedback)
            {
                yield return AnimateLandingFeedbackRoutine(
                    contentObject,
                    targetWorldPosition,
                    targetWorldRotation,
                    baseLocalScale,
                    Mathf.Max(MinimumTimelineDurationSeconds, clampedDuration - movePhaseDuration),
                    animationToken);
            }

            if (contentObject is not null && IsCurrentMoveAnimation(contentObject, animationToken))
            {
                contentTransform.SetPositionAndRotation(targetWorldPosition, targetWorldRotation);
                contentTransform.localScale = baseLocalScale;
                moveAnimationTokens.Remove(contentObject);
            }
        }

        private System.Collections.IEnumerator AnimateLandingFeedbackRoutine(
            GameObject contentObject,
            Vector3 targetWorldPosition,
            Quaternion targetWorldRotation,
            Vector3 baseLocalScale,
            float durationSeconds,
            int animationToken)
        {
            Transform contentTransform = contentObject.transform;
            Vector3 squashScale = Vector3.Scale(
                baseLocalScale,
                new Vector3(boardPieceLandingSquashXScale, boardPieceLandingSquashYScale, boardPieceLandingSquashXScale));
            float clampedDuration = Mathf.Max(MinimumTimelineDurationSeconds, durationSeconds);
            float elapsed = 0f;

            while (elapsed < clampedDuration)
            {
                if (contentObject is null || !IsCurrentMoveAnimation(contentObject, animationToken))
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / clampedDuration);
                float squashPulse = Mathf.Sin(normalized * Mathf.PI);
                float bouncePulse = Mathf.Sin(normalized * Mathf.PI * 2.0f) * (1.0f - normalized);
                Vector3 bounceOffset = Vector3.up * (boardPieceLandingBounceDistance * Mathf.Max(0f, bouncePulse));
                contentTransform.SetPositionAndRotation(targetWorldPosition + bounceOffset, targetWorldRotation);
                contentTransform.localScale = Vector3.LerpUnclamped(baseLocalScale, squashScale, squashPulse);
                yield return null;
            }

            if (contentObject is not null && IsCurrentMoveAnimation(contentObject, animationToken))
            {
                contentTransform.SetPositionAndRotation(targetWorldPosition, targetWorldRotation);
                contentTransform.localScale = baseLocalScale;
            }
        }

        private int RegisterMoveAnimation(GameObject contentObject)
        {
            if (!moveAnimationTokens.TryGetValue(contentObject, out int token))
            {
                token = 0;
            }

            token++;
            moveAnimationTokens[contentObject] = token;
            return token;
        }

        private bool IsCurrentMoveAnimation(GameObject contentObject, int token)
        {
            return contentObject != null &&
                moveAnimationTokens.TryGetValue(contentObject, out int currentToken) &&
                currentToken == token;
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

        private bool TryGetLiveTargetView(string targetId, out TargetVisualView? targetView)
        {
            if (!spawnedTargetsById.TryGetValue(targetId, out targetView) ||
                targetView is null)
            {
                return false;
            }

            if (targetView.Object != null)
            {
                return true;
            }

            spawnedTargetsById.Remove(targetId);
            targetView = null;
            return false;
        }

        private void RemoveRegisteredTarget(string targetId)
        {
            if (!spawnedTargetsById.TryGetValue(targetId, out TargetVisualView? targetView) ||
                targetView is null ||
                targetView.Object == null)
            {
                spawnedTargetsById.Remove(targetId);
                return;
            }

            spawnedTargetsById.Remove(targetId);
            RemoveSpawnedContentReference(targetView.Object);
            DestroyContentObject(targetView.Object);
        }

        private void UnregisterAndDestroyTarget(string targetId, GameObject targetObject)
        {
            if (spawnedTargetsById.TryGetValue(targetId, out TargetVisualView? targetView) &&
                targetView is not null &&
                targetView.Object == targetObject)
            {
                spawnedTargetsById.Remove(targetId);
            }

            RemoveSpawnedContentReference(targetObject);
            DestroyContentObject(targetObject);
        }

        private sealed class BoardPieceView
        {
            public BoardPieceView(TileCoord coord, string contentLabel, GameObject contentObject, Vector3 baseLocalScale)
            {
                Coord = coord;
                ContentLabel = contentLabel;
                Object = contentObject;
                BaseLocalScale = baseLocalScale;
            }

            public TileCoord Coord { get; set; }

            public string ContentLabel { get; set; }

            public GameObject Object { get; }

            public Vector3 BaseLocalScale { get; set; }
        }

        private sealed class TargetVisualView
        {
            public TargetVisualView(GameObject contentObject)
            {
                Object = contentObject;
            }

            public bool IsExtracting { get; set; }

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
