using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.Visuals
{
    [CreateAssetMenu(fileName = "PieceVisualRegistry", menuName = "Rescue Grid/Visuals/Piece Visual Registry")]
    public sealed class PieceVisualRegistry : ScriptableObject
    {
        [Header("Debris Prefabs")]
        [SerializeField] private VisualPrefabConfig debrisA = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Cube };
        [SerializeField] private VisualPrefabConfig debrisB = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Cube };
        [SerializeField] private VisualPrefabConfig debrisC = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Cube };
        [SerializeField] private VisualPrefabConfig debrisD = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Cube };
        [SerializeField] private VisualPrefabConfig debrisE = new VisualPrefabConfig { PlaceholderPrimitive = VisualPrimitiveFallback.Cube };

        public VisualPrefabConfig DebrisA => debrisA;
        public VisualPrefabConfig DebrisB => debrisB;
        public VisualPrefabConfig DebrisC => debrisC;
        public VisualPrefabConfig DebrisD => debrisD;
        public VisualPrefabConfig DebrisE => debrisE;

        public VisualPrefabConfig GetConfig(DebrisType debrisType)
        {
            return debrisType switch
            {
                DebrisType.A => debrisA,
                DebrisType.B => debrisB,
                DebrisType.C => debrisC,
                DebrisType.D => debrisD,
                DebrisType.E => debrisE,
                _ => debrisA,
            };
        }

        public GameObject? ResolvePrefab(DebrisType debrisType)
        {
            return VisualRegistryUtility.ResolvePrefab(GetConfig(debrisType), this, $"Debris {debrisType}");
        }

        public Material? ResolveMaterial(DebrisType debrisType)
        {
            return VisualRegistryUtility.ResolveMaterial(GetConfig(debrisType), this, $"Debris {debrisType}");
        }
    }
}
