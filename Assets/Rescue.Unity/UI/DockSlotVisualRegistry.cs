using System;
using System.Collections.Generic;
using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.UI
{
    internal sealed class DockSlotVisualRegistry
    {
        private readonly DebrisType?[] _slotTypes;
        private readonly GameObject?[] _slotObjects;

        public DockSlotVisualRegistry(int capacity)
        {
            _slotTypes = new DebrisType?[Mathf.Max(0, capacity)];
            _slotObjects = new GameObject?[Mathf.Max(0, capacity)];
        }

        public int Capacity => _slotTypes.Length;

        public DebrisType? GetSlotType(int slotIndex)
        {
            return IsInRange(slotIndex) ? _slotTypes[slotIndex] : null;
        }

        public GameObject? GetSlotObject(int slotIndex)
        {
            return IsInRange(slotIndex) ? _slotObjects[slotIndex] : null;
        }

        public void ClearAll(Action<GameObject?> destroyObject)
        {
            for (int slotIndex = 0; slotIndex < _slotTypes.Length; slotIndex++)
            {
                ClearSlot(slotIndex, destroyObject);
            }
        }

        public void ClearSlot(int slotIndex, Action<GameObject?> destroyObject)
        {
            if (!IsInRange(slotIndex))
            {
                return;
            }

            _slotTypes[slotIndex] = null;

            if (_slotObjects[slotIndex] is null)
            {
                return;
            }

            destroyObject(_slotObjects[slotIndex]);
            _slotObjects[slotIndex] = null;
        }

        public void AssignSlot(
            int slotIndex,
            DebrisType debrisType,
            Transform anchor,
            Func<int, DebrisType, GameObject?> createObject,
            Action<int, DebrisType> renameObject,
            Action<int, Transform> updateTransform,
            Action<GameObject?> destroyObject)
        {
            if (!IsInRange(slotIndex))
            {
                return;
            }

            if (_slotTypes[slotIndex] == debrisType && _slotObjects[slotIndex] is not null)
            {
                renameObject(slotIndex, debrisType);
                updateTransform(slotIndex, anchor);
                return;
            }

            if (_slotTypes[slotIndex] != debrisType)
            {
                ClearSlot(slotIndex, destroyObject);
            }

            _slotTypes[slotIndex] = debrisType;

            if (_slotObjects[slotIndex] is null)
            {
                _slotObjects[slotIndex] = createObject(slotIndex, debrisType);
            }

            renameObject(slotIndex, debrisType);
            updateTransform(slotIndex, anchor);
        }

        public List<GameObject> RemoveFirstMatching(DebrisType debrisType, int count)
        {
            int remaining = Mathf.Max(0, count);
            List<GameObject> removedObjects = new List<GameObject>(remaining);

            for (int slotIndex = 0; slotIndex < _slotTypes.Length && remaining > 0; slotIndex++)
            {
                if (_slotTypes[slotIndex] != debrisType)
                {
                    continue;
                }

                GameObject? removedObject = _slotObjects[slotIndex];
                if (removedObject is not null)
                {
                    removedObjects.Add(removedObject);
                }

                _slotTypes[slotIndex] = null;
                _slotObjects[slotIndex] = null;
                remaining--;
            }

            return removedObjects;
        }

        public List<int> FindFirstMatchingSlotIndices(DebrisType debrisType, int count)
        {
            int remaining = Mathf.Max(0, count);
            List<int> matchingSlots = new List<int>(remaining);

            for (int slotIndex = 0; slotIndex < _slotTypes.Length && remaining > 0; slotIndex++)
            {
                if (_slotTypes[slotIndex] != debrisType)
                {
                    continue;
                }

                matchingSlots.Add(slotIndex);
                remaining--;
            }

            return matchingSlots;
        }

        public List<GameObject> DetachSlots(List<int> slotIndices)
        {
            List<GameObject> detachedObjects = new List<GameObject>(slotIndices.Count);

            for (int i = 0; i < slotIndices.Count; i++)
            {
                int slotIndex = slotIndices[i];
                if (!IsInRange(slotIndex))
                {
                    continue;
                }

                GameObject? detachedObject = _slotObjects[slotIndex];
                if (detachedObject is not null)
                {
                    detachedObjects.Add(detachedObject);
                }

                _slotTypes[slotIndex] = null;
                _slotObjects[slotIndex] = null;
            }

            return detachedObjects;
        }

        public void Compact(
            Transform[] anchors,
            Action<int, Transform> updateTransform,
            Action<GameObject?> destroyObject)
        {
            int maxSlotCount = Mathf.Min(_slotTypes.Length, anchors.Length);
            int writeIndex = 0;

            for (int readIndex = 0; readIndex < maxSlotCount; readIndex++)
            {
                DebrisType? trackedType = _slotTypes[readIndex];
                if (!trackedType.HasValue)
                {
                    continue;
                }

                if (writeIndex != readIndex)
                {
                    _slotTypes[writeIndex] = trackedType;
                    _slotObjects[writeIndex] = _slotObjects[readIndex];
                    _slotTypes[readIndex] = null;
                    _slotObjects[readIndex] = null;
                }

                updateTransform(writeIndex, anchors[writeIndex]);
                writeIndex++;
            }

            for (int slotIndex = writeIndex; slotIndex < _slotTypes.Length; slotIndex++)
            {
                _slotTypes[slotIndex] = null;
                if (slotIndex >= maxSlotCount)
                {
                    ClearSlot(slotIndex, destroyObject);
                    continue;
                }

                if (_slotObjects[slotIndex] is not null)
                {
                    destroyObject(_slotObjects[slotIndex]);
                    _slotObjects[slotIndex] = null;
                }
            }
        }

        private bool IsInRange(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < _slotTypes.Length;
        }
    }
}
