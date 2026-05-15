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
        [SerializeField] private float smoothSeconds = 0.16f;
        [SerializeField] private float forcedMaxYawDegrees = 80f;
        [SerializeField] private float forcedMaxPitchDegrees = 42f;
        [SerializeField] private float forcedLookUpDegrees = 34f;
        [SerializeField] private float forcedLookRightDegrees = 18f;
        [SerializeField] private Vector3 forcedHeadEulerOffset = new Vector3(-34f, 22f, 0f);
        [SerializeField] private Vector3 forcedNeckEulerOffset = new Vector3(-10f, 8f, 0f);

        private Quaternion lastHeadOffset = Quaternion.identity;
        private Quaternion lastNeckOffset = Quaternion.identity;
        private float blendVelocity;
        private float glanceCooldownRemaining;
        private float glanceRemaining;
        private Transform? forcedLookTarget;
        private float forcedLookRemainingSeconds;
        private Animator? poseAnimator;
        private bool extracting;

        public TargetReadiness CurrentReadiness { get; private set; } = TargetReadiness.Trapped;

        public float CurrentBlend { get; private set; }

        public float LastClampedYawDegrees { get; private set; }

        public float LastClampedPitchDegrees { get; private set; }

        public float ActiveMaxYawDegrees => ResolveProfile(CurrentReadiness).MaxYawDegrees;

        public float ActiveMaxPitchDegrees => ResolveProfile(CurrentReadiness).MaxPitchDegrees;

        public float ForcedMaxYawDegrees => forcedMaxYawDegrees;

        public float ForcedMaxPitchDegrees => forcedMaxPitchDegrees;

        public float ForcedLookUpDegrees => forcedLookUpDegrees;

        public float ForcedLookRightDegrees => forcedLookRightDegrees;

        public Vector3 ForcedHeadEulerOffset => forcedHeadEulerOffset;

        public Vector3 ForcedNeckEulerOffset => forcedNeckEulerOffset;

        public float ActiveGlanceCooldownMinSeconds => ResolveProfile(CurrentReadiness).GlanceCooldownMinSeconds;

        public float ActiveGlanceCooldownMaxSeconds => ResolveProfile(CurrentReadiness).GlanceCooldownMaxSeconds;

        public void ApplyReadiness(TargetReadiness readiness)
        {
            CurrentReadiness = readiness;
            extracting = readiness is TargetReadiness.ExtractableLatched or TargetReadiness.Extracted;
            ResetGlanceTimer();
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

            UpdateForcedLook(deltaTime);

            bool animatorDrivenPose = IsAnimatorDrivingPose();
            Quaternion headBase = ResolveBaseRotation(head, lastHeadOffset, animatorDrivenPose);
            Quaternion neckBase = ResolveBaseRotation(neck, lastNeckOffset, animatorDrivenPose);
            Transform? target = ResolveLookTarget();

            if (target == null)
            {
                CurrentBlend = Smooth(CurrentBlend, 0f, deltaTime);
                ApplyOffsets(head, neck, headBase, neckBase, Quaternion.identity, Quaternion.identity);
                return;
            }

            bool isForcedLook = forcedLookRemainingSeconds > 0f && forcedLookTarget != null;
            LookProfile profile = ResolveActiveProfile(isForcedLook);
            UpdateGlance(profile, deltaTime);

            float targetBlend = ResolveTargetBlend(profile);
            CurrentBlend = Smooth(CurrentBlend, targetBlend, deltaTime);

            if (isForcedLook)
            {
                ApplyForcedPose(head, neck, headBase, neckBase);
                return;
            }

            Vector3 worldDirection = target.position - head.position;
            if (worldDirection.sqrMagnitude <= 0.0001f || CurrentBlend <= 0.0001f)
            {
                ApplyOffsets(head, neck, headBase, neckBase, Quaternion.identity, Quaternion.identity);
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
        }

        private Transform? ResolveLookTarget()
        {
            if (forcedLookRemainingSeconds > 0f && forcedLookTarget != null)
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

        private void ApplyForcedPose(
            Transform head,
            Transform neck,
            Quaternion headBase,
            Quaternion neckBase)
        {
            if (TryApplyForcedLandmarkPose(head, neck, headBase, neckBase))
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

        private bool TryApplyForcedLandmarkPose(
            Transform head,
            Transform neck,
            Quaternion headBase,
            Quaternion neckBase)
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
            Vector3 dogRight = ResolveDogRight(head);
            Vector3 dogUp = Vector3.Cross(muzzleDirection, dogRight);
            if (dogUp.sqrMagnitude <= 0.0001f)
            {
                dogUp = ResolveDirectionSpace().up;
            }

            dogUp.Normalize();
            if (Vector3.Dot(dogUp, ResolveDirectionSpace().up) < 0f)
            {
                dogUp = -dogUp;
            }

            Quaternion rightDelta = ResolveSignedDelta(dogUp, forcedLookRightDegrees * CurrentBlend, muzzleDirection, dogRight);
            Vector3 rightAdjustedMuzzle = rightDelta * muzzleDirection;
            Quaternion upDelta = ResolveSignedDelta(dogRight, forcedLookUpDegrees * CurrentBlend, rightAdjustedMuzzle, dogUp);
            Quaternion headWorldDelta = upDelta * rightDelta;
            Quaternion neckWorldDelta = Quaternion.Slerp(Quaternion.identity, headWorldDelta, 0.28f);

            if (!TryApplyWorldDeltas(head, neck, headBase, neckBase, headWorldDelta, neckWorldDelta))
            {
                return false;
            }

            LastClampedYawDegrees = forcedLookRightDegrees * CurrentBlend;
            LastClampedPitchDegrees = forcedLookUpDegrees * CurrentBlend;
            return true;
        }

        private Vector3 ResolveDogRight(Transform head)
        {
            Transform? leftEye = ResolveNamedReference(ref leftEyeReference, head, "eye.L");
            Transform? rightEye = ResolveNamedReference(ref rightEyeReference, head, "eye.R");
            if (leftEye != null && rightEye != null)
            {
                Vector3 eyeRight = rightEye.position - leftEye.position;
                if (eyeRight.sqrMagnitude > 0.0001f)
                {
                    return eyeRight.normalized;
                }
            }

            return head.right;
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

        private static Quaternion ResolveSignedDelta(
            Vector3 axis,
            float degrees,
            Vector3 currentDirection,
            Vector3 desiredDirection)
        {
            if (axis.sqrMagnitude <= 0.0001f || Mathf.Abs(degrees) <= 0.0001f)
            {
                return Quaternion.identity;
            }

            Quaternion positive = Quaternion.AngleAxis(degrees, axis.normalized);
            Quaternion negative = Quaternion.AngleAxis(-degrees, axis.normalized);
            return Vector3.Dot(positive * currentDirection, desiredDirection) >=
                Vector3.Dot(negative * currentDirection, desiredDirection)
                ? positive
                : negative;
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
                forcedLookTarget = null;
                forcedLookRemainingSeconds = 0f;
                return;
            }

            forcedLookRemainingSeconds = Mathf.Max(0f, forcedLookRemainingSeconds - Mathf.Max(0f, deltaTime));
            if (forcedLookRemainingSeconds <= 0f)
            {
                forcedLookTarget = null;
            }
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
    }
}
