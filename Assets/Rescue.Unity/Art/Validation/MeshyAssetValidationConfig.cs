using System;
using UnityEngine;

namespace Rescue.Unity.Art.Validation
{
    [Serializable]
    public sealed class MeshyAssetValidationConfig
    {
        public int triangleWarningThreshold = 20000;
        public int triangleSevereThreshold = 75000;
        public float minExpectedDimension = 0.5f;
        public float maxExpectedDimension = 2.0f;
        public bool writeJsonReport;

        public MeshyAssetValidationConfig()
        {
        }

        public MeshyAssetValidationConfig(MeshyAssetValidationConfig source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            triangleWarningThreshold = source.triangleWarningThreshold;
            triangleSevereThreshold = source.triangleSevereThreshold;
            minExpectedDimension = source.minExpectedDimension;
            maxExpectedDimension = source.maxExpectedDimension;
            writeJsonReport = source.writeJsonReport;
        }

        public MeshyAssetValidationConfig CreateNormalizedCopy()
        {
            MeshyAssetValidationConfig normalized = new MeshyAssetValidationConfig(this);
            normalized.triangleWarningThreshold = Mathf.Max(0, normalized.triangleWarningThreshold);
            normalized.triangleSevereThreshold = Mathf.Max(normalized.triangleWarningThreshold, normalized.triangleSevereThreshold);
            normalized.minExpectedDimension = Mathf.Max(0.0f, normalized.minExpectedDimension);
            normalized.maxExpectedDimension = Mathf.Max(normalized.minExpectedDimension, normalized.maxExpectedDimension);
            return normalized;
        }
    }
}
