using System;

namespace Rescue.Core.State
{
    public enum DockWarningLevel
    {
        Safe,
        Caution,
        Acute,
        Fail,
    }

    public static class DockHelpers
    {
        public static bool IsEmpty(Dock dock)
        {
            return Occupancy(dock) == 0;
        }

        public static int Occupancy(Dock dock)
        {
            int occupied = 0;
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                if (dock.Slots[i].HasValue)
                {
                    occupied++;
                }
            }

            return occupied;
        }

        public static DockWarningLevel GetWarningLevel(Dock dock)
        {
            int occupancy = Occupancy(dock);
            if (occupancy >= dock.Size)
            {
                return DockWarningLevel.Fail;
            }

            return occupancy switch
            {
                <= 4 => DockWarningLevel.Safe,
                5 => DockWarningLevel.Caution,
                6 => DockWarningLevel.Acute,
                _ => throw new InvalidOperationException("Dock occupancy cannot exceed the fixed dock size."),
            };
        }
    }
}
