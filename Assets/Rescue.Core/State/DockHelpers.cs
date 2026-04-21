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
            return Occupancy(dock) switch
            {
                <= 4 => DockWarningLevel.Safe,
                5 => DockWarningLevel.Caution,
                6 => DockWarningLevel.Acute,
                7 => DockWarningLevel.Fail,
                _ => throw new InvalidOperationException("Dock occupancy cannot exceed the fixed dock size."),
            };
        }
    }
}
