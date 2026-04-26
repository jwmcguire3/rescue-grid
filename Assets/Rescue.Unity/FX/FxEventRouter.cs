using System.Collections.Immutable;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Presentation;
using UnityEngine;

namespace Rescue.Unity.FX
{
    [DisallowMultipleComponent]
    public class FxEventRouter : MonoBehaviour
    {
        [SerializeField] private FxVisualRegistry? fxRegistry;
        [SerializeField] private Transform? fxRoot;
        [SerializeField] private BoardGridViewPresenter? boardGrid;

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

            switch (playbackStep.SourceEvent)
            {
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
                case DockCleared:
                    PlayDockTripleClear(GetSafeFallbackPosition());
                    break;
                case TargetExtracted extracted:
                    PlayTargetExtraction(ResolveCellWorldPosition(extracted.Coord));
                    break;
                case WaterRose rose:
                    PlayWaterRise(ResolveRowWorldPosition(rose.FloodedRow));
                    break;
            }
        }

        protected virtual void PlayGroupClear()
        {
            PlayGroupClear(GetSafeFallbackPosition());
        }

        protected virtual void PlayGroupClear(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.GroupClearFx, nameof(FxVisualRegistry.GroupClearFx), worldPosition);
        }

        protected virtual void PlayInvalidTap()
        {
            TrySpawn(fxRegistry?.InvalidTapFx, nameof(FxVisualRegistry.InvalidTapFx));
        }

        protected virtual void PlayCrateBreak()
        {
            PlayCrateBreak(GetSafeFallbackPosition());
        }

        protected virtual void PlayCrateBreak(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.CrateBreakFx, nameof(FxVisualRegistry.CrateBreakFx), worldPosition);
        }

        protected virtual void PlayIceReveal()
        {
            PlayIceReveal(GetSafeFallbackPosition());
        }

        protected virtual void PlayIceReveal(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.IceRevealFx, nameof(FxVisualRegistry.IceRevealFx), worldPosition);
        }

        protected virtual void PlayVineClear()
        {
            PlayVineClear(GetSafeFallbackPosition());
        }

        protected virtual void PlayVineClear(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.VineClearFx, nameof(FxVisualRegistry.VineClearFx), worldPosition);
        }

        protected virtual void PlayVineGrowthPreview()
        {
            TrySpawn(fxRegistry?.VineGrowPreviewFx, nameof(FxVisualRegistry.VineGrowPreviewFx));
        }

        protected virtual void PlayDockInsert()
        {
            TrySpawn(fxRegistry?.DockInsertFx, nameof(FxVisualRegistry.DockInsertFx));
        }

        protected virtual void PlayDockTripleClear()
        {
            PlayDockTripleClear(GetSafeFallbackPosition());
        }

        protected virtual void PlayDockTripleClear(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.DockTripleClearFx, nameof(FxVisualRegistry.DockTripleClearFx), worldPosition);
        }

        protected virtual void PlayDockWarning()
        {
            TrySpawn(fxRegistry?.DockInsertFx, $"{nameof(FxVisualRegistry.DockInsertFx)}_WarningFallback");
        }

        protected virtual void PlayWaterRise()
        {
            PlayWaterRise(GetSafeFallbackPosition());
        }

        protected virtual void PlayWaterRise(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.WaterRiseFx, nameof(FxVisualRegistry.WaterRiseFx), worldPosition);
        }

        protected virtual void PlayNearRescueRelief()
        {
            TrySpawn(fxRegistry?.NearRescueReliefFx, nameof(FxVisualRegistry.NearRescueReliefFx));
        }

        protected virtual void PlayTargetExtraction()
        {
            PlayTargetExtraction(GetSafeFallbackPosition());
        }

        protected virtual void PlayTargetExtraction(Vector3 worldPosition)
        {
            TrySpawn(fxRegistry?.TargetExtractionFx, nameof(FxVisualRegistry.TargetExtractionFx), worldPosition);
        }

        protected virtual void PlayWin()
        {
            TrySpawn(fxRegistry?.WinFx, nameof(FxVisualRegistry.WinFx));
        }

        protected virtual void PlayLossDockOverflow()
        {
            TrySpawn(fxRegistry?.LossFx, $"{nameof(FxVisualRegistry.LossFx)}_DockOverflow");
        }

        protected virtual void PlayLossWaterOnTarget()
        {
            TrySpawn(fxRegistry?.LossFx, $"{nameof(FxVisualRegistry.LossFx)}_WaterOnTarget");
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

        private BoardGridViewPresenter? ResolveBoardGrid()
        {
            if (boardGrid is not null)
            {
                return boardGrid;
            }

            boardGrid = GetComponent<BoardGridViewPresenter>();
            return boardGrid;
        }

        private Vector3 GetSafeFallbackPosition()
        {
            Transform parent = fxRoot != null ? fxRoot : transform;
            return parent.position;
        }

        private GameObject? TrySpawn(GameObject? prefab, string instanceName)
        {
            return TrySpawn(prefab, instanceName, GetSafeFallbackPosition());
        }

        private GameObject? TrySpawn(GameObject? prefab, string instanceName, Vector3 worldPosition)
        {
            if (prefab is null)
            {
                return null;
            }

            Transform parent = fxRoot != null ? fxRoot : transform;
            GameObject instance = Instantiate(prefab, parent);
            instance.name = instanceName;
            instance.transform.position = worldPosition;
            return instance;
        }
    }
}
