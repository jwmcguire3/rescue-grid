#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NUnit.Framework;
using Rescue.Unity.Art.Registries;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Presentation.Targets;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rescue.PlayMode.Tests.Smoke
{
    internal static class DaisyTargetSceneAssertions
    {
        private const string DaisyTargetPrefabPath = "Assets/Rescue.Unity/Art/Prefabs/Targets/PF_Target_Daisy_Puppy.prefab";
        private const string TargetRegistryPath = "Assets/Rescue.Unity/Art/Registries/Phase1TargetVisualRegistry.asset";
        private const string DaisyAnimatorControllerName = "AC_Daisy_Target";

        public static void AssertLiveTargetsAreDaisyBacked(GameState state, BoardContentViewPresenter contentPresenter)
        {
            AssertSceneTargetRegistryIsDaisyBacked(contentPresenter);

            int liveTargetCount = 0;
            for (int targetIndex = 0; targetIndex < state.Targets.Length; targetIndex++)
            {
                TargetState target = state.Targets[targetIndex];
                if (target.Extracted)
                {
                    continue;
                }

                liveTargetCount++;
                Assert.That(
                    contentPresenter.TryGetTargetInstance(target.TargetId, out GameObject? targetObject),
                    Is.True,
                    $"Expected rendered target instance for live target '{target.TargetId}'.");
                Assert.That(targetObject, Is.Not.Null, $"Expected rendered target object for live target '{target.TargetId}'.");
                if (targetObject is null)
                {
                    continue;
                }

                AssertDaisyTargetInstance(targetObject, target);
            }

            Assert.That(liveTargetCount, Is.GreaterThan(0), "Expected the scene state to include at least one live target.");
        }

        private static void AssertDaisyTargetInstance(GameObject targetObject, TargetState target)
        {
            TargetPuppyAnimator? puppyAnimator = targetObject.GetComponentInChildren<TargetPuppyAnimator>(true);
            Assert.That(puppyAnimator, Is.Not.Null, $"Target '{target.TargetId}' should use the Daisy TargetPuppyAnimator.");
            Assert.That(
                puppyAnimator!.CurrentAppliedReadiness,
                Is.EqualTo(target.Readiness),
                $"Target '{target.TargetId}' should apply the current rules readiness to Daisy.");

            Animator? animator = targetObject.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, $"Target '{target.TargetId}' should include a child Animator.");
            Assert.That(animator!.runtimeAnimatorController, Is.Not.Null, $"Target '{target.TargetId}' should have an Animator Controller.");
            Assert.That(
                animator.runtimeAnimatorController!.name,
                Is.EqualTo(DaisyAnimatorControllerName),
                $"Target '{target.TargetId}' should use the Daisy target Animator Controller.");
            Assert.That(animator.applyRootMotion, Is.False, $"Target '{target.TargetId}' should keep root motion disabled.");

            BoardCellView? cellView = targetObject.GetComponent<BoardCellView>();
            Assert.That(cellView, Is.Not.Null, $"Target '{target.TargetId}' should carry its board cell coordinate.");
            Assert.That(cellView!.Coord, Is.EqualTo(target.Coord), $"Target '{target.TargetId}' should stay on its logical tile.");

            Vector3 rootBeforeAnimatorUpdate = targetObject.transform.position;
            animator.Update(0.05f);
            Vector3 rootAfterAnimatorUpdate = targetObject.transform.position;
            Vector2 planarDrift = new Vector2(
                rootAfterAnimatorUpdate.x - rootBeforeAnimatorUpdate.x,
                rootAfterAnimatorUpdate.z - rootBeforeAnimatorUpdate.z);
            Assert.That(
                planarDrift.magnitude,
                Is.LessThan(0.001f),
                $"Target '{target.TargetId}' root should not drift across the tile during idle animation.");

            AssertRenderersHaveValidMaterials(targetObject, target.TargetId);
        }

        private static void AssertSceneTargetRegistryIsDaisyBacked(BoardContentViewPresenter contentPresenter)
        {
            FieldInfo? field = typeof(BoardContentViewPresenter).GetField("targetRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Expected BoardContentViewPresenter to serialize a target registry field.");
            TargetVisualRegistry? registry = field?.GetValue(contentPresenter) as TargetVisualRegistry;
            Assert.That(registry, Is.Not.Null, "Scene BoardContentViewPresenter should assign the Phase 1 target registry.");
            if (registry is null)
            {
                return;
            }

            Assert.That(registry.PuppyPrefab, Is.Not.Null, "Scene target registry should assign the Daisy puppy prefab.");
            Assert.That(registry.FallbackTargetPrefab, Is.Not.Null, "Scene target registry should assign a fallback target prefab.");

#if UNITY_EDITOR
            Assert.That(AssetDatabase.GetAssetPath(registry), Is.EqualTo(TargetRegistryPath), "Scene should use the Phase 1 target registry asset.");
            Assert.That(AssetDatabase.GetAssetPath(registry.PuppyPrefab), Is.EqualTo(DaisyTargetPrefabPath), "Scene target registry puppy slot should point to Daisy.");
            Assert.That(AssetDatabase.GetAssetPath(registry.FallbackTargetPrefab), Is.EqualTo(DaisyTargetPrefabPath), "Scene target registry fallback slot should point to Daisy.");
#endif
        }

        private static void AssertRenderersHaveValidMaterials(GameObject targetObject, string targetId)
        {
            Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0), $"Target '{targetId}' should include visible renderers.");
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                Assert.That(materials.Length, Is.GreaterThan(0), $"Target '{targetId}' renderer '{renderers[rendererIndex].name}' should have materials.");
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material? material = materials[materialIndex];
                    Assert.That(material, Is.Not.Null, $"Target '{targetId}' renderer '{renderers[rendererIndex].name}' has a missing material reference.");
                    Assert.That(material!.shader, Is.Not.Null, $"Target '{targetId}' material '{material.name}' has a missing shader.");
                    Assert.That(
                        material.shader.name,
                        Does.Not.Contain("InternalErrorShader"),
                        $"Target '{targetId}' material '{material.name}' should not render as Unity's missing-material pink.");
                }
            }
        }

    }
}
#endif
