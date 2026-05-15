using System.Collections.Generic;
using NUnit.Framework;
using Rescue.Core.State;
using Rescue.Unity.Presentation.Targets;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rescue.Unity.Targets.Tests
{
    public sealed class TargetPuppyAnimatorTests
    {
        private const int BaseLayer = 0;
        private const string DaisyTargetPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Targets/PF_Target_Daisy_Puppy.prefab";
        private const string BaseLayerName = "Base Layer";
        private const string TrappedIdleState = "Target_Trapped_Idle";
        private const string ProgressingIdleState = "Target_Progress_Idle";
        private const string OneClearAwayIdleState = "Target_OneClearAway_Idle";
        private const string ExtractStartState = "Target_Extract_Start";
        private const string ExtractAirState = "Target_Extract_Air";
        private const string ProgressingFidgetState = "Target_Progress_Fidget";
        private const string OneClearAwayBarkState = "Target_OneClearAway_Bark";

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
            LogAssert.Expect(
                LogType.Warning,
                "TargetPuppyAnimator could not find animator state 'TrappedIdle' on controller '<none>' layer 0 '<missing>' with 0 layer(s). Tried 'TrappedIdle' and '<missing>.TrappedIdle'.");

            puppyAnimator.ApplyReadiness(TargetReadiness.Trapped);

            Assert.That(unityAnimator.applyRootMotion, Is.False);
        }

        [Test]
        public void TargetPuppyAnimator_DaisyPrefabRuntimeAnimatorResolvesReadinessStates()
        {
            TargetPuppyAnimator puppyAnimator = InstantiateDaisyPrefab(out Animator unityAnimator);

            AssertDaisyStateExists(unityAnimator, TrappedIdleState);
            AssertDaisyStateExists(unityAnimator, ProgressingIdleState);
            AssertDaisyStateExists(unityAnimator, ProgressingFidgetState);
            AssertDaisyStateExists(unityAnimator, OneClearAwayIdleState);
            AssertDaisyStateExists(unityAnimator, OneClearAwayBarkState);

            puppyAnimator.ApplyReadiness(TargetReadiness.Trapped);
            Assert.That(puppyAnimator.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.Trapped));
            Assert.That(puppyAnimator.CurrentAppliedStateName, Is.EqualTo(TrappedIdleState));
            AssertAnimatorTargetsState(unityAnimator, TrappedIdleState);

            puppyAnimator.ApplyReadiness(TargetReadiness.Progressing);
            Assert.That(puppyAnimator.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.Progressing));
            Assert.That(puppyAnimator.CurrentAppliedStateName, Is.EqualTo(ProgressingIdleState));
            AssertAnimatorTargetsState(unityAnimator, ProgressingIdleState);

            puppyAnimator.ApplyReadiness(TargetReadiness.OneClearAway);
            Assert.That(puppyAnimator.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.OneClearAway));
            Assert.That(puppyAnimator.CurrentAppliedStateName, Is.EqualTo(OneClearAwayBarkState));
            AssertAnimatorTargetsState(unityAnimator, OneClearAwayBarkState);

            puppyAnimator.AdvanceProceduralAnimationForTests(1f);
            Assert.That(puppyAnimator.CurrentAppliedStateName, Is.EqualTo(OneClearAwayIdleState));
            AssertAnimatorTargetsState(unityAnimator, OneClearAwayIdleState);
            Assert.That(unityAnimator.applyRootMotion, Is.False);
        }

        [Test]
        public void TargetPuppyAnimator_ProgressingFidgetTriggersAfterCooldownAndReturnsToIdle()
        {
            TargetPuppyAnimator animator = CreateAnimator();
            SetPrivateField(animator, "progressingIdleState", "ProgressingIdle");
            SetPrivateField(animator, "progressingFidgetState", "ProgressingFidget");
            SetPrivateField(animator, "progressingFidgetCooldownMinSeconds", 0f);
            SetPrivateField(animator, "progressingFidgetCooldownMaxSeconds", 0f);
            SetPrivateField(animator, "progressingFidgetDurationSeconds", 0.25f);

            animator.ApplyReadiness(TargetReadiness.Progressing);

            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("ProgressingIdle"));

            animator.AdvanceProceduralAnimationForTests(0.01f);
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("ProgressingFidget"));

            animator.AdvanceProceduralAnimationForTests(0.3f);
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("ProgressingIdle"));
        }

        [Test]
        public void TargetPuppyAnimator_OneClearAwayBarkIsEntryAndRareRepeatIntent()
        {
            TargetPuppyAnimator animator = CreateAnimator();
            SetPrivateField(animator, "oneClearAwayIdleState", "OneClearAwayIdle");
            SetPrivateField(animator, "oneClearAwayBarkState", "OneClearAwayBark");
            SetPrivateField(animator, "oneClearAwayBarkDurationSeconds", 0.25f);
            SetPrivateField(animator, "oneClearAwayBarkRepeatCooldownMinSeconds", 0f);
            SetPrivateField(animator, "oneClearAwayBarkRepeatCooldownMaxSeconds", 0f);

            animator.ApplyReadiness(TargetReadiness.OneClearAway);

            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("OneClearAwayBark"));

            animator.AdvanceProceduralAnimationForTests(0.3f);
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("OneClearAwayIdle"));

            animator.AdvanceProceduralAnimationForTests(0.01f);
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("OneClearAwayBark"));
        }

        [Test]
        public void TargetPuppyAnimator_PlayExtractSuppressesProceduralFidgetsAndForwardsLookAt()
        {
            TargetPuppyAnimator animator = CreateAnimator();
            TargetPuppyLookAt lookAt = animator.gameObject.AddComponent<TargetPuppyLookAt>();
            SetPrivateField(animator, "progressingIdleState", "ProgressingIdle");
            SetPrivateField(animator, "progressingFidgetState", "ProgressingFidget");
            SetPrivateField(animator, "extractStartState", "ExtractStart");
            SetPrivateField(animator, "progressingFidgetCooldownMinSeconds", 0f);
            SetPrivateField(animator, "progressingFidgetCooldownMaxSeconds", 0f);

            animator.ApplyReadiness(TargetReadiness.Progressing);
            animator.PlayExtract();
            animator.AdvanceProceduralAnimationForTests(10f);

            Assert.That(animator.IsExtracting, Is.True);
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("ExtractStart"));
            Assert.That(lookAt.CurrentReadiness, Is.EqualTo(TargetReadiness.Extracted));
        }

        [Test]
        public void TargetPuppyAnimator_DebugAnimationCompletesAndRestoresReadinessIdle()
        {
            TargetPuppyAnimator animator = CreateAnimatorWithUnityAnimator();
            SetPrivateField(animator, "progressingIdleState", "ProgressingIdle");
            AnimationClip clip = CreateClip("DebugScratch", 0.25f);

            animator.ApplyReadiness(TargetReadiness.Progressing);

            Assert.That(animator.PlayDebugAnimationClip(clip, repeat: false), Is.True);
            Assert.That(animator.IsDebugAnimationPlaying, Is.True);
            Assert.That(animator.CurrentDebugAnimationName, Is.EqualTo("DebugScratch"));

            animator.AdvanceDebugAnimationForTests(0.3f);

            Assert.That(animator.IsDebugAnimationPlaying, Is.False);
            Assert.That(animator.CurrentDebugAnimationName, Is.Empty);
            Assert.That(animator.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.Progressing));
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("ProgressingIdle"));
        }

        [Test]
        public void TargetPuppyAnimator_DebugRepeatSequenceLoopsUntilStopped()
        {
            TargetPuppyAnimator animator = CreateAnimatorWithUnityAnimator();
            SetPrivateField(animator, "trappedIdleState", "TrappedIdle");
            AnimationClip first = CreateClip("DebugFirst", 0.1f);
            AnimationClip second = CreateClip("DebugSecond", 0.1f);

            animator.ApplyReadiness(TargetReadiness.Trapped);

            Assert.That(animator.PlayDebugAnimationSequence(new[] { first, second }, repeat: true), Is.True);
            Assert.That(animator.CurrentDebugAnimationName, Is.EqualTo("DebugFirst"));

            animator.AdvanceDebugAnimationForTests(0.12f);
            Assert.That(animator.IsDebugAnimationPlaying, Is.True);
            Assert.That(animator.CurrentDebugAnimationName, Is.EqualTo("DebugSecond"));

            animator.AdvanceDebugAnimationForTests(0.12f);
            Assert.That(animator.IsDebugAnimationPlaying, Is.True);
            Assert.That(animator.CurrentDebugAnimationName, Is.EqualTo("DebugFirst"));

            animator.StopDebugAnimation();

            Assert.That(animator.IsDebugAnimationPlaying, Is.False);
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("TrappedIdle"));
        }

        [Test]
        public void TargetPuppyAnimator_DebugStopRestoresIdle()
        {
            TargetPuppyAnimator animator = CreateAnimatorWithUnityAnimator();
            SetPrivateField(animator, "oneClearAwayIdleState", "OneClearAwayIdle");
            AnimationClip clip = CreateClip("DebugBark", 0.25f);

            animator.ApplyReadiness(TargetReadiness.OneClearAway);
            Assert.That(animator.PlayDebugAnimationClip(clip, repeat: false), Is.True);

            animator.StopDebugAnimation();

            Assert.That(animator.IsDebugAnimationPlaying, Is.False);
            Assert.That(animator.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.OneClearAway));
            Assert.That(animator.CurrentAppliedStateName, Is.EqualTo("OneClearAwayIdle"));
        }

        [Test]
        public void TargetPuppyAnimator_DebugAnimationMissingAnimatorOrClipDoesNotThrow()
        {
            TargetPuppyAnimator animator = CreateAnimator();

            Assert.DoesNotThrow(() => animator.PlayDebugAnimationClip(null, repeat: false));
            Assert.DoesNotThrow(() => animator.PlayDebugAnimationSequence(null, repeat: false));
            Assert.That(animator.PlayDebugAnimationClip(CreateClip("NoAnimator", 0.1f), repeat: false), Is.False);
            Assert.That(animator.IsDebugAnimationPlaying, Is.False);
        }

        [Test]
        public void TargetPuppyAnimator_DaisyPrefabExtractableLatchedDoesNotEnterExtractState()
        {
            TargetPuppyAnimator puppyAnimator = InstantiateDaisyPrefab(out Animator unityAnimator);

            puppyAnimator.ApplyReadiness(TargetReadiness.OneClearAway);
            AssertAnimatorTargetsState(unityAnimator, OneClearAwayBarkState);

            puppyAnimator.ApplyReadiness(TargetReadiness.ExtractableLatched);

            Assert.That(puppyAnimator.CurrentAppliedReadiness, Is.EqualTo(TargetReadiness.ExtractableLatched));
            Assert.That(puppyAnimator.CurrentAppliedStateName, Is.EqualTo(OneClearAwayBarkState));
            Assert.That(puppyAnimator.IsExtracting, Is.False);
            AssertAnimatorTargetsState(unityAnimator, OneClearAwayBarkState);
        }

        [Test]
        public void TargetPuppyAnimator_DaisyPrefabPlayExtractEntersExtractStartState()
        {
            TargetPuppyAnimator puppyAnimator = InstantiateDaisyPrefab(out Animator unityAnimator);

            AssertDaisyStateExists(unityAnimator, ExtractStartState);
            AssertDaisyStateExists(unityAnimator, ExtractAirState);

            puppyAnimator.PlayExtract();

            Assert.That(puppyAnimator.IsExtracting, Is.True);
            Assert.That(puppyAnimator.CurrentAppliedStateName, Is.EqualTo(ExtractStartState));
            AssertAnimatorTargetsState(unityAnimator, ExtractStartState);
            Assert.That(unityAnimator.applyRootMotion, Is.False);
        }

        private TargetPuppyAnimator CreateAnimator()
        {
            GameObject gameObject = new GameObject("TargetPuppyAnimatorTestObject");
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<TargetPuppyAnimator>();
        }

        private TargetPuppyAnimator CreateAnimatorWithUnityAnimator()
        {
            GameObject gameObject = new GameObject("TargetPuppyDebugAnimatorTestObject");
            createdObjects.Add(gameObject);
            gameObject.AddComponent<Animator>();
            return gameObject.AddComponent<TargetPuppyAnimator>();
        }

        private AnimationClip CreateClip(string name, float durationSeconds)
        {
            AnimationClip clip = new AnimationClip { name = name };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "m_LocalPosition.x",
                AnimationCurve.Linear(0f, 0f, Mathf.Max(0.01f, durationSeconds), 0f));
            createdObjects.Add(clip);
            return clip;
        }

        private TargetPuppyAnimator InstantiateDaisyPrefab(out Animator unityAnimator)
        {
            GameObject? prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DaisyTargetPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Expected Daisy prefab at {DaisyTargetPrefabPath}.");
            if (prefab is null)
            {
                throw new AssertionException($"Expected Daisy prefab at {DaisyTargetPrefabPath}.");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            createdObjects.Add(instance);

            TargetPuppyAnimator? puppyAnimator = instance.GetComponent<TargetPuppyAnimator>();
            Animator? resolvedAnimator = instance.GetComponentInChildren<Animator>(includeInactive: true);

            Assert.That(puppyAnimator, Is.Not.Null);
            Assert.That(resolvedAnimator, Is.Not.Null);
            if (puppyAnimator is null || resolvedAnimator is null)
            {
                throw new AssertionException("Expected Daisy prefab instance to include TargetPuppyAnimator and Animator.");
            }

            unityAnimator = resolvedAnimator;
            Assert.That(unityAnimator.runtimeAnimatorController, Is.Not.Null);
            return puppyAnimator;
        }

        private static void AssertDaisyStateExists(Animator unityAnimator, string stateName)
        {
            Assert.That(unityAnimator.layerCount, Is.GreaterThan(BaseLayer));
            Assert.That(unityAnimator.GetLayerName(BaseLayer), Is.EqualTo(BaseLayerName));
            Assert.That(
                unityAnimator.HasState(BaseLayer, Animator.StringToHash($"{BaseLayerName}.{stateName}")),
                Is.True,
                $"Expected Daisy controller to expose '{BaseLayerName}.{stateName}'.");
        }

        private static void AssertAnimatorTargetsState(Animator unityAnimator, string stateName)
        {
            int shortNameHash = Animator.StringToHash(stateName);
            int fullPathHash = Animator.StringToHash($"{BaseLayerName}.{stateName}");

            unityAnimator.Update(0.02f);
            AnimatorStateInfo current = unityAnimator.GetCurrentAnimatorStateInfo(BaseLayer);
            AnimatorStateInfo next = unityAnimator.GetNextAnimatorStateInfo(BaseLayer);

            bool matchesCurrent = current.shortNameHash == shortNameHash || current.fullPathHash == fullPathHash;
            bool matchesNext = next.shortNameHash == shortNameHash || next.fullPathHash == fullPathHash;
            Assert.That(
                matchesCurrent || matchesNext,
                Is.True,
                $"Expected Animator to target '{stateName}' but current={current.fullPathHash}/{current.shortNameHash}, next={next.fullPathHash}/{next.shortNameHash}.");
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
