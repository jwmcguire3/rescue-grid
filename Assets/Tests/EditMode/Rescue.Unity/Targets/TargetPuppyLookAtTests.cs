using System.Collections.Generic;
using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.Presentation.Targets;
using UnityEngine;

namespace Rescue.Unity.Targets.Tests
{
    public sealed class TargetPuppyLookAtTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] is null)
                {
                    continue;
                }

                Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void TargetPuppyLookAt_MissingBonesDoesNotThrow()
        {
            TargetPuppyLookAt lookAt = CreateLookAt();

            Assert.DoesNotThrow(() => lookAt.ApplyReadiness(TargetReadiness.Trapped));
            Assert.DoesNotThrow(() => lookAt.ApplyLookAtForTests(0.1f));
            Assert.DoesNotThrow(() => lookAt.ApplyReadiness(TargetReadiness.OneClearAway));
            Assert.DoesNotThrow(lookAt.PlayExtract);
            Assert.DoesNotThrow(() => lookAt.ApplyLookAtForTests(0.1f));
        }

        [Test]
        public void TargetPuppyLookAt_AssignedDummyBonesClampRotation()
        {
            TargetPuppyLookAt lookAt = CreateLookAtRig(out Transform root, out Transform neck, out Transform head, out Transform target);
            SetPrivateField(lookAt, "smoothSeconds", 0f);
            target.position = new Vector3(10f, 10f, 10f);

            lookAt.ApplyReadiness(TargetReadiness.OneClearAway);
            lookAt.ForceGlanceForTests();
            lookAt.ApplyLookAtForTests(0.1f);

            Assert.That(Mathf.Abs(lookAt.LastClampedYawDegrees), Is.LessThanOrEqualTo(lookAt.ActiveMaxYawDegrees + 0.01f));
            Assert.That(Mathf.Abs(lookAt.LastClampedPitchDegrees), Is.LessThanOrEqualTo(lookAt.ActiveMaxPitchDegrees + 0.01f));
            Assert.That(Mathf.Abs(lookAt.LastClampedYawDegrees), Is.EqualTo(lookAt.ActiveMaxYawDegrees).Within(0.01f));
            Assert.That(neck.localRotation, Is.Not.EqualTo(Quaternion.identity));
            Assert.That(head.localRotation, Is.Not.EqualTo(Quaternion.identity));
            Assert.That(root.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void TargetPuppyLookAt_ReadinessChangesActiveFrequencySettings()
        {
            TargetPuppyLookAt lookAt = CreateLookAt();

            lookAt.ApplyReadiness(TargetReadiness.Trapped);
            float trappedMin = lookAt.ActiveGlanceCooldownMinSeconds;
            float trappedMax = lookAt.ActiveGlanceCooldownMaxSeconds;

            lookAt.ApplyReadiness(TargetReadiness.Progressing);
            float progressingMin = lookAt.ActiveGlanceCooldownMinSeconds;
            float progressingMax = lookAt.ActiveGlanceCooldownMaxSeconds;

            lookAt.ApplyReadiness(TargetReadiness.OneClearAway);

            Assert.That(progressingMin, Is.LessThan(trappedMin));
            Assert.That(progressingMax, Is.LessThan(trappedMax));
            Assert.That(lookAt.ActiveGlanceCooldownMinSeconds, Is.LessThan(progressingMin));
            Assert.That(lookAt.ActiveGlanceCooldownMaxSeconds, Is.LessThan(progressingMax));
        }

        [Test]
        public void TargetPuppyLookAt_ExtractionDisablesInfluence()
        {
            TargetPuppyLookAt lookAt = CreateLookAtRig(out _, out Transform neck, out Transform head, out Transform target);
            SetPrivateField(lookAt, "smoothSeconds", 0f);
            target.position = new Vector3(10f, 10f, 10f);

            lookAt.ApplyReadiness(TargetReadiness.OneClearAway);
            lookAt.ForceGlanceForTests();
            lookAt.ApplyLookAtForTests(0.1f);
            Assert.That(lookAt.CurrentBlend, Is.GreaterThan(0f));

            lookAt.PlayExtract();
            lookAt.ApplyLookAtForTests(0.1f);

            Assert.That(lookAt.CurrentBlend, Is.EqualTo(0f).Within(0.001f));
            Assert.That(neck.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(head.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void TargetPuppyLookAt_ForceLookAtPlayerClampsThenReturnsToNormal()
        {
            TargetPuppyLookAt lookAt = CreateLookAtRig(out _, out Transform neck, out Transform head, out _);
            Camera camera = CreateCamera("PlayerCamera");
            SetPrivateField(lookAt, "smoothSeconds", 0f);
            camera.transform.position = new Vector3(10f, 10f, 10f);

            lookAt.ApplyReadiness(TargetReadiness.OneClearAway);

            Assert.That(lookAt.ForceLookAtPlayer(camera, 0.1f), Is.True);
            lookAt.ApplyLookAtForTests(0.01f);

            Assert.That(lookAt.CurrentBlend, Is.EqualTo(1f).Within(0.001f));
            Assert.That(Mathf.Abs(lookAt.LastClampedYawDegrees), Is.LessThanOrEqualTo(lookAt.ActiveMaxYawDegrees + 0.01f));
            Assert.That(neck.localRotation, Is.Not.EqualTo(Quaternion.identity));
            Assert.That(head.localRotation, Is.Not.EqualTo(Quaternion.identity));

            lookAt.ApplyLookAtForTests(0.2f);

            Assert.That(lookAt.CurrentBlend, Is.EqualTo(lookAt.ActiveMaxYawDegrees > 0f ? 0.28f : 0f).Within(0.001f));
        }

        [Test]
        public void TargetPuppyLookAt_ForceLookAtPlayerFailsSoftlyWhenMissingRequirements()
        {
            TargetPuppyLookAt missingBones = CreateLookAt();
            Camera camera = CreateCamera("PlayerCamera");

            Assert.That(missingBones.ForceLookAtPlayer(camera), Is.False);

            TargetPuppyLookAt lookAt = CreateLookAtRig(out _, out _, out _, out _);
            lookAt.PlayExtract();

            Assert.That(lookAt.ForceLookAtPlayer(camera), Is.False);
        }

        private TargetPuppyLookAt CreateLookAt()
        {
            GameObject gameObject = new GameObject("TargetPuppyLookAtTestObject");
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<TargetPuppyLookAt>();
        }

        private Camera CreateCamera(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<Camera>();
        }

        private TargetPuppyLookAt CreateLookAtRig(
            out Transform root,
            out Transform neck,
            out Transform head,
            out Transform target)
        {
            GameObject rootObject = new GameObject("TargetPuppyLookAtRig");
            createdObjects.Add(rootObject);
            root = rootObject.transform;

            GameObject neckObject = new GameObject("neck");
            createdObjects.Add(neckObject);
            neckObject.transform.SetParent(root, worldPositionStays: false);
            neck = neckObject.transform;

            GameObject headObject = new GameObject("head");
            createdObjects.Add(headObject);
            headObject.transform.SetParent(neck, worldPositionStays: false);
            head = headObject.transform;

            GameObject targetObject = new GameObject("lookTarget");
            createdObjects.Add(targetObject);
            target = targetObject.transform;

            TargetPuppyLookAt lookAt = rootObject.AddComponent<TargetPuppyLookAt>();
            SetPrivateField(lookAt, "neckBone", neck);
            SetPrivateField(lookAt, "headBone", head);
            SetPrivateField(lookAt, "lookTarget", target);
            return lookAt;
        }

        private static void SetPrivateField(object target, string fieldName, object? value)
        {
            System.Reflection.FieldInfo? field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}'.");
            if (field is null)
            {
                return;
            }

            field.SetValue(target, value);
        }
    }
}
