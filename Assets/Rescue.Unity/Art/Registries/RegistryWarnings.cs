using System.Collections.Generic;
using UnityEngine;

namespace Rescue.Unity.Art.Registries
{
    internal static class RegistryWarnings
    {
        private static readonly HashSet<string> WarningCache = new HashSet<string>();

        public static void WarnMissing(ScriptableObject owner, string concept, string referenceType)
        {
            string ownerName = owner != null ? owner.name : "VisualRegistry";
            string warningKey = $"{ownerName}|{concept}|{referenceType}";
            if (!WarningCache.Add(warningKey))
            {
                return;
            }

            Debug.LogWarning($"{ownerName}: missing {referenceType} for {concept}. Assign the reference in the inspector or configure a fallback.", owner);
        }

        public static GameObject? ResolvePrefab(
            ScriptableObject owner,
            string concept,
            GameObject? assignedPrefab,
            GameObject? fallbackPrefab = null)
        {
            if (assignedPrefab is not null)
            {
                return assignedPrefab;
            }

            WarnMissing(owner, concept, "prefab");

            if (fallbackPrefab is not null)
            {
                return fallbackPrefab;
            }

            WarnMissing(owner, $"{concept} fallback", "prefab");
            return null;
        }
    }
}
