using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using Rescue.Core.Rng;
using Rescue.Core.State;

namespace Rescue.Content
{
    public static class Loader
    {
        public static GameState LoadLevel(string levelId, int seed)
        {
            return LoadLevel(levelId, seed, LevelTuningOverrides.None);
        }

        public static GameState LoadLevel(string levelId, int seed, LevelTuningOverrides? tuningOverrides)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                throw new ArgumentException("Level id is required.", nameof(levelId));
            }

            LevelJson parsed = LoadLevelDefinition(levelId);
            return LoadLevel(parsed, seed, tuningOverrides);
        }

        public static LevelJson LoadLevelDefinition(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                throw new ArgumentException("Level id is required.", nameof(levelId));
            }

            string json = LoadLevelJsonFromStreamingAssets(levelId);
            return ContentJson.DeserializeLevel(json);
        }

        public static GameState LoadLevel(LevelJson json, int seed)
        {
            return LoadLevel(json, seed, LevelTuningOverrides.None);
        }

        public static GameState LoadLevel(LevelJson json, int seed, LevelTuningOverrides? tuningOverrides)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            ValidationResult validation = Validator.Validate(json);
            if (validation.HasErrors)
            {
                throw new InvalidOperationException(
                    $"Level '{json.Id}' has validation errors: {FormatValidationErrors(validation.Errors)}");
            }

            EffectiveLevelTuning tuning = ResolveTuning(json, tuningOverrides);
            ImmutableArray<ImmutableArray<Tile>> boardRows = BuildBoardRows(json, tuning.DefaultCrateHp);
            Board board = new Board(json.Board.Width, json.Board.Height, boardRows);
            ImmutableArray<TargetState> targets = BuildTargets(json);

            WaterState water = new WaterState(
                FloodedRows: tuning.InitialFloodedRows,
                ActionsUntilRise: tuning.WaterRiseInterval,
                RiseInterval: tuning.WaterRiseInterval,
                PauseUntilFirstAction: json.Meta.IsRuleTeach);

            Board floodedBoard = ApplyInitialFlood(board, tuning.InitialFloodedRows);
            SeededRng rng = new SeededRng(unchecked((uint)seed));
            SpawnOverride? debugSpawnOverride = tuning.ForceEmergencyAssistance.HasValue
                ? new SpawnOverride(tuning.ForceEmergencyAssistance, OverrideAssistanceChance: null)
                : null;

            return new GameState(
                Board: floodedBoard,
                Dock: new Dock(CreateEmptyDockSlots(tuning.DockSize), tuning.DockSize),
                Water: water,
                Vine: new VineState(
                    ActionsSinceLastClear: 0,
                    GrowthThreshold: tuning.VineGrowthThreshold,
                    GrowthPriorityList: BuildGrowthPriority(json),
                    PriorityCursor: 0,
                    PendingGrowthTile: null),
                Targets: InitializeTargetStates(floodedBoard, targets),
                LevelConfig: new LevelConfig(
                    DebrisTypePool: json.DebrisTypePool.ToImmutableArray(),
                    BaseDistribution: json.BaseDistribution?.ToImmutableDictionary(),
                    AssistanceChance: tuning.AssistanceChance,
                    ConsecutiveEmergencyCap: json.Assistance.ConsecutiveEmergencyCap,
                    IsRuleTeach: json.Meta.IsRuleTeach),
                RngState: rng.GetState(),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0,
                DockJamEnabled: tuning.DockJamEnabled,
                DockJamActive: false,
                DebugSpawnOverride: debugSpawnOverride);
        }

        private static EffectiveLevelTuning ResolveTuning(LevelJson json, LevelTuningOverrides? tuningOverrides)
        {
            LevelTuningOverrides overrides = tuningOverrides ?? LevelTuningOverrides.None;

            int waterRiseInterval = overrides.WaterRiseInterval ?? json.Water.RiseInterval;
            if (waterRiseInterval < 0)
            {
                throw new InvalidOperationException("Tuned water rise interval must be >= 0.");
            }

            int initialFloodedRows = overrides.InitialFloodedRows ?? json.InitialFloodedRows;
            if (initialFloodedRows < 0 || initialFloodedRows >= json.Board.Height)
            {
                throw new InvalidOperationException("Tuned initial flooded rows must be >= 0 and less than board height.");
            }

            double assistanceChance = overrides.AssistanceChance ?? json.Assistance.Chance;
            if (double.IsNaN(assistanceChance) || double.IsInfinity(assistanceChance) || assistanceChance < 0.0d || assistanceChance > 1.0d)
            {
                throw new InvalidOperationException("Tuned assistance chance must be between 0 and 1 inclusive.");
            }

            int dockSize = overrides.DockSize ?? json.Dock.Size;
            if (dockSize <= 0)
            {
                throw new InvalidOperationException("Tuned dock size must be positive.");
            }

            int defaultCrateHp = overrides.DefaultCrateHp ?? 1;
            if (defaultCrateHp <= 0)
            {
                throw new InvalidOperationException("Tuned crate HP must be positive.");
            }

            int vineGrowthThreshold = overrides.VineGrowthThreshold ?? json.Vine.GrowthThreshold;
            if (vineGrowthThreshold < 0)
            {
                throw new InvalidOperationException("Tuned vine growth threshold must be >= 0.");
            }

            return new EffectiveLevelTuning(
                WaterRiseInterval: waterRiseInterval,
                InitialFloodedRows: initialFloodedRows,
                AssistanceChance: assistanceChance,
                ForceEmergencyAssistance: overrides.ForceEmergencyAssistance,
                DockJamEnabled: overrides.DockJamEnabled ?? json.Dock.JamEnabled,
                DockSize: dockSize,
                DefaultCrateHp: defaultCrateHp,
                VineGrowthThreshold: vineGrowthThreshold);
        }

        private static ImmutableArray<TileCoord> BuildGrowthPriority(LevelJson json)
        {
            ImmutableArray<TileCoord>.Builder coords = ImmutableArray.CreateBuilder<TileCoord>(json.Vine.GrowthPriority.Length);
            for (int i = 0; i < json.Vine.GrowthPriority.Length; i++)
            {
                TileCoordJson coord = json.Vine.GrowthPriority[i];
                coords.Add(new TileCoord(coord.Row, coord.Col));
            }

            return coords.ToImmutable();
        }

        private static ImmutableArray<ImmutableArray<Tile>> BuildBoardRows(LevelJson json, int defaultCrateHp)
        {
            Dictionary<string, string> targetTileIds = GetTargetTileIds(json);
            ImmutableArray<ImmutableArray<Tile>>.Builder rows = ImmutableArray.CreateBuilder<ImmutableArray<Tile>>(json.Board.Height);
            for (int row = 0; row < json.Board.Height; row++)
            {
                ImmutableArray<Tile>.Builder tiles = ImmutableArray.CreateBuilder<Tile>(json.Board.Width);
                for (int col = 0; col < json.Board.Width; col++)
                {
                    string code = json.Board.Tiles[row][col];
                    tiles.Add(ParseTile(code, targetTileIds, row, col, defaultCrateHp));
                }

                rows.Add(tiles.ToImmutable());
            }

            return rows.ToImmutable();
        }

        private static ImmutableArray<TargetState> BuildTargets(LevelJson json)
        {
            ImmutableArray<TargetState>.Builder targets = ImmutableArray.CreateBuilder<TargetState>(json.Targets.Length);
            for (int i = 0; i < json.Targets.Length; i++)
            {
                TargetJson target = json.Targets[i];
                targets.Add(new TargetState(
                    target.Id,
                    new TileCoord(target.Row, target.Col),
                    Extracted: false,
                    OneClearAway: false));
            }

            return targets.ToImmutable();
        }

        private static ImmutableArray<TargetState> InitializeTargetStates(Board board, ImmutableArray<TargetState> targets)
        {
            ImmutableArray<TargetState>.Builder initialized = ImmutableArray.CreateBuilder<TargetState>(targets.Length);
            for (int i = 0; i < targets.Length; i++)
            {
                TargetState target = targets[i];
                int blockedNeighbors = CountBlockedRequiredNeighbors(board, target.Coord);
                initialized.Add(target with
                {
                    Readiness = CalculateInitialReadiness(board, target.Coord, blockedNeighbors),
                });
            }

            return initialized.ToImmutable();
        }

        private static TargetReadiness CalculateInitialReadiness(Board board, TileCoord targetCoord, int blockedRequiredNeighbors)
        {
            if (blockedRequiredNeighbors == 1)
            {
                return TargetReadiness.OneClearAway;
            }

            int requiredNeighbors = BoardHelpers.OrthogonalNeighbors(board, targetCoord).Length;
            int openNeighbors = requiredNeighbors - blockedRequiredNeighbors;
            return openNeighbors * 2 >= requiredNeighbors
                ? TargetReadiness.Progressing
                : TargetReadiness.Trapped;
        }

        private static int CountBlockedRequiredNeighbors(Board board, TileCoord targetCoord)
        {
            int blocked = 0;
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, targetCoord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (BoardHelpers.GetTile(board, neighbors[i]) is not EmptyTile)
                {
                    blocked++;
                }
            }

            return blocked;
        }

        private static ImmutableArray<DebrisType?> CreateEmptyDockSlots(int size)
        {
            ImmutableArray<DebrisType?>.Builder slots = ImmutableArray.CreateBuilder<DebrisType?>(size);
            for (int i = 0; i < size; i++)
            {
                slots.Add(null);
            }

            return slots.ToImmutable();
        }

        private static Board ApplyInitialFlood(Board board, int floodedRows)
        {
            Board floodedBoard = board;
            for (int row = board.Height - floodedRows; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    floodedBoard = BoardHelpers.SetTile(floodedBoard, new TileCoord(row, col), new FloodedTile());
                }
            }

            return floodedBoard;
        }

        private static Dictionary<string, string> GetTargetTileIds(LevelJson json)
        {
            Dictionary<string, string> ids = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < json.Targets.Length; i++)
            {
                ids[json.Targets[i].Id] = json.Targets[i].Id;
            }

            return ids;
        }

        private static Tile ParseTile(string code, IReadOnlyDictionary<string, string> targetIds, int row, int col, int defaultCrateHp)
        {
            if (code == ".")
            {
                return new EmptyTile();
            }

            if (TryParseDebrisCode(code, out DebrisType debrisType))
            {
                return new DebrisTile(debrisType);
            }

            if (code == "CR")
            {
                return new BlockerTile(BlockerType.Crate, defaultCrateHp, Hidden: null);
            }

            if (code == "CX")
            {
                return new BlockerTile(BlockerType.Crate, 2, Hidden: null);
            }

            if (code == "V")
            {
                return new BlockerTile(BlockerType.Vine, 1, Hidden: null);
            }

            if (code.Length == 2 && code[0] == 'I' && TryParseDebrisCode(code[1].ToString(), out DebrisType hiddenDebris))
            {
                return new BlockerTile(BlockerType.Ice, 1, new DebrisTile(hiddenDebris));
            }

            if (code.Length >= 2 && code[0] == 'T')
            {
                string targetId = code[1..];
                if (targetIds.ContainsKey(targetId))
                {
                    return new TargetTile(targetId, Extracted: false);
                }
            }

            throw new InvalidOperationException($"Unrecognized tile '{code}' at row {row}, col {col}.");
        }

        private static bool TryParseDebrisCode(string code, out DebrisType debrisType)
        {
            debrisType = default;
            return code switch
            {
                "A" => Assign(DebrisType.A, out debrisType),
                "B" => Assign(DebrisType.B, out debrisType),
                "C" => Assign(DebrisType.C, out debrisType),
                "D" => Assign(DebrisType.D, out debrisType),
                "E" => Assign(DebrisType.E, out debrisType),
                _ => false,
            };
        }

        private static bool Assign(DebrisType value, out DebrisType debrisType)
        {
            debrisType = value;
            return true;
        }

        private static string LoadLevelJsonFromStreamingAssets(string levelId)
        {
            Type? applicationType = Type.GetType("UnityEngine.Application, UnityEngine.CoreModule");
            if (applicationType is null)
            {
                throw new PlatformNotSupportedException("Unity streaming assets loading is only available inside the Unity runtime.");
            }

            PropertyInfo? streamingAssetsPathProperty = applicationType.GetProperty(
                "streamingAssetsPath",
                BindingFlags.Public | BindingFlags.Static);
            if (streamingAssetsPathProperty?.GetValue(null) is not string streamingAssetsPath
                || string.IsNullOrWhiteSpace(streamingAssetsPath))
            {
                throw new MissingMemberException("UnityEngine.Application.streamingAssetsPath was not found.");
            }

            string levelPath = Path.Combine(streamingAssetsPath, "Levels", levelId + ".json");
            if (!File.Exists(levelPath))
            {
                throw new InvalidOperationException($"Level '{levelId}' was not found in Assets/StreamingAssets/Levels/.");
            }

            return File.ReadAllText(levelPath);
        }

        private static string FormatValidationErrors(IReadOnlyList<ValidationError> errors)
        {
            List<string> messages = new List<string>(errors.Count);
            for (int i = 0; i < errors.Count; i++)
            {
                ValidationError error = errors[i];
                messages.Add($"{error.Severity}:{error.Code}@{error.Path}");
            }

            return string.Join(", ", messages);
        }
    }
}
