using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rescue.Unity.Art.Tests
{
    public sealed class VisualRegistryTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] is not null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void PieceVisualRegistry_ReturnsAssignedPrefab()
        {
            PieceVisualRegistry registry = CreateScriptableObject<PieceVisualRegistry>();
            GameObject assignedPrefab = CreatePrefab("DebrisA");
            registry.DebrisAPrefab = assignedPrefab;

            GameObject? result = registry.GetPrefab(DebrisType.A);

            Assert.That(result, Is.SameAs(assignedPrefab));
        }

        [Test]
        public void PieceVisualRegistry_ReturnsFallbackWhenMissing()
        {
            PieceVisualRegistry registry = CreateScriptableObject<PieceVisualRegistry>();
            GameObject fallbackPrefab = CreatePrefab("FallbackDebris");
            registry.FallbackPrefab = fallbackPrefab;

            LogAssert.Expect(LogType.Warning, new Regex("missing prefab for DebrisType\\.B"));

            GameObject? result = registry.GetPrefab(DebrisType.B);

            Assert.That(result, Is.SameAs(fallbackPrefab));
        }

        [Test]
        public void BlockerVisualRegistry_ReturnsCorrectPrefabForEachBlockerType()
        {
            BlockerVisualRegistry registry = CreateScriptableObject<BlockerVisualRegistry>();
            GameObject cratePrefab = CreatePrefab("Crate");
            GameObject icePrefab = CreatePrefab("Ice");
            GameObject vinePrefab = CreatePrefab("Vine");
            registry.CratePrefab = cratePrefab;
            registry.IcePrefab = icePrefab;
            registry.VinePrefab = vinePrefab;

            Assert.That(registry.GetPrefab(BlockerType.Crate), Is.SameAs(cratePrefab));
            Assert.That(registry.GetPrefab(BlockerType.Ice), Is.SameAs(icePrefab));
            Assert.That(registry.GetPrefab(BlockerType.Vine), Is.SameAs(vinePrefab));
        }

        [Test]
        public void BlockerVisualRegistry_ReturnsFallbackWhenMissing()
        {
            BlockerVisualRegistry registry = CreateScriptableObject<BlockerVisualRegistry>();
            GameObject fallbackPrefab = CreatePrefab("FallbackBlocker");
            registry.FallbackBlockerPrefab = fallbackPrefab;

            LogAssert.Expect(LogType.Warning, new Regex("missing prefab for BlockerType\\.Ice"));

            GameObject? result = registry.GetPrefab(BlockerType.Ice);

            Assert.That(result, Is.SameAs(fallbackPrefab));
        }

        [Test]
        public void DockVisualConfig_AllowsSingleSharedDockWithMaterials()
        {
            DockVisualConfig config = CreateScriptableObject<DockVisualConfig>();
            GameObject sharedDockPrefab = CreatePrefab("DockShared");
            Material safeMaterial = CreateMaterial();
            Material cautionMaterial = CreateMaterial();
            Material acuteMaterial = CreateMaterial();
            Material failedMaterial = CreateMaterial();

            config.SharedDockPrefab = sharedDockPrefab;
            config.SafeMaterial = safeMaterial;
            config.CautionMaterial = cautionMaterial;
            config.AcuteMaterial = acuteMaterial;
            config.FailedMaterial = failedMaterial;

            Assert.That(config.GetPrefab(DockVisualState.Safe), Is.SameAs(sharedDockPrefab));
            Assert.That(config.GetPrefab(DockVisualState.Caution), Is.SameAs(sharedDockPrefab));
            Assert.That(config.GetMaterial(DockVisualState.Safe), Is.SameAs(safeMaterial));
            Assert.That(config.GetMaterial(DockVisualState.Caution), Is.SameAs(cautionMaterial));
            Assert.That(config.GetMaterial(DockVisualState.Acute), Is.SameAs(acuteMaterial));
            Assert.That(config.GetMaterial(DockVisualState.Failed), Is.SameAs(failedMaterial));
        }

        [Test]
        public void Registries_CreateWithoutThrowing()
        {
            Assert.That(CreateScriptableObject<PieceVisualRegistry>(), Is.Not.Null);
            Assert.That(CreateScriptableObject<TileVisualRegistry>(), Is.Not.Null);
            Assert.That(CreateScriptableObject<BlockerVisualRegistry>(), Is.Not.Null);
            Assert.That(CreateScriptableObject<TargetVisualRegistry>(), Is.Not.Null);
            Assert.That(CreateScriptableObject<DockVisualConfig>(), Is.Not.Null);
            Assert.That(CreateScriptableObject<FxVisualRegistry>(), Is.Not.Null);
        }

        private T CreateScriptableObject<T>()
            where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            instance.name = typeof(T).Name;
            createdObjects.Add(instance);
            return instance;
        }

        private GameObject CreatePrefab(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private Material CreateMaterial()
        {
            Material material = new Material(Shader.Find("Standard"));
            createdObjects.Add(material);
            return material;
        }
    }
}
