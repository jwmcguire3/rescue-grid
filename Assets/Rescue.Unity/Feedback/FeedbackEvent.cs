using Rescue.Core.Pipeline;
using Rescue.Core.State;

namespace Rescue.Unity.Feedback
{
    public readonly record struct FeedbackEvent(
        FeedbackEventId Id,
        ActionEvent? SourceEvent,
        TileCoord? Location,
        string? DebugLabel);
}
