using System.Collections.Generic;
using NUnit.Framework;
using Rescue.Unity.Art.Validation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rescue.Unity.Art.Tests
{
    public sealed class MeshyAssetValidatorTests
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
        public void MeshyAssetValidator_ClassifiesTriangleCounts()
        {
            MeshyAssetValidationConfig config = new MeshyAssetValidationConfig
            {
                triangleWarningThreshold = 20000,
                triangleSevereThreshold = 75000,
                minExpectedDimension = 0.5f,
                maxExpectedDimension = 2.0f,
            };

            Mesh underThresholdMesh = CreateMesh("UnderThreshold", 20000, 1.0f);
            Mesh warningMesh = CreateMesh("WarningMesh", 20001, 1.0f);
            Mesh severeMesh = CreateMesh("SevereMesh", 75001, 1.0f);

            MeshyAssetValidationItem underThresholdItem = MeshyAssetValidator.ValidateMesh("Assets/Under.asset", underThresholdMesh, 1, 0, config);
            MeshyAssetValidationItem warningItem = MeshyAssetValidator.ValidateMesh("Assets/Warning.asset", warningMesh, 1, 0, config);
            MeshyAssetValidationItem severeItem = MeshyAssetValidator.ValidateMesh("Assets/Severe.asset", severeMesh, 1, 0, config);

            Assert.That(underThresholdItem.warningLevel, Is.EqualTo(MeshyAssetWarningLevel.None));
            Assert.That(warningItem.warningLevel, Is.EqualTo(MeshyAssetWarningLevel.Warning));
            Assert.That(severeItem.warningLevel, Is.EqualTo(MeshyAssetWarningLevel.Severe));
        }

        [Test]
        public void MeshyAssetValidator_DetectsOutOfRangeBounds()
        {
            MeshyAssetValidationConfig config = new MeshyAssetValidationConfig
            {
                minExpectedDimension = 0.5f,
                maxExpectedDimension = 2.0f,
            };

            Mesh smallMesh = CreateTriangleMesh("SmallMesh", 0.1f);
            Mesh largeMesh = CreateTriangleMesh("LargeMesh", 3.0f);

            MeshyAssetValidationItem smallItem = MeshyAssetValidator.ValidateMesh("Assets/Small.asset", smallMesh, 0, 0, config);
            MeshyAssetValidationItem largeItem = MeshyAssetValidator.ValidateMesh("Assets/Large.asset", largeMesh, 0, 0, config);

            Assert.That(smallItem.dimensionOutOfRange, Is.True);
            Assert.That(smallItem.warningLevel, Is.EqualTo(MeshyAssetWarningLevel.Warning));
            Assert.That(largeItem.dimensionOutOfRange, Is.True);
            Assert.That(largeItem.warningLevel, Is.EqualTo(MeshyAssetWarningLevel.Warning));
        }

        [Test]
        public void MeshyAssetValidator_DoesNotRequireMaterials()
        {
            Mesh mesh = CreateTriangleMesh("NoMaterials", 1.0f);

            MeshyAssetValidationItem item = MeshyAssetValidator.ValidateMesh("Assets/NoMaterials.asset", mesh, 0, 0);

            Assert.That(item.materialCount, Is.EqualTo(0));
            Assert.That(item.missingMaterialCount, Is.EqualTo(0));
            Assert.That(item.meshName, Is.EqualTo("NoMaterials"));
        }

        [Test]
        public void MeshyAssetValidationReport_CanSerializeToJson()
        {
            MeshyAssetValidationReport report = MeshyAssetValidator.CreateReport(
                new[]
                {
                    new MeshyAssetValidationItem
                    {
                        assetPath = "Assets/Rescue.Unity/Art/Test.asset",
                        meshName = "TestMesh",
                        vertexCount = 12,
                        triangleCount = 4,
                        materialCount = 1,
                        missingMaterialCount = 0,
                        boundsSize = new Vector3(1.0f, 1.0f, 1.0f),
                        longestDimension = 1.0f,
                        warningLevel = MeshyAssetWarningLevel.None,
                    },
                },
                selectedAssetCount: 1,
                generatedAtUtc: "2026-04-24T00:00:00.0000000Z");

            string json = MeshyAssetValidator.SerializeReport(report);

            StringAssert.Contains("Assets/Rescue.Unity/Art/Test.asset", json);
            StringAssert.Contains("TestMesh", json);
        }

        private Mesh CreateMesh(string name, int triangleCount, float size)
        {
            Mesh mesh = new Mesh
            {
                name = name,
                indexFormat = IndexFormat.UInt32,
            };

            Vector3[] vertices = new Vector3[triangleCount * 3];
            int[] triangles = new int[triangleCount * 3];

            for (int i = 0; i < triangleCount; i++)
            {
                int vertexIndex = i * 3;
                float offset = i * size;
                vertices[vertexIndex + 0] = new Vector3(offset, 0.0f, 0.0f);
                vertices[vertexIndex + 1] = new Vector3(offset + size, 0.0f, 0.0f);
                vertices[vertexIndex + 2] = new Vector3(offset, size, 0.0f);
                triangles[vertexIndex + 0] = vertexIndex + 0;
                triangles[vertexIndex + 1] = vertexIndex + 1;
                triangles[vertexIndex + 2] = vertexIndex + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * size);

            createdObjects.Add(mesh);
            return mesh;
        }

        private Mesh CreateTriangleMesh(string name, float size)
        {
            Mesh mesh = new Mesh
            {
                name = name,
                vertices = new[]
                {
                    Vector3.zero,
                    new Vector3(size, 0.0f, 0.0f),
                    new Vector3(0.0f, size, 0.0f),
                },
                triangles = new[] { 0, 1, 2 },
                bounds = new Bounds(Vector3.zero, Vector3.one * size),
            };

            createdObjects.Add(mesh);
            return mesh;
        }
    }
}
