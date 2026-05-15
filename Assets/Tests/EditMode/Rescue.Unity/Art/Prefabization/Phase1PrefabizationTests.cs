using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.EditorTools.Art.Prefabs;
using Rescue.Unity.EditorTools.Diagnostics;
using Rescue.Unity.FX;
using Rescue.Unity.Presentation.Targets;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Rescue.Unity.Art.Tests
{
    public sealed class Phase1PrefabizationTests
    {
        private const string ArtRootPath = Phase1PlaceholderPrefabFactory.DefaultArtRootPath;
        private const string Phase1PrefabsPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1";
        private const string Phase1DockPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/Dock/Dock_Shared_7Slot_Phase1.prefab";
        private const string DirectDryTilePrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/Board/DryTile_Phase1.prefab";
        private const string Phase1DebrisAPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/Pieces/Debris_A_Phase1.prefab";
        private const string Phase1DebrisCPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/Pieces/Debris_C_Phase1.prefab";
        private const string Phase1DebrisDPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/Pieces/Debris_D_Phase1.prefab";
        private const string Phase1IceBlockPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/Blockers/Ice_Block_Phase1.prefab";
        private const string Phase1VinePrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/Blockers/Vine_Phase1.prefab";
        private const string Phase1VineOverlayPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/Blockers/Vine_Overlay_Phase1.prefab";
        private const string Phase1FloodedRowPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/Water/FloodedRowOverlay_Phase1.prefab";
        private const string Phase1IceRevealFxPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/FX/IceRevealFx_Phase1.prefab";
        private const string Phase1VineGrowPreviewFxPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/FX/VineGrowPreviewFx.prefab";
        private const string Phase1DockInsertFxPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/FX/DockInsertFx.prefab";
        private const string DaisyTargetPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Targets/PF_Target_Daisy_Puppy.prefab";
        private const string DaisyAnimatorControllerPath = "Assets/Rescue.Unity/Art/Animation/Targets/Daisy/AC_Daisy_Target.controller";
        private const string WaterRiseFourthFramePath = "Assets/Rescue.Unity/Art/Sprites/WaterRiseFx_04.png";
        private const string WaterFloodedMaterialPath = "Assets/Rescue.Unity/Art/Materials/Water_Flooded.mat";
        private const string WaterForecastMaterialPath = "Assets/Rescue.Unity/Art/Materials/Water_Forecast.mat";
        private const string Phase1FloodedRowMaterialPath = "Assets/Rescue.Unity/Art/Materials/Phase1/Water_Flooded_Row_Phase1.mat";
        private const string TileRegistryPath = "Assets/Rescue.Unity/Art/Registries/Phase1TileVisualRegistry.asset";
        private const string PieceRegistryPath = "Assets/Rescue.Unity/Art/Registries/Phase1PieceVisualRegistry.asset";
        private const string BlockerRegistryPath = "Assets/Rescue.Unity/Art/Registries/Phase1BlockerVisualRegistry.asset";
        private const string TargetRegistryPath = "Assets/Rescue.Unity/Art/Registries/Phase1TargetVisualRegistry.asset";
        private const string DockConfigPath = "Assets/Rescue.Unity/Art/Registries/Phase1DockVisualConfig.asset";
        private const string FxRegistryPath = "Assets/Rescue.Unity/Art/Registries/Phase1FxVisualRegistry.asset";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Phase1PlaceholderPrefabFactory.CreateAll(ArtRootPath);
        }

        [Test]
        public void RealDockPrefab_HasSevenSlotAnchors()
        {
            GameObject dockRoot = PrefabUtility.LoadPrefabContents(Phase1DockPrefabPath);

            try
            {
                List<string> slotAnchors = dockRoot.transform
                    .Cast<Transform>()
                    .Where(child => child.name.StartsWith("Slot_", System.StringComparison.Ordinal))
                    .Select(child => child.name)
                    .OrderBy(name => name)
                    .ToList();

                Assert.That(slotAnchors, Is.EqualTo(new[]
                {
                    "Slot_00",
                    "Slot_01",
                    "Slot_02",
                    "Slot_03",
                    "Slot_04",
                    "Slot_05",
                    "Slot_06",
                }));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(dockRoot);
            }
        }

        [Test]
        public void Phase1Registries_HaveFallbacksAssigned()
        {
            TileVisualRegistry tileRegistry = LoadAsset<TileVisualRegistry>(TileRegistryPath);
            PieceVisualRegistry pieceRegistry = LoadAsset<PieceVisualRegistry>(PieceRegistryPath);
            BlockerVisualRegistry blockerRegistry = LoadAsset<BlockerVisualRegistry>(BlockerRegistryPath);
            TargetVisualRegistry targetRegistry = LoadAsset<TargetVisualRegistry>(TargetRegistryPath);

            Assert.That(tileRegistry.FallbackTilePrefab, Is.Not.Null);
            Assert.That(tileRegistry.WaterlinePrefab, Is.Null);
            Assert.That(pieceRegistry.FallbackPrefab, Is.Not.Null);
            Assert.That(blockerRegistry.FallbackBlockerPrefab, Is.Not.Null);
            Assert.That(targetRegistry.FallbackTargetPrefab, Is.Not.Null);
        }

        [Test]
        public void Phase1DockConfig_HasFourStateMaterials()
        {
            DockVisualConfig config = LoadAsset<DockVisualConfig>(DockConfigPath);

            Assert.That(config.SafeMaterial, Is.Not.Null);
            Assert.That(config.CautionMaterial, Is.Not.Null);
            Assert.That(config.AcuteMaterial, Is.Not.Null);
            Assert.That(config.FailedMaterial, Is.Not.Null);
        }

        [Test]
        public void Phase1TileRegistry_UsesDirectDryTileAsCanonicalEntry()
        {
            TileVisualRegistry tileRegistry = LoadAsset<TileVisualRegistry>(TileRegistryPath);
            GameObject directDryTile = LoadAsset<GameObject>(DirectDryTilePrefabPath);

            Assert.That(tileRegistry.DryTilePrefab, Is.SameAs(directDryTile));
            Assert.That(tileRegistry.GetDryTilePrefab(), Is.SameAs(directDryTile));
        }

        [Test]
        public void Phase1DockConfig_UsesSharedPhase1DockPrefab()
        {
            DockVisualConfig config = LoadAsset<DockVisualConfig>(DockConfigPath);
            GameObject sharedDockPrefab = LoadAsset<GameObject>(Phase1DockPrefabPath);

            Assert.That(config.SharedDockPrefab, Is.SameAs(sharedDockPrefab));
            Assert.That(config.GetSharedDockPrefab(), Is.SameAs(sharedDockPrefab));
        }

        [Test]
        public void Phase1DryTilePrefabWrapper_DoesNotStackExtraRootRotation()
        {
            GameObject phase1DockPrefab = LoadAsset<GameObject>(Phase1DockPrefabPath);
            GameObject phase1DryTilePrefab = LoadAsset<GameObject>(DirectDryTilePrefabPath);

            Assert.That(phase1DryTilePrefab.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(phase1DockPrefab.transform.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void Phase1PieceRegistry_HasFiveDebrisEntriesOrFallbacks()
        {
            PieceVisualRegistry registry = LoadAsset<PieceVisualRegistry>(PieceRegistryPath);

            Assert.That(registry.GetPrefab(DebrisType.A), Is.Not.Null);
            Assert.That(registry.GetPrefab(DebrisType.B), Is.Not.Null);
            Assert.That(registry.GetPrefab(DebrisType.C), Is.Not.Null);
            Assert.That(registry.GetPrefab(DebrisType.D), Is.Not.Null);
            Assert.That(registry.GetPrefab(DebrisType.E), Is.Not.Null);
        }

        [Test]
        public void Phase1PieceRegistry_UsesRequestedDockPoseOverrides()
        {
            PieceVisualRegistry registry = LoadAsset<PieceVisualRegistry>(PieceRegistryPath);

            Assert.That(registry.GetDockScaleMultiplier(DebrisType.A), Is.EqualTo(0.8f));
            Assert.That(registry.GetDockScaleMultiplier(DebrisType.B), Is.EqualTo(0.8f));
            Assert.That(registry.GetDockScaleMultiplier(DebrisType.C), Is.EqualTo(0.8f));
            Assert.That(registry.GetDockScaleMultiplier(DebrisType.D), Is.EqualTo(0.8f));
            Assert.That(registry.GetDockScaleMultiplier(DebrisType.E), Is.EqualTo(0.8f));
            Assert.That(registry.GetDockScaleMultiplier(DebrisType.F), Is.EqualTo(0.8f));
            Assert.That(registry.GetBoardScaleMultiplier(DebrisType.A), Is.EqualTo(1f));
            Assert.That(registry.GetBoardScaleMultiplier(DebrisType.D), Is.EqualTo(1.15f));
            Assert.That(registry.GetDockEulerOffset(DebrisType.A), Is.EqualTo(new Vector3(0f, 180f, 0f)));
            Assert.That(registry.GetDockEulerOffset(DebrisType.D), Is.EqualTo(new Vector3(0f, 270f, 0f)));
            Assert.That(registry.GetDockEulerOffset(DebrisType.F), Is.EqualTo(new Vector3(0f, 90f, 0f)));
        }

        [Test]
        public void Phase1VisualPrefabs_UseRequestedRootPoses()
        {
            GameObject debrisA = LoadAsset<GameObject>(Phase1DebrisAPrefabPath);
            GameObject debrisC = LoadAsset<GameObject>(Phase1DebrisCPrefabPath);
            GameObject debrisD = LoadAsset<GameObject>(Phase1DebrisDPrefabPath);
            GameObject iceReveal = LoadAsset<GameObject>(Phase1IceRevealFxPrefabPath);
            GameObject vineGrowPreview = LoadAsset<GameObject>(Phase1VineGrowPreviewFxPrefabPath);
            GameObject dockInsert = LoadAsset<GameObject>(Phase1DockInsertFxPrefabPath);

            Assert.That(Quaternion.Angle(debrisA.transform.localRotation, Quaternion.Euler(0f, -120f, 0f)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(debrisC.transform.localRotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(debrisD.transform.localRotation, Quaternion.Euler(0f, 220f, 0f)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(iceReveal.transform.localRotation, Quaternion.Euler(180f, 0f, 0f)), Is.LessThan(0.001f));
            Assert.That(iceReveal.transform.localPosition.z, Is.EqualTo(-0.5f).Within(0.001f));
            Assert.That(iceReveal.transform.localScale, Is.EqualTo(Vector3.one * 0.5f));
            Assert.That(vineGrowPreview.transform.localScale, Is.EqualTo(Vector3.one * 0.3f));
            Assert.That(dockInsert.transform.localScale, Is.EqualTo(Vector3.one * 0.4f));
            Assert.That(Quaternion.Angle(dockInsert.transform.localRotation, Quaternion.Euler(0f, 0f, 90f)), Is.LessThan(0.001f));
        }

        [Test]
        public void WaterRowMaterials_UseFourthWaterRiseFrameTexture()
        {
            Texture2D waterRiseFrame = LoadAsset<Texture2D>(WaterRiseFourthFramePath);

            AssertWaterRiseCrop(LoadAsset<Material>(WaterFloodedMaterialPath), waterRiseFrame);
            AssertWaterRiseCrop(LoadAsset<Material>(WaterForecastMaterialPath), waterRiseFrame);
            AssertWaterRiseCrop(LoadAsset<Material>(Phase1FloodedRowMaterialPath), waterRiseFrame);
        }

        [Test]
        public void Phase1Factory_CreatesNewMeshyProductionPrefabs()
        {
            Assert.That(LoadAsset<GameObject>(Phase1IceBlockPrefabPath), Is.Not.Null);
            Assert.That(LoadAsset<GameObject>(Phase1VinePrefabPath), Is.Not.Null);
            Assert.That(LoadAsset<GameObject>(Phase1VineOverlayPrefabPath), Is.Not.Null);
            Assert.That(LoadAsset<GameObject>(Phase1FloodedRowPrefabPath), Is.Not.Null);
            Assert.That(LoadAsset<GameObject>(Phase1IceRevealFxPrefabPath), Is.Not.Null);
        }

        [Test]
        public void Phase1Registries_UseNewMeshyProductionPrefabs()
        {
            BlockerVisualRegistry blockerRegistry = LoadAsset<BlockerVisualRegistry>(BlockerRegistryPath);
            TileVisualRegistry tileRegistry = LoadAsset<TileVisualRegistry>(TileRegistryPath);
            FxVisualRegistry fxRegistry = LoadAsset<FxVisualRegistry>(FxRegistryPath);

            Assert.That(blockerRegistry.GetPrefab(BlockerType.Ice), Is.SameAs(LoadAsset<GameObject>(Phase1IceBlockPrefabPath)));
            Assert.That(blockerRegistry.GetPrefab(BlockerType.Vine), Is.SameAs(LoadAsset<GameObject>(Phase1VinePrefabPath)));
            Assert.That(blockerRegistry.VineOverlayPrefab, Is.SameAs(LoadAsset<GameObject>(Phase1VineOverlayPrefabPath)));
            Assert.That(tileRegistry.GetFloodedRowOverlayPrefab(), Is.SameAs(LoadAsset<GameObject>(Phase1FloodedRowPrefabPath)));
            Assert.That(fxRegistry.IceRevealFx, Is.SameAs(LoadAsset<GameObject>(Phase1IceRevealFxPrefabPath)));
        }

        [Test]
        public void DaisyTargetPrefab_IsAnimatorBackedAndWiredToRegistry()
        {
            GameObject daisyPrefab = LoadAsset<GameObject>(DaisyTargetPrefabPath);
            AnimatorController controller = LoadAsset<AnimatorController>(DaisyAnimatorControllerPath);
            TargetVisualRegistry targetRegistry = LoadAsset<TargetVisualRegistry>(TargetRegistryPath);

            Assert.That(targetRegistry.PuppyPrefab, Is.SameAs(daisyPrefab));
            Assert.That(targetRegistry.FallbackTargetPrefab, Is.SameAs(daisyPrefab));

            Animator? animator = daisyPrefab.GetComponentInChildren<Animator>(true);
            TargetPuppyAnimator? puppyAnimator = daisyPrefab.GetComponent<TargetPuppyAnimator>();
            TargetPuppyLookAt? puppyLookAt = daisyPrefab.GetComponent<TargetPuppyLookAt>();

            Assert.That(animator, Is.Not.Null);
            Assert.That(puppyAnimator, Is.Not.Null);
            Assert.That(puppyLookAt, Is.Not.Null);
            if (animator is null || puppyAnimator is null || puppyLookAt is null)
            {
                throw new AssertionException("Daisy prefab should include animator, puppy animator, and look-at components.");
            }

            Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
            Assert.That(animator.applyRootMotion, Is.False);
            Transform? visual = daisyPrefab.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null);
            if (visual is null)
            {
                throw new AssertionException("Daisy prefab should include a Visual child.");
            }

            Assert.That(visual.localScale, Is.EqualTo(Vector3.one * 3.4417312f));

            SerializedObject serializedAnimator = new SerializedObject(puppyAnimator);
            Assert.That(serializedAnimator.FindProperty("animator").objectReferenceValue, Is.SameAs(animator));
            AssertSerializedString(serializedAnimator, "trappedIdleState", "Target_Trapped_Idle");
            AssertSerializedString(serializedAnimator, "progressingIdleState", "Target_Progress_Idle");
            AssertSerializedString(serializedAnimator, "oneClearAwayIdleState", "Target_OneClearAway_Idle");
            AssertSerializedString(serializedAnimator, "extractStartState", "Target_Extract_Start");
            AssertSerializedString(serializedAnimator, "extractAirState", "Target_Extract_Air");
            AssertSerializedString(serializedAnimator, "progressingFidgetState", "Target_Progress_Fidget");
            AssertSerializedString(serializedAnimator, "oneClearAwayBarkState", "Target_OneClearAway_Bark");

            SerializedObject serializedLookAt = new SerializedObject(puppyLookAt);
            AssertSerializedTransformName(serializedLookAt, "headBone", "head");
            AssertSerializedTransformName(serializedLookAt, "neckBone", "neck");
            AssertSerializedTransformName(serializedLookAt, "lookSpace", "Visual");
            Assert.That(serializedLookAt.FindProperty("lookTarget").objectReferenceValue, Is.Null);
            Assert.That(serializedLookAt.FindProperty("forcedMaxYawDegrees").floatValue, Is.EqualTo(80f).Within(0.001f));
            Assert.That(serializedLookAt.FindProperty("forcedMaxPitchDegrees").floatValue, Is.EqualTo(42f).Within(0.001f));
            Assert.That(serializedLookAt.FindProperty("forcedHeadEulerOffset").vector3Value, Is.EqualTo(new Vector3(-34f, 22f, 0f)));
            Assert.That(serializedLookAt.FindProperty("forcedNeckEulerOffset").vector3Value, Is.EqualTo(new Vector3(-10f, 8f, 0f)));

            string[] controllerStateNames = controller.layers[0].stateMachine.states
                .Select(state => state.state.name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(controllerStateNames, Does.Contain("Target_Trapped_Idle"));
            Assert.That(controllerStateNames, Does.Contain("Target_Progress_Idle"));
            Assert.That(controllerStateNames, Does.Contain("Target_OneClearAway_Idle"));
            Assert.That(controllerStateNames, Does.Contain("Target_Extract_Start"));
            Assert.That(controllerStateNames, Does.Contain("Target_Extract_Air"));
            Assert.That(controllerStateNames, Does.Contain("Target_Progress_Fidget"));
            Assert.That(controllerStateNames, Does.Contain("Target_OneClearAway_Bark"));
        }

        [Test]
        public void DaisyTargetPrefab_FitsBoardCellFootprintAfterRuntimeCentering()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(DaisyTargetPrefabPath);

            try
            {
                Bounds localBounds = CalculateLocalRendererBounds(prefabRoot);
                string summary = $"center={localBounds.center}, size={localBounds.size}";

                Assert.That(localBounds.size.x, Is.LessThanOrEqualTo(1.56f), summary);
                Assert.That(localBounds.size.z, Is.LessThanOrEqualTo(1.56f), summary);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [Test]
        public void Phase1FxRegistry_AssignsFourFrameTransientIceRevealFx()
        {
            FxVisualRegistry fxRegistry = LoadAsset<FxVisualRegistry>(FxRegistryPath);
            GameObject iceReveal = LoadAsset<GameObject>(Phase1IceRevealFxPrefabPath);

            Assert.That(fxRegistry.IceRevealFx, Is.SameAs(iceReveal));
            Assert.That(iceReveal.GetComponent<MeshRenderer>(), Is.Null, "Ice reveal FX should not be a tile-like mesh.");
            Assert.That(iceReveal.GetComponent<Collider>(), Is.Null, "Ice reveal FX should not be tappable board content.");
            SpriteRenderer? renderer = iceReveal.GetComponent<SpriteRenderer>();
            SpriteSequenceFxPlayer? player = iceReveal.GetComponent<SpriteSequenceFxPlayer>();

            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(player.FrameCount, Is.EqualTo(4));
            Assert.That(player.DestroyAfterPlayback, Is.True);
            Assert.That(GetSerializedBool(player, "loop"), Is.False);
        }

        [Test]
        public void Phase1FxRegistry_HasAllRuntimeFxAssigned()
        {
            FxVisualRegistry fxRegistry = LoadAsset<FxVisualRegistry>(FxRegistryPath);

            AssertFxPrefab(fxRegistry.GroupClearFx, nameof(FxVisualRegistry.GroupClearFx));
            AssertFxPrefab(fxRegistry.InvalidTapFx, nameof(FxVisualRegistry.InvalidTapFx));
            AssertFxPrefab(fxRegistry.CrateBreakFx, nameof(FxVisualRegistry.CrateBreakFx));
            AssertFxPrefab(fxRegistry.IceRevealFx, nameof(FxVisualRegistry.IceRevealFx));
            AssertFxPrefab(fxRegistry.VineClearFx, nameof(FxVisualRegistry.VineClearFx));
            AssertFxPrefab(fxRegistry.VineGrowPreviewFx, nameof(FxVisualRegistry.VineGrowPreviewFx));
            AssertFxPrefab(fxRegistry.DockInsertFx, nameof(FxVisualRegistry.DockInsertFx));
            AssertFxPrefab(fxRegistry.DockTripleClearFx, nameof(FxVisualRegistry.DockTripleClearFx));
            AssertFxPrefab(fxRegistry.WaterRiseFx, nameof(FxVisualRegistry.WaterRiseFx));
            AssertFxPrefab(fxRegistry.TargetExtractionFx, nameof(FxVisualRegistry.TargetExtractionFx));
            AssertFxPrefab(fxRegistry.NearRescueReliefFx, nameof(FxVisualRegistry.NearRescueReliefFx));
            AssertFxPrefab(fxRegistry.WinFx, nameof(FxVisualRegistry.WinFx));
            AssertFxPrefab(fxRegistry.LossFx, nameof(FxVisualRegistry.LossFx));
        }

        [Test]
        public void RealPrefabs_DoNotReferenceMissingMaterials()
        {
            List<string> prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { Phase1PrefabsPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();
            Assert.That(prefabPaths.Count, Is.GreaterThan(0));
            if (!prefabPaths.Contains(DaisyTargetPrefabPath))
            {
                prefabPaths.Add(DaisyTargetPrefabPath);
            }

            for (int prefabIndex = 0; prefabIndex < prefabPaths.Count; prefabIndex++)
            {
                string prefabPath = prefabPaths[prefabIndex];
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                try
                {
                    Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        Material[] materials = renderers[rendererIndex].sharedMaterials;
                        Assert.That(materials.Length, Is.GreaterThan(0), $"{prefabPath} has a renderer with no shared materials.");
                        for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                        {
                            Assert.That(materials[materialIndex], Is.Not.Null, $"{prefabPath} has a missing material reference.");
                            Assert.That(materials[materialIndex].shader, Is.Not.Null, $"{prefabPath} has a material with a missing shader.");
                            Assert.That(
                                materials[materialIndex].shader.name,
                                Does.Not.Contain("InternalErrorShader"),
                                $"{prefabPath} has a material that would render as Unity missing-material pink.");
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        [Test]
        public void Phase1DryTilePrefab_FillsOneCellWidthWithinTolerance()
        {
            BoardAssetSpacingDiagnostics.AssetSpacingReport report = BoardAssetSpacingDiagnostics.AnalyzePrefabAsset(
                "Dry tile",
                DirectDryTilePrefabPath,
                Phase1PlaceholderPrefabFactory.DefaultBoardCellSize);

            Assert.That(report.FootprintX, Is.EqualTo(Phase1PlaceholderPrefabFactory.DefaultBoardCellSize).Within(0.08f));
            Assert.That(report.FootprintZ, Is.EqualTo(Phase1PlaceholderPrefabFactory.DefaultBoardCellSize).Within(0.08f));
            Assert.That(report.Verdict, Is.EqualTo("within tolerance"));
        }

        [Test]
        public void RepresentativePhase1Prefabs_MeetMinimumFillRatio()
        {
            IReadOnlyList<BoardAssetSpacingDiagnostics.AssetSpacingReport> reports = BoardAssetSpacingDiagnostics.AnalyzeRepresentativePhase1Prefabs();

            Assert.That(reports.Count, Is.GreaterThanOrEqualTo(4));
            for (int i = 0; i < reports.Count; i++)
            {
                Assert.That(reports[i].FillRatio, Is.GreaterThanOrEqualTo(0.88f), $"{reports[i].Label} is underfilled.");
            }
        }

        [Test]
        public void Phase1DebrisAPrefab_MeetsBoardCellFillTolerance()
        {
            BoardAssetSpacingDiagnostics.AssetSpacingReport report = BoardAssetSpacingDiagnostics.AnalyzePrefabAsset(
                "Debris A",
                Phase1DebrisAPrefabPath,
                Phase1PlaceholderPrefabFactory.DefaultBoardCellSize);
            string reportSummary = $"fillRatio={report.FillRatio:0.000}, footprintX={report.FootprintX:0.000}, footprintZ={report.FootprintZ:0.000}, localBounds={report.LocalBoundsSize}, verdict={report.Verdict}";

            Assert.That(report.FillRatio, Is.GreaterThanOrEqualTo(0.88f), reportSummary);
            Assert.That(report.Verdict, Is.EqualTo("within tolerance"), reportSummary);
        }

        private static T LoadAsset<T>(string assetPath)
            where T : Object
        {
            T? asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            Assert.That(asset, Is.Not.Null, $"Expected asset at '{assetPath}'.");
            return asset ?? throw new AssertionException($"Expected asset at '{assetPath}'.");
        }

        private static Bounds CalculateLocalRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0), "Expected target prefab to include renderers.");

            Matrix4x4 worldToRoot = root.transform.worldToLocalMatrix;
            bool hasBounds = false;
            Bounds combined = default;

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Bounds rendererBounds = renderers[rendererIndex].bounds;
                Vector3 min = rendererBounds.min;
                Vector3 max = rendererBounds.max;
                Vector3[] corners =
                {
                    new Vector3(min.x, min.y, min.z),
                    new Vector3(min.x, min.y, max.z),
                    new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, max.y, max.z),
                    new Vector3(max.x, min.y, min.z),
                    new Vector3(max.x, min.y, max.z),
                    new Vector3(max.x, max.y, min.z),
                    new Vector3(max.x, max.y, max.z),
                };

                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    Vector3 localCorner = worldToRoot.MultiplyPoint3x4(corners[cornerIndex]);
                    if (!hasBounds)
                    {
                        combined = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        combined.Encapsulate(localCorner);
                    }
                }
            }

            return combined;
        }

        private static void AssertWaterRiseCrop(Material material, Texture2D waterRiseFrame)
        {
            Assert.That(material.mainTexture, Is.SameAs(waterRiseFrame));
            Assert.That(material.mainTextureScale.x, Is.EqualTo(0.954f).Within(0.001f));
            Assert.That(material.mainTextureScale.y, Is.EqualTo(0.543f).Within(0.001f));
            Assert.That(material.mainTextureOffset.x, Is.EqualTo(0.021f).Within(0.001f));
            Assert.That(material.mainTextureOffset.y, Is.EqualTo(0.224f).Within(0.001f));
        }

        private static bool GetSerializedBool(Object target, string propertyName)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Expected serialized property '{propertyName}'.");
            return property.boolValue;
        }

        private static void AssertSerializedString(SerializedObject serializedObject, string propertyName, string expected)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Expected serialized property '{propertyName}'.");
            Assert.That(property.stringValue, Is.EqualTo(expected));
        }

        private static void AssertSerializedTransformName(
            SerializedObject serializedObject,
            string propertyName,
            string expectedName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Expected serialized property '{propertyName}'.");
            Assert.That(property.objectReferenceValue, Is.TypeOf<Transform>());
            Assert.That(property.objectReferenceValue.name, Is.EqualTo(expectedName));
        }

        private static void AssertFxPrefab(GameObject? prefab, string registrySlotName)
        {
            Assert.That(prefab, Is.Not.Null, $"{registrySlotName} should be assigned in the Phase 1 FX registry.");
            if (prefab is null)
            {
                throw new AssertionException($"{registrySlotName} should be assigned in the Phase 1 FX registry.");
            }

            SpriteSequenceFxPlayer? player = prefab.GetComponent<SpriteSequenceFxPlayer>();
            SpriteRenderer? spriteRenderer = prefab.GetComponent<SpriteRenderer>();
            Renderer? renderer = prefab.GetComponentInChildren<Renderer>(true);

            Assert.That(
                player is not null || renderer is not null,
                Is.True,
                $"{registrySlotName} should include a runtime-visible sprite sequence or renderer.");
            if (player is not null)
            {
                Assert.That(spriteRenderer, Is.Not.Null, $"{registrySlotName} sprite-sequence FX should include a SpriteRenderer.");
                Assert.That(spriteRenderer?.sprite, Is.Not.Null, $"{registrySlotName} should have an initial visible sprite assigned.");
            }
        }
    }
}
