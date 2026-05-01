using System.Collections.Immutable;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Presentation;
using Rescue.Unity.UI;
using UnityEngine;

namespace Rescue.Unity.FX
{
    [DisallowMultipleComponent]
    public class FxEventRouter : MonoBehaviour
    {
        [SerializeField] private FxVisualRegistry? fxRegistry;
        [SerializeField] private Transform? fxRoot;
        [SerializeField] private BoardGridViewPresenter? boardGrid;
        [SerializeField] private DockViewPresenter? dockView;
        [SerializeField] private bool alignSpawnedFxToPresentationPlane = true;
        [SerializeField] private Vector3 spawnedFxPlaneEulerOffset = new Vector3(90f, 0f, 0f);
        [SerializeField] private float spawnedFxSurfaceOffset = 0.28f;
        [SerializeField] private bool diagnosticsEnabled;
        [SerializeField] private float diagnosticMinimumVisibleSeconds;

        private Coroutine? diagnosticPlaybackCoroutine;
        private string? currentDiagnosticSourceEvent;

        public FxVisualRegistry? FxRegistry
        {
            get => fxRegistry;
            set => fxRegistry = value;
        }

        public Transform? FxRoot
        {
            get => fxRoot;
            set => fxRoot = value;
        }

        public BoardGridViewPresenter? BoardGrid
        {
            get => boardGrid;
            set => boardGrid = value;
        }

        public DockViewPresenter? DockView
        {
            get => dockView;
            set => dockView = value;
        }

        public bool AlignSpawnedFxToPresentationPlane
        {
            get => alignSpawnedFxToPresentationPlane;
            set => alignSpawnedFxToPresentationPlane = value;
        }

        public Vector3 SpawnedFxPlaneEulerOffset
        {
            get => spawnedFxPlaneEulerOffset;
            set => spawnedFxPlaneEulerOffset = value;
        }

        public float SpawnedFxSurfaceOffset
        {
            get => spawnedFxSurfaceOffset;
            set => spawnedFxSurfaceOffset = Mathf.Max(0f, value);
        }

        public bool DiagnosticsEnabled
        {
            get => diagnosticsEnabled;
            set => diagnosticsEnabled = value;
        }

        public float DiagnosticMinimumVisibleSeconds
        {
            get => diagnosticMinimumVisibleSeconds;
            set => diagnosticMinimumVisibleSeconds = Mathf.Max(0f, value);
        }

        public void Route(GameState previousState, ActionInput input, ActionResult result)
        {
            ImmutableArray<FxEventHook> hooks = FxEventClassifier.Classify(previousState, input, result);

            for (int i = 0; i < hooks.Length; i++)
            {
                switch (hooks[i])
                {
                    case FxEventHook.GroupClear:
                        PlayGroupClear();
                        break;
                    case FxEventHook.InvalidTap:
                        PlayInvalidTap();
                        break;
                    case FxEventHook.CrateBreak:
                        PlayCrateBreak();
                        break;
                    case FxEventHook.IceReveal:
                        PlayIceReveal();
                        break;
                    case FxEventHook.VineClear:
                        PlayVineClear();
                        break;
                    case FxEventHook.VineGrowthPreview:
                        PlayVineGrowthPreview();
                        break;
                    case FxEventHook.DockInsert:
                        PlayDockInsert();
                        break;
                    case FxEventHook.DockTripleClear:
                        PlayDockTripleClear();
                        break;
                    case FxEventHook.DockWarning:
                        PlayDockWarning();
                        break;
                    case FxEventHook.WaterRise:
                        PlayWaterRise();
                        break;
                    case FxEventHook.NearRescueRelief:
                        PlayNearRescueRelief();
                        break;
                    case FxEventHook.TargetExtraction:
                        PlayTargetExtraction();
                        break;
                    case FxEventHook.Win:
                        PlayWin();
                        break;
                    case FxEventHook.LossDockOverflow:
                        PlayLossDockOverflow();
                        break;
                    case FxEventHook.LossWaterOnTarget:
                        PlayLossWaterOnTarget();
                        break;
                }
            }
        }

        public void RoutePlaybackBeat(
            GameState previousState,
            ActionInput input,
            GameState resultState,
            ActionPlaybackStep playbackStep)
        {
            _ = previousState;
            _ = input;
            _ = resultState;

            if (playbackStep.SourceEvent is null)
            {
                return;
            }

            currentDiagnosticSourceEvent = playbackStep.SourceEventName ?? playbackStep.SourceEvent.GetType().Name;
            try
            {
                switch (playbackStep.SourceEvent)
                {
                    case InvalidInput invalidInput:
                        PlayInvalidTap(ResolveCellWorldPosition(invalidInput.TappedCoord));
                        break;
                    case GroupRemoved removed:
                        PlayGroupClear(ResolveGroupWorldPosition(removed.Coords));
                        break;
                    case BlockerBroken broken when broken.Type == BlockerType.Crate:
                        PlayCrateBreak(ResolveCellWorldPosition(broken.Coord));
                        break;
                    case BlockerBroken broken when broken.Type == BlockerType.Vine:
                        PlayVineClear(ResolveCellWorldPosition(broken.Coord));
                        break;
                    case IceRevealed revealed:
                        PlayIceReveal(ResolveCellWorldPosition(revealed.Coord));
                        break;
                    case DockCleared cleared:
                        PlayDockTripleClear(ResolveDockClearWorldPosition(cleared));
                        break;
                    case DockInserted inserted:
                        PlayDockInsert(ResolveDockInsertWorldPosition(inserted));
                        break;
                    case DockWarningChanged warningChanged when warningChanged.After != DockWarningLevel.Safe:
                        PlayDockWarning();
                        break;
                    case DockOverflowTriggered:
                        PlayLossDockOverflow();
                        break;
                    case DockJamTriggered:
                        // No dock-jam-specific FX prefab exists yet; reuse the optional dock warning fallback.
                        PlayDockWarning();
                        break;
                    case TargetProgressed progressed:
                        PlayNearRescueRelief(ResolveCellWorldPosition(progressed.Coord));
                        break;
                    case TargetOneClearAway oneClearAway:
                        PlayNearRescueRelief(ResolveCellWorldPosition(oneClearAway.Coord));
                        break;
                    case TargetExtractionLatched latched:
                        PlayNearRescueRelief(ResolveCellWorldPosition(latched.Coord));
                        break;
                    case TargetExtracted extracted:
                        PlayTargetExtraction(ResolveCellWorldPosition(extracted.Coord));
                        break;
                    case WaterWarning warning:
                        PlayWaterRise(ResolveRowWorldPosition(warning.NextFloodRow));
                        break;
                    case WaterRose rose:
                        PlayWaterRise(ResolveRowWorldPosition(rose.FloodedRow));
                        break;
                    case VinePreviewChanged previewChanged when previewChanged.PendingTile.HasValue:
                        PlayVineGrowthPreview(ResolveCellWorldPosition(previewChanged.PendingTile.Value));
                        break;
                    case VineGrown grown:
                        // Placeholder until final art exists: reuse the authored pressure preview hook at the grown tile.
                        PlayVineGrowthPreview(ResolveCellWorldPosition(grown.Coord));
                        break;
                    case Won:
                        PlayWin();
                        break;
                    case Lost lost when lost.Outcome == ActionOutcome.LossDockOverflow:
                        PlayLossDockOverflow();
                        break;
                    case Lost lost when lost.Outcome == ActionOutcome.LossWaterOnTarget
                        || lost.Outcome == ActionOutcome.LossRescuePathFlooded
                        || lost.Outcome == ActionOutcome.LossDistressedExpired:
                        PlayLossWaterOnTarget();
                        break;
                }
            }
            finally
            {
                currentDiagnosticSourceEvent = null;
            }
        }

        public void PlayAllRegisteredFxForDiagnostics(Vector3 worldPosition, float spacingSeconds = 0.65f)
        {
            if (diagnosticPlaybackCoroutine is not null)
            {
                StopCoroutine(diagnosticPlaybackCoroutine);
                diagnosticPlaybackCoroutine = null;
            }

            DiagnosticsEnabled = true;
            DiagnosticMinimumVisibleSeconds = Mathf.Max(DiagnosticMinimumVisibleSeconds, 0.5f);
            diagnosticPlaybackCoroutine = StartCoroutine(PlayAllRegisteredFxSequence(worldPosition, Mathf.Max(0f, spacingSeconds)));
        }

        public GameObject? SpawnManualDebugFx(GameObject? prefab, string instanceName, FxEventHook hook, Vector3 worldPosition)
        {
            GameObject? instance = TrySpawn(prefab, instanceName, hook, worldPosition);
            if (instance is null)
            {
                return null;
            }

            SpriteSequenceFxPlayer? player = FxDebugFramePlayer.EnsureInspectionPlayer(instance);
            if (player is not null)
            {
                player.DestroyAfterPlayback = false;
                player.PausePlayback();
            }

            return instance;
        }

        public Vector3 ResolveDebugFxWorldPosition(Vector3 worldPosition)
        {
            return ResolveFxWorldPosition(worldPosition);
        }

        public Quaternion ResolveDebugFxWorldRotation()
        {
            return ResolveFxWorldRotation();
        }

        public GameObject? GetActivePrefab(FxEventHook hook)
        {
            return hook switch
            {
                FxEventHook.GroupClear => fxRegistry?.GroupClearFx,
                FxEventHook.InvalidTap => fxRegistry?.InvalidTapFx,
                FxEventHook.CrateBreak => fxRegistry?.CrateBreakFx,
                FxEventHook.IceReveal => fxRegistry?.IceRevealFx,
                FxEventHook.VineClear => fxRegistry?.VineClearFx,
                FxEventHook.VineGrowthPreview => fxRegistry?.VineGrowPreviewFx,
                FxEventHook.DockInsert => fxRegistry?.DockInsertFx,
                FxEventHook.DockTripleClear => fxRegistry?.DockTripleClearFx,
                FxEventHook.WaterRise => fxRegistry?.WaterRiseFx,
                FxEventHook.NearRescueRelief => fxRegistry?.NearRescueReliefFx,
                FxEventHook.TargetExtraction => fxRegistry?.TargetExtractionFx,
                FxEventHook.Win => fxRegistry?.WinFx,
                FxEventHook.LossDockOverflow => fxRegistry?.LossFx,
                FxEventHook.LossWaterOnTarget => fxRegistry?.LossFx,
                FxEventHook.DockWarning => null,
                _ => null,
            };
        }

        public GameObject? GetFallbackPrefab(FxEventHook hook)
        {
            return hook switch
            {
                FxEventHook.DockWarning => fxRegistry?.DockInsertFx,
                FxEventHook.LossDockOverflow => fxRegistry?.LossFx,
                FxEventHook.LossWaterOnTarget => fxRegistry?.LossFx,
                FxEventHook.VineGrowthPreview => fxRegistry?.VineGrowPreviewFx,
                _ => null,
            };
        }

        public void ClearSpawnedFx()
        {
            Transform parent = fxRoot != null ? fxRoot : transform;
            for (int childIndex = parent.childCount - 1; childIndex >= 0; childIndex--)
            {
                GameObject child = parent.GetChild(childIndex).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        protected virtual void PlayGroupClear()
        {
            PlayGroupClear(GetSafeFallbackPosition());
        }

        protected virtual void PlayGroupClear(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.GroupClearFx, nameof(FxVisualRegistry.GroupClearFx), FxEventHook.GroupClear, worldPosition);
        }

        protected virtual void PlayInvalidTap()
        {
            PlayInvalidTap(GetSafeFallbackPosition());
        }

        protected virtual void PlayInvalidTap(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.InvalidTapFx, nameof(FxVisualRegistry.InvalidTapFx), FxEventHook.InvalidTap, worldPosition);
        }

        protected virtual void PlayCrateBreak()
        {
            PlayCrateBreak(GetSafeFallbackPosition());
        }

        protected virtual void PlayCrateBreak(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.CrateBreakFx, nameof(FxVisualRegistry.CrateBreakFx), FxEventHook.CrateBreak, worldPosition);
        }

        protected virtual void PlayIceReveal()
        {
            PlayIceReveal(GetSafeFallbackPosition());
        }

        protected virtual void PlayIceReveal(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.IceRevealFx, nameof(FxVisualRegistry.IceRevealFx), FxEventHook.IceReveal, worldPosition);
        }

        protected virtual void PlayVineClear()
        {
            PlayVineClear(GetSafeFallbackPosition());
        }

        protected virtual void PlayVineClear(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.VineClearFx, nameof(FxVisualRegistry.VineClearFx), FxEventHook.VineClear, worldPosition);
        }

        protected virtual void PlayVineGrowthPreview()
        {
            PlayVineGrowthPreview(GetSafeFallbackPosition());
        }

        protected virtual void PlayVineGrowthPreview(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.VineGrowPreviewFx, nameof(FxVisualRegistry.VineGrowPreviewFx), FxEventHook.VineGrowthPreview, worldPosition);
        }

        protected virtual void PlayDockInsert()
        {
            TrySpawn(fxRegistry?.DockInsertFx, nameof(FxVisualRegistry.DockInsertFx), FxEventHook.DockInsert);
        }

        protected virtual void PlayDockInsert(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.DockInsertFx, nameof(FxVisualRegistry.DockInsertFx), FxEventHook.DockInsert, worldPosition);
        }

        protected virtual void PlayDockTripleClear()
        {
            PlayDockTripleClear(GetSafeFallbackPosition());
        }

        protected virtual void PlayDockTripleClear(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.DockTripleClearFx, nameof(FxVisualRegistry.DockTripleClearFx), FxEventHook.DockTripleClear, worldPosition);
        }

        protected virtual void PlayDockWarning()
        {
            TrySpawn(fxRegistry?.DockInsertFx, $"{nameof(FxVisualRegistry.DockInsertFx)}_WarningFallback", FxEventHook.DockWarning);
        }

        protected virtual void PlayWaterRise()
        {
            PlayWaterRise(GetSafeFallbackPosition());
        }

        protected virtual void PlayWaterRise(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.WaterRiseFx, nameof(FxVisualRegistry.WaterRiseFx), FxEventHook.WaterRise, worldPosition);
        }

        protected virtual void PlayNearRescueRelief()
        {
            PlayNearRescueRelief(GetSafeFallbackPosition());
        }

        protected virtual void PlayNearRescueRelief(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.NearRescueReliefFx, nameof(FxVisualRegistry.NearRescueReliefFx), FxEventHook.NearRescueRelief, worldPosition);
        }

        protected virtual void PlayTargetExtraction()
        {
            PlayTargetExtraction(GetSafeFallbackPosition());
        }

        protected virtual void PlayTargetExtraction(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.TargetExtractionFx, nameof(FxVisualRegistry.TargetExtractionFx), FxEventHook.TargetExtraction, worldPosition);
        }

        protected virtual void PlayWin()
        {
            TrySpawn(fxRegistry?.WinFx, nameof(FxVisualRegistry.WinFx), FxEventHook.Win);
        }

        protected virtual void PlayLossDockOverflow()
        {
            TrySpawn(fxRegistry?.LossFx, $"{nameof(FxVisualRegistry.LossFx)}_DockOverflow", FxEventHook.LossDockOverflow);
        }

        protected virtual void PlayLossWaterOnTarget()
        {
            TrySpawn(fxRegistry?.LossFx, $"{nameof(FxVisualRegistry.LossFx)}_WaterOnTarget", FxEventHook.LossWaterOnTarget);
        }

        private System.Collections.IEnumerator PlayAllRegisteredFxSequence(Vector3 worldPosition, float spacingSeconds)
        {
            currentDiagnosticSourceEvent = "ManualDiagnostic";
            try
            {
                SpawnDiagnosticFx(fxRegistry?.GroupClearFx, nameof(FxVisualRegistry.GroupClearFx), FxEventHook.GroupClear, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.InvalidTapFx, nameof(FxVisualRegistry.InvalidTapFx), FxEventHook.InvalidTap, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.CrateBreakFx, nameof(FxVisualRegistry.CrateBreakFx), FxEventHook.CrateBreak, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.IceRevealFx, nameof(FxVisualRegistry.IceRevealFx), FxEventHook.IceReveal, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.VineClearFx, nameof(FxVisualRegistry.VineClearFx), FxEventHook.VineClear, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.VineGrowPreviewFx, nameof(FxVisualRegistry.VineGrowPreviewFx), FxEventHook.VineGrowthPreview, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.DockInsertFx, nameof(FxVisualRegistry.DockInsertFx), FxEventHook.DockInsert, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.DockTripleClearFx, nameof(FxVisualRegistry.DockTripleClearFx), FxEventHook.DockTripleClear, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.DockInsertFx, $"{nameof(FxVisualRegistry.DockInsertFx)}_WarningFallback", FxEventHook.DockWarning, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.WaterRiseFx, nameof(FxVisualRegistry.WaterRiseFx), FxEventHook.WaterRise, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.NearRescueReliefFx, nameof(FxVisualRegistry.NearRescueReliefFx), FxEventHook.NearRescueRelief, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.TargetExtractionFx, nameof(FxVisualRegistry.TargetExtractionFx), FxEventHook.TargetExtraction, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.WinFx, nameof(FxVisualRegistry.WinFx), FxEventHook.Win, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.LossFx, $"{nameof(FxVisualRegistry.LossFx)}_DockOverflow", FxEventHook.LossDockOverflow, worldPosition);
                yield return new WaitForSeconds(spacingSeconds);
                SpawnDiagnosticFx(fxRegistry?.LossFx, $"{nameof(FxVisualRegistry.LossFx)}_WaterOnTarget", FxEventHook.LossWaterOnTarget, worldPosition);
            }
            finally
            {
                currentDiagnosticSourceEvent = null;
                diagnosticPlaybackCoroutine = null;
            }
        }

        private void SpawnDiagnosticFx(GameObject? prefab, string instanceName, FxEventHook hook, Vector3 worldPosition)
        {
            TrySpawn(prefab, instanceName, hook, worldPosition);
        }

        private Vector3 ResolveGroupWorldPosition(ImmutableArray<TileCoord> coords)
        {
            if (coords.IsDefaultOrEmpty)
            {
                return GetSafeFallbackPosition();
            }

            Vector3 accumulated = Vector3.zero;
            int resolvedCount = 0;
            for (int i = 0; i < coords.Length; i++)
            {
                if (!TryResolveCellWorldPosition(coords[i], out Vector3 cellPosition))
                {
                    continue;
                }

                accumulated += cellPosition;
                resolvedCount++;
            }

            return resolvedCount > 0
                ? accumulated / resolvedCount
                : GetSafeFallbackPosition();
        }

        private Vector3 ResolveCellWorldPosition(TileCoord coord)
        {
            return TryResolveCellWorldPosition(coord, out Vector3 worldPosition)
                ? worldPosition
                : GetSafeFallbackPosition();
        }

        private bool TryResolveCellWorldPosition(TileCoord coord, out Vector3 worldPosition)
        {
            BoardGridViewPresenter? resolvedBoardGrid = ResolveBoardGrid();
            if (resolvedBoardGrid is not null &&
                resolvedBoardGrid.TryGetCellWorldPosition(coord, out worldPosition))
            {
                return true;
            }

            worldPosition = GetSafeFallbackPosition();
            return false;
        }

        private Vector3 ResolveRowWorldPosition(int row)
        {
            BoardGridViewPresenter? resolvedBoardGrid = ResolveBoardGrid();
            if (resolvedBoardGrid is not null &&
                resolvedBoardGrid.TryGetRowWorldBounds(row, out BoardGridViewPresenter.RowWorldBounds bounds))
            {
                return bounds.Center;
            }

            return GetSafeFallbackPosition();
        }

        private Vector3 ResolveDockInsertWorldPosition(DockInserted inserted)
        {
            if (inserted.Pieces.IsDefaultOrEmpty)
            {
                return ResolveDockCenterWorldPosition();
            }

            int firstInsertedSlot = Mathf.Max(0, inserted.OccupancyAfterInsert - inserted.Pieces.Length);
            int lastInsertedSlotExclusive = firstInsertedSlot + inserted.Pieces.Length;

            Vector3 accumulated = Vector3.zero;
            int resolvedCount = 0;
            for (int slotIndex = firstInsertedSlot; slotIndex < lastInsertedSlotExclusive; slotIndex++)
            {
                if (!TryResolveDockSlotWorldPosition(slotIndex, out Vector3 slotPosition))
                {
                    continue;
                }

                accumulated += slotPosition;
                resolvedCount++;
            }

            return resolvedCount > 0
                ? accumulated / resolvedCount
                : ResolveDockCenterWorldPosition();
        }

        private Vector3 ResolveDockClearWorldPosition(DockCleared cleared)
        {
            DockViewPresenter? resolvedDockView = ResolveDockView();
            if (resolvedDockView is not null)
            {
                int piecesToClear = Mathf.Max(0, cleared.SetsCleared * 3);
                Vector3 accumulated = Vector3.zero;
                int resolvedCount = 0;

                for (int slotIndex = 0; slotIndex < DockViewPresenter.Phase1SlotCount && resolvedCount < piecesToClear; slotIndex++)
                {
                    if (resolvedDockView.GetTrackedSlotType(slotIndex) != cleared.Type ||
                        !resolvedDockView.TryGetSlotWorldPosition(slotIndex, out Vector3 slotPosition))
                    {
                        continue;
                    }

                    accumulated += slotPosition;
                    resolvedCount++;
                }

                if (resolvedCount > 0)
                {
                    return accumulated / resolvedCount;
                }
            }

            return ResolveDockCenterWorldPosition();
        }

        private Vector3 ResolveDockCenterWorldPosition()
        {
            DockViewPresenter? resolvedDockView = ResolveDockView();
            if (resolvedDockView is not null &&
                resolvedDockView.TryGetDockCenterWorldPosition(out Vector3 dockCenter))
            {
                return dockCenter;
            }

            return GetSafeFallbackPosition();
        }

        private bool TryResolveDockSlotWorldPosition(int slotIndex, out Vector3 worldPosition)
        {
            DockViewPresenter? resolvedDockView = ResolveDockView();
            if (resolvedDockView is not null &&
                resolvedDockView.TryGetSlotWorldPosition(slotIndex, out worldPosition))
            {
                return true;
            }

            worldPosition = GetSafeFallbackPosition();
            return false;
        }

        private BoardGridViewPresenter? ResolveBoardGrid()
        {
            if (boardGrid is not null)
            {
                return boardGrid;
            }

            boardGrid = GetComponent<BoardGridViewPresenter>();
            return boardGrid;
        }

        private DockViewPresenter? ResolveDockView()
        {
            if (dockView is not null)
            {
                return dockView;
            }

            dockView = GetComponent<DockViewPresenter>();
            return dockView;
        }

        private Vector3 GetSafeFallbackPosition()
        {
            Transform parent = fxRoot != null ? fxRoot : transform;
            return parent.position;
        }

        private GameObject? TrySpawn(GameObject? prefab, string instanceName, FxEventHook hook)
        {
            return TrySpawn(prefab, instanceName, hook, GetSafeFallbackPosition());
        }

        private GameObject? TrySpawn(GameObject? prefab, string instanceName, FxEventHook hook, Vector3 worldPosition)
        {
            LogDiagnostic(hook, instanceName, prefab, worldPosition);
            if (prefab is null)
            {
                return null;
            }

            Transform parent = fxRoot != null ? fxRoot : transform;
            GameObject instance = Instantiate(prefab, parent);
            instance.name = instanceName;
            Quaternion presentationRotation = ResolveFxWorldRotation();
            instance.transform.SetPositionAndRotation(
                ResolveFxWorldPosition(worldPosition) + (presentationRotation * prefab.transform.localPosition),
                presentationRotation * prefab.transform.localRotation);
            ApplyDiagnosticVisibility(instance, hook);
            return instance;
        }

        private Vector3 ResolveFxWorldPosition(Vector3 worldPosition)
        {
            if (!alignSpawnedFxToPresentationPlane || spawnedFxSurfaceOffset <= 0f)
            {
                return worldPosition;
            }

            return worldPosition + (ResolveFxSurfaceNormal() * spawnedFxSurfaceOffset);
        }

        private Quaternion ResolveFxWorldRotation()
        {
            if (!alignSpawnedFxToPresentationPlane)
            {
                Transform parent = fxRoot != null ? fxRoot : transform;
                return parent.rotation;
            }

            BoardGridViewPresenter? resolvedBoardGrid = ResolveBoardGrid();
            if (resolvedBoardGrid is not null)
            {
                return resolvedBoardGrid.transform.rotation * Quaternion.Euler(spawnedFxPlaneEulerOffset);
            }

            DockViewPresenter? resolvedDockView = ResolveDockView();
            if (resolvedDockView is not null)
            {
                return resolvedDockView.transform.rotation * Quaternion.Euler(spawnedFxPlaneEulerOffset);
            }

            Transform fallbackParent = fxRoot != null ? fxRoot : transform;
            return fallbackParent.rotation;
        }

        private Vector3 ResolveFxSurfaceNormal()
        {
            BoardGridViewPresenter? resolvedBoardGrid = ResolveBoardGrid();
            if (resolvedBoardGrid is not null)
            {
                return resolvedBoardGrid.transform.up;
            }

            DockViewPresenter? resolvedDockView = ResolveDockView();
            if (resolvedDockView is not null)
            {
                return resolvedDockView.transform.up;
            }

            Transform fallbackParent = fxRoot != null ? fxRoot : transform;
            return fallbackParent.up;
        }

        private void ApplyDiagnosticVisibility(GameObject instance, FxEventHook hook)
        {
            if (!diagnosticsEnabled || diagnosticMinimumVisibleSeconds <= 0f)
            {
                return;
            }

            SpriteSequenceFxPlayer? player = instance.GetComponent<SpriteSequenceFxPlayer>();
            if (player is null)
            {
                Debug.Log(
                    $"[FX Diagnostics] hook={hook} spawned '{instance.name}' without {nameof(SpriteSequenceFxPlayer)}; no debug slow-down applied.",
                    this);
                return;
            }

            player.EnsureMinimumPlaybackDuration(diagnosticMinimumVisibleSeconds);
            player.StartPlayback();
        }

        private void LogDiagnostic(FxEventHook hook, string instanceName, GameObject? prefab, Vector3 worldPosition)
        {
            if (!diagnosticsEnabled)
            {
                return;
            }

            string assigned = prefab is null ? "no" : "yes";
            string prefabName = prefab is null ? "<missing>" : prefab.name;
            string sourceEvent = currentDiagnosticSourceEvent ?? "<direct>";
            Debug.Log(
                $"[FX Diagnostics] hook={hook} source={sourceEvent} instance={instanceName} prefab={prefabName} assigned={assigned} position={worldPosition}",
                this);
        }
    }
}
