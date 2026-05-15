using System.Collections.Generic;
using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.Presentation;
using Rescue.Unity.Presentation.Targets;
using UnityEditor;
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
            Assert.That(Mathf.Abs(lookAt.LastClampedYawDegrees), Is.GreaterThan(lookAt.ActiveMaxYawDegrees + 0.01f));
            Assert.That(Mathf.Abs(lookAt.LastClampedYawDegrees), Is.LessThanOrEqualTo(lookAt.ForcedMaxYawDegrees + 0.01f));
            Assert.That(lookAt.LastClampedYawDegrees, Is.EqualTo(30f).Within(0.01f));
            Assert.That(lookAt.LastClampedPitchDegrees, Is.EqualTo(44f).Within(0.01f));
            Assert.That(neck.localRotation, Is.Not.EqualTo(Quaternion.identity));
            Assert.That(head.localRotation, Is.Not.EqualTo(Quaternion.identity));
            Assert.That(Quaternion.Angle(head.localRotation, Quaternion.Euler(lookAt.ForcedHeadEulerOffset)), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(neck.localRotation, Quaternion.Euler(lookAt.ForcedNeckEulerOffset)), Is.LessThan(0.01f));

            lookAt.ApplyLookAtForTests(0.2f);

            Assert.That(lookAt.CurrentBlend, Is.EqualTo(lookAt.ActiveMaxYawDegrees > 0f ? 0.28f : 0f).Within(0.001f));
        }

        [Test]
        public void TargetPuppyLookAt_ForceLookAtPlayerFailsSoftlyWhenMissingRequirements()
        {
            TargetPuppyLookAt missingBones = CreateLookAt();
            Camera camera = CreateCamera("PlayerCamera");

            Assert.That(missingBones.TryForceLookAtPlayer(camera), Is.EqualTo(TargetPuppyLookAtResult.MissingBones));

            TargetPuppyLookAt lookAt = CreateLookAtRig(out _, out _, out _, out _);
            lookAt.PlayExtract();

            Assert.That(lookAt.TryForceLookAtPlayer(camera), Is.EqualTo(TargetPuppyLookAtResult.Extracting));
        }

        [Test]
        public void TargetPuppyLookAt_ForceLookAtPlayerFailsSoftlyWhenCameraInactive()
        {
            TargetPuppyLookAt lookAt = CreateLookAtRig(out _, out _, out _, out _);
            Camera camera = CreateCamera("InactivePlayerCamera");
            camera.enabled = false;

            Assert.That(lookAt.TryForceLookAtPlayer(camera), Is.EqualTo(TargetPuppyLookAtResult.MissingCamera));
        }

        [Test]
        public void TargetPuppyLookAt_AmbientLookUsesAssignedLookSpaceForDirection()
        {
            TargetPuppyLookAt lookAt = CreateLookAtRig(out Transform root, out _, out _, out _);
            SetPrivateField(lookAt, "smoothSeconds", 0f);

            GameObject visualObject = new GameObject("Visual");
            createdObjects.Add(visualObject);
            visualObject.transform.SetParent(root, worldPositionStays: false);
            visualObject.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            SetPrivateField(lookAt, "lookSpace", visualObject.transform);

            Camera camera = CreateCamera("PlayerCamera");
            camera.transform.position = new Vector3(10f, 0f, 0f);

            lookAt.ApplyReadiness(TargetReadiness.Trapped);
            SetPrivateField(lookAt, "lookTarget", camera.transform);
            lookAt.ForceGlanceForTests();
            lookAt.ApplyLookAtForTests(0.1f);

            Assert.That(Mathf.Abs(lookAt.LastClampedYawDegrees), Is.LessThan(0.01f));
        }

        [Test]
        public void TargetPuppyLookAt_DaisyPrefabForcedLookAtUsesStrongerVisibleAim()
        {
            const string DaisyTargetPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Targets/PF_Target_Daisy_Puppy.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DaisyTargetPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Expected Daisy prefab at '{DaisyTargetPrefabPath}'.");

            GameObject instance = Object.Instantiate(prefab);
            createdObjects.Add(instance);
            TargetPuppyLookAt? lookAt = instance.GetComponent<TargetPuppyLookAt>();
            Assert.That(lookAt, Is.Not.Null);
            if (lookAt is null)
            {
                throw new AssertionException("Expected Daisy prefab to include TargetPuppyLookAt.");
            }

            Transform? visual = instance.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null);
            SetPrivateField(lookAt, "smoothSeconds", 0f);

            Transform head = FindChildTransform(instance.transform, "head")
                ?? throw new AssertionException("Expected Daisy prefab to include a head bone.");
            Transform nose = FindChildTransform(instance.transform, "nose")
                ?? throw new AssertionException("Expected Daisy prefab to include a nose landmark.");
            Transform leftEye = FindChildTransform(instance.transform, "eye.L")
                ?? throw new AssertionException("Expected Daisy prefab to include a left eye landmark.");
            Transform rightEye = FindChildTransform(instance.transform, "eye.R")
                ?? throw new AssertionException("Expected Daisy prefab to include a right eye landmark.");
            Camera camera = CreateCamera("PlayerCamera");
            camera.transform.SetPositionAndRotation(
                PortraitGameSceneLayout.CameraPortraitPosition,
                PortraitGameSceneLayout.CameraPortraitRotation);

            float before = ResolveBestHeadAimAngle(head, camera.transform.position);
            Vector3 beforeNosePosition = nose.position;
            Vector3 beforeMuzzleDirection = (nose.position - head.position).normalized;
            Vector3 dogRight = (rightEye.position - leftEye.position).normalized;
            Vector3 dogUp = Vector3.Cross(beforeMuzzleDirection, dogRight).normalized;
            if (Vector3.Dot(dogUp, visual!.up) < 0f)
            {
                dogUp = -dogUp;
            }

            lookAt.ApplyReadiness(TargetReadiness.Trapped);
            Assert.That(lookAt.TryForceLookAtPlayer(camera), Is.EqualTo(TargetPuppyLookAtResult.Success));
            lookAt.ApplyLookAtForTests(0.1f);
            float after = ResolveBestHeadAimAngle(head, camera.transform.position);
            Vector3 noseMotion = nose.position - beforeNosePosition;

            Assert.That(lookAt.LastClampedYawDegrees, Is.EqualTo(lookAt.ForcedLookRightDegrees).Within(0.01f));
            Assert.That(lookAt.LastClampedPitchDegrees, Is.EqualTo(lookAt.ForcedLookUpDegrees).Within(0.01f));
            Assert.That(Mathf.Abs(lookAt.LastClampedYawDegrees), Is.LessThanOrEqualTo(lookAt.ForcedMaxYawDegrees + 0.01f));
            Assert.That(after, Is.LessThan(before - 1f), $"Expected Daisy head aim to improve. Before={before:0.###}, after={after:0.###}");
            Assert.That(noseMotion.magnitude, Is.GreaterThan(0.02f), "Expected forced look-at to visibly move Daisy's nose.");
            Assert.That(Vector3.Dot(noseMotion, dogUp), Is.GreaterThan(0.005f), "Expected Daisy's nose to move up.");
            Assert.That(Vector3.Dot(noseMotion, dogRight), Is.GreaterThan(0.005f), "Expected Daisy's nose to move to her right.");
        }

        [Test]
        public void TargetPuppyLookAt_DaisyPrefabAnimatorDrivenPoseDoesNotOverSubtractForcedOffset()
        {
            const string DaisyTargetPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Targets/PF_Target_Daisy_Puppy.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DaisyTargetPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Expected Daisy prefab at '{DaisyTargetPrefabPath}'.");

            GameObject instance = Object.Instantiate(prefab);
            createdObjects.Add(instance);
            TargetPuppyLookAt? lookAt = instance.GetComponent<TargetPuppyLookAt>();
            Animator? animator = instance.GetComponentInChildren<Animator>(includeInactive: true);
            Assert.That(lookAt, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            if (lookAt is null || animator is null)
            {
                throw new AssertionException("Expected Daisy prefab to include look-at and animator components.");
            }

            SetPrivateField(lookAt, "smoothSeconds", 0f);
            Transform nose = FindChildTransform(instance.transform, "nose")
                ?? throw new AssertionException("Expected Daisy prefab to include a nose landmark.");
            Camera camera = CreateCamera("PlayerCamera");
            camera.transform.SetPositionAndRotation(
                PortraitGameSceneLayout.CameraPortraitPosition,
                PortraitGameSceneLayout.CameraPortraitRotation);

            animator.Update(0.02f);
            Vector3 baseNosePosition = nose.position;
            lookAt.ApplyReadiness(TargetReadiness.Trapped);
            Assert.That(lookAt.TryForceLookAtPlayer(camera, 1f), Is.EqualTo(TargetPuppyLookAtResult.Success));

            lookAt.ApplyLookAtForTests(0.02f);
            Vector3 firstMotion = nose.position - baseNosePosition;

            animator.Update(0.02f);
            lookAt.ApplyLookAtForTests(0.02f);
            Vector3 secondMotion = nose.position - baseNosePosition;

            Assert.That(firstMotion.magnitude, Is.GreaterThan(0.02f));
            Assert.That(secondMotion.magnitude, Is.GreaterThan(firstMotion.magnitude * 0.75f));
            Assert.That(Vector3.Dot(firstMotion.normalized, secondMotion.normalized), Is.GreaterThan(0.9f));
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

        private static Transform? FindChildTransform(Transform root, string childName)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == childName)
                {
                    return children[i];
                }
            }

            return null;
        }

        private static float ResolveBestHeadAimAngle(Transform head, Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - head.position).normalized;
            Vector3[] axes =
            {
                head.forward,
                -head.forward,
                head.up,
                -head.up,
                head.right,
                -head.right,
            };

            float best = 180f;
            for (int i = 0; i < axes.Length; i++)
            {
                best = Mathf.Min(best, Vector3.Angle(axes[i], direction));
            }

            return best;
        }
    }
}
