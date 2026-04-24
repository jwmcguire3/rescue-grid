using System;
using UnityEngine;

namespace Rescue.Unity.Art.Validation
{
    [Serializable]
    public sealed class MeshyAssetValidationItem
    {
        public string assetPath = string.Empty;
        public string meshName = string.Empty;
        public int vertexCount;
        public int triangleCount;
        public int materialCount;
        public int missingMaterialCount;
        public Vector3 boundsSize;
        public float longestDimension;
        public bool dimensionOutOfRange;
        public MeshyAssetWarningLevel warningLevel;
    }
}
