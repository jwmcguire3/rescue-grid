using UnityEngine;

namespace Rescue.Unity.Visuals
{
    [CreateAssetMenu(fileName = "TileVisualRegistry", menuName = "Rescue Grid/Visuals/Tile Visual Registry")]
    public sealed class TileVisualRegistry : ScriptableObject
    {
        [Header("Board Surface")]
        [SerializeField] private VisualPrefabConfig dryTile = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Quad };
        [SerializeField] private VisualPrefabConfig forecastTile = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Quad };
        [SerializeField] private VisualPrefabConfig floodedTile = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Quad };

        public VisualPrefabConfig DryTile => dryTile;
        public VisualPrefabConfig ForecastTile => forecastTile;
        public VisualPrefabConfig FloodedTile => floodedTile;

        public GameObject? ResolveDryTilePrefab()
        {
            return VisualRegistryUtility.ResolvePrefab(dryTile, this, "Dry Tile");
        }

        public GameObject? ResolveForecastTilePrefab()
        {
            return VisualRegistryUtility.ResolvePrefab(forecastTile, this, "Forecast Tile");
        }

        public GameObject? ResolveFloodedTilePrefab()
        {
            return VisualRegistryUtility.ResolvePrefab(floodedTile, this, "Flooded Tile");
        }

        public Material? ResolveDryTileMaterial()
        {
            return VisualRegistryUtility.ResolveMaterial(dryTile, this, "Dry Tile");
        }

        public Material? ResolveForecastTileMaterial()
        {
            return VisualRegistryUtility.ResolveMaterial(forecastTile, this, "Forecast Tile");
        }

        public Material? ResolveFloodedTileMaterial()
        {
            return VisualRegistryUtility.ResolveMaterial(floodedTile, this, "Flooded Tile");
        }
    }
}
