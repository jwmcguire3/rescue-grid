using System;
using System.Collections.Generic;
using Rescue.Unity.Art.Registries;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rescue.Unity.FX
{
    public enum FxDebugCandidateKind
    {
        Hooked,
        Unhooked,
    }

    public readonly record struct FxDebugCandidate(
        FxEventHook? Hook,
        GameObject Prefab,
        bool IsActive,
        bool IsFallback,
        bool IsUnhooked,
        bool HasFramePlayer,
        bool HasInspectableRenderer,
        string Label);

    public static class FxDebugCatalog
    {
        public const string UnhookedGroupLabel = "Unhooked/All Prefabs";

        private static readonly string[] FxPrefabSearchFolders =
        {
            "Assets/Rescue.Unity/Art/Prefabs/Phase1/FX",
            "Assets/Rescue.Unity/Art/Prefabs/FX",
        };

        public static List<FxDebugCandidate> GetCandidates(FxEventRouter? router, FxEventHook? hook)
        {
            List<GameObject> allPrefabs = FindProjectFxPrefabs();
            HashSet<GameObject> assignedPrefabs = BuildAssignedPrefabSet(router);
            List<FxDebugCandidate> candidates = new List<FxDebugCandidate>();

            if (hook.HasValue)
            {
                GameObject? active = router?.GetActivePrefab(hook.Value);
                GameObject? fallback = router?.GetFallbackPrefab(hook.Value);
                AddCandidate(candidates, hook, active, isActive: active is not null, isFallback: IsSamePrefab(active, fallback), isUnhooked: false);
                AddCandidate(candidates, hook, fallback, isActive: IsSamePrefab(fallback, active), isFallback: fallback is not null, isUnhooked: false);

                for (int i = 0; i < allPrefabs.Count; i++)
                {
                    GameObject prefab = allPrefabs[i];
                    if (assignedPrefabs.Contains(prefab))
                    {
                        continue;
                    }

                    AddCandidate(candidates, hook, prefab, isActive: false, isFallback: false, isUnhooked: true);
                }

                return candidates;
            }

            for (int i = 0; i < allPrefabs.Count; i++)
            {
                GameObject prefab = allPrefabs[i];
                bool assigned = assignedPrefabs.Contains(prefab);
                AddCandidate(candidates, null, prefab, isActive: false, isFallback: false, isUnhooked: !assigned);
            }

            return candidates;
        }

        public static List<GameObject> FindProjectFxPrefabs()
        {
            List<GameObject> prefabs = new List<GameObject>();
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:Prefab", FxPrefabSearchFolders);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!seen.Add(path))
                {
                    continue;
                }

                GameObject? prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab is not null)
                {
                    prefabs.Add(prefab);
                }
            }
#endif
            prefabs.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));
            return prefabs;
        }

        public static HashSet<GameObject> BuildAssignedPrefabSet(FxEventRouter? router)
        {
            HashSet<GameObject> assigned = new HashSet<GameObject>();
            if (router is null)
            {
                return assigned;
            }

            Array hooks = Enum.GetValues(typeof(FxEventHook));
            for (int i = 0; i < hooks.Length; i++)
            {
                object? value = hooks.GetValue(i);
                if (value is not FxEventHook hook)
                {
                    continue;
                }

                GameObject? active = router.GetActivePrefab(hook);
                if (active is not null)
                {
                    assigned.Add(active);
                }

                GameObject? fallback = router.GetFallbackPrefab(hook);
                if (fallback is not null)
                {
                    assigned.Add(fallback);
                }
            }

            return assigned;
        }

        public static GameObject? GetRegistryPrefab(FxVisualRegistry? registry, FxEventHook hook)
        {
            return hook switch
            {
                FxEventHook.GroupClear => registry?.GroupClearFx,
                FxEventHook.InvalidTap => registry?.InvalidTapFx,
                FxEventHook.CrateBreak => registry?.CrateBreakFx,
                FxEventHook.IceReveal => registry?.IceRevealFx,
                FxEventHook.VineClear => registry?.VineClearFx,
                FxEventHook.VineGrowthPreview => registry?.VineGrowPreviewFx,
                FxEventHook.DockInsert => registry?.DockInsertFx,
                FxEventHook.DockTripleClear => registry?.DockTripleClearFx,
                FxEventHook.WaterRise => registry?.WaterRiseFx,
                FxEventHook.NearRescueRelief => registry?.NearRescueReliefFx,
                FxEventHook.TargetExtraction => registry?.TargetExtractionFx,
                FxEventHook.Win => registry?.WinFx,
                FxEventHook.LossDockOverflow => registry?.LossFx,
                FxEventHook.LossWaterOnTarget => registry?.LossFx,
                FxEventHook.DockWarning => null,
                _ => null,
            };
        }

        private static void AddCandidate(
            List<FxDebugCandidate> candidates,
            FxEventHook? hook,
            GameObject? prefab,
            bool isActive,
            bool isFallback,
            bool isUnhooked)
        {
            if (prefab is null)
            {
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (!ReferenceEquals(candidates[i].Prefab, prefab))
                {
                    continue;
                }

                FxDebugCandidate existing = candidates[i];
                candidates[i] = existing with
                {
                    IsActive = existing.IsActive || isActive,
                    IsFallback = existing.IsFallback || isFallback,
                    IsUnhooked = existing.IsUnhooked && isUnhooked,
                    Label = BuildLabel(prefab, existing.IsActive || isActive, existing.IsFallback || isFallback, existing.IsUnhooked && isUnhooked),
                };
                return;
            }

            bool hasFramePlayer = FxDebugFramePlayer.HasFramePlayer(prefab);
            bool hasInspectableRenderer = FxDebugFramePlayer.HasInspectableRenderer(prefab);
            candidates.Add(new FxDebugCandidate(
                hook,
                prefab,
                isActive,
                isFallback,
                isUnhooked,
                hasFramePlayer,
                hasInspectableRenderer,
                BuildLabel(prefab, isActive, isFallback, isUnhooked)));
        }

        private static string BuildLabel(GameObject prefab, bool isActive, bool isFallback, bool isUnhooked)
        {
            List<string> tags = new List<string>();
            if (isActive)
            {
                tags.Add("[active]");
            }

            if (isFallback)
            {
                tags.Add("[fallback]");
            }

            if (isUnhooked)
            {
                tags.Add("[unhooked]");
            }

            if (!FxDebugFramePlayer.HasFramePlayer(prefab))
            {
                tags.Add("[no frame player]");
            }

            if (!FxDebugFramePlayer.HasInspectableRenderer(prefab))
            {
                tags.Add("[no sprite renderer]");
            }

            return tags.Count == 0 ? prefab.name : $"{prefab.name} {string.Join(" ", tags)}";
        }

        private static bool IsSamePrefab(GameObject? left, GameObject? right)
        {
            return left is not null && right is not null && ReferenceEquals(left, right);
        }
    }
}
