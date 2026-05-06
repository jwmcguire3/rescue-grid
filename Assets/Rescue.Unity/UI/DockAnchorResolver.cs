using System.Collections.Generic;
using UnityEngine;

namespace Rescue.Unity.UI
{
    internal static class DockAnchorResolver
    {
        public static string FormatSlotAnchorName(int slotIndex)
        {
            return $"Slot_{slotIndex:00}";
        }

        public static bool IsValidAnchorArray(Transform[]? anchors, int expectedSlotCount)
        {
            if (anchors is null || anchors.Length != expectedSlotCount)
            {
                return false;
            }

            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] is null)
                {
                    return false;
                }
            }

            return true;
        }

        public static Transform[] ResolveSlotAnchors(
            Transform owner,
            Transform? sharedDockRoot,
            Transform[]? assignedAnchors,
            int expectedSlotCount,
            Object warningContext,
            string presenterName)
        {
            Transform[] configAnchors = FindSharedDockAnchors(sharedDockRoot, expectedSlotCount);
            if (IsValidAnchorArray(configAnchors, expectedSlotCount))
            {
                return configAnchors;
            }

            if (IsValidAnchorArray(assignedAnchors, expectedSlotCount))
            {
                return assignedAnchors ?? System.Array.Empty<Transform>();
            }

            List<Transform> anchors = new List<Transform>(expectedSlotCount);
            for (int slotIndex = 0; slotIndex < expectedSlotCount; slotIndex++)
            {
                string anchorName = FormatSlotAnchorName(slotIndex);
                Transform? anchor = owner.Find(anchorName);
                if (anchor is null)
                {
                    GameObject anchorObject = new GameObject(anchorName);
                    anchor = anchorObject.transform;
                    anchor.SetParent(owner, false);
                    Debug.LogWarning(
                        $"{presenterName} could not find anchor '{anchorName}'. Created a fallback anchor; prefer anchors provided by the shared dock prefab.",
                        warningContext);
                }

                anchors.Add(anchor);
            }

            return anchors.ToArray();
        }

        public static Transform ResolveAnchorForSlot(
            int slotIndex,
            Transform[] anchors,
            Transform fallbackParent,
            string overflowAnchorPrefix)
        {
            if (slotIndex >= 0 && slotIndex < anchors.Length)
            {
                return anchors[slotIndex];
            }

            string anchorName = overflowAnchorPrefix + slotIndex.ToString("00");
            Transform? existingAnchor = fallbackParent.Find(anchorName);
            if (existingAnchor is not null)
            {
                return existingAnchor;
            }

            GameObject anchorObject = new GameObject(anchorName);
            Transform anchor = anchorObject.transform;
            anchor.SetParent(fallbackParent, false);

            if (anchors.Length > 0)
            {
                Transform lastAnchor = anchors[anchors.Length - 1];
                anchor.position = lastAnchor.position + new Vector3(slotIndex - anchors.Length + 1, 0f, 0f);
                anchor.rotation = lastAnchor.rotation;
                return anchor;
            }

            anchor.localPosition = new Vector3(slotIndex, 0f, 0f);
            return anchor;
        }

        public static bool TryGetSlotWorldPosition(Transform[] anchors, int slotIndex, out Vector3 position)
        {
            position = Vector3.zero;
            if (slotIndex < 0 || slotIndex >= anchors.Length || anchors[slotIndex] is null)
            {
                return false;
            }

            position = anchors[slotIndex].position;
            return true;
        }

        public static bool TryGetCenterWorldPosition(Transform[] anchors, out Vector3 position)
        {
            position = Vector3.zero;
            if (anchors.Length == 0)
            {
                return false;
            }

            Vector3 accumulated = Vector3.zero;
            int resolvedCount = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] is null)
                {
                    continue;
                }

                accumulated += anchors[i].position;
                resolvedCount++;
            }

            if (resolvedCount == 0)
            {
                return false;
            }

            position = accumulated / resolvedCount;
            return true;
        }

        public static bool IsChildOf(Transform child, Transform parent)
        {
            Transform? current = child;
            while (current is not null)
            {
                if (current == parent)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static Transform[] FindSharedDockAnchors(Transform? sharedDockRoot, int expectedSlotCount)
        {
            if (sharedDockRoot is null)
            {
                return System.Array.Empty<Transform>();
            }

            List<Transform> anchors = new List<Transform>(expectedSlotCount);
            for (int slotIndex = 0; slotIndex < expectedSlotCount; slotIndex++)
            {
                Transform? anchor = FindChildRecursive(sharedDockRoot, FormatSlotAnchorName(slotIndex));
                if (anchor is null)
                {
                    return System.Array.Empty<Transform>();
                }

                anchors.Add(anchor);
            }

            return anchors.ToArray();
        }

        private static Transform? FindChildRecursive(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                Transform child = root.GetChild(childIndex);
                Transform? match = FindChildRecursive(child, name);
                if (match is not null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
