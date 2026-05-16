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
        public void TargetPuppyLookAt_ReapplyingSameReadinessPreservesAmbientGlance()
        {
            TargetPuppyLookAt lookAt = CreateLookAtRig(out _, out _, out _, out Transform target);
            SetPrivateField(lookAt, "smoothSeconds", 0f);
            target.position = new Vector3(10f, 2f, 10f);

            lookAt.ApplyReadiness(TargetReadiness.OneClearAway);
            lookAt.ForceGlanceForTests();
            lookAt.ApplyLookAtForTests(0.1f);
            float glancingBlend = lookAt.CurrentBlend;

            lookAt.ApplyReadiness(TargetReadiness.OneClearAway);
            lookAt.ApplyLookAtForTests(0.1f);

            Assert.That(glancingBlend, Is.GreaterThan(0.8f));
            Assert.That(lookAt.CurrentBlend, Is.EqualTo(glancingBlend).Within(0.001f));
        }

        [Test]
        public void TargetPuppyLookAt_AmbientGlanceReleaseSettlesGradually()
        {
            TargetPuppyLookAt lookAt = CreateLookAtRig(out _, out _, out _, out Transform target);
            SetPrivateField(lookAt, "smoothSeconds", 0f);
            SetPrivateField(lookAt, "forcedReleaseSmoothSeconds", 0.55f);
            target.position = new Vector3(10f, 2f, 10f);

            lookAt.ApplyReadiness(TargetReadiness.Trapped);
            lookAt.ForceGlanceForTests();
            lookAt.ApplyLookAtForTests(0.1f);
            float glancingBlend = lookAt.CurrentBlend;

            lookAt.ApplyLookAtForTests(0.95f);
            lookAt.ApplyLookAtForTests(0.06f);

            Assert.That(glancingBlend, Is.GreaterThan(0.3f));
            Assert.That(lookAt.CurrentBlend, Is.LessThan(glancingBlend));
            Assert.That(lookAt.CurrentBlend, Is.GreaterThan(0.2f));
        }

        [Test]
        public void TargetPuppyLookAt_ExtractionReleasesInfluenceGradually()
        {
            TargetPuppyLookAt lookAt = CreateLookAtRig(out _, out Transform neck, out Transform head, out Transform target);
            SetPrivateField(lookAt, "smoothSeconds", 0f);
            SetPrivateField(lookAt, "forcedReleaseSmoothSeconds", 0.55f);
            target.position = new Vector3(10f, 10f, 10f);

            lookAt.ApplyReadiness(TargetReadiness.OneClearAway);
            lookAt.ForceGlanceForTests();
            lookAt.ApplyLookAtForTests(0.1f);
            float activeBlend = lookAt.CurrentBlend;
            Assert.That(activeBlend, Is.GreaterThan(0f));
            Assert.That(neck.localRotation, Is.Not.EqualTo(Quaternion.identity));
            Assert.That(head.localRotation, Is.Not.EqualTo(Quaternion.identity));

            lookAt.PlayExtract();
            lookAt.ApplyLookAtForTests(0.1f);

            Assert.That(lookAt.CurrentBlend, Is.LessThan(activeBlend));
            Assert.That(lookAt.CurrentBlend, Is.GreaterThan(0f));
            Assert.That(lookAt.TryForceLookAtPlayer(CreateCamera("PlayerCamera")), Is.EqualTo(TargetPuppyLookAtResult.Extracting));

            lookAt.ApplyLookAtForTests(1.5f);

            Assert.That(lookAt.CurrentBlend, Is.LessThan(0.1f));
            Assert.That(Quaternion.Angle(neck.localRotation, Quaternion.identity), Is.LessThan(2f));
            Assert.That(Quaternion.Angle(head.localRotation, Quaternion.identity), Is.LessThan(2f));
        }

        [Test]
        public void TargetPuppyLookAt_ForceLookAtPlayerClampsThenReturnsToNormal()
        {
            TargetPuppyLookAt lookAt = CreateLookAtRig(out _, out Transform neck, out Transform head, out _);
            Camera camera = CreateCamera("PlayerCamera");
            SetPrivateField(lookAt, "smoothSeconds", 0f);
            SetPrivateField(lookAt, "forcedReleaseSmoothSeconds", 0.5f);
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

            Assert.That(lookAt.CurrentBlend, Is.LessThan(1f));
            Assert.That(lookAt.CurrentBlend, Is.GreaterThan(0.28f));
        }

        [Test]
        public void TargetPuppyLookAt_ForcedLookAtReleaseDecaysOverMultipleTicks()
        {
            TargetPuppyLookAt lookAt = CreateLookAtRigWithEyes(
                out _,
                out Transform neck,
                out Transform head,
                out _,
                out Transform leftEye,
                out Transform rightEye);
            Camera camera = CreateCamera("PlayerCamera");
            SetPrivateField(lookAt, "smoothSeconds", 0f);
            SetPrivateField(lookAt, "forcedReleaseSmoothSeconds", 0.5f);
            camera.transform.position = new Vector3(10f, 8f, 10f);

            lookAt.ApplyReadiness(TargetReadiness.Trapped);
            Assert.That(lookAt.ForceLookAtPlayer(camera, 0.05f), Is.True);
            lookAt.ApplyLookAtForTests(0.01f);

            Quaternion heldHead = head.localRotation;
            Quaternion heldNeck = neck.localRotation;
            Quaternion heldLeftEye = leftEye.localRotation;
            Quaternion heldRightEye = rightEye.localRotation;
            Assert.That(lookAt.CurrentBlend, Is.EqualTo(1f).Within(0.001f));
            Assert.That(lookAt.CurrentEyeBlend, Is.EqualTo(1f).Within(0.001f));

            lookAt.ApplyLookAtForTests(0.06f);
            float firstReleaseBlend = lookAt.CurrentBlend;
            float firstEyeReleaseBlend = lookAt.CurrentEyeBlend;
            Assert.That(firstReleaseBlend, Is.LessThan(1f));
            Assert.That(firstReleaseBlend, Is.GreaterThan(lookAt.CurrentReadiness == TargetReadiness.Trapped ? 0.04f : 0f));
            Assert.That(firstEyeReleaseBlend, Is.LessThan(1f));
            Assert.That(firstEyeReleaseBlend, Is.GreaterThan(0f));
            Assert.That(Quaternion.Angle(head.localRotation, heldHead), Is.GreaterThan(0.01f));
            Assert.That(Quaternion.Angle(neck.localRotation, heldNeck), Is.GreaterThan(0.01f));
            Assert.That(Quaternion.Angle(leftEye.localRotation, heldLeftEye), Is.GreaterThan(0.01f));
            Assert.That(Quaternion.Angle(rightEye.localRotation, heldRightEye), Is.GreaterThan(0.01f));

            lookAt.ApplyLookAtForTests(0.06f);

            Assert.That(lookAt.CurrentBlend, Is.LessThan(firstReleaseBlend));
            Assert.That(lookAt.CurrentEyeBlend, Is.LessThan(firstEyeReleaseBlend));
            Assert.That(lookAt.CurrentEyeBlend, Is.GreaterThan(0f));
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
            GameObject instance = InstantiateDaisyPrefab(out TargetPuppyLookAt lookAt);

            SetPrivateField(lookAt, "smoothSeconds", 0f);

            Transform head = FindChildTransform(instance.transform, "head")
                ?? throw new AssertionException("Expected Daisy prefab to include a head bone.");
            Transform nose = FindChildTransform(instance.transform, "nose")
                ?? throw new AssertionException("Expected Daisy prefab to include a nose landmark.");
            Camera camera = CreateCamera("PlayerCamera");
            camera.orthographic = true;
            camera.transform.SetPositionAndRotation(
                PortraitGameSceneLayout.CameraPortraitPosition,
                PortraitGameSceneLayout.CameraPortraitRotation);

            Vector3 desiredDirection = -camera.transform.forward;
            float before = ResolveBestHeadAimAngleToDirection(head, desiredDirection);
            Vector3 beforeNosePosition = nose.position;

            lookAt.ApplyReadiness(TargetReadiness.Trapped);
            Assert.That(lookAt.TryForceLookAtPlayer(camera), Is.EqualTo(TargetPuppyLookAtResult.Success));
            lookAt.ApplyLookAtForTests(0.1f);
            float after = ResolveBestHeadAimAngleToDirection(head, desiredDirection);
            Vector3 noseMotion = nose.position - beforeNosePosition;

            Assert.That(Mathf.Abs(lookAt.LastClampedYawDegrees), Is.LessThanOrEqualTo(lookAt.ForcedMaxYawDegrees + 0.01f));
            Assert.That(Mathf.Abs(lookAt.LastClampedPitchDegrees), Is.LessThanOrEqualTo(lookAt.ForcedMaxPitchDegrees + 0.01f));
            Assert.That(after, Is.LessThan(before - 1f), $"Expected Daisy head aim to improve toward the camera ray. Before={before:0.###}, after={after:0.###}");
            Assert.That(noseMotion.magnitude, Is.GreaterThan(0.02f), "Expected forced look-at to visibly move Daisy's nose.");
        }

        [Test]
        public void TargetPuppyLookAt_DaisyPrefabAmbientGlanceAimsTowardOrthographicCamera()
        {
            GameObject instance = InstantiateDaisyPrefab(out TargetPuppyLookAt lookAt);
            SetPrivateField(lookAt, "smoothSeconds", 0f);

            Transform head = FindChildTransform(instance.transform, "head")
                ?? throw new AssertionException("Expected Daisy prefab to include a head bone.");
            Transform nose = FindChildTransform(instance.transform, "nose")
                ?? throw new AssertionException("Expected Daisy prefab to include a nose landmark.");
            Camera camera = CreateCamera("PlayerCamera");
            camera.orthographic = true;
            camera.transform.SetPositionAndRotation(
                PortraitGameSceneLayout.CameraPortraitPosition,
                PortraitGameSceneLayout.CameraPortraitRotation);
            SetPrivateField(lookAt, "lookTarget", camera.transform);

            Vector3 desiredDirection = -camera.transform.forward;
            float before = ResolveBestHeadAimAngleToDirection(head, desiredDirection);
            Vector3 beforeNosePosition = nose.position;

            lookAt.ApplyReadiness(TargetReadiness.Trapped);
            lookAt.ForceGlanceForTests();
            lookAt.ApplyLookAtForTests(0.1f);

            float after = ResolveBestHeadAimAngleToDirection(head, desiredDirection);
            Vector3 noseMotion = nose.position - beforeNosePosition;

            Assert.That(lookAt.CurrentBlend, Is.LessThan(1f));
            Assert.That(after, Is.LessThan(before - 0.25f), $"Expected ambient glance to improve Daisy head aim toward the camera ray. Before={before:0.###}, after={after:0.###}");
            Assert.That(noseMotion.magnitude, Is.GreaterThan(0.005f), "Expected ambient glance to visibly move Daisy's nose.");
        }

        [Test]
        public void TargetPuppyLookAt_DaisyPrefabForcedLookAtImprovesEyeCameraAlignment()
        {
            GameObject instanceWithEyes = InstantiateDaisyPrefab(out TargetPuppyLookAt lookAtWithEyes);
            GameObject instanceWithoutEyes = InstantiateDaisyPrefab(out TargetPuppyLookAt lookAtWithoutEyes);
            SetPrivateField(lookAtWithEyes, "smoothSeconds", 0f);
            SetPrivateField(lookAtWithoutEyes, "smoothSeconds", 0f);
            SetPrivateField(lookAtWithoutEyes, "forcedEyeMaxYawDegrees", 0f);
            SetPrivateField(lookAtWithoutEyes, "forcedEyeMaxPitchDegrees", 0f);

            Transform leftEyeWithAim = FindChildTransform(instanceWithEyes.transform, "eye.L")
                ?? throw new AssertionException("Expected Daisy prefab to include a left eye bone.");
            Transform rightEyeWithAim = FindChildTransform(instanceWithEyes.transform, "eye.R")
                ?? throw new AssertionException("Expected Daisy prefab to include a right eye bone.");
            Transform leftEyeEndWithAim = FindChildTransform(instanceWithEyes.transform, "eye.L_end")
                ?? throw new AssertionException("Expected Daisy prefab to include a left eye end landmark.");
            Transform rightEyeEndWithAim = FindChildTransform(instanceWithEyes.transform, "eye.R_end")
                ?? throw new AssertionException("Expected Daisy prefab to include a right eye end landmark.");
            Transform leftEyeWithoutAim = FindChildTransform(instanceWithoutEyes.transform, "eye.L")
                ?? throw new AssertionException("Expected Daisy prefab to include a left eye bone.");
            Transform rightEyeWithoutAim = FindChildTransform(instanceWithoutEyes.transform, "eye.R")
                ?? throw new AssertionException("Expected Daisy prefab to include a right eye bone.");
            Transform leftEyeEndWithoutAim = FindChildTransform(instanceWithoutEyes.transform, "eye.L_end")
                ?? throw new AssertionException("Expected Daisy prefab to include a left eye end landmark.");
            Transform rightEyeEndWithoutAim = FindChildTransform(instanceWithoutEyes.transform, "eye.R_end")
                ?? throw new AssertionException("Expected Daisy prefab to include a right eye end landmark.");
            Camera camera = CreateCamera("PlayerCamera");
            camera.transform.SetPositionAndRotation(
                PortraitGameSceneLayout.CameraPortraitPosition,
                PortraitGameSceneLayout.CameraPortraitRotation);

            lookAtWithEyes.ApplyReadiness(TargetReadiness.Trapped);
            lookAtWithoutEyes.ApplyReadiness(TargetReadiness.Trapped);
            Assert.That(lookAtWithEyes.TryForceLookAtPlayer(camera), Is.EqualTo(TargetPuppyLookAtResult.Success));
            Assert.That(lookAtWithoutEyes.TryForceLookAtPlayer(camera), Is.EqualTo(TargetPuppyLookAtResult.Success));

            lookAtWithEyes.ApplyLookAtForTests(0.1f);
            lookAtWithoutEyes.ApplyLookAtForTests(0.1f);

            float withEyesAim = ResolveAverageEyeEndAimAngle(
                leftEyeWithAim,
                leftEyeEndWithAim,
                rightEyeWithAim,
                rightEyeEndWithAim,
                -camera.transform.forward);
            float withoutEyesAim = ResolveAverageEyeEndAimAngle(
                leftEyeWithoutAim,
                leftEyeEndWithoutAim,
                rightEyeWithoutAim,
                rightEyeEndWithoutAim,
                -camera.transform.forward);

            Assert.That(lookAtWithEyes.CurrentEyeBlend, Is.EqualTo(1f).Within(0.001f));
            Assert.That(Mathf.Abs(lookAtWithEyes.LastClampedEyeYawDegrees), Is.LessThanOrEqualTo(lookAtWithEyes.ForcedEyeMaxYawDegrees + 0.01f));
            Assert.That(Mathf.Abs(lookAtWithEyes.LastClampedEyePitchDegrees), Is.LessThanOrEqualTo(lookAtWithEyes.ForcedEyeMaxPitchDegrees + 0.01f));
            Assert.That(
                withEyesAim,
                Is.LessThan(withoutEyesAim - 0.25f),
                $"Expected Daisy eye aim to improve toward camera. WithEyes={withEyesAim:0.###}, WithoutEyes={withoutEyesAim:0.###}");
        }

        [Test]
        public void TargetPuppyLookAt_DaisyPrefabAnimatorDrivenPoseDoesNotOverSubtractForcedOffset()
        {
            GameObject instance = InstantiateDaisyPrefab(out TargetPuppyLookAt lookAt);
            Animator? animator = instance.GetComponentInChildren<Animator>(includeInactive: true);
            Assert.That(animator, Is.Not.Null);
            if (animator is null)
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

        private TargetPuppyLookAt CreateLookAtRigWithEyes(
            out Transform root,
            out Transform neck,
            out Transform head,
            out Transform target,
            out Transform leftEye,
            out Transform rightEye)
        {
            TargetPuppyLookAt lookAt = CreateLookAtRig(out root, out neck, out head, out target);

            GameObject leftEyeObject = new GameObject("eye.L");
            createdObjects.Add(leftEyeObject);
            leftEyeObject.transform.SetParent(head, worldPositionStays: false);
            leftEyeObject.transform.localPosition = new Vector3(-0.2f, 0.1f, 0.45f);
            leftEye = leftEyeObject.transform;

            GameObject rightEyeObject = new GameObject("eye.R");
            createdObjects.Add(rightEyeObject);
            rightEyeObject.transform.SetParent(head, worldPositionStays: false);
            rightEyeObject.transform.localPosition = new Vector3(0.2f, 0.1f, 0.45f);
            rightEye = rightEyeObject.transform;

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

        private GameObject InstantiateDaisyPrefab(out TargetPuppyLookAt lookAt)
        {
            const string DaisyTargetPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Targets/PF_Target_Daisy_Puppy.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DaisyTargetPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Expected Daisy prefab at '{DaisyTargetPrefabPath}'.");

            GameObject instance = Object.Instantiate(prefab);
            createdObjects.Add(instance);
            TargetPuppyLookAt? component = instance.GetComponent<TargetPuppyLookAt>();
            Assert.That(component, Is.Not.Null);
            if (component is null)
            {
                throw new AssertionException("Expected Daisy prefab to include TargetPuppyLookAt.");
            }

            lookAt = component;
            return instance;
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
            return ResolveBestHeadAimAngleToDirection(head, direction);
        }

        private static float ResolveBestHeadAimAngleToDirection(Transform head, Vector3 targetDirection)
        {
            Vector3 direction = targetDirection.normalized;
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

        private static float ResolveAverageEyeEndAimAngle(
            Transform leftEye,
            Transform leftEyeEnd,
            Transform rightEye,
            Transform rightEyeEnd,
            Vector3 targetDirection)
        {
            return (ResolveEyeEndAimAngle(leftEye, leftEyeEnd, targetDirection) +
                ResolveEyeEndAimAngle(rightEye, rightEyeEnd, targetDirection)) * 0.5f;
        }

        private static float ResolveEyeEndAimAngle(Transform eye, Transform eyeEnd, Vector3 targetDirection)
        {
            Vector3 aimDirection = (eyeEnd.position - eye.position).normalized;
            return Vector3.Angle(aimDirection, targetDirection.normalized);
        }
    }
}
