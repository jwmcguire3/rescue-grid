using UnityEngine;

namespace Rescue.Unity.Art.Registries
{
    [CreateAssetMenu(fileName = "TileVisualRegistry", menuName = "Rescue Grid/Art/Registries/Tile Visual Registry")]
    public sealed class TileVisualRegistry : ScriptableObject
    {
        [Header("Board Tile Prefabs")]
        [SerializeField] private GameObject? dryTilePrefab;
        [SerializeField] private GameObject? floodedRowOverlayPrefab;
        [SerializeField] private GameObject? forecastRowOverlayPrefab;
        [SerializeField] private GameObject? waterlinePrefab;
        [SerializeField] private GameObject? fallbackTilePrefab;

        public GameObject? DryTilePrefab
        {
            get => dryTilePrefab;
            set => dryTilePrefab = value;
        }

        public GameObject? FloodedRowOverlayPrefab
        {
            get => floodedRowOverlayPrefab;
            set => floodedRowOverlayPrefab = value;
        }

        public GameObject? ForecastRowOverlayPrefab
        {
            get => forecastRowOverlayPrefab;
            set => forecastRowOverlayPrefab = value;
        }

        public GameObject? WaterlinePrefab
        {
            get => waterlinePrefab;
            set => waterlinePrefab = value;
        }

        public GameObject? FallbackTilePrefab
        {
            get => fallbackTilePrefab;
            set => fallbackTilePrefab = value;
        }

        public GameObject? GetDryTilePrefab()
        {
            return RegistryWarnings.ResolvePrefab(this, "dry tile", dryTilePrefab, fallbackTilePrefab);
        }

        public GameObject? GetFloodedRowOverlayPrefab()
        {
            return RegistryWarnings.ResolvePrefab(this, "flooded row overlay", floodedRowOverlayPrefab, fallbackTilePrefab);
        }

        public GameObject? GetForecastRowOverlayPrefab()
        {
            return RegistryWarnings.ResolvePrefab(this, "forecast row overlay", forecastRowOverlayPrefab, fallbackTilePrefab);
        }

        public GameObject? GetWaterlinePrefab()
        {
            return RegistryWarnings.ResolvePrefab(this, "waterline", waterlinePrefab, fallbackTilePrefab);
        }
    }
}
