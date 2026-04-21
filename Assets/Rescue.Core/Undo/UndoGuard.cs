using System;
using Rescue.Core.State;

namespace Rescue.Core.Undo
{
    public static class UndoGuard
    {
        public static bool CanUndo(GameState state, Snapshot? lastSnapshot)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return state.UndoAvailable && lastSnapshot is not null;
        }

        public static GameState PerformUndo(GameState state, Snapshot lastSnapshot)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (lastSnapshot is null)
            {
                throw new ArgumentNullException(nameof(lastSnapshot));
            }

            GameState restored = SnapshotHelpers.Apply(lastSnapshot);
            return restored with { UndoAvailable = false };
        }
    }
}
