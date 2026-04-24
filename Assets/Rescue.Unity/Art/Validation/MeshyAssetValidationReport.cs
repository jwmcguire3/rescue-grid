using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rescue.Unity.Art.Validation
{
    [Serializable]
    public sealed class MeshyAssetValidationReport
    {
        public string generatedAtUtc = string.Empty;
        public MeshyAssetValidationConfig config = new MeshyAssetValidationConfig();
        public MeshyAssetValidationItem[] items = Array.Empty<MeshyAssetValidationItem>();
        public int selectedAssetCount;
        public int meshCount;
        public int warningCount;
        public int severeCount;

        public string CreateConsoleSummary()
        {
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("Meshy asset validation complete.");
            summary.AppendLine($"Selected assets: {selectedAssetCount}");
            summary.AppendLine($"Meshes inspected: {meshCount}");
            summary.AppendLine($"Warnings: {warningCount}");
            summary.AppendLine($"Severe: {severeCount}");

            if (items.Length == 0)
            {
                summary.Append("No meshes were found in the current selection.");
                return summary.ToString();
            }

            for (int i = 0; i < items.Length; i++)
            {
                MeshyAssetValidationItem item = items[i];
                summary.Append("- ");
                summary.Append(item.assetPath);
                summary.Append(" | ");
                summary.Append(item.meshName);
                summary.Append(" | tris=");
                summary.Append(item.triangleCount);
                summary.Append(" verts=");
                summary.Append(item.vertexCount);
                summary.Append(" mats=");
                summary.Append(item.materialCount);
                summary.Append(" missingMats=");
                summary.Append(item.missingMaterialCount);
                summary.Append(" longest=");
                summary.Append(item.longestDimension.ToString("0.###"));
                summary.Append(" bounds=");
                summary.Append(item.boundsSize);
                summary.Append(" level=");
                summary.Append(item.warningLevel);
                if (i < items.Length - 1)
                {
                    summary.AppendLine();
                }
            }

            return summary.ToString();
        }

        public static MeshyAssetValidationReport Create(
            IReadOnlyList<MeshyAssetValidationItem> items,
            MeshyAssetValidationConfig config,
            int selectedAssetCount,
            string generatedAtUtc)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            MeshyAssetValidationItem[] copiedItems = new MeshyAssetValidationItem[items.Count];
            int warningCount = 0;
            int severeCount = 0;

            for (int i = 0; i < items.Count; i++)
            {
                copiedItems[i] = items[i];
                if (items[i].warningLevel >= MeshyAssetWarningLevel.Warning)
                {
                    warningCount++;
                }

                if (items[i].warningLevel >= MeshyAssetWarningLevel.Severe)
                {
                    severeCount++;
                }
            }

            return new MeshyAssetValidationReport
            {
                generatedAtUtc = generatedAtUtc,
                config = new MeshyAssetValidationConfig(config),
                items = copiedItems,
                selectedAssetCount = selectedAssetCount,
                meshCount = copiedItems.Length,
                warningCount = warningCount,
                severeCount = severeCount,
            };
        }
    }
}
