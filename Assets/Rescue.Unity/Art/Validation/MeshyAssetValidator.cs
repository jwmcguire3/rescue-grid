using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rescue.Unity.Art.Validation
{
    public enum MeshyAssetWarningLevel
    {
        None = 0,
        Warning = 1,
        Severe = 2,
    }

    public static class MeshyAssetValidator
    {
        public static MeshyAssetValidationItem ValidateMesh(
            string assetPath,
            Mesh mesh,
            int materialCount,
            int missingMaterialCount,
            MeshyAssetValidationConfig? config = null)
        {
            if (mesh is null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            MeshyAssetValidationConfig normalizedConfig = NormalizeConfig(config);
            Vector3 boundsSize = mesh.bounds.size;
            float longestDimension = Mathf.Max(boundsSize.x, Mathf.Max(boundsSize.y, boundsSize.z));
            int triangleCount = CountTriangles(mesh);
            bool dimensionOutOfRange = longestDimension < normalizedConfig.minExpectedDimension
                || longestDimension > normalizedConfig.maxExpectedDimension;

            return new MeshyAssetValidationItem
            {
                assetPath = assetPath ?? string.Empty,
                meshName = string.IsNullOrWhiteSpace(mesh.name) ? "Unnamed Mesh" : mesh.name,
                vertexCount = mesh.vertexCount,
                triangleCount = triangleCount,
                materialCount = Mathf.Max(0, materialCount),
                missingMaterialCount = Mathf.Max(0, missingMaterialCount),
                boundsSize = boundsSize,
                longestDimension = longestDimension,
                dimensionOutOfRange = dimensionOutOfRange,
                warningLevel = ClassifyWarningLevel(
                    triangleCount,
                    dimensionOutOfRange,
                    missingMaterialCount,
                    normalizedConfig),
            };
        }

        public static MeshyAssetValidationReport CreateReport(
            IReadOnlyList<MeshyAssetValidationItem> items,
            int selectedAssetCount,
            MeshyAssetValidationConfig? config = null,
            string? generatedAtUtc = null)
        {
            MeshyAssetValidationConfig normalizedConfig = NormalizeConfig(config);
            return MeshyAssetValidationReport.Create(
                items,
                normalizedConfig,
                selectedAssetCount,
                generatedAtUtc ?? DateTime.UtcNow.ToString("O"));
        }

        public static string SerializeReport(MeshyAssetValidationReport report, bool prettyPrint = true)
        {
            if (report is null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            return JsonUtility.ToJson(report, prettyPrint);
        }

        public static MeshyAssetWarningLevel ClassifyWarningLevel(
            int triangleCount,
            float longestDimension,
            MeshyAssetValidationConfig? config = null)
        {
            MeshyAssetValidationConfig normalizedConfig = NormalizeConfig(config);
            bool dimensionOutOfRange = longestDimension < normalizedConfig.minExpectedDimension
                || longestDimension > normalizedConfig.maxExpectedDimension;
            return ClassifyWarningLevel(triangleCount, dimensionOutOfRange, 0, normalizedConfig);
        }

        public static MeshyAssetWarningLevel ClassifyWarningLevel(
            int triangleCount,
            bool dimensionOutOfRange,
            int missingMaterialCount,
            MeshyAssetValidationConfig? config = null)
        {
            MeshyAssetValidationConfig normalizedConfig = NormalizeConfig(config);
            if (triangleCount > normalizedConfig.triangleSevereThreshold)
            {
                return MeshyAssetWarningLevel.Severe;
            }

            if (triangleCount > normalizedConfig.triangleWarningThreshold)
            {
                return MeshyAssetWarningLevel.Warning;
            }

            if (dimensionOutOfRange || missingMaterialCount > 0)
            {
                return MeshyAssetWarningLevel.Warning;
            }

            return MeshyAssetWarningLevel.None;
        }

        public static int CountTriangles(Mesh mesh)
        {
            if (mesh is null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            long triangleCount = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                triangleCount += (long)(mesh.GetIndexCount(i) / 3);
            }

            return triangleCount > int.MaxValue ? int.MaxValue : (int)triangleCount;
        }

        private static MeshyAssetValidationConfig NormalizeConfig(MeshyAssetValidationConfig? config)
        {
            return (config ?? new MeshyAssetValidationConfig()).CreateNormalizedCopy();
        }
    }
}
