namespace Rescue.Content
{
    public sealed record LevelPacketManifest
    {
        public string PacketId { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string FirstLevelId { get; init; } = string.Empty;

        public string LastLevelId { get; init; } = string.Empty;

        public string[] ExpectedLevelIds { get; init; } = System.Array.Empty<string>();

        public string[] RuleTeachLevelIds { get; init; } = System.Array.Empty<string>();

        public string[] DockJamLevelIds { get; init; } = System.Array.Empty<string>();

        public string[] StaticVineIntroLevelIds { get; init; } = System.Array.Empty<string>();

        public DebrisPoolBand[] DebrisPoolBands { get; init; } = System.Array.Empty<DebrisPoolBand>();

        public int WaterIntervalMinimum { get; init; }

        public string Notes { get; init; } = string.Empty;
    }

    public sealed record DebrisPoolBand
    {
        public string FirstLevelId { get; init; } = string.Empty;

        public string LastLevelId { get; init; } = string.Empty;

        public int DebrisTypePoolSize { get; init; }
    }
}
