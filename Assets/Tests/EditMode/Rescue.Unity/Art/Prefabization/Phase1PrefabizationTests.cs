using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.EditorTools.Art.Prefabs;
using UnityEditor;
using UnityEngine;

namespace Rescue.Unity.Art.Tests
{
    public sealed class Phase1PrefabizationTests
    {
        private const string ArtRootPath = Phase1PlaceholderPrefabFactory.DefaultArtRootPath;
        private const string Phase1PrefabsPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1";
        private const string Phase1DockPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/Dock/Dock_Shared_7Slot_Phase1.prefab";
        private const string DirectDryTilePrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/Board/DryTile_Phase1.prefab";
        private const string TileRegistryPath = "Assets/Rescue.Unity/Art/Registries/Phase1TileVisualRegistry.asset";
        private const string PieceRegistryPath = "Assets/Rescue.Unity/Art/Registries/Phase1PieceVisualRegistry.asset";
        private const string BlockerRegistryPath = "Assets/Rescue.Unity/Art/Registries/Phase1BlockerVisualRegistry.asset";
        private const string TargetRegistryPath = "Assets/Rescue.Unity/Art/Registries/Phase1TargetVisualRegistry.asset";
        private const string DockConfigPath = "Assets/Rescue.Unity/Art/Registries/Phase1DockVisualConfig.asset";

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
        public void RealPrefabs_DoNotReferenceMissingMaterials()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { Phase1PrefabsPath });
            Assert.That(prefabGuids.Length, Is.GreaterThan(0));

            for (int prefabIndex = 0; prefabIndex < prefabGuids.Length; prefabIndex++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex]);
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
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static T LoadAsset<T>(string assetPath)
            where T : Object
        {
            T? asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            Assert.That(asset, Is.Not.Null, $"Expected asset at '{assetPath}'.");
            return asset ?? throw new AssertionException($"Expected asset at '{assetPath}'.");
        }
    }
}
