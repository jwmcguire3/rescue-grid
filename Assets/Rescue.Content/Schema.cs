using System.Collections.Generic;
using Rescue.Core.State;

namespace Rescue.Content
{
    public sealed record LevelJson
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public BoardJson Board { get; init; } = new BoardJson();

        public DebrisType[] DebrisTypePool { get; init; } = System.Array.Empty<DebrisType>();

        public Dictionary<DebrisType, double>? BaseDistribution { get; init; }

        public TargetJson[] Targets { get; init; } = System.Array.Empty<TargetJson>();

        public int InitialFloodedRows { get; init; }

        public WaterJson Water { get; init; } = new WaterJson();

        public VineJson Vine { get; init; } = new VineJson();

        public DockJson Dock { get; init; } = new DockJson();

        public AssistanceJson Assistance { get; init; } = new AssistanceJson();

        public MetaJson Meta { get; init; } = new MetaJson();
    }

    public sealed record BoardJson
    {
        public int Width { get; init; }

        public int Height { get; init; }

        public string[][] Tiles { get; init; } = System.Array.Empty<string[]>();
    }

    public sealed record TargetJson
    {
        public string Id { get; init; } = string.Empty;

        public int Row { get; init; }

        public int Col { get; init; }
    }

    public sealed record WaterJson
    {
        public int RiseInterval { get; init; }

        public WaterContactMode ContactMode { get; init; } = WaterContactMode.ImmediateLoss;
    }

    public sealed record VineJson
    {
        public int GrowthThreshold { get; init; }

        public TileCoordJson[] GrowthPriority { get; init; } = System.Array.Empty<TileCoordJson>();
    }

    public sealed record DockJson
    {
        public int Size { get; init; }

        public bool JamEnabled { get; init; }
    }

    public sealed record AssistanceJson
    {
        public double Chance { get; init; }

        public int ConsecutiveEmergencyCap { get; init; }

        public SpawnIntegrityJson SpawnIntegrity { get; init; } = new SpawnIntegrityJson();
    }

    public sealed record SpawnIntegrityJson
    {
        public bool AllowExactTripleSpawns { get; init; }

        public bool AllowOversizedSpawnGroups { get; init; }
    }

    public sealed record MetaJson
    {
        public string Intent { get; init; } = string.Empty;

        public string ExpectedPath { get; init; } = string.Empty;

        public string ExpectedFailMode { get; init; } = string.Empty;

        public string WhatItProves { get; init; } = string.Empty;

        public string? Notes { get; init; }

        public bool IsRuleTeach { get; init; }
    }

    public sealed record TileCoordJson
    {
        public int Row { get; init; }

        public int Col { get; init; }
    }

    public enum ValidationSeverity
    {
        Warning,
        Error,
    }

    public sealed record ValidationError(
        ValidationSeverity Severity,
        string Code,
        string Message,
        string Path);

    public sealed record ValidationResult(
        bool IsValid,
        IReadOnlyList<ValidationError> Errors)
    {
        public bool HasWarnings
        {
            get
            {
                for (int i = 0; i < Errors.Count; i++)
                {
                    if (Errors[i].Severity == ValidationSeverity.Warning)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Errors.Count; i++)
                {
                    if (Errors[i].Severity == ValidationSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static ValidationResult Success()
        {
            return new ValidationResult(IsValid: true, Errors: System.Array.Empty<ValidationError>());
        }

        public static ValidationResult FromErrors(IReadOnlyList<ValidationError> errors)
        {
            bool hasErrors = false;
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i].Severity == ValidationSeverity.Error)
                {
                    hasErrors = true;
                    break;
                }
            }

            return new ValidationResult(!hasErrors, errors);
        }
    }
}
