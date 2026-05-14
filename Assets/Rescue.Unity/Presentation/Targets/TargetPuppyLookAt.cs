using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.Presentation.Targets
{
    public sealed class TargetPuppyLookAt : MonoBehaviour
    {
        [SerializeField] private Transform? headBone;
        [SerializeField] private Transform? neckBone;
        [SerializeField] private Transform? lookTarget;
        [SerializeField] private float smoothSeconds = 0.16f;

        private Quaternion lastHeadOffset = Quaternion.identity;
        private Quaternion lastNeckOffset = Quaternion.identity;
        private float blendVelocity;
        private float glanceCooldownRemaining;
        private float glanceRemaining;
        private bool extracting;

        public TargetReadiness CurrentReadiness { get; private set; } = TargetReadiness.Trapped;

        public float CurrentBlend { get; private set; }

        public float LastClampedYawDegrees { get; private set; }

        public float LastClampedPitchDegrees { get; private set; }

        public float ActiveMaxYawDegrees => ResolveProfile(CurrentReadiness).MaxYawDegrees;

        public float ActiveMaxPitchDegrees => ResolveProfile(CurrentReadiness).MaxPitchDegrees;

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

        private void Awake()
        {
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

            Quaternion headBase = RemovePreviousOffset(head, lastHeadOffset);
            Quaternion neckBase = RemovePreviousOffset(neck, lastNeckOffset);
            Transform? target = ResolveLookTarget();

            if (target == null)
            {
                CurrentBlend = Smooth(CurrentBlend, 0f, deltaTime);
                ApplyOffsets(head, neck, headBase, neckBase, Quaternion.identity, Quaternion.identity);
                return;
            }

            LookProfile profile = ResolveProfile(CurrentReadiness);
            UpdateGlance(profile, deltaTime);

            float targetBlend = ResolveTargetBlend(profile);
            CurrentBlend = Smooth(CurrentBlend, targetBlend, deltaTime);

            Vector3 worldDirection = target.position - head.position;
            if (worldDirection.sqrMagnitude <= 0.0001f || CurrentBlend <= 0.0001f)
            {
                ApplyOffsets(head, neck, headBase, neckBase, Quaternion.identity, Quaternion.identity);
                return;
            }

            Vector3 localDirection = transform.InverseTransformDirection(worldDirection.normalized);
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

            return profile.BaseBlend + (glanceRemaining > 0f ? profile.GlanceBlend : 0f);
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

        private static Quaternion RemovePreviousOffset(Transform bone, Quaternion previousOffset)
        {
            return bone.localRotation * Quaternion.Inverse(previousOffset);
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
