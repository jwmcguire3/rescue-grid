using System.Collections.Immutable;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using UnityEngine;

namespace Rescue.Unity.FX
{
    [DisallowMultipleComponent]
    public class FxEventRouter : MonoBehaviour
    {
        [SerializeField] private FxVisualRegistry? fxRegistry;
        [SerializeField] private Transform? fxRoot;

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

        protected virtual void PlayGroupClear()
        {
            TrySpawn(fxRegistry?.GroupClearFx, nameof(FxVisualRegistry.GroupClearFx));
        }

        protected virtual void PlayInvalidTap()
        {
            TrySpawn(fxRegistry?.InvalidTapFx, nameof(FxVisualRegistry.InvalidTapFx));
        }

        protected virtual void PlayCrateBreak()
        {
            TrySpawn(fxRegistry?.CrateBreakFx, nameof(FxVisualRegistry.CrateBreakFx));
        }

        protected virtual void PlayIceReveal()
        {
            TrySpawn(fxRegistry?.IceRevealFx, nameof(FxVisualRegistry.IceRevealFx));
        }

        protected virtual void PlayVineClear()
        {
            TrySpawn(fxRegistry?.VineClearFx, nameof(FxVisualRegistry.VineClearFx));
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
            TrySpawn(fxRegistry?.DockTripleClearFx, nameof(FxVisualRegistry.DockTripleClearFx));
        }

        protected virtual void PlayDockWarning()
        {
            TrySpawn(fxRegistry?.DockInsertFx, $"{nameof(FxVisualRegistry.DockInsertFx)}_WarningFallback");
        }

        protected virtual void PlayWaterRise()
        {
            TrySpawn(fxRegistry?.WaterRiseFx, nameof(FxVisualRegistry.WaterRiseFx));
        }

        protected virtual void PlayNearRescueRelief()
        {
            TrySpawn(fxRegistry?.NearRescueReliefFx, nameof(FxVisualRegistry.NearRescueReliefFx));
        }

        protected virtual void PlayTargetExtraction()
        {
            TrySpawn(fxRegistry?.TargetExtractionFx, nameof(FxVisualRegistry.TargetExtractionFx));
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

        private GameObject? TrySpawn(GameObject? prefab, string instanceName)
        {
            if (prefab is null)
            {
                return null;
            }

            Transform parent = fxRoot != null ? fxRoot : transform;
            GameObject instance = Instantiate(prefab, parent);
            instance.name = instanceName;
            return instance;
        }
    }
}
