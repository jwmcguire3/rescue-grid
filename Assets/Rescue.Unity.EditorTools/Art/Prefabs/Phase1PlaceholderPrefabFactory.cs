using System;
using Rescue.Unity.Art.Registries;
using UnityEditor;
using UnityEngine;

namespace Rescue.Unity.EditorTools.Art.Prefabs
{
    public static class Phase1PlaceholderPrefabFactory
    {
        public const string DefaultArtRootPath = "Assets/Rescue.Unity/Art";

        private const string PrefabsFolderName = "Prefabs";
        private const string MaterialsFolderName = "Materials";
        private const string RegistriesFolderName = "Registries";
        private const string BoardFolderName = "Board";
        private const string PiecesFolderName = "Pieces";
        private const string BlockersFolderName = "Blockers";
        private const string TargetsFolderName = "Targets";
        private const string DockFolderName = "Dock";
        private const string WaterFolderName = "Water";
        private const string FxFolderName = "FX";

        private static readonly string[] RequiredFolderPaths =
        {
            $"{PrefabsFolderName}/{BoardFolderName}",
            $"{PrefabsFolderName}/{PiecesFolderName}",
            $"{PrefabsFolderName}/{BlockersFolderName}",
            $"{PrefabsFolderName}/{TargetsFolderName}",
            $"{PrefabsFolderName}/{DockFolderName}",
            $"{PrefabsFolderName}/{WaterFolderName}",
            $"{PrefabsFolderName}/{FxFolderName}",
            MaterialsFolderName,
            RegistriesFolderName,
        };

        [MenuItem("Rescue Grid/Art/Create Phase 1 Placeholder Prefabs")]
        public static void CreateDefaultPhase1Placeholders()
        {
            CreateAll(DefaultArtRootPath);
            Debug.Log($"Phase 1 placeholder prefabs created under '{DefaultArtRootPath}'.");
        }

        public static void CreateAll(string artRootPath)
        {
            if (string.IsNullOrWhiteSpace(artRootPath))
            {
                throw new ArgumentException("Art root path must not be null or empty.", nameof(artRootPath));
            }

            EnsureRequiredFolders(artRootPath);

            Shader shader = ResolveShader();

            string materialsPath = CombinePath(artRootPath, MaterialsFolderName);
            Material tileDryMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Tile_Dry.mat"), shader, new Color(0.74f, 0.66f, 0.50f));
            Material debrisAMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Debris_A.mat"), shader, new Color(0.87f, 0.47f, 0.37f));
            Material debrisBMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Debris_B.mat"), shader, new Color(0.89f, 0.72f, 0.31f));
            Material debrisCMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Debris_C.mat"), shader, new Color(0.36f, 0.72f, 0.49f));
            Material debrisDMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Debris_D.mat"), shader, new Color(0.33f, 0.61f, 0.86f));
            Material debrisEMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Debris_E.mat"), shader, new Color(0.69f, 0.46f, 0.83f));
            Material crateMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Crate.mat"), shader, new Color(0.53f, 0.35f, 0.18f));
            Material iceMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Ice.mat"), shader, new Color(0.67f, 0.90f, 1.00f), transparent: true);
            Material vineMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Vine.mat"), shader, new Color(0.23f, 0.57f, 0.20f));
            Material puppyTargetMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "PuppyTarget.mat"), shader, new Color(0.96f, 0.84f, 0.49f));
            Material dockSafeMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Dock_Safe.mat"), shader, new Color(0.40f, 0.74f, 0.49f));
            Material dockCautionMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Dock_Caution.mat"), shader, new Color(0.95f, 0.75f, 0.29f));
            Material dockAcuteMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Dock_Acute.mat"), shader, new Color(0.93f, 0.42f, 0.28f));
            Material dockFailedMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Dock_Failed.mat"), shader, new Color(0.44f, 0.15f, 0.15f));
            Material waterFloodedMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Water_Flooded.mat"), shader, new Color(0.12f, 0.43f, 0.79f, 0.78f), transparent: true);
            Material waterForecastMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Water_Forecast.mat"), shader, new Color(0.35f, 0.76f, 0.96f, 0.50f), transparent: true);
            Material waterlineMaterial = CreateOrUpdateMaterial(CombinePath(materialsPath, "Waterline.mat"), shader, new Color(0.05f, 0.27f, 0.58f, 0.92f), transparent: true);

            string prefabsPath = CombinePath(artRootPath, PrefabsFolderName);
            GameObject dryTilePrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, BoardFolderName, "DryTile.prefab"),
                () => CreatePrimitivePlaceholder("DryTile", PrimitiveType.Cube, tileDryMaterial, new Vector3(1.0f, 0.18f, 1.0f)));

            GameObject debrisAPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_A.prefab"),
                () => CreatePrimitivePlaceholder("Debris_A", PrimitiveType.Cube, debrisAMaterial, new Vector3(0.62f, 0.62f, 0.62f), new Vector3(0.0f, 0.22f, 0.0f), new Vector3(0.0f, 18.0f, 0.0f)));
            GameObject debrisBPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_B.prefab"),
                () => CreatePrimitivePlaceholder("Debris_B", PrimitiveType.Cube, debrisBMaterial, new Vector3(0.56f, 0.68f, 0.56f), new Vector3(0.0f, 0.24f, 0.0f), new Vector3(12.0f, -10.0f, 4.0f)));
            GameObject debrisCPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_C.prefab"),
                () => CreatePrimitivePlaceholder("Debris_C", PrimitiveType.Cube, debrisCMaterial, new Vector3(0.72f, 0.42f, 0.72f), new Vector3(0.0f, 0.16f, 0.0f), new Vector3(-10.0f, 22.0f, -6.0f)));
            GameObject debrisDPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_D.prefab"),
                () => CreatePrimitivePlaceholder("Debris_D", PrimitiveType.Cube, debrisDMaterial, new Vector3(0.48f, 0.78f, 0.48f), new Vector3(0.0f, 0.28f, 0.0f), new Vector3(6.0f, 35.0f, 0.0f)));
            GameObject debrisEPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_E.prefab"),
                () => CreatePrimitivePlaceholder("Debris_E", PrimitiveType.Cube, debrisEMaterial, new Vector3(0.70f, 0.52f, 0.50f), new Vector3(0.0f, 0.19f, 0.0f), new Vector3(0.0f, -24.0f, 11.0f)));

            GameObject cratePrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, BlockersFolderName, "Crate.prefab"),
                () => CreatePrimitivePlaceholder("Crate", PrimitiveType.Cube, crateMaterial, new Vector3(0.88f, 0.88f, 0.88f), new Vector3(0.0f, 0.34f, 0.0f)));
            GameObject icePrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, BlockersFolderName, "Ice.prefab"),
                () => CreatePrimitivePlaceholder("Ice", PrimitiveType.Cube, iceMaterial, new Vector3(0.94f, 0.94f, 0.94f), new Vector3(0.0f, 0.36f, 0.0f)));
            GameObject vinePrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, BlockersFolderName, "Vine.prefab"),
                () => CreatePrimitivePlaceholder("Vine", PrimitiveType.Cylinder, vineMaterial, new Vector3(0.34f, 0.48f, 0.34f), new Vector3(0.0f, 0.25f, 0.0f)));

            GameObject puppyTargetPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, TargetsFolderName, "PuppyTarget.prefab"),
                () => CreatePrimitivePlaceholder("PuppyTarget", PrimitiveType.Capsule, puppyTargetMaterial, new Vector3(0.52f, 0.42f, 0.52f), new Vector3(0.0f, 0.40f, 0.0f), new Vector3(0.0f, 0.0f, 90.0f)));

            GameObject dockPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, DockFolderName, "Dock_Shared_7Slot.prefab"),
                () => CreateDockPlaceholder("Dock_Shared_7Slot", dockSafeMaterial));

            GameObject floodedRowOverlayPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, WaterFolderName, "FloodedRowOverlay.prefab"),
                () => CreateFlatOverlayPlaceholder("FloodedRowOverlay", waterFloodedMaterial, new Vector3(1.0f, 1.0f, 1.0f)));
            GameObject forecastRowOverlayPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, WaterFolderName, "ForecastRowOverlay.prefab"),
                () => CreateFlatOverlayPlaceholder("ForecastRowOverlay", waterForecastMaterial, new Vector3(1.0f, 1.0f, 1.0f)));
            GameObject waterlinePrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, WaterFolderName, "Waterline.prefab"),
                () => CreatePrimitivePlaceholder("Waterline", PrimitiveType.Cube, waterlineMaterial, new Vector3(1.0f, 0.08f, 0.10f), new Vector3(0.0f, 0.04f, 0.0f)));

            GameObject groupClearFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "GroupClearFx.prefab"), () => CreateEmptyPlaceholder("GroupClearFx"));
            GameObject invalidTapFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "InvalidTapFx.prefab"), () => CreateEmptyPlaceholder("InvalidTapFx"));
            GameObject crateBreakFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "CrateBreakFx.prefab"), () => CreateEmptyPlaceholder("CrateBreakFx"));
            GameObject iceRevealFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "IceRevealFx.prefab"), () => CreateEmptyPlaceholder("IceRevealFx"));
            GameObject vineClearFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "VineClearFx.prefab"), () => CreateEmptyPlaceholder("VineClearFx"));
            GameObject vineGrowPreviewFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "VineGrowPreviewFx.prefab"), () => CreateEmptyPlaceholder("VineGrowPreviewFx"));
            GameObject dockInsertFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "DockInsertFx.prefab"), () => CreateEmptyPlaceholder("DockInsertFx"));
            GameObject dockTripleClearFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "DockTripleClearFx.prefab"), () => CreateEmptyPlaceholder("DockTripleClearFx"));
            GameObject waterRiseFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "WaterRiseFx.prefab"), () => CreateEmptyPlaceholder("WaterRiseFx"));
            GameObject targetExtractionFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "TargetExtractionFx.prefab"), () => CreateEmptyPlaceholder("TargetExtractionFx"));
            GameObject nearRescueReliefFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "NearRescueReliefFx.prefab"), () => CreateEmptyPlaceholder("NearRescueReliefFx"));
            GameObject winFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "WinFx.prefab"), () => CreateEmptyPlaceholder("WinFx"));
            GameObject lossFxPrefab = CreateOrUpdatePrefab(CombinePath(prefabsPath, FxFolderName, "LossFx.prefab"), () => CreateEmptyPlaceholder("LossFx"));

            string registriesPath = CombinePath(artRootPath, RegistriesFolderName);
            TileVisualRegistry tileRegistry = CreateOrLoadAsset<TileVisualRegistry>(CombinePath(registriesPath, "Phase1TileVisualRegistry.asset"));
            tileRegistry.DryTilePrefab = dryTilePrefab;
            tileRegistry.FloodedRowOverlayPrefab = floodedRowOverlayPrefab;
            tileRegistry.ForecastRowOverlayPrefab = forecastRowOverlayPrefab;
            tileRegistry.WaterlinePrefab = waterlinePrefab;
            tileRegistry.FallbackTilePrefab = dryTilePrefab;
            EditorUtility.SetDirty(tileRegistry);

            PieceVisualRegistry pieceRegistry = CreateOrLoadAsset<PieceVisualRegistry>(CombinePath(registriesPath, "Phase1PieceVisualRegistry.asset"));
            pieceRegistry.DebrisAPrefab = debrisAPrefab;
            pieceRegistry.DebrisBPrefab = debrisBPrefab;
            pieceRegistry.DebrisCPrefab = debrisCPrefab;
            pieceRegistry.DebrisDPrefab = debrisDPrefab;
            pieceRegistry.DebrisEPrefab = debrisEPrefab;
            pieceRegistry.FallbackPrefab = debrisAPrefab;
            EditorUtility.SetDirty(pieceRegistry);

            BlockerVisualRegistry blockerRegistry = CreateOrLoadAsset<BlockerVisualRegistry>(CombinePath(registriesPath, "Phase1BlockerVisualRegistry.asset"));
            blockerRegistry.CratePrefab = cratePrefab;
            blockerRegistry.IcePrefab = icePrefab;
            blockerRegistry.VinePrefab = vinePrefab;
            blockerRegistry.FallbackBlockerPrefab = cratePrefab;
            EditorUtility.SetDirty(blockerRegistry);

            TargetVisualRegistry targetRegistry = CreateOrLoadAsset<TargetVisualRegistry>(CombinePath(registriesPath, "Phase1TargetVisualRegistry.asset"));
            targetRegistry.PuppyPrefab = puppyTargetPrefab;
            targetRegistry.FallbackTargetPrefab = puppyTargetPrefab;
            EditorUtility.SetDirty(targetRegistry);

            DockVisualConfig dockConfig = CreateOrLoadAsset<DockVisualConfig>(CombinePath(registriesPath, "Phase1DockVisualConfig.asset"));
            dockConfig.SharedDockPrefab = dockPrefab;
            dockConfig.SafePrefab = dockPrefab;
            dockConfig.CautionPrefab = dockPrefab;
            dockConfig.AcutePrefab = dockPrefab;
            dockConfig.FailedPrefab = dockPrefab;
            dockConfig.SafeMaterial = dockSafeMaterial;
            dockConfig.CautionMaterial = dockCautionMaterial;
            dockConfig.AcuteMaterial = dockAcuteMaterial;
            dockConfig.FailedMaterial = dockFailedMaterial;
            EditorUtility.SetDirty(dockConfig);

            FxVisualRegistry fxRegistry = CreateOrLoadAsset<FxVisualRegistry>(CombinePath(registriesPath, "Phase1FxVisualRegistry.asset"));
            fxRegistry.GroupClearFx = groupClearFxPrefab;
            fxRegistry.InvalidTapFx = invalidTapFxPrefab;
            fxRegistry.CrateBreakFx = crateBreakFxPrefab;
            fxRegistry.IceRevealFx = iceRevealFxPrefab;
            fxRegistry.VineClearFx = vineClearFxPrefab;
            fxRegistry.VineGrowPreviewFx = vineGrowPreviewFxPrefab;
            fxRegistry.DockInsertFx = dockInsertFxPrefab;
            fxRegistry.DockTripleClearFx = dockTripleClearFxPrefab;
            fxRegistry.WaterRiseFx = waterRiseFxPrefab;
            fxRegistry.TargetExtractionFx = targetExtractionFxPrefab;
            fxRegistry.NearRescueReliefFx = nearRescueReliefFxPrefab;
            fxRegistry.WinFx = winFxPrefab;
            fxRegistry.LossFx = lossFxPrefab;
            EditorUtility.SetDirty(fxRegistry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void EnsureRequiredFolders(string artRootPath)
        {
            EnsureFolderExists(artRootPath);
            for (int i = 0; i < RequiredFolderPaths.Length; i++)
            {
                EnsureFolderExists(CombinePath(artRootPath, RequiredFolderPaths[i]));
            }
        }

        private static Material CreateOrUpdateMaterial(string assetPath, Shader shader, Color color, bool transparent = false)
        {
            Material? material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material is null)
            {
                material = new Material(shader)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(assetPath),
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.shader = shader;
            material.color = color;
            ConfigureMaterial(material, transparent);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureMaterial(Material material, bool transparent)
        {
            if (material.shader.name != "Standard")
            {
                material.color = transparent
                    ? new Color(material.color.r, material.color.g, material.color.b, material.color.a)
                    : new Color(material.color.r, material.color.g, material.color.b, 1.0f);
                return;
            }

            if (transparent)
            {
                material.SetFloat("_Mode", 3.0f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
                return;
            }

            material.SetFloat("_Mode", 0.0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = -1;
            material.color = new Color(material.color.r, material.color.g, material.color.b, 1.0f);
        }

        private static Shader ResolveShader()
        {
            Shader? shader = Shader.Find("Standard");
            shader ??= Shader.Find("Legacy Shaders/Diffuse");
            shader ??= Shader.Find("Sprites/Default");

            if (shader is null)
            {
                throw new InvalidOperationException("Could not resolve a built-in shader for placeholder material creation.");
            }

            return shader;
        }

        private static GameObject CreateOrUpdatePrefab(string assetPath, Func<GameObject> createPrefabRoot)
        {
            GameObject prefabRoot = createPrefabRoot();
            try
            {
                prefabRoot.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }

            GameObject? prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab is null)
            {
                throw new InvalidOperationException($"Failed to load prefab created at '{assetPath}'.");
            }

            return prefab;
        }

        private static GameObject CreatePrimitivePlaceholder(
            string name,
            PrimitiveType primitiveType,
            Material material,
            Vector3 localScale,
            Vector3? localPosition = null,
            Vector3? localEulerAngles = null)
        {
            GameObject root = GameObject.CreatePrimitive(primitiveType);
            root.name = name;
            root.transform.position = Vector3.zero;
            root.transform.localScale = localScale;

            if (localPosition.HasValue)
            {
                root.transform.position = localPosition.Value;
            }

            if (localEulerAngles.HasValue)
            {
                root.transform.rotation = Quaternion.Euler(localEulerAngles.Value);
            }

            RemoveCollider(root);
            AssignMaterial(root, material);
            return root;
        }

        private static GameObject CreateFlatOverlayPlaceholder(string name, Material material, Vector3 localScale)
        {
            GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = name;
            overlay.transform.position = Vector3.zero;
            overlay.transform.rotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
            overlay.transform.localScale = localScale;
            RemoveCollider(overlay);
            AssignMaterial(overlay, material);
            return overlay;
        }

        private static GameObject CreateDockPlaceholder(string name, Material material)
        {
            GameObject dock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dock.name = name;
            dock.transform.position = Vector3.zero;
            dock.transform.localScale = new Vector3(7.2f, 0.30f, 1.1f);
            RemoveCollider(dock);
            AssignMaterial(dock, material);

            for (int slotIndex = 0; slotIndex < 7; slotIndex++)
            {
                GameObject anchor = new GameObject($"Slot_{slotIndex:00}");
                anchor.transform.SetParent(dock.transform, false);
                anchor.transform.localPosition = new Vector3(-3.0f + slotIndex, 0.75f, 0.0f);
                anchor.transform.localRotation = Quaternion.identity;
                anchor.transform.localScale = Vector3.one;
            }

            return dock;
        }

        private static GameObject CreateEmptyPlaceholder(string name)
        {
            return new GameObject(name);
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider? collider = gameObject.GetComponent<Collider>();
            if (collider is not null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static void AssignMaterial(GameObject gameObject, Material material)
        {
            Renderer? renderer = gameObject.GetComponent<Renderer>();
            if (renderer is not null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static T CreateOrLoadAsset<T>(string assetPath)
            where T : ScriptableObject
        {
            T? asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset is not null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void EnsureFolderExists(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            int separatorIndex = assetPath.LastIndexOf('/');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException($"Cannot create Unity folder '{assetPath}'.");
            }

            string parentPath = assetPath[..separatorIndex];
            string folderName = assetPath[(separatorIndex + 1)..];

            EnsureFolderExists(parentPath);
            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static string CombinePath(params string[] parts)
        {
            string combined = string.Empty;
            for (int i = 0; i < parts.Length; i++)
            {
                string normalizedPart = parts[i].Replace('\\', '/').Trim('/');
                if (string.IsNullOrWhiteSpace(normalizedPart))
                {
                    continue;
                }

                combined = string.IsNullOrEmpty(combined)
                    ? normalizedPart
                    : $"{combined}/{normalizedPart}";
            }

            return combined;
        }
    }
}
