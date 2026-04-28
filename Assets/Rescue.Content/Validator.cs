using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Content
{
    public static class Validator
    {
        public static ValidationResult Validate(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return ValidationResult.FromErrors(new[]
                {
                    new ValidationError(ValidationSeverity.Error, "json.empty", "Level JSON must not be empty.", "$"),
                });
            }

            LevelJson parsed;
            try
            {
                parsed = ContentJson.DeserializeLevel(json);
            }
            catch (ContentJsonException ex)
            {
                string path = string.IsNullOrWhiteSpace(ex.Path) ? "$" : ex.Path!;
                return ValidationResult.FromErrors(new[]
                {
                    new ValidationError(ValidationSeverity.Error, "json.parse", ex.Message, path),
                });
            }

            return Validate(parsed);
        }

        public static ValidationResult Validate(LevelJson level)
        {
            if (level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            List<ValidationError> errors = new List<ValidationError>();
            ValidateRequiredText(level, errors);
            ValidateBoardShape(level, errors);
            ValidatePoolAndDistribution(level, errors);
            ValidateTargets(level, errors);
            ValidateSimpleRanges(level, errors);

            if (HasBlockingErrors(errors))
            {
                return ValidationResult.FromErrors(errors);
            }

            AnalyzedLevel analysis = Analyze(level);
            ValidateBoardCodes(level, analysis, errors);
            ValidateTargetTiles(level, analysis, errors);
            ValidateGrowthPriority(level, errors);

            if (HasBlockingErrors(errors))
            {
                return ValidationResult.FromErrors(errors);
            }

            AddHeuristicWarnings(level, analysis, errors);
            return errors.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.FromErrors(errors);
        }

        private static void ValidateRequiredText(LevelJson level, List<ValidationError> errors)
        {
            AddRequiredTextError(level.Id, "$.id", "id", errors);
            AddRequiredTextError(level.Name, "$.name", "name", errors);
            AddRequiredTextError(level.Meta.Intent, "$.meta.intent", "meta.intent", errors);
            AddRequiredTextError(level.Meta.ExpectedPath, "$.meta.expectedPath", "meta.expectedPath", errors);
            AddRequiredTextError(level.Meta.ExpectedFailMode, "$.meta.expectedFailMode", "meta.expectedFailMode", errors);
            AddRequiredTextError(level.Meta.WhatItProves, "$.meta.whatItProves", "meta.whatItProves", errors);
        }

        private static void AddRequiredTextError(string value, string path, string fieldName, List<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new ValidationError(ValidationSeverity.Error, "field.required", $"{fieldName} is required.", path));
            }
        }

        private static void ValidateBoardShape(LevelJson level, List<ValidationError> errors)
        {
            if (level.Board.Width <= 0)
            {
                errors.Add(new ValidationError(ValidationSeverity.Error, "board.width", "Board width must be positive.", "$.board.width"));
            }

            if (level.Board.Height <= 0)
            {
                errors.Add(new ValidationError(ValidationSeverity.Error, "board.height", "Board height must be positive.", "$.board.height"));
            }

            if (level.Board.Tiles.Length != level.Board.Height)
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Error,
                    "board.tiles.height",
                    $"Board height is {level.Board.Height}, but tiles has {level.Board.Tiles.Length} rows.",
                    "$.board.tiles"));
            }

            for (int row = 0; row < level.Board.Tiles.Length; row++)
            {
                string[] tileRow = level.Board.Tiles[row];
                if (tileRow.Length != level.Board.Width)
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Error,
                        "board.tiles.width",
                        $"Row {row} has {tileRow.Length} tiles, expected {level.Board.Width}.",
                        $"$.board.tiles[{row}]"));
                }
            }
        }

        private static void ValidatePoolAndDistribution(LevelJson level, List<ValidationError> errors)
        {
            if (level.DebrisTypePool.Length < 4 || level.DebrisTypePool.Length > 5)
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Error,
                    "debris.pool.size",
                    "debrisTypePool must contain 4 or 5 entries.",
                    "$.debrisTypePool"));
            }

            HashSet<DebrisType> distinctPool = new HashSet<DebrisType>();
            for (int i = 0; i < level.DebrisTypePool.Length; i++)
            {
                DebrisType debrisType = level.DebrisTypePool[i];
                if (!distinctPool.Add(debrisType))
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Error,
                        "debris.pool.duplicate",
                        $"debrisTypePool contains duplicate entry '{debrisType}'.",
                        $"$.debrisTypePool[{i}]"));
                }
            }

            if (level.BaseDistribution is null)
            {
                return;
            }

            foreach ((DebrisType type, double weight) in level.BaseDistribution)
            {
                if (!distinctPool.Contains(type))
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Error,
                        "distribution.unknownType",
                        $"baseDistribution contains '{type}' which is not in debrisTypePool.",
                        $"$.baseDistribution.{type}"));
                }

                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0.0d)
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Error,
                        "distribution.invalidWeight",
                        $"baseDistribution weight for '{type}' must be finite and non-negative.",
                        $"$.baseDistribution.{type}"));
                }
            }
        }

        private static void ValidateTargets(LevelJson level, List<ValidationError> errors)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < level.Targets.Length; i++)
            {
                TargetJson target = level.Targets[i];
                string path = $"$.targets[{i}]";

                if (string.IsNullOrWhiteSpace(target.Id))
                {
                    errors.Add(new ValidationError(ValidationSeverity.Error, "target.id", "Target id is required.", path + ".id"));
                }
                else if (!ids.Add(target.Id))
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Error,
                        "target.duplicateId",
                        $"Target id '{target.Id}' is duplicated.",
                        path + ".id"));
                }

                if (!IsInBounds(level.Board.Height, level.Board.Width, target.Row, target.Col))
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Error,
                        "target.bounds",
                        $"Target '{target.Id}' is out of bounds at ({target.Row}, {target.Col}).",
                        path));
                }
            }
        }

        private static void ValidateSimpleRanges(LevelJson level, List<ValidationError> errors)
        {
            if (level.InitialFloodedRows < 0 || level.InitialFloodedRows >= level.Board.Height)
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Error,
                    "water.initialFloodedRows",
                    "initialFloodedRows must be >= 0 and less than board height.",
                    "$.initialFloodedRows"));
            }

            if (level.Water.RiseInterval < 0)
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Error,
                    "water.riseInterval",
                    "water.riseInterval must be >= 0.",
                    "$.water.riseInterval"));
            }

            if (level.Meta.IsRuleTeach && level.Water.RiseInterval <= 0)
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Error,
                    "water.ruleTeachRiseInterval",
                    "Rule-teach levels must set water.riseInterval to a positive value so ticking can begin on the first valid action.",
                    "$.water.riseInterval"));
            }

            if (level.Dock.Size != 7)
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Error,
                    "dock.size",
                    "dock.size must be exactly 7 for Phase 1.",
                    "$.dock.size"));
            }

            if (level.Assistance.Chance < 0.0d || level.Assistance.Chance > 1.0d)
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Error,
                    "assistance.chance",
                    "assistance.chance must be between 0 and 1 inclusive.",
                    "$.assistance.chance"));
            }

            if (level.Assistance.SpawnIntegrity is null)
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Error,
                    "assistance.spawnIntegrity",
                    "assistance.spawnIntegrity must be an object when provided.",
                    "$.assistance.spawnIntegrity"));
            }
        }

        private static void ValidateBoardCodes(LevelJson level, AnalyzedLevel analysis, List<ValidationError> errors)
        {
            HashSet<DebrisType> pool = new HashSet<DebrisType>(level.DebrisTypePool);
            for (int row = 0; row < level.Board.Height; row++)
            {
                for (int col = 0; col < level.Board.Width; col++)
                {
                    string code = level.Board.Tiles[row][col];
                    if (!TryParseCell(code, out CellInfo cell))
                    {
                        errors.Add(new ValidationError(
                            ValidationSeverity.Error,
                            "tile.unknown",
                            $"Tile code '{code}' is not recognized.",
                            $"$.board.tiles[{row}][{col}]"));
                        continue;
                    }

                    if (cell.DebrisType.HasValue && !pool.Contains(cell.DebrisType.Value))
                    {
                        errors.Add(new ValidationError(
                            ValidationSeverity.Error,
                            "tile.debrisNotInPool",
                            $"Tile code '{code}' uses debris type '{cell.DebrisType.Value}' which is not in debrisTypePool.",
                            $"$.board.tiles[{row}][{col}]"));
                    }

                    if (cell.HiddenDebrisType.HasValue && !pool.Contains(cell.HiddenDebrisType.Value))
                    {
                        errors.Add(new ValidationError(
                            ValidationSeverity.Error,
                            "tile.hiddenDebrisNotInPool",
                            $"Ice tile '{code}' hides debris type '{cell.HiddenDebrisType.Value}' which is not in debrisTypePool.",
                            $"$.board.tiles[{row}][{col}]"));
                    }

                    if (cell.Kind == CellKind.Target)
                    {
                        analysis.TargetTiles[$"{row}:{col}"] = cell.TargetId!;
                    }
                }
            }
        }

        private static void ValidateTargetTiles(LevelJson level, AnalyzedLevel analysis, List<ValidationError> errors)
        {
            HashSet<string> declaredTargetIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < level.Targets.Length; i++)
            {
                declaredTargetIds.Add(level.Targets[i].Id);
            }

            for (int i = 0; i < level.Targets.Length; i++)
            {
                TargetJson target = level.Targets[i];
                string coordKey = $"{target.Row}:{target.Col}";
                if (!analysis.TargetTiles.TryGetValue(coordKey, out string? tileId))
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Error,
                        "target.tileMissing",
                        $"Target '{target.Id}' must sit on a matching T<id> tile.",
                        $"$.targets[{i}]"));
                    continue;
                }

                if (!string.Equals(tileId, target.Id, StringComparison.Ordinal))
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Error,
                        "target.tileMismatch",
                        $"Target '{target.Id}' does not match tile id '{tileId}'.",
                        $"$.targets[{i}]"));
                }
            }

            foreach ((string key, string tileId) in analysis.TargetTiles)
            {
                if (!declaredTargetIds.Contains(tileId))
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Error,
                        "target.tileWithoutEntry",
                        $"Tile target '{tileId}' at {key} has no entry in targets[].",
                        "$.board.tiles"));
                }
            }
        }

        private static void ValidateGrowthPriority(LevelJson level, List<ValidationError> errors)
        {
            for (int i = 0; i < level.Vine.GrowthPriority.Length; i++)
            {
                TileCoordJson coord = level.Vine.GrowthPriority[i];
                if (!IsInBounds(level.Board.Height, level.Board.Width, coord.Row, coord.Col))
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Error,
                        "vine.priority.bounds",
                        $"vine.growthPriority entry ({coord.Row}, {coord.Col}) is out of bounds.",
                        $"$.vine.growthPriority[{i}]"));
                }
            }
        }

        private static void AddHeuristicWarnings(LevelJson level, AnalyzedLevel analysis, List<ValidationError> errors)
        {
            AddPhase1PolicyWarnings(level, analysis, errors);

            if (CountConnectedPlayableComponents(analysis) > 1)
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "heuristic.disconnectedRegions",
                    "The board has multiple disconnected dry playable regions at start.",
                    "$.board.tiles"));
            }

            if (TriggersDockTrapWarning(analysis))
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "heuristic.dockTrap",
                    "The initial debris layout strongly suggests a singleton-heavy dock trap.",
                    "$.board.tiles"));
            }

            for (int i = 0; i < level.Targets.Length; i++)
            {
                TargetJson target = level.Targets[i];
                if (IsImpossibleAtStart(level, target))
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Error,
                        "heuristic.impossibleStart",
                        $"Target '{target.Id}' begins unsaveable because required access is already flooded.",
                        $"$.targets[{i}]"));
                    continue;
                }

                if (!HasDryRouteToTop(analysis, target))
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Warning,
                        "heuristic.unreachableTarget",
                        $"Target '{target.Id}' has no dry route to the top region.",
                        $"$.targets[{i}]"));
                }

                if (ExceedsHazardBudget(level, analysis, target))
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Warning,
                        "heuristic.hazardBudget",
                        $"Target '{target.Id}' appears to require more actions than the available water budget.",
                        $"$.targets[{i}]"));
                }
            }
        }

        private static void AddPhase1PolicyWarnings(LevelJson level, AnalyzedLevel analysis, List<ValidationError> errors)
        {
            if (TryGetLevelNumber(level.Id, out int levelNumber))
            {
                bool expectedDockJam = levelNumber is 1 or 2;
                if (level.Dock.JamEnabled != expectedDockJam)
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Warning,
                        "phase1.dockJamLevel",
                        "Dock Jam should be enabled only on L01 and L02 for Phase 1.",
                        "$.dock.jamEnabled"));
                }

                if (levelNumber >= 1)
                {
                    int expectedPoolSize = levelNumber <= 4 ? 4 : 5;
                    if (level.DebrisTypePool.Length != expectedPoolSize)
                    {
                        errors.Add(new ValidationError(
                            ValidationSeverity.Warning,
                            "phase1.debrisPoolSize",
                            $"L{levelNumber:00} should use a debrisTypePool size of {expectedPoolSize} for Phase 1.",
                            "$.debrisTypePool"));
                    }
                }

                if (levelNumber == 7
                    && level.Vine.GrowthPriority.Length > 0
                    && level.Vine.GrowthThreshold < 999)
                {
                    errors.Add(new ValidationError(
                        ValidationSeverity.Warning,
                        "phase1.l07VineGrowth",
                        "L07 is the static vine introduction; vine growth should be disabled.",
                        "$.vine"));
                }
            }

            if (!level.Meta.IsRuleTeach && level.Water.RiseInterval > 0 && level.Water.RiseInterval < 6)
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.waterIntervalBelow6",
                    "Phase 1 water.riseInterval should not be below 6 outside the L00 rule-teach special case.",
                    "$.water.riseInterval"));
            }

            if (ContainsReinforcedCrate(analysis))
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.reinforcedCrate",
                    "Reinforced crates are off by default in Phase 1 and should only appear after explicit late-packet tuning approval.",
                    "$.board.tiles"));
            }

            if (level.Assistance.SpawnIntegrity.AllowExactTripleSpawns
                && string.IsNullOrWhiteSpace(level.Meta.Notes))
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.spawnIntegrity.exactTripleException",
                    "Exact triple spawn exceptions require meta.notes explaining the teaching, coaching, or relief reason.",
                    "$.assistance.spawnIntegrity.allowExactTripleSpawns"));
            }

            if (level.Assistance.SpawnIntegrity.AllowOversizedSpawnGroups
                && string.IsNullOrWhiteSpace(level.Meta.Notes))
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    "phase1.spawnIntegrity.oversizedException",
                    "Oversized spawn group exceptions require meta.notes explaining the teaching, coaching, or relief reason.",
                    "$.assistance.spawnIntegrity.allowOversizedSpawnGroups"));
            }
        }

        private static bool TryGetLevelNumber(string id, out int levelNumber)
        {
            levelNumber = 0;
            if (id.Length != 3 || id[0] != 'L')
            {
                return false;
            }

            return int.TryParse(id[1..], out levelNumber);
        }

        private static bool ContainsReinforcedCrate(AnalyzedLevel analysis)
        {
            for (int row = 0; row < analysis.Height; row++)
            {
                for (int col = 0; col < analysis.Width; col++)
                {
                    CellInfo cell = analysis.Cells[row, col];
                    if (cell.Kind == CellKind.Crate && cell.Hp > 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsImpossibleAtStart(LevelJson level, TargetJson target)
        {
            if (IsFloodedRow(level, target.Row))
            {
                return true;
            }

            ImmutableArray<CellCoord> neighbors = GetRequiredNeighbors(level.Board.Height, level.Board.Width, new CellCoord(target.Row, target.Col));
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (IsFloodedRow(level, neighbors[i].Row))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasDryRouteToTop(AnalyzedLevel analysis, TargetJson target)
        {
            Queue<CellCoord> frontier = new Queue<CellCoord>();
            HashSet<CellCoord> visited = new HashSet<CellCoord>();
            CellCoord start = new CellCoord(target.Row, target.Col);
            frontier.Enqueue(start);
            visited.Add(start);

            while (frontier.Count > 0)
            {
                CellCoord current = frontier.Dequeue();
                if (current.Row == 0 && analysis.IsTopRegion(current))
                {
                    return true;
                }

                ImmutableArray<CellCoord> neighbors = GetRequiredNeighbors(analysis.Height, analysis.Width, current);
                for (int i = 0; i < neighbors.Length; i++)
                {
                    CellCoord neighbor = neighbors[i];
                    if (visited.Contains(neighbor) || !analysis.IsPassable(neighbor))
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    frontier.Enqueue(neighbor);
                }
            }

            return false;
        }

        private static int CountConnectedPlayableComponents(AnalyzedLevel analysis)
        {
            HashSet<CellCoord> visited = new HashSet<CellCoord>();
            int components = 0;

            for (int row = 0; row < analysis.Height; row++)
            {
                for (int col = 0; col < analysis.Width; col++)
                {
                    CellCoord start = new CellCoord(row, col);
                    if (visited.Contains(start) || !analysis.IsPlayable(start))
                    {
                        continue;
                    }

                    components++;
                    Queue<CellCoord> frontier = new Queue<CellCoord>();
                    frontier.Enqueue(start);
                    visited.Add(start);

                    while (frontier.Count > 0)
                    {
                        CellCoord current = frontier.Dequeue();
                        ImmutableArray<CellCoord> neighbors = GetRequiredNeighbors(analysis.Height, analysis.Width, current);
                        for (int i = 0; i < neighbors.Length; i++)
                        {
                            CellCoord neighbor = neighbors[i];
                            if (visited.Contains(neighbor) || !analysis.IsPlayable(neighbor))
                            {
                                continue;
                            }

                            visited.Add(neighbor);
                            frontier.Enqueue(neighbor);
                        }
                    }
                }
            }

            return components;
        }

        private static bool TriggersDockTrapWarning(AnalyzedLevel analysis)
        {
            Dictionary<DebrisType, int> debrisCounts = new Dictionary<DebrisType, int>();
            bool hasPair = false;

            for (int row = 0; row < analysis.Height; row++)
            {
                for (int col = 0; col < analysis.Width; col++)
                {
                    CellInfo cell = analysis.Cells[row, col];
                    if (cell.Kind != CellKind.Debris || analysis.IsFlooded(new CellCoord(row, col)))
                    {
                        continue;
                    }

                    debrisCounts.TryGetValue(cell.DebrisType!.Value, out int currentCount);
                    debrisCounts[cell.DebrisType.Value] = currentCount + 1;

                    ImmutableArray<CellCoord> neighbors = GetRequiredNeighbors(analysis.Height, analysis.Width, new CellCoord(row, col));
                    for (int i = 0; i < neighbors.Length; i++)
                    {
                        CellInfo neighbor = analysis.Cells[neighbors[i].Row, neighbors[i].Col];
                        if (neighbor.Kind == CellKind.Debris
                            && neighbor.DebrisType == cell.DebrisType
                            && !analysis.IsFlooded(neighbors[i]))
                        {
                            hasPair = true;
                            break;
                        }
                    }
                }
            }

            if (hasPair)
            {
                return false;
            }

            foreach ((DebrisType _, int count) in debrisCounts)
            {
                if (count >= 3)
                {
                    return false;
                }
            }

            return debrisCounts.Count > 0;
        }

        private static bool ExceedsHazardBudget(LevelJson level, AnalyzedLevel analysis, TargetJson target)
        {
            if (level.Water.RiseInterval == 0)
            {
                return false;
            }

            int nextFloodRow = level.Board.Height - level.InitialFloodedRows - 1;
            int risesRemaining = (nextFloodRow - target.Row) + 1;
            if (risesRemaining < 0)
            {
                return true;
            }

            int waterBudget = risesRemaining * level.Water.RiseInterval;
            int blockedNeighbors = CountBlockedRequiredNeighbors(analysis, target);
            int routeDistance = EstimateRouteDistanceToTarget(analysis, target);
            return waterBudget < blockedNeighbors + routeDistance;
        }

        private static int CountBlockedRequiredNeighbors(AnalyzedLevel analysis, TargetJson target)
        {
            int blocked = 0;
            ImmutableArray<CellCoord> neighbors = GetRequiredNeighbors(analysis.Height, analysis.Width, new CellCoord(target.Row, target.Col));
            for (int i = 0; i < neighbors.Length; i++)
            {
                CellInfo cell = analysis.Cells[neighbors[i].Row, neighbors[i].Col];
                if (cell.Kind != CellKind.Empty)
                {
                    blocked++;
                }
            }

            return blocked;
        }

        private static int EstimateRouteDistanceToTarget(AnalyzedLevel analysis, TargetJson target)
        {
            Queue<(CellCoord Coord, int Distance)> frontier = new Queue<(CellCoord Coord, int Distance)>();
            HashSet<CellCoord> visited = new HashSet<CellCoord>();

            for (int col = 0; col < analysis.Width; col++)
            {
                CellCoord coord = new CellCoord(0, col);
                if (!analysis.IsPassable(coord))
                {
                    continue;
                }

                frontier.Enqueue((coord, 0));
                visited.Add(coord);
            }

            ImmutableArray<CellCoord> targetZone = GetRequiredNeighbors(analysis.Height, analysis.Width, new CellCoord(target.Row, target.Col));

            while (frontier.Count > 0)
            {
                (CellCoord current, int distance) = frontier.Dequeue();
                if (Contains(targetZone, current))
                {
                    return distance;
                }

                ImmutableArray<CellCoord> neighbors = GetRequiredNeighbors(analysis.Height, analysis.Width, current);
                for (int i = 0; i < neighbors.Length; i++)
                {
                    CellCoord neighbor = neighbors[i];
                    if (visited.Contains(neighbor) || !analysis.IsPassable(neighbor))
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    frontier.Enqueue((neighbor, distance + 1));
                }
            }

            return analysis.Height + analysis.Width;
        }

        private static bool Contains(ImmutableArray<CellCoord> coords, CellCoord candidate)
        {
            for (int i = 0; i < coords.Length; i++)
            {
                if (coords[i] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFloodedRow(LevelJson level, int row)
        {
            return row >= level.Board.Height - level.InitialFloodedRows;
        }

        private static bool HasBlockingErrors(List<ValidationError> errors)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i].Severity == ValidationSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static AnalyzedLevel Analyze(LevelJson level)
        {
            CellInfo[,] cells = new CellInfo[level.Board.Height, level.Board.Width];
            for (int row = 0; row < level.Board.Height; row++)
            {
                for (int col = 0; col < level.Board.Width; col++)
                {
                    TryParseCell(level.Board.Tiles[row][col], out CellInfo cell);
                    cells[row, col] = cell;
                }
            }

            return new AnalyzedLevel(level.Board.Height, level.Board.Width, level.InitialFloodedRows, cells);
        }

        private static bool TryParseCell(string code, out CellInfo cell)
        {
            if (code == ".")
            {
                cell = new CellInfo(CellKind.Empty, null, null, null, Hp: 0);
                return true;
            }

            if (TryParseDebris(code, out DebrisType debrisType))
            {
                cell = new CellInfo(CellKind.Debris, debrisType, null, null, Hp: 0);
                return true;
            }

            if (code == "CR")
            {
                cell = new CellInfo(CellKind.Crate, null, null, null, Hp: 1);
                return true;
            }

            if (code == "CX")
            {
                cell = new CellInfo(CellKind.Crate, null, null, null, Hp: 2);
                return true;
            }

            if (code == "V")
            {
                cell = new CellInfo(CellKind.Vine, null, null, null, Hp: 1);
                return true;
            }

            if (code.Length == 2 && code[0] == 'I' && TryParseDebris(code[1].ToString(), out DebrisType hiddenType))
            {
                cell = new CellInfo(CellKind.Ice, null, hiddenType, null, Hp: 1);
                return true;
            }

            if (code.Length >= 2 && code[0] == 'T')
            {
                cell = new CellInfo(CellKind.Target, null, null, code[1..], Hp: 0);
                return true;
            }

            cell = default;
            return false;
        }

        private static bool TryParseDebris(string code, out DebrisType debrisType)
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

        private static bool Assign(DebrisType debrisTypeValue, out DebrisType debrisType)
        {
            debrisType = debrisTypeValue;
            return true;
        }

        private static bool IsInBounds(int height, int width, int row, int col)
        {
            return row >= 0 && row < height && col >= 0 && col < width;
        }

        private static ImmutableArray<CellCoord> GetRequiredNeighbors(int height, int width, CellCoord coord)
        {
            ImmutableArray<CellCoord>.Builder neighbors = ImmutableArray.CreateBuilder<CellCoord>(4);
            TryAdd(height, width, coord.Row - 1, coord.Col, neighbors);
            TryAdd(height, width, coord.Row, coord.Col + 1, neighbors);
            TryAdd(height, width, coord.Row + 1, coord.Col, neighbors);
            TryAdd(height, width, coord.Row, coord.Col - 1, neighbors);
            return neighbors.ToImmutable();
        }

        private static void TryAdd(int height, int width, int row, int col, ImmutableArray<CellCoord>.Builder neighbors)
        {
            if (IsInBounds(height, width, row, col))
            {
                neighbors.Add(new CellCoord(row, col));
            }
        }

        private readonly record struct CellCoord(int Row, int Col);

        private enum CellKind
        {
            Empty,
            Debris,
            Crate,
            Ice,
            Vine,
            Target,
        }

        private readonly record struct CellInfo(
            CellKind Kind,
            DebrisType? DebrisType,
            DebrisType? HiddenDebrisType,
            string? TargetId,
            int Hp);

        private sealed class AnalyzedLevel
        {
            public AnalyzedLevel(int height, int width, int floodedRows, CellInfo[,] cells)
            {
                Height = height;
                Width = width;
                FloodedRows = floodedRows;
                Cells = cells;
                TargetTiles = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            public int Height { get; }

            public int Width { get; }

            public int FloodedRows { get; }

            public CellInfo[,] Cells { get; }

            public Dictionary<string, string> TargetTiles { get; }

            public bool IsFlooded(CellCoord coord)
            {
                return coord.Row >= Height - FloodedRows;
            }

            public bool IsPlayable(CellCoord coord)
            {
                if (IsFlooded(coord))
                {
                    return false;
                }

                return Cells[coord.Row, coord.Col].Kind switch
                {
                    CellKind.Empty => true,
                    CellKind.Debris => true,
                    CellKind.Crate => true,
                    CellKind.Ice => true,
                    CellKind.Vine => true,
                    CellKind.Target => true,
                    _ => false,
                };
            }

            public bool IsPassable(CellCoord coord)
            {
                if (IsFlooded(coord))
                {
                    return false;
                }

                return Cells[coord.Row, coord.Col].Kind switch
                {
                    CellKind.Empty => true,
                    CellKind.Debris => true,
                    CellKind.Crate => true,
                    CellKind.Ice => true,
                    CellKind.Vine => true,
                    CellKind.Target => true,
                    _ => false,
                };
            }

            public bool IsTopRegion(CellCoord coord)
            {
                if (coord.Row != 0 || IsFlooded(coord))
                {
                    return false;
                }

                return Cells[coord.Row, coord.Col].Kind is CellKind.Empty or CellKind.Debris;
            }
        }
    }
}
