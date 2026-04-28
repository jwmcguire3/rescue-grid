using NUnit.Framework;
using Rescue.Unity.EditorTools.Art.Prefabs;
using Rescue.Unity.EditorTools.Diagnostics;

namespace Rescue.Unity.Art.Tests
{
    public sealed class DebrisInstanceTracerTests
    {
        private const string ArtRootPath = Phase1PlaceholderPrefabFactory.DefaultArtRootPath;
        private const string DebrisCPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Phase1/Pieces/Debris_C_Phase1.prefab";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Phase1PlaceholderPrefabFactory.CreateAll(ArtRootPath);
        }

        [Test]
        public void TracePrefabAsset_ResolvesModelAssetAndVisualWrapper()
        {
            PrefabTraceReport report = DebrisInstanceTracer.TracePrefabAsset(DebrisCPrefabPath);

            Assert.That(report.ModelAssetPath, Does.EndWith("Meshy_AI_Multicolored_Rope_Kno_0428040509_texture.fbx"));
            Assert.That(report.FirstNonIdentityTransform, Is.Not.Null);
            Assert.That(report.FirstNonIdentityTransform!.Path, Does.Contain("/Visual"));
        }

        [Test]
        public void TraceModelAsset_FindsOffsetOrBoundsCarrier()
        {
            PrefabTraceReport prefabReport = DebrisInstanceTracer.TracePrefabAsset(DebrisCPrefabPath);
            ModelTraceReport modelReport = DebrisInstanceTracer.TraceModelAsset(prefabReport.ModelAssetPath);

            Assert.That(modelReport.Nodes.Count, Is.GreaterThan(0));
            Assert.That(modelReport.ModelAssetPath, Does.EndWith("Meshy_AI_Multicolored_Rope_Kno_0428040509_texture.fbx"));
            Assert.That(modelReport.Nodes[0].Path, Does.Contain(modelReport.ModelName));

            if (modelReport.FirstOffsetTransform is not null)
            {
                Assert.That(modelReport.FirstOffsetTransform.Path, Does.Contain(modelReport.ModelName));
            }
        }
    }
}
