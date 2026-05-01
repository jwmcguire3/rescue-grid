using System;
using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Rules
{
    public readonly record struct DockInsertResult(
        Dock Dock,
        ImmutableArray<DebrisType> InsertedPieces,
        int OverflowCount);

    public static class DockInsertOps
    {
        public static DockInsertResult Insert(Dock dock, ImmutableArray<DebrisType> pieces)
        {
            if (dock.Slots.Length < dock.Size)
            {
                throw new InvalidOperationException("Dock slots cannot be smaller than the fixed dock size.");
            }

            ImmutableArray<DebrisType> insertablePieces = CalculateInsertablePieces(pieces);
            if (insertablePieces.IsDefaultOrEmpty)
            {
                return new DockInsertResult(dock, ImmutableArray<DebrisType>.Empty, OverflowCount: 0);
            }

            ImmutableArray<DebrisType?>.Builder slots = ImmutableArray.CreateBuilder<DebrisType?>(dock.Slots.Length + insertablePieces.Length);
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                slots.Add(dock.Slots[i]);
            }

            ImmutableArray<DebrisType>.Builder inserted = ImmutableArray.CreateBuilder<DebrisType>(insertablePieces.Length);
            int searchStart = 0;
            int overflowCount = 0;

            for (int i = 0; i < insertablePieces.Length; i++)
            {
                int slotIndex = FindFirstAvailable(slots, searchStart);
                if (slotIndex < 0)
                {
                    slots.Add(insertablePieces[i]);
                    inserted.Add(insertablePieces[i]);
                    overflowCount = System.Math.Max(overflowCount, slots.Count - dock.Size);
                    continue;
                }

                slots[slotIndex] = insertablePieces[i];
                inserted.Add(insertablePieces[i]);
                searchStart = slotIndex + 1;
                overflowCount = System.Math.Max(overflowCount, DockOccupancy(slots) - dock.Size);
            }

            Dock updatedDock = dock with { Slots = slots.ToImmutable() };
            return new DockInsertResult(updatedDock, inserted.ToImmutable(), overflowCount);
        }

        public static ImmutableArray<DebrisType> CalculateInsertablePieces(ImmutableArray<DebrisType> removedPieces)
        {
            if (removedPieces.IsDefaultOrEmpty)
            {
                return ImmutableArray<DebrisType>.Empty;
            }

            int remainder = removedPieces.Length % 3;
            if (remainder == 0)
            {
                return ImmutableArray<DebrisType>.Empty;
            }

            ImmutableArray<DebrisType>.Builder insertable = ImmutableArray.CreateBuilder<DebrisType>(remainder);
            for (int i = 0; i < remainder; i++)
            {
                insertable.Add(removedPieces[i]);
            }

            return insertable.ToImmutable();
        }

        private static int FindFirstAvailable(ImmutableArray<DebrisType?>.Builder slots, int searchStart)
        {
            for (int i = searchStart; i < slots.Count; i++)
            {
                if (!slots[i].HasValue)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int DockOccupancy(ImmutableArray<DebrisType?>.Builder slots)
        {
            int occupied = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].HasValue)
                {
                    occupied++;
                }
            }

            return occupied;
        }
    }
}
