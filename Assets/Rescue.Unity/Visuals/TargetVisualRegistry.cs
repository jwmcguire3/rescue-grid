using UnityEngine;

namespace Rescue.Unity.Visuals
{
    [CreateAssetMenu(fileName = "TargetVisualRegistry", menuName = "Rescue Grid/Visuals/Target Visual Registry")]
    public sealed class TargetVisualRegistry : ScriptableObject
    {
        [Header("Target Tile / State")]
        [SerializeField] private VisualPrefabConfig targetTile = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Sphere };
        [SerializeField] private VisualPrefabConfig oneClearAwayTarget = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Sphere };
        [SerializeField] private VisualPrefabConfig extractedTarget = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.None };

        public VisualPrefabConfig TargetTile => targetTile;
        public VisualPrefabConfig OneClearAwayTarget => oneClearAwayTarget;
        public VisualPrefabConfig ExtractedTarget => extractedTarget;

        public GameObject? ResolveTargetTilePrefab()
        {
            return VisualRegistryUtility.ResolvePrefab(targetTile, this, "Target Tile");
        }

        public GameObject? ResolveOneClearAwayPrefab()
        {
            return VisualRegistryUtility.ResolvePrefab(oneClearAwayTarget, this, "Target One-Clear-Away");
        }

        public GameObject? ResolveExtractedPrefab()
        {
            return VisualRegistryUtility.ResolvePrefab(extractedTarget, this, "Target Extracted");
        }

        public Material? ResolveTargetTileMaterial()
        {
            return VisualRegistryUtility.ResolveMaterial(targetTile, this, "Target Tile");
        }

        public Material? ResolveOneClearAwayMaterial()
        {
            return VisualRegistryUtility.ResolveMaterial(oneClearAwayTarget, this, "Target One-Clear-Away");
        }

        public Material? ResolveExtractedMaterial()
        {
            return VisualRegistryUtility.ResolveMaterial(extractedTarget, this, "Target Extracted");
        }
    }
}
