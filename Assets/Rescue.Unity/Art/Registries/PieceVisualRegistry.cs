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
        [SerializeField] private GameObject? fallbackPrefab;

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

        public GameObject? FallbackPrefab
        {
            get => fallbackPrefab;
            set => fallbackPrefab = value;
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
                _ => null,
            };

            return RegistryWarnings.ResolvePrefab(this, $"DebrisType.{type}", assignedPrefab, fallbackPrefab);
        }
    }
}
