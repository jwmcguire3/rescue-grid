using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.State;

namespace Rescue.Core.Tests.State
{
    public sealed class DockHelpersTests
    {
        [Test]
        public void IsEmptyReturnsTrueOnlyWhenNoSlotsAreOccupied()
        {
            Dock emptyDock = new Dock(
                ImmutableArray.Create<DebrisType?>(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                Size: 7);

            Dock occupiedDock = emptyDock with
            {
                Slots = emptyDock.Slots.SetItem(3, DebrisType.C),
            };

            Assert.That(DockHelpers.IsEmpty(emptyDock), Is.True);
            Assert.That(DockHelpers.IsEmpty(occupiedDock), Is.False);
        }

        [Test]
        public void OccupancyCountsOnlyNonNullSlots()
        {
            Dock dock = new Dock(
                ImmutableArray.Create<DebrisType?>(
                    DebrisType.A,
                    null,
                    DebrisType.B,
                    null,
                    DebrisType.C,
                    null,
                    DebrisType.A),
                Size: 7);

            Assert.That(DockHelpers.Occupancy(dock), Is.EqualTo(4));
        }

        [TestCase(0, DockWarningLevel.Safe)]
        [TestCase(4, DockWarningLevel.Safe)]
        [TestCase(5, DockWarningLevel.Caution)]
        [TestCase(6, DockWarningLevel.Acute)]
        [TestCase(7, DockWarningLevel.Fail)]
        [TestCase(8, DockWarningLevel.Fail)]
        public void GetWarningLevelMatchesSpecThresholds(int occupancy, DockWarningLevel expected)
        {
            Dock dock = CreateDock(occupancy);

            DockWarningLevel actual = DockHelpers.GetWarningLevel(dock);

            Assert.That(actual, Is.EqualTo(expected));
        }

        private static Dock CreateDock(int occupancy)
        {
            int slotCount = System.Math.Max(occupancy, 7);
            ImmutableArray<DebrisType?>.Builder slots = ImmutableArray.CreateBuilder<DebrisType?>(slotCount);
            for (int i = 0; i < slotCount; i++)
            {
                slots.Add(i < occupancy ? DebrisType.A : null);
            }

            return new Dock(slots.MoveToImmutable(), Size: 7);
        }
    }
}
