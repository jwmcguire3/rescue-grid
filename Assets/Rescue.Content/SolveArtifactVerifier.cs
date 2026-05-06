using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rescue.Core.Pipeline;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.Content
{
    public static class SolveArtifactVerifier
    {
        public static SolveArtifactVerificationResult VerifySolvePath(string path, string? repoRoot = null)
        {
            if (!File.Exists(path))
            {
                return SolveArtifactVerificationResult.MissingResult(Path.GetFileName(path), $"Solve file was not found at '{path}'.");
            }

            try
            {
                string json = File.ReadAllText(path);
                SolveScriptJson solve = JsonSerializer.Deserialize<SolveScriptJson>(json)
                    ?? throw new InvalidOperationException($"Could not deserialize solve file '{path}'.");

                LevelJson level = LoadLevel(solve.LevelId, repoRoot);
                ActionOutcome outcome = ReplayOutcome(level, solve.Seed, solve.Actions);
                bool passed = string.Equals(outcome.ToString(), solve.ExpectedOutcome, StringComparison.Ordinal);
                string? failure = passed ? null : $"expected {solve.ExpectedOutcome}, got {outcome}";

                if (passed && solve.ExpectAlternateSeedDivergence)
                {
                    ImmutableArray<TileCoord> actions = solve.Actions.Select(static action => new TileCoord(action.Row, action.Col)).ToImmutableArray();
                    ImmutableArray<string> defaultTrajectory = Replay(level, solve.Seed, actions);
                    ImmutableArray<string> alternateTrajectory = Replay(level, solve.AlternateSeed, actions);
                    bool diverged = !defaultTrajectory.SequenceEqual(alternateTrajectory, StringComparer.Ordinal);
                    if (!diverged)
                    {
                        passed = false;
                        failure = $"alternate seed {solve.AlternateSeed} divergence expected True, got False";
                    }
                }

                return new SolveArtifactVerificationResult(
                    Path.GetFileName(path),
                    solve.LevelId,
                    solve.ExpectedOutcome,
                    outcome.ToString(),
                    passed,
                    Missing: false,
                    failure);
            }
            catch (Exception ex)
            {
                return SolveArtifactVerificationResult.Fail(Path.GetFileName(path), "<unknown>", "NotRun", ex.Message);
            }
        }

        public static SolveArtifactVerificationResult VerifyGoldenPath(string path, string? repoRoot = null)
        {
            if (!File.Exists(path))
            {
                return SolveArtifactVerificationResult.MissingResult(Path.GetFileName(path), $"Golden path file was not found at '{path}'.");
            }

            try
            {
                string json = File.ReadAllText(path);
                GoldenPathJson golden = DeserializeGoldenPath(json, path);
                string label = string.IsNullOrWhiteSpace(golden.LevelId) ? Path.GetFileNameWithoutExtension(path) : golden.LevelId;

                string? schemaFailure = ValidateGoldenPath(golden);
                if (schemaFailure is not null)
                {
                    return SolveArtifactVerificationResult.Fail(label, golden.ExpectedOutcome ?? "<missing>", "NotRun", schemaFailure);
                }

                if (golden.Actions.Length > golden.MaxActions)
                {
                    return SolveArtifactVerificationResult.Fail(
                        golden.LevelId,
                        golden.ExpectedOutcome,
                        "NotRun",
                        $"actions used {golden.Actions.Length} exceeds maxActions {golden.MaxActions}");
                }

                LevelJson level = LoadLevel(golden.LevelId, repoRoot);
                GameState state = Loader.LoadLevel(level, golden.Seed);
                ActionOutcome outcome = ActionOutcome.Ok;
                List<string> eventNames = new List<string>();

                for (int i = 0; i < golden.Actions.Length; i++)
                {
                    GoldenActionJson action = golden.Actions[i];
                    TileCoord coord = new TileCoord(action.Row, action.Col);
                    ActionResult result = Pipeline.RunAction(state, new ActionInput(coord), new RunOptions(RecordSnapshot: false));

                    for (int eventIndex = 0; eventIndex < result.Events.Length; eventIndex++)
                    {
                        ActionEvent actionEvent = result.Events[eventIndex];
                        eventNames.Add(actionEvent.GetType().Name);
                        if (actionEvent is InvalidInput)
                        {
                            return SolveArtifactVerificationResult.Fail(
                                golden.LevelId,
                                golden.ExpectedOutcome,
                                result.Outcome.ToString(),
                                $"step {i + 1} invalid input at {action.Row},{action.Col}");
                        }
                    }

                    outcome = result.Outcome;
                    state = result.State;

                    if (outcome != ActionOutcome.Ok && i < golden.Actions.Length - 1)
                    {
                        return SolveArtifactVerificationResult.Fail(
                            golden.LevelId,
                            golden.ExpectedOutcome,
                            outcome.ToString(),
                            $"terminal outcome {outcome} occurred at step {i + 1} before final golden action");
                    }
                }

                if (!string.Equals(outcome.ToString(), golden.ExpectedOutcome, StringComparison.Ordinal))
                {
                    return SolveArtifactVerificationResult.Fail(
                        golden.LevelId,
                        golden.ExpectedOutcome,
                        outcome.ToString(),
                        "final outcome mismatch");
                }

                if (golden.ExpectedEventsInOrder is { Length: > 0 }
                    && !ContainsEventsInOrder(eventNames, golden.ExpectedEventsInOrder, out string? missingEvent))
                {
                    return SolveArtifactVerificationResult.Fail(
                        golden.LevelId,
                        golden.ExpectedOutcome,
                        outcome.ToString(),
                        $"expected event '{missingEvent}' was not found in order");
                }

                if (golden.ExpectedExtractionOrder is { Length: > 0 }
                    && !state.ExtractedTargetOrder.SequenceEqual(golden.ExpectedExtractionOrder, StringComparer.Ordinal))
                {
                    return SolveArtifactVerificationResult.Fail(
                        golden.LevelId,
                        golden.ExpectedOutcome,
                        outcome.ToString(),
                        $"expected extraction order [{string.Join(",", golden.ExpectedExtractionOrder)}], got [{string.Join(",", state.ExtractedTargetOrder)}]");
                }

                return SolveArtifactVerificationResult.Pass(golden.LevelId, golden.ExpectedOutcome, outcome.ToString());
            }
            catch (Exception ex)
            {
                return SolveArtifactVerificationResult.Fail(Path.GetFileName(path), "<unknown>", "NotRun", ex.Message);
            }
        }

        public static string ResolveLevelPath(string levelId, string? repoRoot = null)
        {
            string root = repoRoot ?? FindRepoRoot(Directory.GetCurrentDirectory())
                ?? throw new FileNotFoundException("Could not locate repository root containing Assets/StreamingAssets/Levels.");
            return Path.Combine(root, "Assets", "StreamingAssets", "Levels", levelId + ".json");
        }

        public static string? FindRepoRoot(params string?[] startPaths)
        {
            for (int i = 0; i < startPaths.Length; i++)
            {
                string? startPath = startPaths[i];
                if (string.IsNullOrWhiteSpace(startPath))
                {
                    continue;
                }

                DirectoryInfo? directory = File.Exists(startPath)
                    ? new FileInfo(startPath).Directory
                    : new DirectoryInfo(startPath);
                while (directory is not null)
                {
                    string levelsPath = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "Levels");
                    string resourcesPath = Path.Combine(directory.FullName, "Assets", "Resources", "Levels");
                    if (Directory.Exists(levelsPath) && Directory.Exists(resourcesPath))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            return null;
        }

        private static GoldenPathJson DeserializeGoldenPath(string json, string path)
        {
            ValidateRequiredGoldenProperties(json, path);
            return JsonSerializer.Deserialize<GoldenPathJson>(json)
                ?? throw new InvalidOperationException($"Could not deserialize golden path file '{path}'.");
        }

        private static void ValidateRequiredGoldenProperties(string json, string path)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            RequireProperty(root, "levelId", JsonValueKind.String, path);
            RequireProperty(root, "seed", JsonValueKind.Number, path);
            RequireProperty(root, "pathType", JsonValueKind.String, path);
            RequireProperty(root, "expectedOutcome", JsonValueKind.String, path);
            RequireProperty(root, "maxActions", JsonValueKind.Number, path);
            JsonElement actions = RequireProperty(root, "actions", JsonValueKind.Array, path);
            for (int i = 0; i < actions.GetArrayLength(); i++)
            {
                JsonElement action = actions[i];
                RequireProperty(action, "row", JsonValueKind.Number, path);
                RequireProperty(action, "col", JsonValueKind.Number, path);
                RequireProperty(action, "intent", JsonValueKind.String, path);
            }
        }

        private static JsonElement RequireProperty(JsonElement root, string propertyName, JsonValueKind expectedKind, string path)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement property))
            {
                throw new InvalidOperationException($"Golden path file '{path}' is missing required property '{propertyName}'.");
            }

            if (property.ValueKind != expectedKind)
            {
                throw new InvalidOperationException($"Golden path file '{path}' property '{propertyName}' must be {expectedKind}.");
            }

            return property;
        }

        private static string? ValidateGoldenPath(GoldenPathJson golden)
        {
            if (string.IsNullOrWhiteSpace(golden.LevelId))
            {
                return "levelId is required";
            }

            if (!string.Equals(golden.PathType, "golden", StringComparison.Ordinal))
            {
                return "pathType must equal 'golden'";
            }

            if (string.IsNullOrWhiteSpace(golden.ExpectedOutcome))
            {
                return "expectedOutcome is required";
            }

            if (golden.MaxActions <= 0)
            {
                return "maxActions must be positive";
            }

            if (golden.Actions.Length == 0)
            {
                return "actions must be non-empty";
            }

            for (int i = 0; i < golden.Actions.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(golden.Actions[i].Intent))
                {
                    return $"action {i + 1} intent is required";
                }
            }

            return null;
        }

        private static bool ContainsEventsInOrder(List<string> actualEvents, string[] expectedEvents, out string? missingEvent)
        {
            int startIndex = 0;
            for (int expectedIndex = 0; expectedIndex < expectedEvents.Length; expectedIndex++)
            {
                string expectedEvent = expectedEvents[expectedIndex];
                bool found = false;
                for (int actualIndex = startIndex; actualIndex < actualEvents.Count; actualIndex++)
                {
                    if (!string.Equals(actualEvents[actualIndex], expectedEvent, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    startIndex = actualIndex + 1;
                    found = true;
                    break;
                }

                if (!found)
                {
                    missingEvent = expectedEvent;
                    return false;
                }
            }

            missingEvent = null;
            return true;
        }

        private static LevelJson LoadLevel(string levelId, string? repoRoot)
        {
            string path = ResolveLevelPath(levelId, repoRoot);
            string json = File.ReadAllText(path);
            return ContentJson.DeserializeLevel(json);
        }

        private static ActionOutcome ReplayOutcome(LevelJson level, int seed, SolveActionJson[] actions)
        {
            GameState state = Loader.LoadLevel(level, seed);
            ActionOutcome outcome = ActionOutcome.Ok;
            for (int i = 0; i < actions.Length; i++)
            {
                SolveActionJson action = actions[i];
                ActionResult result = Pipeline.RunAction(state, new ActionInput(new TileCoord(action.Row, action.Col)), new RunOptions(RecordSnapshot: false));
                outcome = result.Outcome;
                state = result.State;

                if (outcome != ActionOutcome.Ok)
                {
                    return outcome;
                }
            }

            return outcome;
        }

        private static ImmutableArray<string> Replay(LevelJson level, int seed, IReadOnlyList<TileCoord> actions)
        {
            ImmutableArray<string>.Builder frames = ImmutableArray.CreateBuilder<string>(actions.Count + 1);
            GameState state = Loader.LoadLevel(level, seed);
            frames.Add(Fingerprint(state));

            for (int i = 0; i < actions.Count; i++)
            {
                ActionResult result = Pipeline.RunAction(state, new ActionInput(actions[i]), new RunOptions(RecordSnapshot: false));
                state = result.State;
                frames.Add(Fingerprint(state) + "|" + result.Outcome);
            }

            return frames.ToImmutable();
        }

        private static string Fingerprint(GameState state)
        {
            List<string> rows = new List<string>(state.Board.Height);
            for (int row = 0; row < state.Board.Height; row++)
            {
                List<string> tiles = new List<string>(state.Board.Width);
                for (int col = 0; col < state.Board.Width; col++)
                {
                    tiles.Add(TileCode(BoardHelpers.GetTile(state.Board, new TileCoord(row, col))));
                }

                rows.Add(string.Join(",", tiles));
            }

            List<string> dock = new List<string>(state.Dock.Slots.Length);
            for (int i = 0; i < state.Dock.Slots.Length; i++)
            {
                dock.Add(state.Dock.Slots[i]?.ToString() ?? ".");
            }

            List<string> targets = new List<string>(state.Targets.Length);
            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                targets.Add($"{target.TargetId}@{target.Coord.Row},{target.Coord.Col}:{target.Readiness}");
            }

            return string.Join("|", new[]
            {
                string.Join("/", rows),
                string.Join(",", dock),
                state.Water.FloodedRows.ToString(),
                state.Water.ActionsUntilRise.ToString(),
                state.Water.PauseUntilFirstAction.ToString(),
                state.Vine.ActionsSinceLastClear.ToString(),
                state.Vine.PriorityCursor.ToString(),
                state.Vine.PendingGrowthTile?.ToString() ?? "none",
                string.Join(";", targets),
                string.Join(",", state.ExtractedTargetOrder),
                state.RngState.S0.ToString(),
                state.RngState.S1.ToString(),
                state.ActionCount.ToString(),
                state.DockJamUsed.ToString(),
                state.DockJamActive.ToString(),
                state.Frozen.ToString(),
            });
        }

        private static string TileCode(Tile tile)
        {
            return tile switch
            {
                EmptyTile => ".",
                FloodedTile => "~",
                DebrisTile debris => debris.Type.ToString(),
                BlockerTile blocker when blocker.Type == BlockerType.Crate => blocker.Hp > 1 ? "CX" : "CR",
                BlockerTile blocker when blocker.Type == BlockerType.Vine => "V",
                BlockerTile blocker when blocker.Type == BlockerType.Ice && blocker.Hidden is not null => "I" + blocker.Hidden.Type,
                BlockerTile blocker when blocker.Type == BlockerType.Ice => "I?",
                TargetTile target => "T" + target.TargetId + (target.Extracted ? "!" : string.Empty),
                _ => tile.GetType().Name,
            };
        }
    }

    public sealed record SolveArtifactVerificationResult(
        string Label,
        string LevelId,
        string ExpectedOutcome,
        string ActualOutcome,
        bool Passed,
        bool Missing,
        string? Failure)
    {
        public bool Failed => !Passed && !Missing;

        public static SolveArtifactVerificationResult Pass(string levelId, string expectedOutcome, string actualOutcome)
        {
            return new SolveArtifactVerificationResult(levelId, levelId, expectedOutcome, actualOutcome, Passed: true, Missing: false, Failure: null);
        }

        public static SolveArtifactVerificationResult Fail(string label, string expectedOutcome, string actualOutcome, string failure)
        {
            return new SolveArtifactVerificationResult(label, label, expectedOutcome, actualOutcome, Passed: false, Missing: false, failure);
        }

        public static SolveArtifactVerificationResult MissingResult(string label, string message)
        {
            return new SolveArtifactVerificationResult(label, label, "<missing>", "NotRun", Passed: false, Missing: true, message);
        }
    }

    public sealed record SolveScriptJson(
        string LevelId,
        int Seed,
        int AlternateSeed,
        string ExpectedOutcome,
        bool ExpectAlternateSeedDivergence,
        SolveActionJson[] Actions);

    public sealed record SolveActionJson(int Row, int Col);

    public sealed record GoldenPathJson(
        [property: JsonPropertyName("levelId")] string LevelId,
        [property: JsonPropertyName("seed")] int Seed,
        [property: JsonPropertyName("pathType")] string PathType,
        [property: JsonPropertyName("expectedOutcome")] string ExpectedOutcome,
        [property: JsonPropertyName("maxActions")] int MaxActions,
        [property: JsonPropertyName("actions")] GoldenActionJson[] Actions,
        [property: JsonPropertyName("expectedEventsInOrder")] string[]? ExpectedEventsInOrder,
        [property: JsonPropertyName("expectedExtractionOrder")] string[]? ExpectedExtractionOrder,
        [property: JsonPropertyName("notes")] string? Notes);

    public sealed record GoldenActionJson(
        [property: JsonPropertyName("row")] int Row,
        [property: JsonPropertyName("col")] int Col,
        [property: JsonPropertyName("intent")] string Intent);
}
