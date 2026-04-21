using System;
using Rescue.Core.State;

namespace Rescue.Core.Undo
{
    public sealed record Snapshot(GameState CapturedState);

    public static class SnapshotHelpers
    {
        public static Snapshot Take(GameState state)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return new Snapshot(state);
        }

        public static GameState Apply(Snapshot snapshot)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return snapshot.CapturedState;
        }
    }
}
