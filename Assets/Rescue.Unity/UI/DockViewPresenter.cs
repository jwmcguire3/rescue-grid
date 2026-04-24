using System.Collections.Generic;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using UnityEngine;

namespace Rescue.Unity.UI
{
    public static class DockVisualStateResolver
    {
        public static DockVisualState FromOccupancy(int occupiedSlots, int dockSize)
        {
            if (dockSize <= 0)
            {
                return DockVisualState.Failed;
            }

            int clampedOccupancy = Mathf.Max(0, occupiedSlots);

            if (clampedOccupancy >= dockSize)
            {
                return DockVisualState.Failed;
            }

            if (clampedOccupancy == dockSize - 1)
            {
                return DockVisualState.Acute;
            }

            if (clampedOccupancy == dockSize - 2)
            {
                return DockVisualState.Caution;
            }

            return DockVisualState.Safe;
        }
    }

    public sealed class DockViewPresenter : MonoBehaviour
    {
        private const int Phase1DockSize = 7;
        private const string DefaultPieceContainerName = "DockPieces";
        private const string SharedDockInstanceName = "SharedDockVisualInstance";

        [Header("Shared Dock")]
        [SerializeField] private DockVisualConfig? dockVisualConfig;
        [SerializeField] private MeshRenderer? sharedDockRenderer;
        [SerializeField] private Material? safeMaterial;
        [SerializeField] private Material? cautionMaterial;
        [SerializeField] private Material? acuteMaterial;
        [SerializeField] private Material? failedMaterial;

        [Header("Slot Layout")]
        [SerializeField] private Transform[]? slotAnchors;
        [SerializeField] private Transform? pieceContainer;

        [Header("Piece Visuals")]
        [SerializeField] private PieceVisualRegistry? pieceRegistry;
        [SerializeField] private GameObject? fallbackPiecePrefab;

        private readonly List<GameObject> _spawnedPieces = new List<GameObject>();
        private GameObject? _sharedDockInstance;

        public void Rebuild(GameState state)
        {
            if (state is null)
            {
                Debug.LogWarning($"{nameof(DockViewPresenter)} requires a valid GameState to rebuild.", this);
                return;
            }

            EnsureSharedDockVisual();

            Transform[] anchors = ResolveSlotAnchors();
            if (anchors.Length != Phase1DockSize)
            {
                Debug.LogWarning(
                    $"{nameof(DockViewPresenter)} expected exactly {Phase1DockSize} slot anchors but found {anchors.Length}. Assign the existing Dock_Shared_7Slot anchors in the inspector.",
                    this);
            }

            SetDockVisualState(DockVisualStateResolver.FromOccupancy(CountOccupiedSlots(state.Dock), state.Dock.Size));

            ClearSlots();

            if (anchors.Length == 0)
            {
                return;
            }

            Transform container = ResolvePieceContainer();

            int maxSlots = Mathf.Min(state.Dock.Slots.Length, anchors.Length);
            for (int slotIndex = 0; slotIndex < maxSlots; slotIndex++)
            {
                DebrisType? debrisType = state.Dock.Slots[slotIndex];
                if (!debrisType.HasValue)
                {
                    continue;
                }

                GameObject? piecePrefab = ResolvePiecePrefab(debrisType.Value);
                if (piecePrefab is null)
                {
                    Debug.LogWarning(
                        $"{nameof(DockViewPresenter)} could not resolve a prefab for dock slot {slotIndex} ({debrisType.Value}).",
                        this);
                    continue;
                }

                GameObject pieceObject = Instantiate(piecePrefab, container);
                pieceObject.name = $"DockPiece_{slotIndex:00}_{debrisType.Value}";

                Transform pieceTransform = pieceObject.transform;
                pieceTransform.SetPositionAndRotation(anchors[slotIndex].position, anchors[slotIndex].rotation);
                pieceTransform.localScale = piecePrefab.transform.localScale;

                _spawnedPieces.Add(pieceObject);
            }
        }

        public void SetDockVisualState(DockVisualState state)
        {
            MeshRenderer? renderer = ResolveSharedDockRenderer();
            if (renderer is null)
            {
                Debug.LogWarning($"{nameof(DockViewPresenter)} is missing {nameof(sharedDockRenderer)}.", this);
                return;
            }

            Material? material = dockVisualConfig?.GetMaterial(state) ?? ResolveLegacyMaterial(state);

            if (material is null)
            {
                Debug.LogWarning($"{nameof(DockViewPresenter)} is missing a material for dock state {state}.", this);
                return;
            }

            renderer.sharedMaterial = material;
        }

        public void ClearSlots()
        {
            for (int i = _spawnedPieces.Count - 1; i >= 0; i--)
            {
                GameObject? spawnedPiece = _spawnedPieces[i];
                if (spawnedPiece is null)
                {
                    _spawnedPieces.RemoveAt(i);
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(spawnedPiece);
                }
                else
                {
                    DestroyImmediate(spawnedPiece);
                }

                _spawnedPieces.RemoveAt(i);
            }
        }

        private static int CountOccupiedSlots(Dock dock)
        {
            int occupiedSlots = 0;
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                if (dock.Slots[i].HasValue)
                {
                    occupiedSlots++;
                }
            }

            return occupiedSlots;
        }

        private Transform ResolvePieceContainer()
        {
            if (pieceContainer is not null)
            {
                return pieceContainer;
            }

            Transform existingContainer = transform.Find(DefaultPieceContainerName);
            if (existingContainer is not null)
            {
                pieceContainer = existingContainer;
                return existingContainer;
            }

            GameObject containerObject = new GameObject(DefaultPieceContainerName);
            Transform containerTransform = containerObject.transform;
            containerTransform.SetParent(transform, false);
            pieceContainer = containerTransform;

            Debug.LogWarning(
                $"{nameof(DockViewPresenter)} is missing {nameof(pieceContainer)}. Created a fallback '{DefaultPieceContainerName}' container.",
                this);

            return containerTransform;
        }

        private Transform[] ResolveSlotAnchors()
        {
            Transform[] configAnchors = FindSharedDockAnchors();
            if (IsValidAnchorArray(configAnchors))
            {
                slotAnchors = configAnchors;
                return configAnchors;
            }

            if (IsValidAnchorArray(slotAnchors))
            {
                Transform[] assignedAnchors = slotAnchors ?? System.Array.Empty<Transform>();
                return assignedAnchors;
            }

            List<Transform> anchors = new List<Transform>(Phase1DockSize);
            for (int slotIndex = 0; slotIndex < Phase1DockSize; slotIndex++)
            {
                string anchorName = $"Slot_{slotIndex:00}";
                Transform? anchor = transform.Find(anchorName);
                if (anchor is null)
                {
                    GameObject anchorObject = new GameObject(anchorName);
                    anchor = anchorObject.transform;
                    anchor.SetParent(transform, false);
                    Debug.LogWarning(
                        $"{nameof(DockViewPresenter)} could not find anchor '{anchorName}'. Created a fallback anchor; prefer the existing Dock_Shared_7Slot prefab anchors.",
                        this);
                }

                anchors.Add(anchor);
            }

            slotAnchors = anchors.ToArray();
            return slotAnchors;
        }

        private void EnsureSharedDockVisual()
        {
            GameObject? sharedDockPrefab = dockVisualConfig?.GetSharedDockPrefab();
            if (sharedDockPrefab is null)
            {
                return;
            }

            if (_sharedDockInstance is null)
            {
                Transform? existingInstance = transform.Find(SharedDockInstanceName);
                if (existingInstance is not null)
                {
                    _sharedDockInstance = existingInstance.gameObject;
                }
            }

            if (_sharedDockInstance is null)
            {
                _sharedDockInstance = Instantiate(sharedDockPrefab, transform);
                _sharedDockInstance.name = SharedDockInstanceName;
            }

            if (sharedDockRenderer is not null && _sharedDockInstance is not null && !IsChildOf(sharedDockRenderer.transform, _sharedDockInstance.transform))
            {
                sharedDockRenderer.enabled = false;
            }
        }

        private MeshRenderer? ResolveSharedDockRenderer()
        {
            if (_sharedDockInstance is not null)
            {
                MeshRenderer? instanceRenderer = _sharedDockInstance.GetComponentInChildren<MeshRenderer>(true);
                if (instanceRenderer is not null)
                {
                    return instanceRenderer;
                }
            }

            return sharedDockRenderer;
        }

        private Material? ResolveLegacyMaterial(DockVisualState state)
        {
            return state switch
            {
                DockVisualState.Safe => safeMaterial,
                DockVisualState.Caution => cautionMaterial,
                DockVisualState.Acute => acuteMaterial,
                DockVisualState.Failed => failedMaterial,
                _ => null,
            };
        }

        private Transform[] FindSharedDockAnchors()
        {
            if (_sharedDockInstance is null)
            {
                return System.Array.Empty<Transform>();
            }

            List<Transform> anchors = new List<Transform>(Phase1DockSize);
            for (int slotIndex = 0; slotIndex < Phase1DockSize; slotIndex++)
            {
                Transform? anchor = FindChildRecursive(_sharedDockInstance.transform, $"Slot_{slotIndex:00}");
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

        private static bool IsChildOf(Transform child, Transform parent)
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

        private GameObject? ResolvePiecePrefab(DebrisType debrisType)
        {
            GameObject? registryPrefab = pieceRegistry?.GetPrefab(debrisType);
            if (registryPrefab is not null)
            {
                return registryPrefab;
            }

            if (fallbackPiecePrefab is not null)
            {
                return fallbackPiecePrefab;
            }

            Debug.LogWarning(
                $"{nameof(DockViewPresenter)} is missing both a registry entry and {nameof(fallbackPiecePrefab)} for debris type {debrisType}.",
                this);
            return null;
        }

        private static bool IsValidAnchorArray(Transform[]? anchors)
        {
            if (anchors is null || anchors.Length != Phase1DockSize)
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
    }
}
