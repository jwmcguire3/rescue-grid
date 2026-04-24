using UnityEngine;

namespace Rescue.Unity.Art.Registries
{
    [CreateAssetMenu(fileName = "FxVisualRegistry", menuName = "Rescue Grid/Art/Registries/FX Visual Registry")]
    public sealed class FxVisualRegistry : ScriptableObject
    {
        [Header("Optional FX Prefabs")]
        [SerializeField] private GameObject? groupClearFx;
        [SerializeField] private GameObject? invalidTapFx;
        [SerializeField] private GameObject? crateBreakFx;
        [SerializeField] private GameObject? iceRevealFx;
        [SerializeField] private GameObject? vineClearFx;
        [SerializeField] private GameObject? vineGrowPreviewFx;
        [SerializeField] private GameObject? dockInsertFx;
        [SerializeField] private GameObject? dockTripleClearFx;
        [SerializeField] private GameObject? waterRiseFx;
        [SerializeField] private GameObject? targetExtractionFx;
        [SerializeField] private GameObject? nearRescueReliefFx;
        [SerializeField] private GameObject? winFx;
        [SerializeField] private GameObject? lossFx;

        public GameObject? GroupClearFx
        {
            get => groupClearFx;
            set => groupClearFx = value;
        }

        public GameObject? InvalidTapFx
        {
            get => invalidTapFx;
            set => invalidTapFx = value;
        }

        public GameObject? CrateBreakFx
        {
            get => crateBreakFx;
            set => crateBreakFx = value;
        }

        public GameObject? IceRevealFx
        {
            get => iceRevealFx;
            set => iceRevealFx = value;
        }

        public GameObject? VineClearFx
        {
            get => vineClearFx;
            set => vineClearFx = value;
        }

        public GameObject? VineGrowPreviewFx
        {
            get => vineGrowPreviewFx;
            set => vineGrowPreviewFx = value;
        }

        public GameObject? DockInsertFx
        {
            get => dockInsertFx;
            set => dockInsertFx = value;
        }

        public GameObject? DockTripleClearFx
        {
            get => dockTripleClearFx;
            set => dockTripleClearFx = value;
        }

        public GameObject? WaterRiseFx
        {
            get => waterRiseFx;
            set => waterRiseFx = value;
        }

        public GameObject? TargetExtractionFx
        {
            get => targetExtractionFx;
            set => targetExtractionFx = value;
        }

        public GameObject? NearRescueReliefFx
        {
            get => nearRescueReliefFx;
            set => nearRescueReliefFx = value;
        }

        public GameObject? WinFx
        {
            get => winFx;
            set => winFx = value;
        }

        public GameObject? LossFx
        {
            get => lossFx;
            set => lossFx = value;
        }
    }
}
