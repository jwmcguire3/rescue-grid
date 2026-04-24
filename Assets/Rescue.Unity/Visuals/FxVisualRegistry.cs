using UnityEngine;

namespace Rescue.Unity.Visuals
{
    [CreateAssetMenu(fileName = "FxVisualRegistry", menuName = "Rescue Grid/Visuals/FX Visual Registry")]
    public sealed class FxVisualRegistry : ScriptableObject
    {
        [Header("Gameplay FX Prefabs")]
        [SerializeField] private GameObject? debrisClearFxPrefab;
        [SerializeField] private GameObject? blockerBreakFxPrefab;
        [SerializeField] private GameObject? extractionFxPrefab;
        [SerializeField] private GameObject? waterRiseFxPrefab;
        [SerializeField] private GameObject? waterForecastFxPrefab;
        [SerializeField] private GameObject? invalidTapFxPrefab;

        public GameObject? DebrisClearFxPrefab => ResolvePrefab(debrisClearFxPrefab, "Debris Clear FX");
        public GameObject? BlockerBreakFxPrefab => ResolvePrefab(blockerBreakFxPrefab, "Blocker Break FX");
        public GameObject? ExtractionFxPrefab => ResolvePrefab(extractionFxPrefab, "Extraction FX");
        public GameObject? WaterRiseFxPrefab => ResolvePrefab(waterRiseFxPrefab, "Water Rise FX");
        public GameObject? WaterForecastFxPrefab => ResolvePrefab(waterForecastFxPrefab, "Water Forecast FX");
        public GameObject? InvalidTapFxPrefab => ResolvePrefab(invalidTapFxPrefab, "Invalid Tap FX");

        private GameObject? ResolvePrefab(GameObject? prefab, string label)
        {
            if (prefab is not null)
            {
                return prefab;
            }

            VisualRegistryUtility.WarnMissing(this, label, "prefab");
            return null;
        }
    }
}
