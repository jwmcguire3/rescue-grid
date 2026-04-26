using Rescue.Core.Pipeline;

namespace Rescue.Unity.Presentation
{
    public sealed record ActionPlaybackStep(
        ActionPlaybackStepType StepType,
        string? SourceEventName,
        ActionEvent? SourceEvent);
}
