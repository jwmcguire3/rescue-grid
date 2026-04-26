using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rescue.Unity.Presentation
{
    public sealed class ActionPlaybackPlan : IReadOnlyList<ActionPlaybackStep>
    {
        public static ActionPlaybackPlan Empty { get; } = new ActionPlaybackPlan(ImmutableArray<ActionPlaybackStep>.Empty);

        public ActionPlaybackPlan(ImmutableArray<ActionPlaybackStep> steps)
        {
            Steps = steps;
        }

        public ImmutableArray<ActionPlaybackStep> Steps { get; }

        public int Count => Steps.Length;

        public ActionPlaybackStep this[int index] => Steps[index];

        public ImmutableArray<ActionPlaybackStep>.Enumerator GetEnumerator()
        {
            return Steps.GetEnumerator();
        }

        IEnumerator<ActionPlaybackStep> IEnumerable<ActionPlaybackStep>.GetEnumerator()
        {
            return ((IEnumerable<ActionPlaybackStep>)Steps).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Steps).GetEnumerator();
        }
    }
}
