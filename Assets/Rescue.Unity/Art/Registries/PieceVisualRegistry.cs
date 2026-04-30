using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.Art.Registries
{
    [CreateAssetMenu(fileName = "PieceVisualRegistry", menuName = "Rescue Grid/Art/Registries/Piece Visual Registry")]
    public sealed class PieceVisualRegistry : ScriptableObject
    {
        [Header("Debris Prefabs")]
        [SerializeField] private GameObject? debrisAPrefab;
        [SerializeField] private GameObject? debrisBPrefab;
        [SerializeField] private GameObject? debrisCPrefab;
        [SerializeField] private GameObject? debrisDPrefab;
        [SerializeField] private GameObject? debrisEPrefab;
        [SerializeField] private GameObject? debrisFPrefab;
        [SerializeField] private GameObject? fallbackPrefab;

        [Header("Dock Pose Overrides")]
        [SerializeField] private Vector3 debrisADockEulerOffset;
        [SerializeField] private Vector3 debrisBDockEulerOffset;
        [SerializeField] private Vector3 debrisCDockEulerOffset;
        [SerializeField] private Vector3 debrisDDockEulerOffset;
        [SerializeField] private Vector3 debrisEDockEulerOffset;
        [SerializeField] private Vector3 debrisFDockEulerOffset;
        [SerializeField] private float debrisADockScaleMultiplier = 1f;
        [SerializeField] private float debrisBDockScaleMultiplier = 1f;
        [SerializeField] private float debrisCDockScaleMultiplier = 1f;
        [SerializeField] private float debrisDDockScaleMultiplier = 1f;
        [SerializeField] private float debrisEDockScaleMultiplier = 1f;
        [SerializeField] private float debrisFDockScaleMultiplier = 1f;

        public GameObject? DebrisAPrefab
        {
            get => debrisAPrefab;
            set => debrisAPrefab = value;
        }

        public GameObject? DebrisBPrefab
        {
            get => debrisBPrefab;
            set => debrisBPrefab = value;
        }

        public GameObject? DebrisCPrefab
        {
            get => debrisCPrefab;
            set => debrisCPrefab = value;
        }

        public GameObject? DebrisDPrefab
        {
            get => debrisDPrefab;
            set => debrisDPrefab = value;
        }

        public GameObject? DebrisEPrefab
        {
            get => debrisEPrefab;
            set => debrisEPrefab = value;
        }

        public GameObject? DebrisFPrefab
        {
            get => debrisFPrefab;
            set => debrisFPrefab = value;
        }

        public GameObject? FallbackPrefab
        {
            get => fallbackPrefab;
            set => fallbackPrefab = value;
        }

        public Vector3 DebrisADockEulerOffset
        {
            get => debrisADockEulerOffset;
            set => debrisADockEulerOffset = value;
        }

        public Vector3 DebrisBDockEulerOffset
        {
            get => debrisBDockEulerOffset;
            set => debrisBDockEulerOffset = value;
        }

        public Vector3 DebrisCDockEulerOffset
        {
            get => debrisCDockEulerOffset;
            set => debrisCDockEulerOffset = value;
        }

        public Vector3 DebrisDDockEulerOffset
        {
            get => debrisDDockEulerOffset;
            set => debrisDDockEulerOffset = value;
        }

        public Vector3 DebrisEDockEulerOffset
        {
            get => debrisEDockEulerOffset;
            set => debrisEDockEulerOffset = value;
        }

        public Vector3 DebrisFDockEulerOffset
        {
            get => debrisFDockEulerOffset;
            set => debrisFDockEulerOffset = value;
        }

        public float DebrisADockScaleMultiplier
        {
            get => ResolveDockScaleMultiplier(debrisADockScaleMultiplier);
            set => debrisADockScaleMultiplier = value;
        }

        public float DebrisBDockScaleMultiplier
        {
            get => ResolveDockScaleMultiplier(debrisBDockScaleMultiplier);
            set => debrisBDockScaleMultiplier = value;
        }

        public float DebrisCDockScaleMultiplier
        {
            get => ResolveDockScaleMultiplier(debrisCDockScaleMultiplier);
            set => debrisCDockScaleMultiplier = value;
        }

        public float DebrisDDockScaleMultiplier
        {
            get => ResolveDockScaleMultiplier(debrisDDockScaleMultiplier);
            set => debrisDDockScaleMultiplier = value;
        }

        public float DebrisEDockScaleMultiplier
        {
            get => ResolveDockScaleMultiplier(debrisEDockScaleMultiplier);
            set => debrisEDockScaleMultiplier = value;
        }

        public float DebrisFDockScaleMultiplier
        {
            get => ResolveDockScaleMultiplier(debrisFDockScaleMultiplier);
            set => debrisFDockScaleMultiplier = value;
        }

        public GameObject? GetPrefab(DebrisType type)
        {
            GameObject? assignedPrefab = type switch
            {
                DebrisType.A => debrisAPrefab,
                DebrisType.B => debrisBPrefab,
                DebrisType.C => debrisCPrefab,
                DebrisType.D => debrisDPrefab,
                DebrisType.E => debrisEPrefab,
                DebrisType.F => debrisFPrefab,
                _ => null,
            };

            return RegistryWarnings.ResolvePrefab(this, $"DebrisType.{type}", assignedPrefab, fallbackPrefab);
        }

        public Quaternion GetDockRotationOffset(DebrisType type)
        {
            return Quaternion.Euler(GetDockEulerOffset(type));
        }

        public Vector3 GetDockEulerOffset(DebrisType type)
        {
            return type switch
            {
                DebrisType.A => debrisADockEulerOffset,
                DebrisType.B => debrisBDockEulerOffset,
                DebrisType.C => debrisCDockEulerOffset,
                DebrisType.D => debrisDDockEulerOffset,
                DebrisType.E => debrisEDockEulerOffset,
                DebrisType.F => debrisFDockEulerOffset,
                _ => Vector3.zero,
            };
        }

        public float GetDockScaleMultiplier(DebrisType type)
        {
            float scaleMultiplier = type switch
            {
                DebrisType.A => debrisADockScaleMultiplier,
                DebrisType.B => debrisBDockScaleMultiplier,
                DebrisType.C => debrisCDockScaleMultiplier,
                DebrisType.D => debrisDDockScaleMultiplier,
                DebrisType.E => debrisEDockScaleMultiplier,
                DebrisType.F => debrisFDockScaleMultiplier,
                _ => 1f,
            };

            return ResolveDockScaleMultiplier(scaleMultiplier);
        }

        private static float ResolveDockScaleMultiplier(float scaleMultiplier)
        {
            return scaleMultiplier > 0f ? scaleMultiplier : 1f;
        }
    }
}
