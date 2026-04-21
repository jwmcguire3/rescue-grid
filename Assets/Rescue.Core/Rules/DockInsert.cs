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
            if (dock.Slots.Length != dock.Size)
            {
                throw new InvalidOperationException("Dock slots must match the fixed dock size.");
            }

            if (pieces.IsDefaultOrEmpty)
            {
                return new DockInsertResult(dock, ImmutableArray<DebrisType>.Empty, OverflowCount: 0);
            }

            ImmutableArray<DebrisType?>.Builder slots = ImmutableArray.CreateBuilder<DebrisType?>(dock.Size);
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                slots.Add(dock.Slots[i]);
            }

            ImmutableArray<DebrisType>.Builder inserted = ImmutableArray.CreateBuilder<DebrisType>(pieces.Length);
            int searchStart = 0;
            int overflowCount = 0;

            for (int i = 0; i < pieces.Length; i++)
            {
                int slotIndex = FindFirstAvailable(slots, searchStart);
                if (slotIndex < 0)
                {
                    overflowCount++;
                    continue;
                }

                slots[slotIndex] = pieces[i];
                inserted.Add(pieces[i]);
                searchStart = slotIndex + 1;
            }

            Dock updatedDock = dock with { Slots = slots.ToImmutable() };
            return new DockInsertResult(updatedDock, inserted.ToImmutable(), overflowCount);
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
    }
}
