namespace Rescue.Content
{
    public sealed record LevelBrief
    {
        public string Id { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string CampaignBand { get; init; } = string.Empty;

        public string Role { get; init; } = string.Empty;

        public string PrimarySkill { get; init; } = string.Empty;

        public string SecondarySkill { get; init; } = string.Empty;

        public string[]? AllowedMechanics { get; init; } = System.Array.Empty<string>();

        public string[]? ForbiddenMechanics { get; init; } = System.Array.Empty<string>();

        public LevelBriefBoardSize? BoardSize { get; init; }

        public int TargetCount { get; init; }

        public string DensityTarget { get; init; } = string.Empty;

        public string TargetFirstAttemptWinRate { get; init; } = string.Empty;

        public string IntendedTensionBeat { get; init; } = string.Empty;

        public string IntendedReleaseBeat { get; init; } = string.Empty;

        public string ExpectedPath { get; init; } = string.Empty;

        public string ExpectedFailMode { get; init; } = string.Empty;

        public string DesignNotes { get; init; } = string.Empty;
    }

    public sealed record LevelBriefBoardSize
    {
        public int Width { get; init; }

        public int Height { get; init; }
    }
}
