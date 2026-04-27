using System;
using System.Collections.Generic;
using System.Linq;
using Rescue.Unity.Art.Registries;
using UnityEditor;
using UnityEngine;

namespace Rescue.Unity.EditorTools.Art.Prefabs
{
    public static class Phase1PlaceholderPrefabFactory
    {
        public const string DefaultArtRootPath = "Assets/Rescue.Unity/Art";
        public const float DefaultBoardCellSize = 1.0f;

        private const string PrefabsFolderName = "Prefabs";
        private const string ModelsFolderName = "Models";
        private const string TexturesFolderName = "Textures";
        private const string MaterialsFolderName = "Materials";
        private const string RegistriesFolderName = "Registries";
        private const string BoardFolderName = "Board";
        private const string PiecesFolderName = "Pieces";
        private const string BlockersFolderName = "Blockers";
        private const string TargetsFolderName = "Targets";
        private const string DockFolderName = "Dock";
        private const string WaterFolderName = "Water";
        private const string FxFolderName = "FX";
        private const string Phase1FolderName = "Phase1";

        private static readonly string[] RequiredFolderPaths =
        {
            $"{PrefabsFolderName}/{BoardFolderName}",
            $"{PrefabsFolderName}/{PiecesFolderName}",
            $"{PrefabsFolderName}/{BlockersFolderName}",
            $"{PrefabsFolderName}/{TargetsFolderName}",
            $"{PrefabsFolderName}/{DockFolderName}",
            $"{PrefabsFolderName}/{WaterFolderName}",
            $"{PrefabsFolderName}/{FxFolderName}",
            $"{PrefabsFolderName}/{Phase1FolderName}/{BoardFolderName}",
            $"{PrefabsFolderName}/{Phase1FolderName}/{PiecesFolderName}",
            $"{PrefabsFolderName}/{Phase1FolderName}/{BlockersFolderName}",
            $"{PrefabsFolderName}/{Phase1FolderName}/{TargetsFolderName}",
            $"{PrefabsFolderName}/{Phase1FolderName}/{DockFolderName}",
            $"{PrefabsFolderName}/{Phase1FolderName}/{WaterFolderName}",
            $"{PrefabsFolderName}/{Phase1FolderName}/{FxFolderName}",
            $"{ModelsFolderName}/{BlockersFolderName}",
            $"{ModelsFolderName}/{WaterFolderName}",
            $"{ModelsFolderName}/{FxFolderName}",
            $"{TexturesFolderName}/{BlockersFolderName}",
            $"{TexturesFolderName}/{WaterFolderName}",
            $"{TexturesFolderName}/{FxFolderName}",
            MaterialsFolderName,
            $"{MaterialsFolderName}/{Phase1FolderName}",
            RegistriesFolderName,
        };

        private static readonly AssetSizingProfile TileSizingProfile = new AssetSizingProfile(DefaultBoardCellSize);
        private static readonly AssetSizingProfile DebrisSizingProfile = new AssetSizingProfile(0.92f);
        private static readonly AssetSizingProfile CrateSizingProfile = new AssetSizingProfile(0.96f);
        private static readonly AssetSizingProfile IceSizingProfile = new AssetSizingProfile(0.96f);
        private static readonly AssetSizingProfile VineSizingProfile = new AssetSizingProfile(0.92f);
        private static readonly AssetSizingProfile TargetSizingProfile = new AssetSizingProfile(0.90f);
        private static readonly AssetSizingProfile RowOverlaySizingProfile = new AssetSizingProfile(1.0f);
        private static readonly AssetSizingProfile FxSizingProfile = new AssetSizingProfile(1.0f);

        [MenuItem("Rescue Grid/Art/Create Phase 1 Placeholder Prefabs")]
        public static void CreateDefaultPhase1Placeholders()
        {
            CreateAll(DefaultArtRootPath);
            Debug.Log($"Phase 1 art prefabs created under '{DefaultArtRootPath}'.");
        }

        public static void CreateAll(string artRootPath)
        {
            if (string.IsNullOrWhiteSpace(artRootPath))
            {
                throw new ArgumentException("Art root path must not be null or empty.", nameof(artRootPath));
            }

            EnsureRequiredFolders(artRootPath);

            Shader shader = ResolveShader();
            PlaceholderAssets placeholderAssets = CreatePlaceholderAssets(artRootPath, shader);
            CreateOrUpdateRegistries(artRootPath, placeholderAssets, CreateProductionAssets(artRootPath, shader, placeholderAssets));

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

        private static PlaceholderAssets CreatePlaceholderAssets(string artRootPath, Shader shader)
        {
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
                () => CreatePrimitivePlaceholder("Debris_A", PrimitiveType.Cube, debrisAMaterial, new Vector3(0.62f, 0.30f, 0.62f), new Vector3(0.0f, 0.15f, 0.0f), new Vector3(0.0f, 18.0f, 0.0f)));
            GameObject debrisBPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_B.prefab"),
                () => CreatePrimitivePlaceholder("Debris_B", PrimitiveType.Cube, debrisBMaterial, new Vector3(0.56f, 0.32f, 0.56f), new Vector3(0.0f, 0.16f, 0.0f), new Vector3(12.0f, -10.0f, 4.0f)));
            GameObject debrisCPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_C.prefab"),
                () => CreatePrimitivePlaceholder("Debris_C", PrimitiveType.Cube, debrisCMaterial, new Vector3(0.72f, 0.34f, 0.72f), new Vector3(0.0f, 0.17f, 0.0f), new Vector3(-10.0f, 22.0f, -6.0f)));
            GameObject debrisDPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_D.prefab"),
                () => CreatePrimitivePlaceholder("Debris_D", PrimitiveType.Cube, debrisDMaterial, new Vector3(0.48f, 0.30f, 0.48f), new Vector3(0.0f, 0.15f, 0.0f), new Vector3(6.0f, 35.0f, 0.0f)));
            GameObject debrisEPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_E.prefab"),
                () => CreatePrimitivePlaceholder("Debris_E", PrimitiveType.Cube, debrisEMaterial, new Vector3(0.70f, 0.25f, 0.50f), new Vector3(0.0f, 0.125f, 0.0f), new Vector3(0.0f, -24.0f, 11.0f)));

            GameObject cratePrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, BlockersFolderName, "Crate.prefab"),
                () => CreatePrimitivePlaceholder("Crate", PrimitiveType.Cube, crateMaterial, new Vector3(0.90f, 0.90f, 0.90f), new Vector3(0.0f, 0.45f, 0.0f)));
            GameObject icePrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, BlockersFolderName, "Ice.prefab"),
                () => CreatePrimitivePlaceholder("Ice", PrimitiveType.Cube, iceMaterial, new Vector3(0.74f, 0.52f, 0.74f), new Vector3(0.0f, 0.26f, 0.0f)));
            GameObject vinePrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, BlockersFolderName, "Vine.prefab"),
                () => CreateFlatOverlayPlaceholder("Vine", vineMaterial, new Vector3(0.82f, 0.82f, 1.0f), 0.04f));

            GameObject puppyTargetPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, TargetsFolderName, "PuppyTarget.prefab"),
                () => CreatePrimitivePlaceholder("PuppyTarget", PrimitiveType.Capsule, puppyTargetMaterial, new Vector3(0.42f, 0.375f, 0.42f), new Vector3(0.0f, 0.375f, 0.0f)));

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

            return new PlaceholderAssets(
                dryTilePrefab,
                debrisAPrefab,
                debrisBPrefab,
                debrisCPrefab,
                debrisDPrefab,
                debrisEPrefab,
                cratePrefab,
                icePrefab,
                vinePrefab,
                puppyTargetPrefab,
                dockPrefab,
                dockSafeMaterial,
                dockCautionMaterial,
                dockAcuteMaterial,
                dockFailedMaterial,
                floodedRowOverlayPrefab,
                forecastRowOverlayPrefab,
                waterlinePrefab,
                groupClearFxPrefab,
                invalidTapFxPrefab,
                crateBreakFxPrefab,
                iceRevealFxPrefab,
                vineClearFxPrefab,
                vineGrowPreviewFxPrefab,
                dockInsertFxPrefab,
                dockTripleClearFxPrefab,
                waterRiseFxPrefab,
                targetExtractionFxPrefab,
                nearRescueReliefFxPrefab,
                winFxPrefab,
                lossFxPrefab);
        }

        private static ProductionAssets CreateProductionAssets(string artRootPath, Shader shader, PlaceholderAssets placeholderAssets)
        {
            string materialsPath = CombinePath(artRootPath, MaterialsFolderName, Phase1FolderName);
            string prefabsPath = CombinePath(artRootPath, PrefabsFolderName, Phase1FolderName);

            Material? tileMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Tile_Dry_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Board", "Meshy_AI_Terrazzo_Square_Box_L_0424155101_texture.png"));

            Material? debrisAMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Debris_A_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Pieces", "Meshy_AI_Blue_Rimmed_Paw_Print_0424154835_texture.png"));
            Material? debrisBMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Debris_B_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Pieces", "Meshy_AI_Blue_Speckled_Paw_Bow_0424154905_texture.png"));
            Material? debrisCMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Debris_C_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Pieces", "Meshy_AI_Knot_of_Colors_0424154848_texture.png"));
            Material? debrisDMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Debris_D_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Pieces", "Meshy_AI_Heart_Shaped_Massager_0424155210_texture.png"));
            Material? debrisEMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Debris_E_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Pieces", "Meshy_AI_Beige_terry_towel_wit_0424155142_texture.png"));

            Material? crateMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Crate_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Blockers", "Meshy_AI_Pawprint_Wooden_Crate_0424155503_texture.png"));
            Material? iceBlockMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Ice_Block_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Blockers", "Meshy_AI_Ice_Block_0425081828_texture.png"),
                CombinePath(artRootPath, "Textures", "Blockers", "Meshy_AI_Ice_Block_0425081828_texture_normal.png"));
            Material? vineMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Vine_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Blockers", "Meshy_AI_Entwined_Ivy_0425081715_texture.png"),
                CombinePath(artRootPath, "Textures", "Blockers", "Meshy_AI_Entwined_Ivy_0425081715_texture_normal.png"));
            Material? puppyMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "PuppyTarget_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Targets", "Meshy_AI_Curious_Wet_Puppy_0424155427_texture.png"));
            Material? floodedRowMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Water_Flooded_Row_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Water", "Meshy_AI_Blue_Puddle_0425081657_texture.png"),
                CombinePath(artRootPath, "Textures", "Water", "Meshy_AI_Blue_Puddle_0425081657_texture_normal.png"));
            Material? iceRevealFxMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "IceRevealFx_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "FX", "Meshy_AI_Cracked_Ice_Tile_0425081737_texture.png"),
                CombinePath(artRootPath, "Textures", "FX", "Meshy_AI_Cracked_Ice_Tile_0425081737_texture_normal.png"));

            Material safeDockMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Dock_Safe_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Dock", "Meshy_AI_Dock_Safe_0424154642_texture_fbx.png"))
                ?? placeholderAssets.DockSafeMaterial;
            Material cautionDockMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Dock_Caution_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Dock", "Meshy_AI_Dock_Caution_0424154706_texture_fbx.png"))
                ?? placeholderAssets.DockCautionMaterial;
            Material acuteDockMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Dock_Acute_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Dock", "Meshy_AI_Dock_Alert_0424154721_texture_fbx.png"))
                ?? placeholderAssets.DockAcuteMaterial;
            Material failedDockMaterial = CreateOrUpdateTexturedMaterial(
                CombinePath(materialsPath, "Dock_Failed_Phase1.mat"),
                shader,
                CombinePath(artRootPath, "Textures", "Dock", "Meshy_AI_Dock_fail_0424154736_texture_fbx.png"))
                ?? placeholderAssets.DockFailedMaterial;

            Material iceOverlayMaterial = CreateOrUpdateMaterial(
                CombinePath(materialsPath, "Ice_Overlay_Phase1.mat"),
                shader,
                new Color(0.74f, 0.92f, 1.0f, 0.68f),
                transparent: true);
            Material vineOverlayMaterial = CreateOrUpdateMaterial(
                CombinePath(materialsPath, "Vine_Overlay_Phase1.mat"),
                shader,
                new Color(0.20f, 0.52f, 0.21f, 0.95f));

            GameObject? tilePrefab = CreateMeshWrapperPrefab(
                CombinePath(prefabsPath, BoardFolderName, "DryTile_Phase1.prefab"),
                CombinePath(artRootPath, "Models", "Board", "Meshy_AI_Terrazzo_Square_Box_L_0424155101_texture.fbx"),
                tileMaterial,
                TileSizingProfile);

            GameObject? debrisAPrefab = CreateMeshWrapperPrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_A_Phase1.prefab"),
                CombinePath(artRootPath, "Models", "Pieces", "Meshy_AI_Blue_Rimmed_Paw_Print_0424154835_texture.fbx"),
                debrisAMaterial,
                DebrisSizingProfile);
            GameObject? debrisBPrefab = CreateMeshWrapperPrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_B_Phase1.prefab"),
                CombinePath(artRootPath, "Models", "Pieces", "Meshy_AI_Blue_Speckled_Paw_Bow_0424154905_texture.fbx"),
                debrisBMaterial,
                DebrisSizingProfile);
            GameObject? debrisCPrefab = CreateMeshWrapperPrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_C_Phase1.prefab"),
                CombinePath(artRootPath, "Models", "Pieces", "Meshy_AI_Knot_of_Colors_0424154848_texture.fbx"),
                debrisCMaterial,
                DebrisSizingProfile);
            GameObject? debrisDPrefab = CreateMeshWrapperPrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_D_Phase1.prefab"),
                CombinePath(artRootPath, "Models", "Pieces", "Meshy_AI_Heart_Shaped_Massager_0424155210_texture.fbx"),
                debrisDMaterial,
                DebrisSizingProfile);
            GameObject? debrisEPrefab = CreateMeshWrapperPrefab(
                CombinePath(prefabsPath, PiecesFolderName, "Debris_E_Phase1.prefab"),
                CombinePath(artRootPath, "Models", "Pieces", "Meshy_AI_Beige_terry_towel_wit_0424155142_texture.fbx"),
                debrisEMaterial,
                DebrisSizingProfile);

            GameObject? cratePrefab = CreateMeshWrapperPrefab(
                CombinePath(prefabsPath, BlockersFolderName, "Crate_Phase1.prefab"),
                CombinePath(artRootPath, "Models", "Blockers", "Meshy_AI_Pawprint_Wooden_Crate_0424155503_texture.fbx"),
                crateMaterial,
                CrateSizingProfile);
            GameObject? iceBlockPrefab = CreateMeshWrapperPrefab(
                CombinePath(prefabsPath, BlockersFolderName, "Ice_Block_Phase1.prefab"),
                CombinePath(artRootPath, "Models", "Blockers", "Meshy_AI_Ice_Block_0425081828_texture.fbx"),
                iceBlockMaterial,
                IceSizingProfile);
            GameObject iceOverlayPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, BlockersFolderName, "Ice_Overlay_Phase1.prefab"),
                () => CreateFlatOverlayPlaceholder("Ice", iceOverlayMaterial, new Vector3(0.96f, 0.96f, 1.0f), 0.03f));
            GameObject? vineMeshPrefab = CreateMeshWrapperPrefab(
                CombinePath(prefabsPath, BlockersFolderName, "Vine_Phase1.prefab"),
                CombinePath(artRootPath, "Models", "Blockers", "Meshy_AI_Entwined_Ivy_0425081715_texture.fbx"),
                vineMaterial,
                VineSizingProfile);
            GameObject vineOverlayPrefab = CreateOrUpdatePrefab(
                CombinePath(prefabsPath, BlockersFolderName, "Vine_Overlay_Phase1.prefab"),
                () => CreateFlatOverlayPlaceholder("Vine", vineOverlayMaterial, new Vector3(0.86f, 0.86f, 1.0f), 0.04f));

            GameObject? puppyPrefab = CreateMeshWrapperPrefab(
                CombinePath(prefabsPath, TargetsFolderName, "PuppyTarget_Phase1.prefab"),
                CombinePath(artRootPath, "Models", "Targets", "Meshy_AI_Curious_Wet_Puppy_0424155427_texture.fbx"),
                puppyMaterial,
                TargetSizingProfile);
            GameObject? floodedRowOverlayPrefab = CreateMeshWrapperPrefab(
                CombinePath(prefabsPath, WaterFolderName, "FloodedRowOverlay_Phase1.prefab"),
                CombinePath(artRootPath, "Models", "Water", "Meshy_AI_Blue_Puddle_0425081657_texture.fbx"),
                floodedRowMaterial,
                RowOverlaySizingProfile);
            GameObject? iceRevealFxPrefab = CreateMeshWrapperPrefab(
                CombinePath(prefabsPath, FxFolderName, "IceRevealFx_Phase1.prefab"),
                CombinePath(artRootPath, "Models", "FX", "Meshy_AI_Cracked_Ice_Tile_0425081737_texture.fbx"),
                iceRevealFxMaterial,
                FxSizingProfile);

            string sharedDockModelPath = CombinePath(artRootPath, "Models", "Dock", "Meshy_AI_Dock_Safe_0424154642_texture_fbx.fbx");
            GameObject? sharedDockPrefab = CreateDockPrefab(
                CombinePath(prefabsPath, DockFolderName, "Dock_Shared_7Slot_Phase1.prefab"),
                sharedDockModelPath,
                safeDockMaterial);
            GameObject? safeDockPrefab = sharedDockPrefab;
            GameObject? cautionDockPrefab = sharedDockPrefab;
            GameObject? acuteDockPrefab = sharedDockPrefab;
            GameObject? failedDockPrefab = sharedDockPrefab;

            return new ProductionAssets(
                tilePrefab,
                debrisAPrefab,
                debrisBPrefab,
                debrisCPrefab,
                debrisDPrefab,
                debrisEPrefab,
                cratePrefab,
                iceBlockPrefab ?? iceOverlayPrefab,
                vineMeshPrefab ?? vineOverlayPrefab,
                puppyPrefab,
                floodedRowOverlayPrefab,
                iceRevealFxPrefab,
                sharedDockPrefab,
                safeDockPrefab,
                cautionDockPrefab,
                acuteDockPrefab,
                failedDockPrefab,
                safeDockMaterial,
                cautionDockMaterial,
                acuteDockMaterial,
                failedDockMaterial,
                LoadFirstMesh(sharedDockModelPath));
        }

        private static void CreateOrUpdateRegistries(string artRootPath, PlaceholderAssets placeholderAssets, ProductionAssets productionAssets)
        {
            string registriesPath = CombinePath(artRootPath, RegistriesFolderName);
            GameObject canonicalDryTilePrefab = productionAssets.TilePrefab ?? placeholderAssets.DryTilePrefab;

            TileVisualRegistry tileRegistry = CreateOrLoadAsset<TileVisualRegistry>(CombinePath(registriesPath, "Phase1TileVisualRegistry.asset"));
            // Keep the direct dry tile as the canonical board entry. Phase 1 wrappers are optional visuals,
            // not a registry-level transform compensation layer.
            tileRegistry.DryTilePrefab = canonicalDryTilePrefab;
            tileRegistry.FloodedRowOverlayPrefab = productionAssets.FloodedRowOverlayPrefab ?? placeholderAssets.FloodedRowOverlayPrefab;
            tileRegistry.ForecastRowOverlayPrefab = placeholderAssets.ForecastRowOverlayPrefab;
            tileRegistry.WaterlinePrefab = placeholderAssets.WaterlinePrefab;
            tileRegistry.FallbackTilePrefab = canonicalDryTilePrefab;
            EditorUtility.SetDirty(tileRegistry);

            PieceVisualRegistry pieceRegistry = CreateOrLoadAsset<PieceVisualRegistry>(CombinePath(registriesPath, "Phase1PieceVisualRegistry.asset"));
            pieceRegistry.DebrisAPrefab = productionAssets.DebrisAPrefab ?? placeholderAssets.DebrisAPrefab;
            pieceRegistry.DebrisBPrefab = productionAssets.DebrisBPrefab ?? placeholderAssets.DebrisBPrefab;
            pieceRegistry.DebrisCPrefab = productionAssets.DebrisCPrefab ?? placeholderAssets.DebrisCPrefab;
            pieceRegistry.DebrisDPrefab = productionAssets.DebrisDPrefab ?? placeholderAssets.DebrisDPrefab;
            pieceRegistry.DebrisEPrefab = productionAssets.DebrisEPrefab ?? placeholderAssets.DebrisEPrefab;
            pieceRegistry.FallbackPrefab = productionAssets.DebrisAPrefab ?? placeholderAssets.DebrisAPrefab;
            EditorUtility.SetDirty(pieceRegistry);

            BlockerVisualRegistry blockerRegistry = CreateOrLoadAsset<BlockerVisualRegistry>(CombinePath(registriesPath, "Phase1BlockerVisualRegistry.asset"));
            blockerRegistry.CratePrefab = productionAssets.CratePrefab ?? placeholderAssets.CratePrefab;
            blockerRegistry.IcePrefab = productionAssets.IcePrefab ?? placeholderAssets.IcePrefab;
            blockerRegistry.VinePrefab = productionAssets.VinePrefab ?? placeholderAssets.VinePrefab;
            blockerRegistry.FallbackBlockerPrefab = productionAssets.CratePrefab ?? placeholderAssets.CratePrefab;
            EditorUtility.SetDirty(blockerRegistry);

            TargetVisualRegistry targetRegistry = CreateOrLoadAsset<TargetVisualRegistry>(CombinePath(registriesPath, "Phase1TargetVisualRegistry.asset"));
            targetRegistry.PuppyPrefab = productionAssets.PuppyPrefab ?? placeholderAssets.PuppyTargetPrefab;
            targetRegistry.FallbackTargetPrefab = productionAssets.PuppyPrefab ?? placeholderAssets.PuppyTargetPrefab;
            EditorUtility.SetDirty(targetRegistry);

            DockVisualConfig dockConfig = CreateOrLoadAsset<DockVisualConfig>(CombinePath(registriesPath, "Phase1DockVisualConfig.asset"));
            dockConfig.SharedDockPrefab = productionAssets.SharedDockPrefab ?? placeholderAssets.DockPrefab;
            dockConfig.SharedDockMesh = productionAssets.SharedDockMesh;
            dockConfig.SafePrefab = productionAssets.SafeDockPrefab ?? dockConfig.SharedDockPrefab;
            dockConfig.CautionPrefab = productionAssets.CautionDockPrefab ?? dockConfig.SharedDockPrefab;
            dockConfig.AcutePrefab = productionAssets.AcuteDockPrefab ?? dockConfig.SharedDockPrefab;
            dockConfig.FailedPrefab = productionAssets.FailedDockPrefab ?? dockConfig.SharedDockPrefab;
            dockConfig.SafeMaterial = productionAssets.SafeDockMaterial;
            dockConfig.CautionMaterial = productionAssets.CautionDockMaterial;
            dockConfig.AcuteMaterial = productionAssets.AcuteDockMaterial;
            dockConfig.FailedMaterial = productionAssets.FailedDockMaterial;
            EditorUtility.SetDirty(dockConfig);

            FxVisualRegistry fxRegistry = CreateOrLoadAsset<FxVisualRegistry>(CombinePath(registriesPath, "Phase1FxVisualRegistry.asset"));
            fxRegistry.GroupClearFx = placeholderAssets.GroupClearFxPrefab;
            fxRegistry.InvalidTapFx = placeholderAssets.InvalidTapFxPrefab;
            fxRegistry.CrateBreakFx = placeholderAssets.CrateBreakFxPrefab;
            fxRegistry.IceRevealFx = productionAssets.IceRevealFxPrefab ?? placeholderAssets.IceRevealFxPrefab;
            fxRegistry.VineClearFx = placeholderAssets.VineClearFxPrefab;
            fxRegistry.VineGrowPreviewFx = placeholderAssets.VineGrowPreviewFxPrefab;
            fxRegistry.DockInsertFx = placeholderAssets.DockInsertFxPrefab;
            fxRegistry.DockTripleClearFx = placeholderAssets.DockTripleClearFxPrefab;
            fxRegistry.WaterRiseFx = placeholderAssets.WaterRiseFxPrefab;
            fxRegistry.TargetExtractionFx = placeholderAssets.TargetExtractionFxPrefab;
            fxRegistry.NearRescueReliefFx = placeholderAssets.NearRescueReliefFxPrefab;
            fxRegistry.WinFx = placeholderAssets.WinFxPrefab;
            fxRegistry.LossFx = placeholderAssets.LossFxPrefab;
            EditorUtility.SetDirty(fxRegistry);
        }

        private static Material? CreateOrUpdateTexturedMaterial(
            string assetPath,
            Shader shader,
            string texturePath,
            string? normalTexturePath = null,
            bool transparent = false)
        {
            Texture2D? texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture is null)
            {
                return null;
            }

            Material material = CreateOrUpdateMaterial(assetPath, shader, Color.white, transparent);
            material.mainTexture = texture;

            if (!string.IsNullOrWhiteSpace(normalTexturePath))
            {
                Texture2D? normalTexture = LoadNormalTexture(normalTexturePath);
                if (normalTexture is not null)
                {
                    material.SetTexture("_BumpMap", normalTexture);
                    material.EnableKeyword("_NORMALMAP");
                }
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D? LoadNormalTexture(string normalTexturePath)
        {
            Texture2D? normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalTexturePath);
            if (normalTexture is null)
            {
                return null;
            }

            TextureImporter? importer = AssetImporter.GetAtPath(normalTexturePath) as TextureImporter;
            if (importer is not null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
                normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalTexturePath);
            }

            return normalTexture;
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

        private static GameObject? CreateMeshWrapperPrefab(string assetPath, string sourceModelPath, Material? material, AssetSizingProfile sizingProfile)
        {
            GameObject? sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(sourceModelPath);
            if (sourceModel is null || material is null)
            {
                return null;
            }

            return CreateOrUpdatePrefab(assetPath, () =>
            {
                GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(assetPath));
                GameObject art = (GameObject)PrefabUtility.InstantiatePrefab(sourceModel);
                art.name = "Visual";
                art.transform.SetParent(root.transform, false);
                NormalizeChildToFootprint(art, sizingProfile);
                AssignMaterialRecursively(art, material);
                RemoveCollidersRecursively(art);
                return root;
            });
        }

        private static GameObject? CreateDockPrefab(string assetPath, string sourceModelPath, Material material)
        {
            GameObject? sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(sourceModelPath);
            if (sourceModel is null)
            {
                return null;
            }

            return CreateOrUpdatePrefab(assetPath, () =>
            {
                GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(assetPath));
                GameObject art = (GameObject)PrefabUtility.InstantiatePrefab(sourceModel);
                art.name = "Visual";
                art.transform.SetParent(root.transform, false);
                NormalizeChildToDockSize(art, 7.2f, 1.1f);
                AssignMaterialRecursively(art, material);
                RemoveCollidersRecursively(art);
                AddDockAnchors(root.transform, GetCombinedRendererBounds(art));
                return root;
            });
        }

        private static void NormalizeChildToFootprint(GameObject art, AssetSizingProfile sizingProfile)
        {
            Bounds bounds = GetCombinedRendererBounds(art);
            float footprint = CalculateFootprint(bounds);
            if (footprint <= 0.0001f)
            {
                return;
            }

            float scale = (sizingProfile.TargetFootprint / footprint) * sizingProfile.UniformScaleMultiplier;
            art.transform.localScale = Vector3.one * scale;
            RecenterChildToRoot(art);
            art.transform.localPosition += sizingProfile.LocalPositionOffset;
        }

        private static void NormalizeChildToDockSize(GameObject art, float targetWidth, float targetDepth)
        {
            Bounds bounds = GetCombinedRendererBounds(art);
            float width = Mathf.Max(bounds.size.x, 0.0001f);
            float depth = Mathf.Max(bounds.size.z, 0.0001f);
            float scale = Mathf.Min(targetWidth / width, targetDepth / depth);
            art.transform.localScale = Vector3.one * scale;
            RecenterChildToRoot(art);
        }

        private static void RecenterChildToRoot(GameObject art)
        {
            Bounds bounds = GetCombinedRendererBounds(art);
            Vector3 adjustment = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            art.transform.localPosition += adjustment;
        }

        private static void AddDockAnchors(Transform root, Bounds artBounds)
        {
            float anchorHeight = artBounds.max.y + 0.12f;
            for (int slotIndex = 0; slotIndex < 7; slotIndex++)
            {
                GameObject anchor = new GameObject($"Slot_{slotIndex:00}");
                anchor.transform.SetParent(root, false);
                anchor.transform.localPosition = new Vector3(-3.0f + slotIndex, anchorHeight, 0.0f);
                anchor.transform.localRotation = Quaternion.identity;
                anchor.transform.localScale = Vector3.one;
            }
        }

        private static Mesh? LoadFirstMesh(string assetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Mesh mesh)
                {
                    return mesh;
                }
            }

            return null;
        }

        public static Bounds GetCombinedRendererBounds(GameObject root)
        {
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            bool hasBounds = false;
            Bounds combined = default;
            Matrix4x4 worldToRoot = root.transform.worldToLocalMatrix;

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                Mesh? mesh = meshFilter.sharedMesh;
                if (mesh is null)
                {
                    continue;
                }

                Bounds transformedBounds = TransformBounds(mesh.bounds, worldToRoot * meshFilter.transform.localToWorldMatrix);
                if (!hasBounds)
                {
                    combined = transformedBounds;
                    hasBounds = true;
                    continue;
                }

                combined.Encapsulate(transformedBounds.min);
                combined.Encapsulate(transformedBounds.max);
            }

            if (hasBounds)
            {
                return combined;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            Bounds fallback = new Bounds(worldToRoot.MultiplyPoint3x4(renderers[0].bounds.center), Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                EncapsulateWorldBounds(ref fallback, renderers[i].bounds, worldToRoot);
            }

            return fallback;
        }

        public static float CalculateFootprint(Bounds bounds)
        {
            return Mathf.Max(bounds.size.x, bounds.size.z);
        }

        private static Bounds TransformBounds(Bounds sourceBounds, Matrix4x4 matrix)
        {
            Vector3 extents = sourceBounds.extents;
            Vector3[] corners =
            {
                sourceBounds.center + new Vector3( extents.x,  extents.y,  extents.z),
                sourceBounds.center + new Vector3( extents.x,  extents.y, -extents.z),
                sourceBounds.center + new Vector3( extents.x, -extents.y,  extents.z),
                sourceBounds.center + new Vector3( extents.x, -extents.y, -extents.z),
                sourceBounds.center + new Vector3(-extents.x,  extents.y,  extents.z),
                sourceBounds.center + new Vector3(-extents.x,  extents.y, -extents.z),
                sourceBounds.center + new Vector3(-extents.x, -extents.y,  extents.z),
                sourceBounds.center + new Vector3(-extents.x, -extents.y, -extents.z),
            };

            Bounds transformed = new Bounds(matrix.MultiplyPoint3x4(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
            {
                transformed.Encapsulate(matrix.MultiplyPoint3x4(corners[i]));
            }

            return transformed;
        }

        private static void EncapsulateWorldBounds(ref Bounds combined, Bounds worldBounds, Matrix4x4 worldToLocal)
        {
            Vector3 extents = worldBounds.extents;
            Vector3[] corners =
            {
                worldBounds.center + new Vector3( extents.x,  extents.y,  extents.z),
                worldBounds.center + new Vector3( extents.x,  extents.y, -extents.z),
                worldBounds.center + new Vector3( extents.x, -extents.y,  extents.z),
                worldBounds.center + new Vector3( extents.x, -extents.y, -extents.z),
                worldBounds.center + new Vector3(-extents.x,  extents.y,  extents.z),
                worldBounds.center + new Vector3(-extents.x,  extents.y, -extents.z),
                worldBounds.center + new Vector3(-extents.x, -extents.y,  extents.z),
                worldBounds.center + new Vector3(-extents.x, -extents.y, -extents.z),
            };

            for (int i = 0; i < corners.Length; i++)
            {
                combined.Encapsulate(worldToLocal.MultiplyPoint3x4(corners[i]));
            }
        }

        private static void RemoveCollidersRecursively(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(colliders[i]);
            }
        }

        private static void AssignMaterialRecursively(GameObject root, Material material)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = Enumerable.Repeat(material, renderers[i].sharedMaterials.Length == 0 ? 1 : renderers[i].sharedMaterials.Length).ToArray();
                renderers[i].sharedMaterials = materials;
            }
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

        private static GameObject CreateFlatOverlayPlaceholder(string name, Material material, Vector3 localScale, float yOffset = 0.02f)
        {
            GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = name;
            overlay.transform.position = new Vector3(0.0f, yOffset, 0.0f);
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
            dock.transform.localScale = new Vector3(7.2f, 0.60f, 1.1f);
            RemoveCollider(dock);
            AssignMaterial(dock, material);

            for (int slotIndex = 0; slotIndex < 7; slotIndex++)
            {
                GameObject anchor = new GameObject($"Slot_{slotIndex:00}");
                anchor.transform.SetParent(dock.transform, false);
                anchor.transform.localPosition = new Vector3(-3.0f + slotIndex, 0.48f, 0.0f);
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

        private sealed class PlaceholderAssets
        {
            public PlaceholderAssets(
                GameObject dryTilePrefab,
                GameObject debrisAPrefab,
                GameObject debrisBPrefab,
                GameObject debrisCPrefab,
                GameObject debrisDPrefab,
                GameObject debrisEPrefab,
                GameObject cratePrefab,
                GameObject icePrefab,
                GameObject vinePrefab,
                GameObject puppyTargetPrefab,
                GameObject dockPrefab,
                Material dockSafeMaterial,
                Material dockCautionMaterial,
                Material dockAcuteMaterial,
                Material dockFailedMaterial,
                GameObject floodedRowOverlayPrefab,
                GameObject forecastRowOverlayPrefab,
                GameObject waterlinePrefab,
                GameObject groupClearFxPrefab,
                GameObject invalidTapFxPrefab,
                GameObject crateBreakFxPrefab,
                GameObject iceRevealFxPrefab,
                GameObject vineClearFxPrefab,
                GameObject vineGrowPreviewFxPrefab,
                GameObject dockInsertFxPrefab,
                GameObject dockTripleClearFxPrefab,
                GameObject waterRiseFxPrefab,
                GameObject targetExtractionFxPrefab,
                GameObject nearRescueReliefFxPrefab,
                GameObject winFxPrefab,
                GameObject lossFxPrefab)
            {
                DryTilePrefab = dryTilePrefab;
                DebrisAPrefab = debrisAPrefab;
                DebrisBPrefab = debrisBPrefab;
                DebrisCPrefab = debrisCPrefab;
                DebrisDPrefab = debrisDPrefab;
                DebrisEPrefab = debrisEPrefab;
                CratePrefab = cratePrefab;
                IcePrefab = icePrefab;
                VinePrefab = vinePrefab;
                PuppyTargetPrefab = puppyTargetPrefab;
                DockPrefab = dockPrefab;
                DockSafeMaterial = dockSafeMaterial;
                DockCautionMaterial = dockCautionMaterial;
                DockAcuteMaterial = dockAcuteMaterial;
                DockFailedMaterial = dockFailedMaterial;
                FloodedRowOverlayPrefab = floodedRowOverlayPrefab;
                ForecastRowOverlayPrefab = forecastRowOverlayPrefab;
                WaterlinePrefab = waterlinePrefab;
                GroupClearFxPrefab = groupClearFxPrefab;
                InvalidTapFxPrefab = invalidTapFxPrefab;
                CrateBreakFxPrefab = crateBreakFxPrefab;
                IceRevealFxPrefab = iceRevealFxPrefab;
                VineClearFxPrefab = vineClearFxPrefab;
                VineGrowPreviewFxPrefab = vineGrowPreviewFxPrefab;
                DockInsertFxPrefab = dockInsertFxPrefab;
                DockTripleClearFxPrefab = dockTripleClearFxPrefab;
                WaterRiseFxPrefab = waterRiseFxPrefab;
                TargetExtractionFxPrefab = targetExtractionFxPrefab;
                NearRescueReliefFxPrefab = nearRescueReliefFxPrefab;
                WinFxPrefab = winFxPrefab;
                LossFxPrefab = lossFxPrefab;
            }

            public GameObject DryTilePrefab { get; }
            public GameObject DebrisAPrefab { get; }
            public GameObject DebrisBPrefab { get; }
            public GameObject DebrisCPrefab { get; }
            public GameObject DebrisDPrefab { get; }
            public GameObject DebrisEPrefab { get; }
            public GameObject CratePrefab { get; }
            public GameObject IcePrefab { get; }
            public GameObject VinePrefab { get; }
            public GameObject PuppyTargetPrefab { get; }
            public GameObject DockPrefab { get; }
            public Material DockSafeMaterial { get; }
            public Material DockCautionMaterial { get; }
            public Material DockAcuteMaterial { get; }
            public Material DockFailedMaterial { get; }
            public GameObject FloodedRowOverlayPrefab { get; }
            public GameObject ForecastRowOverlayPrefab { get; }
            public GameObject WaterlinePrefab { get; }
            public GameObject GroupClearFxPrefab { get; }
            public GameObject InvalidTapFxPrefab { get; }
            public GameObject CrateBreakFxPrefab { get; }
            public GameObject IceRevealFxPrefab { get; }
            public GameObject VineClearFxPrefab { get; }
            public GameObject VineGrowPreviewFxPrefab { get; }
            public GameObject DockInsertFxPrefab { get; }
            public GameObject DockTripleClearFxPrefab { get; }
            public GameObject WaterRiseFxPrefab { get; }
            public GameObject TargetExtractionFxPrefab { get; }
            public GameObject NearRescueReliefFxPrefab { get; }
            public GameObject WinFxPrefab { get; }
            public GameObject LossFxPrefab { get; }
        }

        private sealed class ProductionAssets
        {
            public ProductionAssets(
                GameObject? tilePrefab,
                GameObject? debrisAPrefab,
                GameObject? debrisBPrefab,
                GameObject? debrisCPrefab,
                GameObject? debrisDPrefab,
                GameObject? debrisEPrefab,
                GameObject? cratePrefab,
                GameObject icePrefab,
                GameObject vinePrefab,
                GameObject? puppyPrefab,
                GameObject? floodedRowOverlayPrefab,
                GameObject? iceRevealFxPrefab,
                GameObject? sharedDockPrefab,
                GameObject? safeDockPrefab,
                GameObject? cautionDockPrefab,
                GameObject? acuteDockPrefab,
                GameObject? failedDockPrefab,
                Material safeDockMaterial,
                Material cautionDockMaterial,
                Material acuteDockMaterial,
                Material failedDockMaterial,
                Mesh? sharedDockMesh)
            {
                TilePrefab = tilePrefab;
                DebrisAPrefab = debrisAPrefab;
                DebrisBPrefab = debrisBPrefab;
                DebrisCPrefab = debrisCPrefab;
                DebrisDPrefab = debrisDPrefab;
                DebrisEPrefab = debrisEPrefab;
                CratePrefab = cratePrefab;
                IcePrefab = icePrefab;
                VinePrefab = vinePrefab;
                PuppyPrefab = puppyPrefab;
                FloodedRowOverlayPrefab = floodedRowOverlayPrefab;
                IceRevealFxPrefab = iceRevealFxPrefab;
                SharedDockPrefab = sharedDockPrefab;
                SafeDockPrefab = safeDockPrefab;
                CautionDockPrefab = cautionDockPrefab;
                AcuteDockPrefab = acuteDockPrefab;
                FailedDockPrefab = failedDockPrefab;
                SafeDockMaterial = safeDockMaterial;
                CautionDockMaterial = cautionDockMaterial;
                AcuteDockMaterial = acuteDockMaterial;
                FailedDockMaterial = failedDockMaterial;
                SharedDockMesh = sharedDockMesh;
            }

            public GameObject? TilePrefab { get; }
            public GameObject? DebrisAPrefab { get; }
            public GameObject? DebrisBPrefab { get; }
            public GameObject? DebrisCPrefab { get; }
            public GameObject? DebrisDPrefab { get; }
            public GameObject? DebrisEPrefab { get; }
            public GameObject? CratePrefab { get; }
            public GameObject IcePrefab { get; }
            public GameObject VinePrefab { get; }
            public GameObject? PuppyPrefab { get; }
            public GameObject? FloodedRowOverlayPrefab { get; }
            public GameObject? IceRevealFxPrefab { get; }
            public GameObject? SharedDockPrefab { get; }
            public GameObject? SafeDockPrefab { get; }
            public GameObject? CautionDockPrefab { get; }
            public GameObject? AcuteDockPrefab { get; }
            public GameObject? FailedDockPrefab { get; }
            public Material SafeDockMaterial { get; }
            public Material CautionDockMaterial { get; }
            public Material AcuteDockMaterial { get; }
            public Material FailedDockMaterial { get; }
            public Mesh? SharedDockMesh { get; }
        }

        private readonly struct AssetSizingProfile
        {
            public AssetSizingProfile(float targetFootprint, float uniformScaleMultiplier = 1.0f, Vector3? localPositionOffset = null)
            {
                TargetFootprint = targetFootprint;
                UniformScaleMultiplier = uniformScaleMultiplier;
                LocalPositionOffset = localPositionOffset ?? Vector3.zero;
            }

            public float TargetFootprint { get; }

            public float UniformScaleMultiplier { get; }

            public Vector3 LocalPositionOffset { get; }
        }
    }
}
