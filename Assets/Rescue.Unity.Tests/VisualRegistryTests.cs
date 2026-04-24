using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.Visuals;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rescue.Unity.Tests
{
    public sealed class VisualRegistryTests
    {
        [Test]
        public void PieceRegistry_UsesPlaceholderAndWarns_WhenPrefabMissing()
        {
            PieceVisualRegistry registry = ScriptableObject.CreateInstance<PieceVisualRegistry>();
            registry.name = "PieceRegistryTest";
            registry.DebrisA.PlaceholderPrimitive = VisualPrimitiveFallback.Cube;

            LogAssert.Expect(LogType.Warning, new Regex("PieceRegistryTest: missing prefab reference for Debris A"));

            GameObject? placeholder = registry.ResolvePrefab(DebrisType.A);

            Assert.That(placeholder, Is.Not.Null);
            Assert.That(placeholder!.name, Does.Contain("Debris A"));

            Object.DestroyImmediate(placeholder);
            Object.DestroyImmediate(registry);
        }

        [Test]
        public void TileRegistry_ReturnsNullAndWarns_WhenMaterialMissing()
        {
            TileVisualRegistry registry = ScriptableObject.CreateInstance<TileVisualRegistry>();
            registry.name = "TileRegistryTest";

            LogAssert.Expect(LogType.Warning, new Regex("TileRegistryTest: missing material reference for Dry Tile"));

            Material? material = registry.ResolveDryTileMaterial();

            Assert.That(material, Is.Null);
            Object.DestroyImmediate(registry);
        }

        [Test]
        public void FxRegistry_ReturnsNullAndWarns_WhenFxPrefabMissing()
        {
            FxVisualRegistry registry = ScriptableObject.CreateInstance<FxVisualRegistry>();
            registry.name = "FxRegistryTest";

            LogAssert.Expect(LogType.Warning, new Regex("FxRegistryTest: missing prefab reference for Extraction FX"));

            GameObject? prefab = registry.ExtractionFxPrefab;

            Assert.That(prefab, Is.Null);
            Object.DestroyImmediate(registry);
        }
    }
}
