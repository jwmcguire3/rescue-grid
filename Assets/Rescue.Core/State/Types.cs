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
        int RiseInterval,
        bool PauseUntilFirstAction = false);

    public enum WaterContactMode
    {
        ImmediateLoss,
        OneTickGrace,
    }

    public sealed record VineState(
        int ActionsSinceLastClear,
        int GrowthThreshold,
        ImmutableArray<TileCoord> GrowthPriorityList,
        int PriorityCursor,
        TileCoord? PendingGrowthTile);

    public sealed record SpawnOverride(
        bool? ForceEmergency,
        double? OverrideAssistanceChance);

    public enum TargetReadiness
    {
        Trapped,
        Progressing,
        OneClearAway,
        ExtractableLatched,
        Extracted,
        Distressed,
    }

    public sealed record TargetState(string TargetId, TileCoord Coord, TargetReadiness Readiness)
    {
        public TargetState(
            string targetId,
            TileCoord coord,
            bool Extracted,
            bool OneClearAway,
            bool ExtractableLatched = false)
            : this(targetId, coord, FromLegacyState(Extracted, OneClearAway, ExtractableLatched))
        {
        }

        public bool Extracted => Readiness == TargetReadiness.Extracted;

        public bool OneClearAway => Readiness == TargetReadiness.OneClearAway;

        public bool ExtractableLatched => Readiness == TargetReadiness.ExtractableLatched;

        private static TargetReadiness FromLegacyState(
            bool extracted,
            bool oneClearAway,
            bool extractableLatched)
        {
            if (extracted)
            {
                return TargetReadiness.Extracted;
            }

            if (extractableLatched)
            {
                return TargetReadiness.ExtractableLatched;
            }

            return oneClearAway ? TargetReadiness.OneClearAway : TargetReadiness.Trapped;
        }
    }

    public sealed record GameState(
        Board Board,
        Dock Dock,
        WaterState Water,
        VineState Vine,
        ImmutableArray<TargetState> Targets,
        LevelConfig LevelConfig,
        RngState RngState,
        int ActionCount,
        bool DockJamUsed,
        bool UndoAvailable,
        ImmutableArray<string> ExtractedTargetOrder,
        bool Frozen,
        int ConsecutiveEmergencySpawns,
        int SpawnRecoveryCounter,
        bool DockJamEnabled = false,
        bool DockJamActive = false,
        SpawnOverride? DebugSpawnOverride = null);
}
