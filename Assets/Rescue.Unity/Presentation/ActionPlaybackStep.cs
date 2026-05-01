using System.Collections.Immutable;
using Rescue.Core.Pipeline;

namespace Rescue.Unity.Presentation
{
    public sealed record ActionPlaybackStep(
        ActionPlaybackStepType StepType,
        string? SourceEventName,
        ActionEvent? SourceEvent,
        ImmutableArray<ActionEvent> SourceEvents = default)
    {
        public ImmutableArray<ActionEvent> Events
        {
            get
            {
                if (!SourceEvents.IsDefaultOrEmpty)
                {
                    return SourceEvents;
                }

                return SourceEvent is null
                    ? ImmutableArray<ActionEvent>.Empty
                    : ImmutableArray.Create(SourceEvent);
            }
        }
    }
}
