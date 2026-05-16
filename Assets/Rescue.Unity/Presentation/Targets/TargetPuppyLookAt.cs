using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.Presentation.Targets
{
    public enum TargetPuppyLookAtResult
    {
        Success,
        MissingBones,
        MissingCamera,
        Extracting,
    }

    [DefaultExecutionOrder(10)]
    public sealed class TargetPuppyLookAt : MonoBehaviour
    {
        [SerializeField] private Transform? headBone;
        [SerializeField] private Transform? neckBone;
        [SerializeField] private Transform? lookTarget;
        [SerializeField] private Transform? lookSpace;
        [SerializeField] private Transform? muzzleReference;
        [SerializeField] private Transform? leftEyeReference;
        [SerializeField] private Transform? rightEyeReference;
        [SerializeField] private Transform? leftEyelidReference;
        [SerializeField] private Transform? rightEyelidReference;
        [SerializeField] private Transform? leftEyeEndReference;
        [SerializeField] private Transform? rightEyeEndReference;
        [SerializeField] private float smoothSeconds = 0.16f;
        [SerializeField] private float forcedReleaseSmoothSeconds = 0.55f;
        [SerializeField] private float forcedMaxYawDegrees = 80f;
        [SerializeField] private float forcedMaxPitchDegrees = 42f;
        [SerializeField] private float forcedEyeMaxYawDegrees = 18f;
        [SerializeField] private float forcedEyeMaxPitchDegrees = 10f;
        [SerializeField] private float forcedLookUpDegrees = 34f;
        [SerializeField] private float forcedLookRightDegrees = 18f;
        [SerializeField] private Vector3 forcedHeadEulerOffset = new Vector3(-34f, 22f, 0f);
        [SerializeField] private Vector3 forcedNeckEulerOffset = new Vector3(-10f, 8f, 0f);

        private Quaternion lastHeadOffset = Quaternion.identity;
        private Quaternion lastNeckOffset = Quaternion.identity;
        private Quaternion lastLeftEyeOffset = Quaternion.identity;
        private Quaternion lastRightEyeOffset = Quaternion.identity;
        private Quaternion lastLeftEyelidOffset = Quaternion.identity;
        private Quaternion lastRightEyelidOffset = Quaternion.identity;
        private float blendVelocity;
        private float forcedReleaseBlendVelocity;
        private float eyeBlendVelocity;
        private float glanceCooldownRemaining;
        private float glanceRemaining;
        private Transform? forcedLookTarget;
        private Camera? forcedLookCamera;
        private float forcedLookRemainingSeconds;
        private Animator? poseAnimator;
        private bool extracting;

        public TargetReadiness CurrentReadiness { get; private set; } = TargetReadiness.Trapped;

        public float CurrentBlend { get; private set; }

        public float CurrentEyeBlend { get; private set; }

        public float LastClampedYawDegrees { get; private set; }

        public float LastClampedPitchDegrees { get; private set; }

        public float LastClampedEyeYawDegrees { get; private set; }

        public float LastClampedEyePitchDegrees { get; private set; }

        public float ActiveMaxYawDegrees => ResolveProfile(CurrentReadiness).MaxYawDegrees;

        public float ActiveMaxPitchDegrees => ResolveProfile(CurrentReadiness).MaxPitchDegrees;

        public float ForcedMaxYawDegrees => forcedMaxYawDegrees;

        public float ForcedMaxPitchDegrees => forcedMaxPitchDegrees;

        public float ForcedEyeMaxYawDegrees => forcedEyeMaxYawDegrees;

        public float ForcedEyeMaxPitchDegrees => forcedEyeMaxPitchDegrees;

        public float ForcedLookUpDegrees => forcedLookUpDegrees;

        public float ForcedLookRightDegrees => forcedLookRightDegrees;

        public Vector3 ForcedHeadEulerOffset => forcedHeadEulerOffset;

        public Vector3 ForcedNeckEulerOffset => forcedNeckEulerOffset;

        public float ActiveGlanceCooldownMinSeconds => ResolveProfile(CurrentReadiness).GlanceCooldownMinSeconds;

        public float ActiveGlanceCooldownMaxSeconds => ResolveProfile(CurrentReadiness).GlanceCooldownMaxSeconds;

        public void ApplyReadiness(TargetReadiness readiness)
        {
            bool readinessChanged = CurrentReadiness != readiness;
            CurrentReadiness = readiness;
            extracting = readiness is TargetReadiness.ExtractableLatched or TargetReadiness.Extracted;
            if (readinessChanged)
            {
                ResetGlanceTimer();
            }
        }

        public void PlayExtract()
        {
            extracting = true;
            CurrentReadiness = TargetReadiness.Extracted;
            glanceRemaining = 0f;
            glanceCooldownRemaining = float.PositiveInfinity;
        }

        public void ApplyLookAtForTests(float deltaTime)
        {
            ApplyLookAt(Mathf.Max(0f, deltaTime));
        }

        public void ForceGlanceForTests()
        {
            LookProfile profile = ResolveProfile(CurrentReadiness);
            glanceRemaining = profile.GlanceDurationSeconds;
        }

        public bool ForceLookAtPlayer(Camera? camera = null, float durationSeconds = 1.35f)
        {
            return TryForceLookAtPlayer(camera, durationSeconds) == TargetPuppyLookAtResult.Success;
        }

        public TargetPuppyLookAtResult TryForceLookAtPlayer(Camera? camera = null, float durationSeconds = 1.75f)
        {
            if (extracting || headBone == null || neckBone == null)
            {
                return extracting
                    ? TargetPuppyLookAtResult.Extracting
                    : TargetPuppyLookAtResult.MissingBones;
            }

            Camera? targetCamera = camera == null ? Camera.main : camera;
            if (targetCamera == null ||
                !targetCamera.isActiveAndEnabled ||
                !targetCamera.gameObject.activeInHierarchy)
            {
                return TargetPuppyLookAtResult.MissingCamera;
            }

            forcedLookTarget = targetCamera.transform;
            forcedLookCamera = targetCamera;
            forcedLookRemainingSeconds = Mathf.Max(0.01f, durationSeconds);
            return TargetPuppyLookAtResult.Success;
        }

        private void Awake()
        {
            ResolvePoseAnimator();
            ResetGlanceTimer();
        }

        private void LateUpdate()
        {
            ApplyLookAt(Time.deltaTime);
        }

        private void ApplyLookAt(float deltaTime)
        {
            Transform? head = headBone;
            Transform? neck = neckBone;
            if (head == null || neck == null)
            {
                return;
            }

            bool wasForcedLookActive = IsForcedLookActive();
            UpdateForcedLook(deltaTime);

            bool animatorDrivenPose = IsAnimatorDrivingPose();
            Quaternion headBase = ResolveBaseRotation(head, lastHeadOffset, animatorDrivenPose);
            Quaternion neckBase = ResolveBaseRotation(neck, lastNeckOffset, animatorDrivenPose);
            EyePoseBases eyeBases = ResolveEyePoseBases(head, animatorDrivenPose);
            Transform? target = ResolveLookTarget();

            if (target == null)
            {
                CurrentBlend = Smooth(CurrentBlend, 0f, deltaTime);
                CurrentEyeBlend = SmoothEyeBlend(CurrentEyeBlend, 0f, deltaTime);
                ApplyOffsets(head, neck, headBase, neckBase, Quaternion.identity, Quaternion.identity);
                ApplyEyeOffsets(eyeBases, Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity);
                return;
            }

            bool isForcedLook = IsForcedLookActive();
            LookProfile profile = ResolveActiveProfile(isForcedLook);
            UpdateGlance(profile, deltaTime);

            float targetBlend = ResolveTargetBlend(profile);
            bool isForcedLookReleasing = forcedLookTarget != null &&
                !isForcedLook &&
                CurrentBlend > targetBlend + 0.001f;
            CurrentBlend = isForcedLookReleasing || (wasForcedLookActive && !isForcedLook)
                ? SmoothForcedRelease(CurrentBlend, targetBlend, deltaTime)
                : Smooth(CurrentBlend, targetBlend, deltaTime);
            CurrentEyeBlend = SmoothEyeBlend(CurrentEyeBlend, isForcedLook ? 1f : 0f, deltaTime);

            if (isForcedLook || (forcedLookTarget != null && CurrentBlend > targetBlend + 0.001f))
            {
                ApplyForcedPose(head, neck, headBase, neckBase, target);
                ApplyForcedEyePose(eyeBases, target);
                return;
            }

            Vector3 worldDirection = ResolveLookWorldDirection(head.position, target);
            if (worldDirection.sqrMagnitude <= 0.0001f || CurrentBlend <= 0.0001f)
            {
                ApplyOffsets(head, neck, headBase, neckBase, Quaternion.identity, Quaternion.identity);
                ApplyEyeOffsets(eyeBases, Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity);
                return;
            }

            Transform directionSpace = ResolveDirectionSpace();
            Vector3 localDirection = directionSpace.InverseTransformDirection(worldDirection.normalized);
            float horizontalMagnitude = new Vector2(localDirection.x, localDirection.z).magnitude;
            float yawDegrees = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
            float pitchDegrees = Mathf.Atan2(localDirection.y, horizontalMagnitude) * Mathf.Rad2Deg;

            yawDegrees = Mathf.Clamp(yawDegrees, -profile.MaxYawDegrees, profile.MaxYawDegrees);
            pitchDegrees = Mathf.Clamp(
                pitchDegrees + profile.HeadDownPitchDegrees,
                -profile.MaxPitchDegrees,
                profile.MaxPitchDegrees);

            LastClampedYawDegrees = yawDegrees;
            LastClampedPitchDegrees = pitchDegrees;

            float weightedYaw = yawDegrees * CurrentBlend;
            float weightedPitch = pitchDegrees * CurrentBlend;
            Quaternion neckOffset = Quaternion.Euler(
                -weightedPitch * profile.NeckPitchShare,
                weightedYaw * profile.NeckYawShare,
                0f);
            Quaternion headOffset = Quaternion.Euler(
                -weightedPitch * profile.HeadPitchShare,
                weightedYaw * profile.HeadYawShare,
                0f);

            ApplyOffsets(head, neck, headBase, neckBase, headOffset, neckOffset);
            ApplyEyeOffsets(eyeBases, Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity);
        }

        private Transform? ResolveLookTarget()
        {
            if (forcedLookTarget != null)
            {
                return forcedLookTarget;
            }

            if (lookTarget != null)
            {
                return lookTarget;
            }

            if (!Application.isPlaying)
            {
                return null;
            }

            Camera? mainCamera = Camera.main;
            return mainCamera == null ? null : mainCamera.transform;
        }

        private void UpdateGlance(LookProfile profile, float deltaTime)
        {
            if (extracting || profile.GlanceDurationSeconds <= 0f)
            {
                glanceRemaining = 0f;
                return;
            }

            if (glanceRemaining > 0f)
            {
                glanceRemaining = Mathf.Max(0f, glanceRemaining - deltaTime);
                if (glanceRemaining <= 0f)
                {
                    ScheduleNextGlance(profile);
                }

                return;
            }

            glanceCooldownRemaining -= deltaTime;
            if (glanceCooldownRemaining > 0f)
            {
                return;
            }

            glanceRemaining = profile.GlanceDurationSeconds;
        }

        private float ResolveTargetBlend(LookProfile profile)
        {
            if (extracting)
            {
                return profile.ExtractionBlend;
            }

            if (forcedLookRemainingSeconds > 0f && forcedLookTarget != null)
            {
                return 1f;
            }

            return profile.BaseBlend + (glanceRemaining > 0f ? profile.GlanceBlend : 0f);
        }

        private LookProfile ResolveActiveProfile(bool isForcedLook)
        {
            LookProfile profile = ResolveProfile(CurrentReadiness);
            if (!isForcedLook)
            {
                return profile;
            }

            return profile with
            {
                MaxYawDegrees = Mathf.Max(profile.MaxYawDegrees, forcedMaxYawDegrees),
                MaxPitchDegrees = Mathf.Max(profile.MaxPitchDegrees, forcedMaxPitchDegrees),
                HeadDownPitchDegrees = 0f,
            };
        }

        private Transform ResolveDirectionSpace()
        {
            if (lookSpace != null)
            {
                return lookSpace;
            }

            return transform;
        }

        private bool IsForcedLookActive()
        {
            return forcedLookRemainingSeconds > 0f && forcedLookTarget != null;
        }

        private void ApplyForcedPose(
            Transform head,
            Transform neck,
            Quaternion headBase,
            Quaternion neckBase,
            Transform target)
        {
            if (TryApplyForcedLandmarkPose(head, neck, headBase, neckBase, target))
            {
                return;
            }

            Vector3 headEuler = forcedHeadEulerOffset * CurrentBlend;
            Vector3 neckEuler = forcedNeckEulerOffset * CurrentBlend;
            LastClampedYawDegrees = headEuler.y + neckEuler.y;
            LastClampedPitchDegrees = -(headEuler.x + neckEuler.x);
            ApplyOffsets(
                head,
                neck,
                headBase,
                neckBase,
                Quaternion.Euler(headEuler),
                Quaternion.Euler(neckEuler));
        }

        private EyePoseBases ResolveEyePoseBases(Transform head, bool animatorDrivenPose)
        {
            Transform? leftEye = ResolveNamedReference(ref leftEyeReference, head, "eye.L");
            Transform? rightEye = ResolveNamedReference(ref rightEyeReference, head, "eye.R");
            Transform? leftEyelid = ResolveNamedReference(ref leftEyelidReference, head, "eyelid.L");
            Transform? rightEyelid = ResolveNamedReference(ref rightEyelidReference, head, "eyelid.R");
            ResolveNamedReference(ref leftEyeEndReference, head, "eye.L_end");
            ResolveNamedReference(ref rightEyeEndReference, head, "eye.R_end");

            return new EyePoseBases(
                leftEye,
                rightEye,
                leftEyelid,
                rightEyelid,
                leftEye == null ? Quaternion.identity : ResolveBaseRotation(leftEye, lastLeftEyeOffset, animatorDrivenPose),
                rightEye == null ? Quaternion.identity : ResolveBaseRotation(rightEye, lastRightEyeOffset, animatorDrivenPose),
                leftEyelid == null ? Quaternion.identity : ResolveBaseRotation(leftEyelid, lastLeftEyelidOffset, animatorDrivenPose),
                rightEyelid == null ? Quaternion.identity : ResolveBaseRotation(rightEyelid, lastRightEyelidOffset, animatorDrivenPose));
        }

        private void ApplyForcedEyePose(EyePoseBases eyeBases, Transform target)
        {
            if (CurrentEyeBlend <= 0.0001f ||
                eyeBases.LeftEye == null ||
                eyeBases.RightEye == null)
            {
                LastClampedEyeYawDegrees = 0f;
                LastClampedEyePitchDegrees = 0f;
                ApplyEyeOffsets(eyeBases, Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity);
                return;
            }

            Vector3 eyeCenter = (eyeBases.LeftEye.position + eyeBases.RightEye.position) * 0.5f;
            Vector3 worldDirection = ResolveForcedEyeDesiredDirection(eyeCenter, target);
            if (worldDirection.sqrMagnitude <= 0.0001f)
            {
                LastClampedEyeYawDegrees = 0f;
                LastClampedEyePitchDegrees = 0f;
                ApplyEyeOffsets(eyeBases, Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity);
                return;
            }

            Transform directionSpace = ResolveDirectionSpace();
            Vector3 localDirection = directionSpace.InverseTransformDirection(worldDirection.normalized);
            float horizontalMagnitude = new Vector2(localDirection.x, localDirection.z).magnitude;
            float yawDegrees = Mathf.Clamp(
                Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg,
                -forcedEyeMaxYawDegrees,
                forcedEyeMaxYawDegrees);
            float pitchDegrees = Mathf.Clamp(
                Mathf.Atan2(localDirection.y, horizontalMagnitude) * Mathf.Rad2Deg,
                -forcedEyeMaxPitchDegrees,
                forcedEyeMaxPitchDegrees);

            LastClampedEyeYawDegrees = yawDegrees;
            LastClampedEyePitchDegrees = pitchDegrees;

            Quaternion leftEyeOffset = ResolveEyeAimOffset(
                eyeBases.LeftEye,
                leftEyeEndReference,
                eyeBases.LeftEyeBase,
                target);
            Quaternion rightEyeOffset = ResolveEyeAimOffset(
                eyeBases.RightEye,
                rightEyeEndReference,
                eyeBases.RightEyeBase,
                target);
            Quaternion eyelidOffset = Quaternion.Euler(
                -pitchDegrees * 0.35f * CurrentEyeBlend,
                yawDegrees * 0.25f * CurrentEyeBlend,
                0f);

            ApplyEyeOffsets(eyeBases, leftEyeOffset, rightEyeOffset, eyelidOffset, eyelidOffset);
        }

        private Quaternion ResolveEyeAimOffset(
            Transform? eye,
            Transform? eyeEnd,
            Quaternion eyeBase,
            Transform target)
        {
            if (eye == null || CurrentEyeBlend <= 0.0001f || !IsFinite(eyeBase))
            {
                return Quaternion.identity;
            }

            Quaternion previousLocalRotation = eye.localRotation;
            eye.localRotation = eyeBase;

            Vector3 currentDirection = eyeEnd == null
                ? eye.forward
                : eyeEnd.position - eye.position;
            Vector3 desiredDirection = ResolveForcedEyeDesiredDirection(eye.position, target);
            if (currentDirection.sqrMagnitude <= 0.0001f || desiredDirection.sqrMagnitude <= 0.0001f)
            {
                eye.localRotation = previousLocalRotation;
                return Quaternion.identity;
            }

            currentDirection.Normalize();
            desiredDirection.Normalize();
            Quaternion worldDelta = Quaternion.FromToRotation(currentDirection, desiredDirection);
            float angle = Quaternion.Angle(Quaternion.identity, worldDelta);
            float maxAngle = Mathf.Sqrt(
                forcedEyeMaxYawDegrees * forcedEyeMaxYawDegrees +
                forcedEyeMaxPitchDegrees * forcedEyeMaxPitchDegrees);
            if (angle > maxAngle && angle > 0.0001f)
            {
                worldDelta = Quaternion.Slerp(Quaternion.identity, worldDelta, maxAngle / angle);
            }

            worldDelta = Quaternion.Slerp(Quaternion.identity, worldDelta, CurrentEyeBlend);
            Quaternion targetLocal = ResolveTargetLocalRotation(eye, worldDelta * ResolveBaseWorldRotation(eye, eyeBase));
            eye.localRotation = previousLocalRotation;
            if (!IsFinite(targetLocal))
            {
                return Quaternion.identity;
            }

            Quaternion localOffset = Quaternion.Inverse(eyeBase) * targetLocal;
            return IsFinite(localOffset) ? localOffset : Quaternion.identity;
        }

        private Vector3 ResolveForcedEyeDesiredDirection(Vector3 fromPosition, Transform target)
        {
            return ResolveLookWorldDirection(fromPosition, target);
        }

        private Vector3 ResolveLookWorldDirection(Vector3 fromPosition, Transform target)
        {
            Camera? camera = forcedLookCamera;
            if (camera != null &&
                forcedLookTarget == target &&
                camera.isActiveAndEnabled &&
                camera.gameObject.activeInHierarchy &&
                camera.orthographic)
            {
                return -camera.transform.forward;
            }

            if (Application.isPlaying)
            {
                Camera? mainCamera = Camera.main;
                if (mainCamera != null &&
                    mainCamera.transform == target &&
                    mainCamera.isActiveAndEnabled &&
                    mainCamera.gameObject.activeInHierarchy &&
                    mainCamera.orthographic)
                {
                    return -mainCamera.transform.forward;
                }
            }

            Camera? targetCamera = target.GetComponent<Camera>();
            if (targetCamera != null &&
                targetCamera.isActiveAndEnabled &&
                targetCamera.gameObject.activeInHierarchy &&
                targetCamera.orthographic)
            {
                return -targetCamera.transform.forward;
            }

            return target.position - fromPosition;
        }

        private bool TryApplyForcedLandmarkPose(
            Transform head,
            Transform neck,
            Quaternion headBase,
            Quaternion neckBase,
            Transform target)
        {
            Transform? muzzle = ResolveNamedReference(ref muzzleReference, head, "nose");
            if (muzzle == null || CurrentBlend <= 0.0001f)
            {
                return false;
            }

            if (!IsFinite(headBase) || !IsFinite(neckBase))
            {
                return false;
            }

            neck.localRotation = neckBase;
            head.localRotation = headBase;

            Vector3 muzzleDirection = muzzle.position - head.position;
            if (muzzleDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            muzzleDirection.Normalize();
            Vector3 desiredDirection = ResolveLookWorldDirection(muzzle.position, target);
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            desiredDirection.Normalize();
            Quaternion desiredDelta = Quaternion.FromToRotation(muzzleDirection, desiredDirection);
            float maxDegrees = Mathf.Sqrt(
                forcedMaxYawDegrees * forcedMaxYawDegrees +
                forcedMaxPitchDegrees * forcedMaxPitchDegrees);
            float angle = Quaternion.Angle(Quaternion.identity, desiredDelta);
            if (angle > maxDegrees && angle > 0.0001f)
            {
                desiredDelta = Quaternion.Slerp(Quaternion.identity, desiredDelta, maxDegrees / angle);
            }

            desiredDelta = Quaternion.Slerp(Quaternion.identity, desiredDelta, CurrentBlend);
            Quaternion neckWorldDelta = Quaternion.Slerp(Quaternion.identity, desiredDelta, 0.28f);
            Quaternion headWorldDelta = Quaternion.Slerp(Quaternion.identity, desiredDelta, 0.72f);

            if (!TryApplyWorldDeltas(head, neck, headBase, neckBase, headWorldDelta, neckWorldDelta))
            {
                return false;
            }

            Vector3 localDirection = ResolveDirectionSpace().InverseTransformDirection(desiredDirection);
            float horizontalMagnitude = new Vector2(localDirection.x, localDirection.z).magnitude;
            LastClampedYawDegrees = Mathf.Clamp(
                Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg,
                -forcedMaxYawDegrees,
                forcedMaxYawDegrees);
            LastClampedPitchDegrees = Mathf.Clamp(
                Mathf.Atan2(localDirection.y, horizontalMagnitude) * Mathf.Rad2Deg,
                -forcedMaxPitchDegrees,
                forcedMaxPitchDegrees);
            return true;
        }

        private static Transform? ResolveNamedReference(ref Transform? reference, Transform root, string childName)
        {
            if (reference != null)
            {
                return reference;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == childName)
                {
                    reference = children[i];
                    return reference;
                }
            }

            return null;
        }

        private bool TryApplyWorldDeltas(
            Transform head,
            Transform neck,
            Quaternion headBase,
            Quaternion neckBase,
            Quaternion headWorldDelta,
            Quaternion neckWorldDelta)
        {
            if (!IsFinite(headBase) ||
                !IsFinite(neckBase) ||
                !IsFinite(headWorldDelta) ||
                !IsFinite(neckWorldDelta))
            {
                return false;
            }

            Quaternion neckBaseWorld = ResolveBaseWorldRotation(neck, neckBase);
            Quaternion neckTargetLocal = ResolveTargetLocalRotation(neck, neckWorldDelta * neckBaseWorld);
            if (!IsFinite(neckTargetLocal))
            {
                return false;
            }

            neck.localRotation = neckTargetLocal;

            Quaternion headBaseWorld = ResolveBaseWorldRotation(head, headBase);
            Quaternion headTargetLocal = ResolveTargetLocalRotation(head, headWorldDelta * headBaseWorld);
            if (!IsFinite(headTargetLocal))
            {
                neck.localRotation = neckBase;
                return false;
            }

            head.localRotation = headTargetLocal;

            Quaternion neckOffset = Quaternion.Inverse(neckBase) * neckTargetLocal;
            Quaternion headOffset = Quaternion.Inverse(headBase) * headTargetLocal;
            if (!IsFinite(neckOffset) || !IsFinite(headOffset))
            {
                neck.localRotation = neckBase;
                head.localRotation = headBase;
                return false;
            }

            lastNeckOffset = neckOffset;
            lastHeadOffset = headOffset;
            return true;
        }

        private static Quaternion ResolveBaseWorldRotation(Transform bone, Quaternion localBase)
        {
            Transform? parent = bone.parent;
            return parent == null ? localBase : parent.rotation * localBase;
        }

        private static Quaternion ResolveTargetLocalRotation(Transform bone, Quaternion targetWorldRotation)
        {
            Transform? parent = bone.parent;
            return parent == null ? targetWorldRotation : Quaternion.Inverse(parent.rotation) * targetWorldRotation;
        }

        private void UpdateForcedLook(float deltaTime)
        {
            if (forcedLookRemainingSeconds <= 0f)
            {
                forcedLookRemainingSeconds = 0f;
                if (CurrentBlend <= ResolveTargetBlend(ResolveProfile(CurrentReadiness)) + 0.001f &&
                    CurrentEyeBlend <= 0.001f)
                {
                    forcedLookTarget = null;
                    forcedLookCamera = null;
                }

                return;
            }

            forcedLookRemainingSeconds = Mathf.Max(0f, forcedLookRemainingSeconds - Mathf.Max(0f, deltaTime));
        }

        private void ResetGlanceTimer()
        {
            LookProfile profile = ResolveProfile(CurrentReadiness);
            glanceRemaining = 0f;
            ScheduleNextGlance(profile);
        }

        private void ScheduleNextGlance(LookProfile profile)
        {
            glanceCooldownRemaining = Random.Range(
                profile.GlanceCooldownMinSeconds,
                Mathf.Max(profile.GlanceCooldownMinSeconds, profile.GlanceCooldownMaxSeconds));
        }

        private float Smooth(float current, float target, float deltaTime)
        {
            if (smoothSeconds <= 0f || deltaTime <= 0f)
            {
                return target;
            }

            return Mathf.SmoothDamp(current, target, ref blendVelocity, smoothSeconds, Mathf.Infinity, deltaTime);
        }

        private float SmoothForcedRelease(float current, float target, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return current;
            }

            float smoothTime = target > current ? smoothSeconds : forcedReleaseSmoothSeconds;
            if (smoothTime <= 0f)
            {
                return target;
            }

            return Mathf.SmoothDamp(current, target, ref forcedReleaseBlendVelocity, smoothTime, Mathf.Infinity, deltaTime);
        }

        private float SmoothEyeBlend(float current, float target, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return current;
            }

            float smoothTime = target > current ? smoothSeconds : forcedReleaseSmoothSeconds;
            if (smoothTime <= 0f)
            {
                return target;
            }

            return Mathf.SmoothDamp(current, target, ref eyeBlendVelocity, smoothTime, Mathf.Infinity, deltaTime);
        }

        private bool IsAnimatorDrivingPose()
        {
            ResolvePoseAnimator();
            return poseAnimator != null &&
                poseAnimator.isActiveAndEnabled &&
                poseAnimator.runtimeAnimatorController != null;
        }

        private void ResolvePoseAnimator()
        {
            if (poseAnimator != null)
            {
                return;
            }

            poseAnimator = GetComponent<Animator>();
            if (poseAnimator == null)
            {
                poseAnimator = GetComponentInChildren<Animator>(includeInactive: true);
            }
        }

        private static Quaternion ResolveBaseRotation(
            Transform bone,
            Quaternion previousOffset,
            bool animatorDrivenPose)
        {
            Quaternion current = bone.localRotation;
            if (!IsFinite(current))
            {
                return Quaternion.identity;
            }

            if (animatorDrivenPose)
            {
                return current;
            }

            return RemovePreviousOffset(current, previousOffset);
        }

        private static Quaternion RemovePreviousOffset(Quaternion current, Quaternion previousOffset)
        {
            if (!IsFinite(previousOffset) || Quaternion.Dot(previousOffset, previousOffset) <= 0.0001f)
            {
                return current;
            }

            Quaternion baseRotation = current * Quaternion.Inverse(previousOffset);
            return IsFinite(baseRotation) ? baseRotation : current;
        }

        private void ApplyOffsets(
            Transform head,
            Transform neck,
            Quaternion headBase,
            Quaternion neckBase,
            Quaternion headOffset,
            Quaternion neckOffset)
        {
            head.localRotation = headBase * headOffset;
            neck.localRotation = neckBase * neckOffset;

            lastHeadOffset = headOffset;
            lastNeckOffset = neckOffset;
        }

        private void ApplyEyeOffsets(
            EyePoseBases eyeBases,
            Quaternion leftEyeOffset,
            Quaternion rightEyeOffset,
            Quaternion leftEyelidOffset,
            Quaternion rightEyelidOffset)
        {
            if (eyeBases.LeftEye != null)
            {
                eyeBases.LeftEye.localRotation = eyeBases.LeftEyeBase * leftEyeOffset;
                lastLeftEyeOffset = leftEyeOffset;
            }
            else
            {
                lastLeftEyeOffset = Quaternion.identity;
            }

            if (eyeBases.RightEye != null)
            {
                eyeBases.RightEye.localRotation = eyeBases.RightEyeBase * rightEyeOffset;
                lastRightEyeOffset = rightEyeOffset;
            }
            else
            {
                lastRightEyeOffset = Quaternion.identity;
            }

            if (eyeBases.LeftEyelid != null)
            {
                eyeBases.LeftEyelid.localRotation = eyeBases.LeftEyelidBase * leftEyelidOffset;
                lastLeftEyelidOffset = leftEyelidOffset;
            }
            else
            {
                lastLeftEyelidOffset = Quaternion.identity;
            }

            if (eyeBases.RightEyelid != null)
            {
                eyeBases.RightEyelid.localRotation = eyeBases.RightEyelidBase * rightEyelidOffset;
                lastRightEyelidOffset = rightEyelidOffset;
            }
            else
            {
                lastRightEyelidOffset = Quaternion.identity;
            }
        }

        private static bool IsFinite(Quaternion rotation)
        {
            return IsFinite(rotation.x) &&
                IsFinite(rotation.y) &&
                IsFinite(rotation.z) &&
                IsFinite(rotation.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static LookProfile ResolveProfile(TargetReadiness readiness)
        {
            return readiness switch
            {
                TargetReadiness.Progressing => new LookProfile(
                    MaxYawDegrees: 18f,
                    MaxPitchDegrees: 12f,
                    HeadDownPitchDegrees: -4f,
                    BaseBlend: 0.12f,
                    GlanceBlend: 0.38f,
                    ExtractionBlend: 0.02f,
                    GlanceCooldownMinSeconds: 2.5f,
                    GlanceCooldownMaxSeconds: 5.5f,
                    GlanceDurationSeconds: 1.15f),
                TargetReadiness.OneClearAway => new LookProfile(
                    MaxYawDegrees: 28f,
                    MaxPitchDegrees: 18f,
                    HeadDownPitchDegrees: 3f,
                    BaseBlend: 0.28f,
                    GlanceBlend: 0.58f,
                    ExtractionBlend: 0.04f,
                    GlanceCooldownMinSeconds: 0.9f,
                    GlanceCooldownMaxSeconds: 2.4f,
                    GlanceDurationSeconds: 1.35f),
                TargetReadiness.ExtractableLatched or TargetReadiness.Extracted => new LookProfile(
                    MaxYawDegrees: 10f,
                    MaxPitchDegrees: 8f,
                    HeadDownPitchDegrees: 0f,
                    BaseBlend: 0f,
                    GlanceBlend: 0f,
                    ExtractionBlend: 0f,
                    GlanceCooldownMinSeconds: 999f,
                    GlanceCooldownMaxSeconds: 999f,
                    GlanceDurationSeconds: 0f),
                _ => new LookProfile(
                    MaxYawDegrees: 8f,
                    MaxPitchDegrees: 8f,
                    HeadDownPitchDegrees: -12f,
                    BaseBlend: 0.04f,
                    GlanceBlend: 0.18f,
                    ExtractionBlend: 0f,
                    GlanceCooldownMinSeconds: 6f,
                    GlanceCooldownMaxSeconds: 10f,
                    GlanceDurationSeconds: 0.75f),
            };
        }

        private readonly record struct LookProfile(
            float MaxYawDegrees,
            float MaxPitchDegrees,
            float HeadDownPitchDegrees,
            float BaseBlend,
            float GlanceBlend,
            float ExtractionBlend,
            float GlanceCooldownMinSeconds,
            float GlanceCooldownMaxSeconds,
            float GlanceDurationSeconds)
        {
            public float NeckYawShare => 0.45f;

            public float HeadYawShare => 0.55f;

            public float NeckPitchShare => 0.35f;

            public float HeadPitchShare => 0.65f;
        }

        private readonly record struct EyePoseBases(
            Transform? LeftEye,
            Transform? RightEye,
            Transform? LeftEyelid,
            Transform? RightEyelid,
            Quaternion LeftEyeBase,
            Quaternion RightEyeBase,
            Quaternion LeftEyelidBase,
            Quaternion RightEyelidBase);
    }
}
