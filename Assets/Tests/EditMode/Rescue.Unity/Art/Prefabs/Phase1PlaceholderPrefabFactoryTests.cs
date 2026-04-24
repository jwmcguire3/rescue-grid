using NUnit.Framework;
using Rescue.Unity.EditorTools.Art.Prefabs;
using UnityEditor;
using UnityEngine;

namespace Rescue.Unity.Art.Tests
{
    public sealed class Phase1PlaceholderPrefabFactoryTests
    {
        private const string TempRootPath = "Assets/Tests/Temp/Phase1PlaceholderPrefabFactory";

        [SetUp]
        public void SetUp()
        {
            DeleteTempRoot();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTempRoot();
        }

        [Test]
        public void Phase1PrefabFactory_CreatesExpectedFolders()
        {
            Phase1PlaceholderPrefabFactory.CreateAll(TempRootPath);

            Assert.That(AssetDatabase.IsValidFolder($"{TempRootPath}/Prefabs/Board"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder($"{TempRootPath}/Prefabs/Pieces"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder($"{TempRootPath}/Prefabs/Blockers"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder($"{TempRootPath}/Prefabs/Targets"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder($"{TempRootPath}/Prefabs/Dock"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder($"{TempRootPath}/Prefabs/Water"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder($"{TempRootPath}/Prefabs/FX"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder($"{TempRootPath}/Materials"), Is.True);
        }

        [Test]
        public void Phase1PrefabFactory_CreatesDockWithSevenSlotAnchors()
        {
            Phase1PlaceholderPrefabFactory.CreateAll(TempRootPath);

            string dockPath = $"{TempRootPath}/Prefabs/Dock/Dock_Shared_7Slot.prefab";
            GameObject dockRoot = PrefabUtility.LoadPrefabContents(dockPath);

            try
            {
                Assert.That(dockRoot.transform.childCount, Is.EqualTo(7));

                for (int slotIndex = 0; slotIndex < 7; slotIndex++)
                {
                    string anchorName = $"Slot_{slotIndex:00}";
                    Transform? anchor = dockRoot.transform.Find(anchorName);
                    Assert.That(anchor, Is.Not.Null, $"Expected anchor '{anchorName}' to exist.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(dockRoot);
            }
        }

        [Test]
        public void Phase1PrefabFactory_CreatesAllDebrisPrefabs()
        {
            Phase1PlaceholderPrefabFactory.CreateAll(TempRootPath);

            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>($"{TempRootPath}/Prefabs/Pieces/Debris_A.prefab"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>($"{TempRootPath}/Prefabs/Pieces/Debris_B.prefab"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>($"{TempRootPath}/Prefabs/Pieces/Debris_C.prefab"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>($"{TempRootPath}/Prefabs/Pieces/Debris_D.prefab"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>($"{TempRootPath}/Prefabs/Pieces/Debris_E.prefab"), Is.Not.Null);
        }

        [Test]
        public void Phase1PrefabFactory_CreatesWaterPrefabs()
        {
            Phase1PlaceholderPrefabFactory.CreateAll(TempRootPath);

            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>($"{TempRootPath}/Prefabs/Water/FloodedRowOverlay.prefab"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>($"{TempRootPath}/Prefabs/Water/ForecastRowOverlay.prefab"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>($"{TempRootPath}/Prefabs/Water/Waterline.prefab"), Is.Not.Null);
        }

        [Test]
        public void Phase1PrefabFactory_IsIdempotent()
        {
            Phase1PlaceholderPrefabFactory.CreateAll(TempRootPath);

            int initialPrefabCount = AssetDatabase.FindAssets("t:Prefab", new[] { $"{TempRootPath}/Prefabs" }).Length;
            int initialMaterialCount = AssetDatabase.FindAssets("t:Material", new[] { $"{TempRootPath}/Materials" }).Length;
            int initialRegistryCount = AssetDatabase.FindAssets("t:ScriptableObject", new[] { $"{TempRootPath}/Registries" }).Length;

            Phase1PlaceholderPrefabFactory.CreateAll(TempRootPath);

            Assert.That(AssetDatabase.FindAssets("t:Prefab", new[] { $"{TempRootPath}/Prefabs" }).Length, Is.EqualTo(initialPrefabCount));
            Assert.That(AssetDatabase.FindAssets("t:Material", new[] { $"{TempRootPath}/Materials" }).Length, Is.EqualTo(initialMaterialCount));
            Assert.That(AssetDatabase.FindAssets("t:ScriptableObject", new[] { $"{TempRootPath}/Registries" }).Length, Is.EqualTo(initialRegistryCount));

            string dockPath = $"{TempRootPath}/Prefabs/Dock/Dock_Shared_7Slot.prefab";
            GameObject dockRoot = PrefabUtility.LoadPrefabContents(dockPath);

            try
            {
                Assert.That(dockRoot.transform.childCount, Is.EqualTo(7));
                for (int slotIndex = 0; slotIndex < 7; slotIndex++)
                {
                    string anchorName = $"Slot_{slotIndex:00}";
                    Transform? anchor = dockRoot.transform.Find(anchorName);
                    Assert.That(anchor, Is.Not.Null, $"Expected anchor '{anchorName}' to exist after rerun.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(dockRoot);
            }
        }

        private static void DeleteTempRoot()
        {
            if (AssetDatabase.DeleteAsset(TempRootPath))
            {
                AssetDatabase.Refresh();
            }
        }
    }
}
