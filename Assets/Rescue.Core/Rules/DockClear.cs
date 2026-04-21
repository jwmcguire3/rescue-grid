using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Rules
{
    public readonly record struct DockClearResult(
        Dock Dock,
        ImmutableArray<DebrisType> ClearedTriples);

    public static class DockClearOps
    {
        public static DockClearResult ClearTriples(Dock dock)
        {
            int debrisTypeCount = System.Enum.GetValues(typeof(DebrisType)).Length;
            int[] counts = new int[debrisTypeCount];
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                if (dock.Slots[i].HasValue)
                {
                    counts[(int)dock.Slots[i]!.Value]++;
                }
            }

            int[] piecesToClear = new int[debrisTypeCount];
            ImmutableArray<DebrisType>.Builder clearedTriples = ImmutableArray.CreateBuilder<DebrisType>();
            for (int i = 0; i < counts.Length; i++)
            {
                int triples = counts[i] / 3;
                if (triples <= 0)
                {
                    continue;
                }

                piecesToClear[i] = triples * 3;
                for (int triple = 0; triple < triples; triple++)
                {
                    clearedTriples.Add((DebrisType)i);
                }
            }

            if (clearedTriples.Count == 0)
            {
                return new DockClearResult(dock, ImmutableArray<DebrisType>.Empty);
            }

            ImmutableArray<DebrisType?>.Builder clearedSlots = ImmutableArray.CreateBuilder<DebrisType?>(dock.Size);
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                DebrisType? slot = dock.Slots[i];
                if (slot.HasValue && piecesToClear[(int)slot.Value] > 0)
                {
                    piecesToClear[(int)slot.Value]--;
                    clearedSlots.Add(null);
                    continue;
                }

                clearedSlots.Add(slot);
            }

            ImmutableArray<DebrisType?>.Builder compactedSlots = ImmutableArray.CreateBuilder<DebrisType?>(dock.Size);
            for (int i = 0; i < clearedSlots.Count; i++)
            {
                if (clearedSlots[i].HasValue)
                {
                    compactedSlots.Add(clearedSlots[i]);
                }
            }

            while (compactedSlots.Count < dock.Size)
            {
                compactedSlots.Add(null);
            }

            Dock updatedDock = dock with { Slots = compactedSlots.ToImmutable() };
            return new DockClearResult(updatedDock, clearedTriples.ToImmutable());
        }
    }
}
