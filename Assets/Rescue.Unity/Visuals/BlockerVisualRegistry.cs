using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.Visuals
{
    [CreateAssetMenu(fileName = "BlockerVisualRegistry", menuName = "Rescue Grid/Visuals/Blocker Visual Registry")]
    public sealed class BlockerVisualRegistry : ScriptableObject
    {
        [Header("Blocker Prefabs")]
        [SerializeField] private VisualPrefabConfig crate = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Cube };
        [SerializeField] private VisualPrefabConfig ice = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Cube };
        [SerializeField] private VisualPrefabConfig vine = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Cylinder };

        public VisualPrefabConfig Crate => crate;
        public VisualPrefabConfig Ice => ice;
        public VisualPrefabConfig Vine => vine;

        public VisualPrefabConfig GetConfig(BlockerType blockerType)
        {
            return blockerType switch
            {
                BlockerType.Crate => crate,
                BlockerType.Ice => ice,
                BlockerType.Vine => vine,
                _ => crate,
            };
        }

        public GameObject? ResolvePrefab(BlockerType blockerType)
        {
            return VisualRegistryUtility.ResolvePrefab(GetConfig(blockerType), this, $"Blocker {blockerType}");
        }

        public Material? ResolveMaterial(BlockerType blockerType)
        {
            return VisualRegistryUtility.ResolveMaterial(GetConfig(blockerType), this, $"Blocker {blockerType}");
        }
    }
}
