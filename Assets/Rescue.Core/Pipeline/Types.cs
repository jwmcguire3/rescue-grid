using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline
{
    public readonly record struct ActionInput(TileCoord TappedCoord);

    public readonly record struct RunOptions(bool RecordSnapshot = true);

    public sealed record ActionResult(
        GameState State,
        ImmutableArray<ActionEvent> Events,
        ActionOutcome Outcome,
        Snapshot? Snapshot);

    public enum ActionOutcome
    {
        Ok,
        Win,
        LossDockOverflow,
        LossWaterOnTarget,
    }

    public sealed record Snapshot(GameState State);

    internal sealed record StepResult(
        GameState State,
        StepContext Context,
        ImmutableArray<ActionEvent> Events);

    internal readonly record struct StepTrace(
        string StepName,
        GameState State,
        StepContext Context,
        ImmutableArray<ActionEvent> Events);
}
