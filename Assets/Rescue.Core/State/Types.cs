using System.Collections.Immutable;
using Rescue.Core.Rng;

namespace Rescue.Core.State
{
    public readonly record struct TileCoord(int Row, int Col);

    public enum DebrisType
    {
        A,
        B,
        C,
        D,
        E,
    }

    public abstract record Tile;

    public sealed record EmptyTile : Tile;

    public sealed record DebrisTile(DebrisType Type) : Tile;

    public enum BlockerType
    {
        Crate,
        Ice,
        Vine,
    }

    public sealed record BlockerTile(BlockerType Type, int Hp, DebrisTile? Hidden) : Tile;

    public sealed record TargetTile(string TargetId, bool Extracted) : Tile;

    public sealed record FloodedTile : Tile;

    public sealed record Board(int Width, int Height, ImmutableArray<ImmutableArray<Tile>> Tiles);

    public sealed record Dock(ImmutableArray<DebrisType?> Slots, int Size);

    public sealed record WaterState(
        int FloodedRows,
        int ActionsUntilRise,
        int RiseInterval);

    public sealed record VineState(
        int ActionsSinceLastClear,
        int GrowthThreshold,
        ImmutableArray<TileCoord> GrowthPriorityList,
        int PriorityCursor,
        TileCoord? PendingGrowthTile);

    public sealed record TargetState(
        string TargetId,
        TileCoord Coord,
        bool Extracted,
        bool OneClearAway);

    public sealed record GameState(
        Board Board,
        Dock Dock,
        WaterState Water,
        VineState Vine,
        ImmutableArray<TargetState> Targets,
        RngState RngState,
        int ActionCount,
        bool DockJamUsed,
        bool UndoAvailable,
        ImmutableArray<string> ExtractedTargetOrder,
        bool Frozen,
        int ConsecutiveEmergencySpawns,
        int SpawnRecoveryCounter);
}
