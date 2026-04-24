using UnityEngine;

namespace Rescue.Unity.Art.Validation
{
    [CreateAssetMenu(fileName = "MeshyAssetValidationSettings", menuName = "Rescue Grid/Art/Meshy Asset Validation Settings")]
    public sealed class MeshyAssetValidationSettings : ScriptableObject
    {
        [SerializeField] private MeshyAssetValidationConfig config = new MeshyAssetValidationConfig();

        public MeshyAssetValidationConfig CreateConfig()
        {
            return new MeshyAssetValidationConfig(config).CreateNormalizedCopy();
        }
    }
}
