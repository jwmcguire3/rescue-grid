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

        [Header("Board Pose Overrides")]
        [SerializeField] private Vector3 debrisABoardEulerOffset;
        [SerializeField] private Vector3 debrisBBoardEulerOffset;
        [SerializeField] private Vector3 debrisCBoardEulerOffset;
        [SerializeField] private Vector3 debrisDBoardEulerOffset;
        [SerializeField] private Vector3 debrisEBoardEulerOffset;
        [SerializeField] private Vector3 debrisFBoardEulerOffset;
        [SerializeField] private float debrisABoardScaleMultiplier = 1f;
        [SerializeField] private float debrisBBoardScaleMultiplier = 1f;
        [SerializeField] private float debrisCBoardScaleMultiplier = 1f;
        [SerializeField] private float debrisDBoardScaleMultiplier = 1f;
        [SerializeField] private float debrisEBoardScaleMultiplier = 1f;
        [SerializeField] private float debrisFBoardScaleMultiplier = 1f;

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

        public Vector3 DebrisABoardEulerOffset
        {
            get => debrisABoardEulerOffset;
            set => debrisABoardEulerOffset = value;
        }

        public Vector3 DebrisBBoardEulerOffset
        {
            get => debrisBBoardEulerOffset;
            set => debrisBBoardEulerOffset = value;
        }

        public Vector3 DebrisCBoardEulerOffset
        {
            get => debrisCBoardEulerOffset;
            set => debrisCBoardEulerOffset = value;
        }

        public Vector3 DebrisDBoardEulerOffset
        {
            get => debrisDBoardEulerOffset;
            set => debrisDBoardEulerOffset = value;
        }

        public Vector3 DebrisEBoardEulerOffset
        {
            get => debrisEBoardEulerOffset;
            set => debrisEBoardEulerOffset = value;
        }

        public Vector3 DebrisFBoardEulerOffset
        {
            get => debrisFBoardEulerOffset;
            set => debrisFBoardEulerOffset = value;
        }

        public float DebrisABoardScaleMultiplier
        {
            get => ResolveScaleMultiplier(debrisABoardScaleMultiplier);
            set => debrisABoardScaleMultiplier = value;
        }

        public float DebrisBBoardScaleMultiplier
        {
            get => ResolveScaleMultiplier(debrisBBoardScaleMultiplier);
            set => debrisBBoardScaleMultiplier = value;
        }

        public float DebrisCBoardScaleMultiplier
        {
            get => ResolveScaleMultiplier(debrisCBoardScaleMultiplier);
            set => debrisCBoardScaleMultiplier = value;
        }

        public float DebrisDBoardScaleMultiplier
        {
            get => ResolveScaleMultiplier(debrisDBoardScaleMultiplier);
            set => debrisDBoardScaleMultiplier = value;
        }

        public float DebrisEBoardScaleMultiplier
        {
            get => ResolveScaleMultiplier(debrisEBoardScaleMultiplier);
            set => debrisEBoardScaleMultiplier = value;
        }

        public float DebrisFBoardScaleMultiplier
        {
            get => ResolveScaleMultiplier(debrisFBoardScaleMultiplier);
            set => debrisFBoardScaleMultiplier = value;
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
            get => ResolveScaleMultiplier(debrisADockScaleMultiplier);
            set => debrisADockScaleMultiplier = value;
        }

        public float DebrisBDockScaleMultiplier
        {
            get => ResolveScaleMultiplier(debrisBDockScaleMultiplier);
            set => debrisBDockScaleMultiplier = value;
        }

        public float DebrisCDockScaleMultiplier
        {
            get => ResolveScaleMultiplier(debrisCDockScaleMultiplier);
            set => debrisCDockScaleMultiplier = value;
        }

        public float DebrisDDockScaleMultiplier
        {
            get => ResolveScaleMultiplier(debrisDDockScaleMultiplier);
            set => debrisDDockScaleMultiplier = value;
        }

        public float DebrisEDockScaleMultiplier
        {
            get => ResolveScaleMultiplier(debrisEDockScaleMultiplier);
            set => debrisEDockScaleMultiplier = value;
        }

        public float DebrisFDockScaleMultiplier
        {
            get => ResolveScaleMultiplier(debrisFDockScaleMultiplier);
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

        public float GetBoardScaleMultiplier(DebrisType type)
        {
            float scaleMultiplier = type switch
            {
                DebrisType.A => debrisABoardScaleMultiplier,
                DebrisType.B => debrisBBoardScaleMultiplier,
                DebrisType.C => debrisCBoardScaleMultiplier,
                DebrisType.D => debrisDBoardScaleMultiplier,
                DebrisType.E => debrisEBoardScaleMultiplier,
                DebrisType.F => debrisFBoardScaleMultiplier,
                _ => 1f,
            };

            return ResolveScaleMultiplier(scaleMultiplier);
        }

        public Quaternion GetBoardRotationOffset(DebrisType type)
        {
            return Quaternion.Euler(GetBoardEulerOffset(type));
        }

        public Vector3 GetBoardEulerOffset(DebrisType type)
        {
            return type switch
            {
                DebrisType.A => debrisABoardEulerOffset,
                DebrisType.B => debrisBBoardEulerOffset,
                DebrisType.C => debrisCBoardEulerOffset,
                DebrisType.D => debrisDBoardEulerOffset,
                DebrisType.E => debrisEBoardEulerOffset,
                DebrisType.F => debrisFBoardEulerOffset,
                _ => Vector3.zero,
            };
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

            return ResolveScaleMultiplier(scaleMultiplier);
        }

        private static float ResolveScaleMultiplier(float scaleMultiplier)
        {
            return scaleMultiplier > 0f ? scaleMultiplier : 1f;
        }
    }
}
