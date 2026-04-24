using UnityEngine;

namespace Rescue.Unity.Visuals
{
    [CreateAssetMenu(fileName = "DockVisualConfig", menuName = "Rescue Grid/Visuals/Dock Visual Config")]
    public sealed class DockVisualConfig : ScriptableObject
    {
        [Header("Dock Structure")]
        [SerializeField] private VisualPrefabConfig dockRoot = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Cube };
        [SerializeField] private VisualPrefabConfig dockSlot = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Cube };
        [SerializeField] private VisualPrefabConfig dockPiece = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Cube };

        [Header("Dock Materials")]
        [SerializeField] private Material? defaultMaterial;
        [SerializeField] private Material? warningAmberMaterial;
        [SerializeField] private Material? warningRedMaterial;
        [SerializeField] private Material? dockJamMaterial;

        public VisualPrefabConfig DockRoot => dockRoot;
        public VisualPrefabConfig DockSlot => dockSlot;
        public VisualPrefabConfig DockPiece => dockPiece;

        public Material? DefaultMaterial => ResolveMaterial(defaultMaterial, "Dock Default Material");
        public Material? WarningAmberMaterial => ResolveMaterial(warningAmberMaterial, "Dock Warning Amber Material");
        public Material? WarningRedMaterial => ResolveMaterial(warningRedMaterial, "Dock Warning Red Material");
        public Material? DockJamMaterial => ResolveMaterial(dockJamMaterial, "Dock Jam Material");

        public GameObject? ResolveDockRootPrefab()
        {
            return VisualRegistryUtility.ResolvePrefab(dockRoot, this, "Dock Root");
        }

        public GameObject? ResolveDockSlotPrefab()
        {
            return VisualRegistryUtility.ResolvePrefab(dockSlot, this, "Dock Slot");
        }

        public GameObject? ResolveDockPiecePrefab()
        {
            return VisualRegistryUtility.ResolvePrefab(dockPiece, this, "Dock Piece");
        }

        private Material? ResolveMaterial(Material? material, string label)
        {
            if (material is not null)
            {
                return material;
            }

            VisualRegistryUtility.WarnMissing(this, label, "material");
            return null;
        }
    }
}
