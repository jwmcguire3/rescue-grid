using UnityEngine;

namespace Rescue.Unity.Art.Registries
{
    public enum DockVisualState
    {
        Safe,
        Caution,
        Acute,
        Jammed,
        Failed,
    }

    [CreateAssetMenu(fileName = "DockVisualConfig", menuName = "Rescue Grid/Art/Registries/Dock Visual Config")]
    public sealed class DockVisualConfig : ScriptableObject
    {
        [Header("Shared Dock References")]
        [SerializeField] private GameObject? sharedDockPrefab;
        [SerializeField] private Mesh? sharedDockMesh;

        [Header("Optional State Prefabs")]
        [SerializeField] private GameObject? safePrefab;
        [SerializeField] private GameObject? cautionPrefab;
        [SerializeField] private GameObject? acutePrefab;
        [SerializeField] private GameObject? jammedPrefab;
        [SerializeField] private GameObject? failedPrefab;

        [Header("State Materials")]
        [SerializeField] private Material? safeMaterial;
        [SerializeField] private Material? cautionMaterial;
        [SerializeField] private Material? acuteMaterial;
        [SerializeField] private Material? jammedMaterial;
        [SerializeField] private Material? failedMaterial;

        public GameObject? SharedDockPrefab
        {
            get => sharedDockPrefab;
            set => sharedDockPrefab = value;
        }

        public Mesh? SharedDockMesh
        {
            get => sharedDockMesh;
            set => sharedDockMesh = value;
        }

        public GameObject? SafePrefab
        {
            get => safePrefab;
            set => safePrefab = value;
        }

        public GameObject? CautionPrefab
        {
            get => cautionPrefab;
            set => cautionPrefab = value;
        }

        public GameObject? AcutePrefab
        {
            get => acutePrefab;
            set => acutePrefab = value;
        }

        public GameObject? JammedPrefab
        {
            get => jammedPrefab;
            set => jammedPrefab = value;
        }

        public GameObject? FailedPrefab
        {
            get => failedPrefab;
            set => failedPrefab = value;
        }

        public Material? SafeMaterial
        {
            get => safeMaterial;
            set => safeMaterial = value;
        }

        public Material? CautionMaterial
        {
            get => cautionMaterial;
            set => cautionMaterial = value;
        }

        public Material? AcuteMaterial
        {
            get => acuteMaterial;
            set => acuteMaterial = value;
        }

        public Material? JammedMaterial
        {
            get => jammedMaterial;
            set => jammedMaterial = value;
        }

        public Material? FailedMaterial
        {
            get => failedMaterial;
            set => failedMaterial = value;
        }

        public GameObject? GetSharedDockPrefab()
        {
            return RegistryWarnings.ResolvePrefab(this, "shared dock", sharedDockPrefab);
        }

        public Mesh? GetSharedDockMesh()
        {
            if (sharedDockMesh is not null)
            {
                return sharedDockMesh;
            }

            RegistryWarnings.WarnMissing(this, "shared dock", "mesh");
            return null;
        }

        public GameObject? GetPrefab(DockVisualState state)
        {
            GameObject? statePrefab = state switch
            {
                DockVisualState.Safe => safePrefab,
                DockVisualState.Caution => cautionPrefab,
                DockVisualState.Acute => acutePrefab,
                DockVisualState.Jammed => jammedPrefab,
                DockVisualState.Failed => failedPrefab,
                _ => null,
            };

            return RegistryWarnings.ResolvePrefab(this, $"dock {state}", statePrefab, sharedDockPrefab);
        }

        public Material? GetMaterial(DockVisualState state)
        {
            Material? material = state switch
            {
                DockVisualState.Safe => safeMaterial,
                DockVisualState.Caution => cautionMaterial,
                DockVisualState.Acute => acuteMaterial,
                DockVisualState.Jammed => jammedMaterial ?? acuteMaterial ?? failedMaterial,
                DockVisualState.Failed => failedMaterial,
                _ => null,
            };

            if (material is not null)
            {
                return material;
            }

            RegistryWarnings.WarnMissing(this, $"dock {state}", "material");
            return null;
        }
    }
}
