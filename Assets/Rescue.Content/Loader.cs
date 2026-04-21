using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Rescue.Core.Rng;
using Rescue.Core.State;

namespace Rescue.Content
{
    public static class Loader
    {
        public static GameState LoadLevel(string levelId, int seed)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                throw new ArgumentException("Level id is required.", nameof(levelId));
            }

            string json = LoadLevelJsonFromUnityResources(levelId);
            LevelJson parsed = ContentJson.DeserializeLevel(json);
            return LoadLevel(parsed, seed);
        }

        public static GameState LoadLevel(LevelJson json, int seed)
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

            ImmutableArray<ImmutableArray<Tile>> boardRows = BuildBoardRows(json);
            Board board = new Board(json.Board.Width, json.Board.Height, boardRows);
            ImmutableArray<TargetState> targets = BuildTargets(json);

            WaterState water = new WaterState(
                FloodedRows: json.InitialFloodedRows,
                ActionsUntilRise: json.Water.RiseInterval,
                RiseInterval: json.Water.RiseInterval);

            Board floodedBoard = ApplyInitialFlood(board, json.InitialFloodedRows);
            SeededRng rng = new SeededRng(unchecked((uint)seed));

            return new GameState(
                Board: floodedBoard,
                Dock: new Dock(CreateEmptyDockSlots(json.Dock.Size), json.Dock.Size),
                Water: water,
                Vine: new VineState(
                    ActionsSinceLastClear: 0,
                    GrowthThreshold: json.Vine.GrowthThreshold,
                    GrowthPriorityList: BuildGrowthPriority(json),
                    PriorityCursor: 0,
                    PendingGrowthTile: null),
                Targets: InitializeTargetStates(floodedBoard, targets),
                LevelConfig: new LevelConfig(
                    DebrisTypePool: json.DebrisTypePool.ToImmutableArray(),
                    BaseDistribution: json.BaseDistribution?.ToImmutableDictionary(),
                    AssistanceChance: json.Assistance.Chance,
                    ConsecutiveEmergencyCap: json.Assistance.ConsecutiveEmergencyCap),
                RngState: rng.GetState(),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0,
                DockJamEnabled: json.Dock.JamEnabled,
                DockJamActive: false,
                DebugSpawnOverride: null);
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

        private static ImmutableArray<ImmutableArray<Tile>> BuildBoardRows(LevelJson json)
        {
            Dictionary<string, string> targetTileIds = GetTargetTileIds(json);
            ImmutableArray<ImmutableArray<Tile>>.Builder rows = ImmutableArray.CreateBuilder<ImmutableArray<Tile>>(json.Board.Height);
            for (int row = 0; row < json.Board.Height; row++)
            {
                ImmutableArray<Tile>.Builder tiles = ImmutableArray.CreateBuilder<Tile>(json.Board.Width);
                for (int col = 0; col < json.Board.Width; col++)
                {
                    string code = json.Board.Tiles[row][col];
                    tiles.Add(ParseTile(code, targetTileIds, row, col));
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
                    OneClearAway = blockedNeighbors == 1,
                });
            }

            return initialized.ToImmutable();
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

        private static Tile ParseTile(string code, IReadOnlyDictionary<string, string> targetIds, int row, int col)
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
                return new BlockerTile(BlockerType.Crate, 1, Hidden: null);
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

        private static string LoadLevelJsonFromUnityResources(string levelId)
        {
            Type? resourcesType = Type.GetType("UnityEngine.Resources, UnityEngine.CoreModule");
            Type? textAssetType = Type.GetType("UnityEngine.TextAsset, UnityEngine.CoreModule");
            if (resourcesType is null || textAssetType is null)
            {
                throw new PlatformNotSupportedException("Unity Resources loading is only available inside the Unity runtime.");
            }

            MethodInfo? loadMethod = resourcesType.GetMethod(
                "Load",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[]
                {
                    typeof(string),
                    typeof(Type),
                },
                modifiers: null);

            if (loadMethod is null)
            {
                throw new MissingMethodException("UnityEngine.Resources.Load(string, Type) was not found.");
            }

            object? asset = loadMethod.Invoke(null, new object?[] { "Levels/" + levelId, textAssetType });
            if (asset is null)
            {
                throw new InvalidOperationException($"Level '{levelId}' was not found in Assets/Resources/Levels/.");
            }

            PropertyInfo? textProperty = textAssetType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (textProperty?.GetValue(asset) is not string text)
            {
                throw new InvalidOperationException($"Level '{levelId}' could not be read as a TextAsset.");
            }

            return text;
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
