using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline
{
    public abstract record ActionEvent;

    public enum InvalidInputReason
    {
        Frozen,
        OutOfBounds,
        Flooded,
        Ice,
        Blocker,
        Target,
        Empty,
        SingleTile,
    }

    public sealed record InvalidInput(TileCoord TappedCoord, InvalidInputReason Reason) : ActionEvent;

    public sealed record GroupRemoved(DebrisType Type, ImmutableArray<TileCoord> Coords) : ActionEvent;

    public sealed record BlockerDamaged(TileCoord Coord, BlockerType Type, int RemainingHp) : ActionEvent;

    public sealed record BlockerBroken(TileCoord Coord, BlockerType Type) : ActionEvent;

    public sealed record IceRevealed(TileCoord Coord, DebrisType RevealedType) : ActionEvent;

    public sealed record DockInserted(ImmutableArray<DebrisType> Pieces, int OccupancyAfterInsert, int OverflowCount) : ActionEvent;

    public sealed record DockOverflowTriggered(int OverflowCount) : ActionEvent;

    public sealed record DockCleared(DebrisType Type, int SetsCleared, int OccupancyAfterClear) : ActionEvent;

    public sealed record DockWarningChanged(DockWarningLevel Before, DockWarningLevel After) : ActionEvent;

    public sealed record GravitySettled(ImmutableArray<(TileCoord From, TileCoord To)> Moves) : ActionEvent;

    public sealed record Spawned(ImmutableArray<(TileCoord Coord, DebrisType Type)> Pieces) : ActionEvent;

    public sealed record TargetOneClearAway(string TargetId, TileCoord Coord) : ActionEvent;

    public sealed record TargetExtracted(string TargetId, TileCoord Coord) : ActionEvent;

    public sealed record WaterWarning(int ActionsUntilRise, int NextFloodRow) : ActionEvent;

    public sealed record WaterRose(int NewFloodedRows) : ActionEvent;

    public sealed record VinePreviewChanged(TileCoord? PendingTile) : ActionEvent;

    public sealed record VineGrown(TileCoord Coord) : ActionEvent;

    public sealed record DockJamTriggered(int OverflowCount) : ActionEvent;

    public sealed record Lost(ActionOutcome Outcome) : ActionEvent;

    public sealed record Won(ImmutableArray<string> ExtractedTargetOrder) : ActionEvent;
}
