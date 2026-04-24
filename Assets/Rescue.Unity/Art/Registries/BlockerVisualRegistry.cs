using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.Art.Registries
{
    [CreateAssetMenu(fileName = "BlockerVisualRegistry", menuName = "Rescue Grid/Art/Registries/Blocker Visual Registry")]
    public sealed class BlockerVisualRegistry : ScriptableObject
    {
        [Header("Blocker Prefabs")]
        [SerializeField] private GameObject? cratePrefab;
        [SerializeField] private GameObject? icePrefab;
        [SerializeField] private GameObject? vinePrefab;
        [SerializeField] private GameObject? fallbackBlockerPrefab;

        public GameObject? CratePrefab
        {
            get => cratePrefab;
            set => cratePrefab = value;
        }

        public GameObject? IcePrefab
        {
            get => icePrefab;
            set => icePrefab = value;
        }

        public GameObject? VinePrefab
        {
            get => vinePrefab;
            set => vinePrefab = value;
        }

        public GameObject? FallbackBlockerPrefab
        {
            get => fallbackBlockerPrefab;
            set => fallbackBlockerPrefab = value;
        }

        public GameObject? GetPrefab(BlockerType type)
        {
            GameObject? assignedPrefab = type switch
            {
                BlockerType.Crate => cratePrefab,
                BlockerType.Ice => icePrefab,
                BlockerType.Vine => vinePrefab,
                _ => null,
            };

            return RegistryWarnings.ResolvePrefab(this, $"BlockerType.{type}", assignedPrefab, fallbackBlockerPrefab);
        }
    }
}
