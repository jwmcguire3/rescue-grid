using System.Collections.Generic;
using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.Presentation.Targets;
using UnityEngine;

namespace Rescue.Unity.Targets.Tests
{
    public sealed class TargetPuppyAnimatorTests
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
        public void TargetPuppyAnimator_CanInstantiateWithoutAnimator()
        {
            TargetPuppyAnimator animator = CreateAnimator();

            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.CurrentAppliedReadiness, Is.Null);
            Assert.That(animator.CurrentAppliedStateName, Is.Empty);
            Assert.That(animator.IsExtracting, Is.False);
        }

        [Test]
        public void TargetPuppyAnimator_MissingAnimatorDoesNotThrow()
        {
            TargetPuppyAnimator animator = CreateAnimator();

            Assert.DoesNotThrow(() => animator.ApplyReadiness(TargetReadiness.Trapped));
            Assert.DoesNotThrow(animator.PlayExtract);
        }

        [Test]
        public void TargetPuppyAnimator_ApplyReadinessTrappedStoresTrappedIntent()
        {
            TargetPuppyAnimator animator = CreateAnimator();
            SetPrivateField(animator, "trappedIdleState", "TrappedIdle");

            animator.ApplyReadiness(TargetReadiness.Trapped);

            Assert.That(animator.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.Trapped));
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("TrappedIdle"));
            Assert.That(animator.IsExtracting, Is.False);
        }

        [Test]
        public void TargetPuppyAnimator_ApplyReadinessProgressingChangesIntent()
        {
            TargetPuppyAnimator animator = CreateAnimator();
            SetPrivateField(animator, "trappedIdleState", "TrappedIdle");
            SetPrivateField(animator, "progressingIdleState", "ProgressingIdle");

            animator.ApplyReadiness(TargetReadiness.Trapped);
            animator.ApplyReadiness(TargetReadiness.Progressing);

            Assert.That(animator.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.Progressing));
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("ProgressingIdle"));
            Assert.That(animator.IsExtracting, Is.False);
        }

        [Test]
        public void TargetPuppyAnimator_ApplyReadinessOneClearAwayChangesIntent()
        {
            TargetPuppyAnimator animator = CreateAnimator();
            SetPrivateField(animator, "progressingIdleState", "ProgressingIdle");
            SetPrivateField(animator, "oneClearAwayIdleState", "OneClearAwayIdle");

            animator.ApplyReadiness(TargetReadiness.Progressing);
            animator.ApplyReadiness(TargetReadiness.OneClearAway);

            Assert.That(animator.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.OneClearAway));
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("OneClearAwayIdle"));
            Assert.That(animator.IsExtracting, Is.False);
        }

        [Test]
        public void TargetPuppyAnimator_ApplyReadinessDistressedUsesTrappedFallbackIntent()
        {
            TargetPuppyAnimator animator = CreateAnimator();
            SetPrivateField(animator, "trappedIdleState", "TrappedIdle");

            animator.ApplyReadiness(TargetReadiness.Distressed);

            Assert.That(animator.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.Distressed));
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("TrappedIdle"));
            Assert.That(animator.IsExtracting, Is.False);
        }

        [Test]
        public void TargetPuppyAnimator_ApplyReadinessExtractableLatchedDoesNotExtract()
        {
            TargetPuppyAnimator animator = CreateAnimator();
            SetPrivateField(animator, "oneClearAwayIdleState", "OneClearAwayIdle");
            SetPrivateField(animator, "extractStartState", "ExtractStart");

            animator.ApplyReadiness(TargetReadiness.OneClearAway);
            animator.ApplyReadiness(TargetReadiness.ExtractableLatched);

            Assert.That(animator.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.ExtractableLatched));
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("OneClearAwayIdle"));
            Assert.That(animator.IsExtracting, Is.False);
        }

        [Test]
        public void TargetPuppyAnimator_PlayExtractStoresExtractIntent()
        {
            TargetPuppyAnimator animator = CreateAnimator();
            SetPrivateField(animator, "extractStartState", "ExtractStart");
            SetPrivateField(animator, "extractAirState", "ExtractAir");

            Assert.DoesNotThrow(animator.PlayExtract);

            Assert.That(animator.IsExtracting, Is.True);
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("ExtractStart"));
        }

        [Test]
        public void TargetPuppyAnimator_PlayExtractFallsBackToAirWhenStartIsBlank()
        {
            TargetPuppyAnimator animator = CreateAnimator();
            SetPrivateField(animator, "extractStartState", string.Empty);
            SetPrivateField(animator, "extractAirState", "ExtractAir");

            animator.PlayExtract();

            Assert.That(animator.IsExtracting, Is.True);
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("ExtractAir"));
        }

        [Test]
        public void TargetPuppyAnimator_DisablesRootMotionOnAvailableAnimator()
        {
            GameObject gameObject = new GameObject("TargetPuppyAnimatorRootMotionTestObject");
            createdObjects.Add(gameObject);
            Animator unityAnimator = gameObject.AddComponent<Animator>();
            unityAnimator.applyRootMotion = true;
            TargetPuppyAnimator puppyAnimator = gameObject.AddComponent<TargetPuppyAnimator>();
            SetPrivateField(puppyAnimator, "trappedIdleState", "TrappedIdle");

            puppyAnimator.ApplyReadiness(TargetReadiness.Trapped);

            Assert.That(unityAnimator.applyRootMotion, Is.False);
        }

        private TargetPuppyAnimator CreateAnimator()
        {
            GameObject gameObject = new GameObject("TargetPuppyAnimatorTestObject");
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<TargetPuppyAnimator>();
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
