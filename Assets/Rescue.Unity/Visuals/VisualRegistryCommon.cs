using System.Collections.Generic;
using UnityEngine;

namespace Rescue.Unity.Visuals
{
    public enum VisualPrimitiveFallback
    {
        None,
        Cube,
        Sphere,
        Capsule,
        Cylinder,
        Quad,
        Plane,
    }

    [System.Serializable]
    public sealed class VisualPrefabConfig
    {
        [SerializeField] private GameObject? prefab;
        [SerializeField] private Material? materialOverride;
        [SerializeField] private VisualPrimitiveFallback placeholderPrimitive = VisualPrimitiveFallback.None;

        public GameObject? Prefab
        {
            get => prefab;
            set => prefab = value;
        }

        public Material? MaterialOverride
        {
            get => materialOverride;
            set => materialOverride = value;
        }

        public VisualPrimitiveFallback PlaceholderPrimitive
        {
            get => placeholderPrimitive;
            set => placeholderPrimitive = value;
        }
    }

    internal static class VisualRegistryUtility
    {
        private static readonly HashSet<string> WarningCache = new HashSet<string>();

        public static GameObject? ResolvePrefab(VisualPrefabConfig config, ScriptableObject owner, string label)
        {
            if (config.Prefab is not null)
            {
                return config.Prefab;
            }

            WarnMissing(owner, label, "prefab");

            return config.PlaceholderPrimitive switch
            {
                VisualPrimitiveFallback.None => null,
                _ => CreatePlaceholder(owner.name, label, config.PlaceholderPrimitive, config.MaterialOverride),
            };
        }

        public static Material? ResolveMaterial(VisualPrefabConfig config, ScriptableObject owner, string label)
        {
            if (config.MaterialOverride is not null)
            {
                return config.MaterialOverride;
            }

            WarnMissing(owner, label, "material");
            return null;
        }

        public static void WarnMissing(ScriptableObject owner, string label, string referenceType)
        {
            string ownerName = owner != null ? owner.name : "VisualRegistry";
            string warningKey = $"{ownerName}|{label}|{referenceType}";
            if (!WarningCache.Add(warningKey))
            {
                return;
            }

            Debug.LogWarning($"{ownerName}: missing {referenceType} reference for {label}. Assign one in the inspector or use a placeholder primitive.", owner);
        }

        public static PrimitiveType ToPrimitiveType(VisualPrimitiveFallback fallback)
        {
            return fallback switch
            {
                VisualPrimitiveFallback.Cube => PrimitiveType.Cube,
                VisualPrimitiveFallback.Sphere => PrimitiveType.Sphere,
                VisualPrimitiveFallback.Capsule => PrimitiveType.Capsule,
                VisualPrimitiveFallback.Cylinder => PrimitiveType.Cylinder,
                VisualPrimitiveFallback.Quad => PrimitiveType.Quad,
                VisualPrimitiveFallback.Plane => PrimitiveType.Plane,
                _ => PrimitiveType.Cube,
            };
        }

        private static GameObject CreatePlaceholder(string ownerName, string label, VisualPrimitiveFallback fallback, Material? materialOverride)
        {
            GameObject placeholder = GameObject.CreatePrimitive(ToPrimitiveType(fallback));
            placeholder.name = $"{ownerName}_{label}_Placeholder";
            placeholder.hideFlags = HideFlags.HideAndDontSave;

            if (materialOverride is not null
                && placeholder.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = materialOverride;
            }

            return placeholder;
        }
    }
}
