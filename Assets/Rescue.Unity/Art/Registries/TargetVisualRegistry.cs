using UnityEngine;

namespace Rescue.Unity.Art.Registries
{
    [CreateAssetMenu(fileName = "TargetVisualRegistry", menuName = "Rescue Grid/Art/Registries/Target Visual Registry")]
    public sealed class TargetVisualRegistry : ScriptableObject
    {
        [Header("Phase 1 Target Prefabs")]
        [SerializeField] private GameObject? puppyPrefab;
        [SerializeField] private GameObject? fallbackTargetPrefab;

        public GameObject? PuppyPrefab
        {
            get => puppyPrefab;
            set => puppyPrefab = value;
        }

        public GameObject? FallbackTargetPrefab
        {
            get => fallbackTargetPrefab;
            set => fallbackTargetPrefab = value;
        }

        public GameObject? GetTargetPrefab(string targetId)
        {
            if (!string.IsNullOrWhiteSpace(targetId) && puppyPrefab is not null)
            {
                return puppyPrefab;
            }

            if (string.IsNullOrWhiteSpace(targetId))
            {
                RegistryWarnings.WarnMissing(this, "target id", "value");
            }
            else
            {
                RegistryWarnings.WarnMissing(this, $"target '{targetId}'", "prefab");
            }

            if (fallbackTargetPrefab is not null)
            {
                return fallbackTargetPrefab;
            }

            RegistryWarnings.WarnMissing(this, "target fallback", "prefab");
            return null;
        }
    }
}
