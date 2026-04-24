using UnityEngine;

namespace Rescue.Unity.Art.Registries
{
    public enum DockVisualState
    {
        Safe,
        Caution,
        Acute,
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
        [SerializeField] private GameObject? failedPrefab;

        [Header("State Materials")]
        [SerializeField] private Material? safeMaterial;
        [SerializeField] private Material? cautionMaterial;
        [SerializeField] private Material? acuteMaterial;
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

        public Material? FailedMaterial
        {
            get => failedMaterial;
            set => failedMaterial = value;
        }

        public GameObject? GetPrefab(DockVisualState state)
        {
            GameObject? statePrefab = state switch
            {
                DockVisualState.Safe => safePrefab,
                DockVisualState.Caution => cautionPrefab,
                DockVisualState.Acute => acutePrefab,
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
