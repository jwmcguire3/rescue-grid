#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Rescue.Unity.Art.Validation;
using UnityEditor;
using UnityEngine;

namespace Rescue.Unity.Editor.Art.Validation
{
    internal static class MeshyAssetValidationMenu
    {
        private const string MenuItemPath = "Rescue Grid/Art/Validate Selected Meshy Assets";
        private const string SettingsSearchFilter = "t:MeshyAssetValidationSettings";
        private const string ReportsFolderPath = "Assets/Rescue.Unity/Art/Reports";

        [MenuItem(MenuItemPath)]
        private static void ValidateSelectedMeshyAssets()
        {
            List<string> selectedAssetPaths = GetSelectedAssetPaths();
            MeshyAssetValidationConfig config = LoadConfig();
            List<MeshyAssetValidationItem> items = CollectValidationItems(selectedAssetPaths, config);
            MeshyAssetValidationReport report = MeshyAssetValidator.CreateReport(items, selectedAssetPaths.Count, config);

            LogReport(report);

            if (config.writeJsonReport)
            {
                WriteJsonReport(report);
            }
        }

        [MenuItem(MenuItemPath, true)]
        private static bool CanValidateSelectedMeshyAssets()
        {
            return Selection.objects is { Length: > 0 };
        }

        private static List<string> GetSelectedAssetPaths()
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (UnityEngine.Object selectedObject in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selectedObject);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    AddAssetPathOrFolderContents(path, paths);
                }
            }

            return new List<string>(paths);
        }

        private static void AddAssetPathOrFolderContents(string path, HashSet<string> paths)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                AddFolderContents(path, "t:GameObject", paths);
                AddFolderContents(path, "t:Mesh", paths);
                return;
            }

            paths.Add(path);
        }

        private static void AddFolderContents(string folderPath, string filter, HashSet<string> paths)
        {
            string[] guids = AssetDatabase.FindAssets(filter, new[] { folderPath });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrWhiteSpace(assetPath) && !AssetDatabase.IsValidFolder(assetPath))
                {
                    paths.Add(assetPath);
                }
            }
        }

        private static MeshyAssetValidationConfig LoadConfig()
        {
            string[] guids = AssetDatabase.FindAssets(SettingsSearchFilter);
            if (guids.Length == 0)
            {
                return new MeshyAssetValidationConfig().CreateNormalizedCopy();
            }

            string settingsPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            MeshyAssetValidationSettings? settings = AssetDatabase.LoadAssetAtPath<MeshyAssetValidationSettings>(settingsPath);
            if (settings is null)
            {
                Debug.LogWarning($"Meshy validation settings asset at '{settingsPath}' could not be loaded. Using defaults.");
                return new MeshyAssetValidationConfig().CreateNormalizedCopy();
            }

            if (guids.Length > 1)
            {
                Debug.LogWarning($"Multiple Meshy validation settings assets were found. Using '{settingsPath}'.");
            }

            return settings.CreateConfig();
        }

        private static List<MeshyAssetValidationItem> CollectValidationItems(
            IReadOnlyList<string> assetPaths,
            MeshyAssetValidationConfig config)
        {
            List<MeshyAssetValidationItem> items = new List<MeshyAssetValidationItem>();
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string assetPath = assetPaths[i];
                GameObject? prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefabAsset is not null)
                {
                    int itemCountBefore = items.Count;
                    CollectPrefabItems(prefabAsset, assetPath, config, items);
                    if (items.Count > itemCountBefore)
                    {
                        continue;
                    }
                }

                Mesh? meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (meshAsset is not null)
                {
                    items.Add(MeshyAssetValidator.ValidateMesh(assetPath, meshAsset, 0, 0, config));
                }
            }

            return items;
        }

        private static void CollectPrefabItems(
            GameObject prefabAsset,
            string assetPath,
            MeshyAssetValidationConfig config,
            ICollection<MeshyAssetValidationItem> items)
        {
            MeshFilter[] meshFilters = prefabAsset.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                Mesh? sharedMesh = meshFilters[i].sharedMesh;
                if (sharedMesh is null)
                {
                    continue;
                }

                MeshRenderer? renderer = meshFilters[i].GetComponent<MeshRenderer>();
                GetMaterialStats(renderer, out int materialCount, out int missingMaterialCount);
                items.Add(MeshyAssetValidator.ValidateMesh(assetPath, sharedMesh, materialCount, missingMaterialCount, config));
            }

            SkinnedMeshRenderer[] skinnedRenderers = prefabAsset.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                Mesh? sharedMesh = skinnedRenderers[i].sharedMesh;
                if (sharedMesh is null)
                {
                    continue;
                }

                GetMaterialStats(skinnedRenderers[i], out int materialCount, out int missingMaterialCount);
                items.Add(MeshyAssetValidator.ValidateMesh(assetPath, sharedMesh, materialCount, missingMaterialCount, config));
            }
        }

        private static void GetMaterialStats(Renderer? renderer, out int materialCount, out int missingMaterialCount)
        {
            if (renderer is null)
            {
                materialCount = 0;
                missingMaterialCount = 0;
                return;
            }

            Material[] sharedMaterials = renderer.sharedMaterials;
            materialCount = sharedMaterials.Length;
            missingMaterialCount = 0;
            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                if (sharedMaterials[i] is null)
                {
                    missingMaterialCount++;
                }
            }
        }

        private static void LogReport(MeshyAssetValidationReport report)
        {
            string summary = report.CreateConsoleSummary();
            if (report.severeCount > 0 || report.warningCount > 0)
            {
                Debug.LogWarning(summary);
                return;
            }

            Debug.Log(summary);
        }

        private static void WriteJsonReport(MeshyAssetValidationReport report)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            string reportsDirectory = Path.Combine(projectRoot, "Assets", "Rescue.Unity", "Art", "Reports");
            Directory.CreateDirectory(reportsDirectory);

            string fileName = $"meshy-validation-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            string absolutePath = Path.Combine(reportsDirectory, fileName);
            File.WriteAllText(absolutePath, MeshyAssetValidator.SerializeReport(report));
            AssetDatabase.Refresh();

            string assetRelativePath = $"{ReportsFolderPath}/{fileName}";
            Debug.Log($"Meshy validation JSON report written to {assetRelativePath}");
        }
    }
}
#endif
