using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.Rules;
using Rescue.Core.State;
using Rescue.Core.Undo;
using UnityEngine;

namespace Rescue.PlayMode.Tests.Smoke
{
    internal sealed record SolveActionJson(int Row, int Col);

    internal sealed record SolveScriptJson(
        string LevelId,
        int Seed,
        int AlternateSeed,
        string ExpectedOutcome,
        bool ExpectAlternateSeedDivergence,
        SolveActionJson[] Actions);

    internal sealed record ScriptFrame(
        int StepIndex,
        TileCoord? Action,
        GameState State,
        ActionOutcome? Outcome,
        ImmutableArray<ActionEvent> Events,
        string Fingerprint);

    internal sealed record ScriptRun(
        SolveScriptJson Solve,
        ImmutableArray<ScriptFrame> Frames)
    {
        public ScriptFrame InitialFrame => Frames[0];

        public ScriptFrame FinalFrame => Frames[Frames.Length - 1];
    }

    internal static class SmokeTestHarness
    {
        private static readonly string[] LevelIds = Enumerable.Range(1, 15)
            .Select(static index => "L" + index.ToString("00"))
            .ToArray();

        private static readonly object JsonGate = new object();
        private static Assembly? _jsonAssembly;
        private static object? _jsonOptions;
        private static MethodInfo? _deserializeMethod;

        public static IReadOnlyList<string> MainLevelIds => LevelIds;

        public static SolveScriptJson LoadSolve(string levelId)
        {
            TextAsset asset = Resources.Load<TextAsset>($"Levels/{levelId}.solve");
            Assert.That(asset, Is.Not.Null, $"Missing solve asset for {levelId}.");
            return DeserializeSolve(asset!.text);
        }

        public static GameState LoadState(string levelId, int seed)
        {
            return Loader.LoadLevel(levelId, seed);
        }

        public static System.Collections.IEnumerator RunSolveStrict(
            SolveScriptJson solve,
            List<ScriptFrame> frames,
            int? seedOverride = null)
        {
            yield return RunSolveInternal(solve, frames, seedOverride, allowInvalidInput: false);
        }

        public static System.Collections.IEnumerator RunSolveLoose(
            SolveScriptJson solve,
            List<ScriptFrame> frames,
            int seedOverride)
        {
            yield return RunSolveInternal(solve, frames, seedOverride, allowInvalidInput: true);
        }

        public static string Fingerprint(GameState state)
        {
            List<string> rows = new List<string>(state.Board.Height);
            for (int row = 0; row < state.Board.Height; row++)
            {
                List<string> cells = new List<string>(state.Board.Width);
                for (int col = 0; col < state.Board.Width; col++)
                {
                    cells.Add(TileCode(BoardHelpers.GetTile(state.Board, new TileCoord(row, col))));
                }

                rows.Add(string.Join(",", cells));
            }

            string dock = string.Join(",", state.Dock.Slots.Select(static slot => slot?.ToString() ?? "."));
            string targets = string.Join(";", state.Targets.Select(static target =>
                $"{target.TargetId}@{target.Coord.Row},{target.Coord.Col}:{target.Extracted}:{target.OneClearAway}"));
            string extracted = string.Join(",", state.ExtractedTargetOrder);

            return string.Join("|", new[]
            {
                string.Join("/", rows),
                dock,
                state.Water.FloodedRows.ToString(),
                state.Water.ActionsUntilRise.ToString(),
                state.Water.RiseInterval.ToString(),
                state.Water.PauseUntilFirstAction.ToString(),
                state.Vine.ActionsSinceLastClear.ToString(),
                state.Vine.GrowthThreshold.ToString(),
                state.Vine.PriorityCursor.ToString(),
                state.Vine.PendingGrowthTile?.ToString() ?? "none",
                targets,
                extracted,
                state.RngState.S0.ToString(),
                state.RngState.S1.ToString(),
                state.ActionCount.ToString(),
                state.UndoAvailable.ToString(),
                state.DockJamUsed.ToString(),
                state.DockJamActive.ToString(),
                state.Frozen.ToString(),
            });
        }

        public static void AssertInvariants(string levelId, GameState state)
        {
            Assert.That(state.Board.Width, Is.GreaterThan(0), $"{levelId} board width should stay positive.");
            Assert.That(state.Board.Height, Is.GreaterThan(0), $"{levelId} board height should stay positive.");
            Assert.That(state.Board.Tiles.Length, Is.EqualTo(state.Board.Height), $"{levelId} row count changed.");
            Assert.That(state.Dock.Slots.Length, Is.EqualTo(state.Dock.Size), $"{levelId} dock slot count drifted.");
            Assert.That(DockHelpers.Occupancy(state.Dock), Is.LessThanOrEqualTo(state.Dock.Size), $"{levelId} dock occupancy exceeded size.");
            Assert.That(state.Water.FloodedRows, Is.InRange(0, state.Board.Height), $"{levelId} flooded rows out of range.");
            Assert.That(state.ExtractedTargetOrder.Length, Is.EqualTo(state.Targets.Count(static target => target.Extracted)), $"{levelId} extracted target order drifted.");

            int floodStartRow = state.Board.Height - state.Water.FloodedRows;
            for (int row = 0; row < state.Board.Height; row++)
            {
                Assert.That(state.Board.Tiles[row].Length, Is.EqualTo(state.Board.Width), $"{levelId} board row {row} width drifted.");
                for (int col = 0; col < state.Board.Width; col++)
                {
                    Tile tile = BoardHelpers.GetTile(state.Board, new TileCoord(row, col));
                    if (row >= floodStartRow)
                    {
                        Assert.That(tile, Is.TypeOf<FloodedTile>(), $"{levelId} row {row} should be fully flooded.");
                    }
                    else
                    {
                        Assert.That(tile, Is.Not.TypeOf<FloodedTile>(), $"{levelId} dry row {row} contained a flooded tile.");
                    }
                }
            }

            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                Assert.That(BoardHelpers.InBounds(state.Board, target.Coord), Is.True, $"{levelId} target {target.TargetId} moved out of bounds.");
                if (target.Extracted)
                {
                    Assert.That(target.OneClearAway, Is.False, $"{levelId} extracted target {target.TargetId} should not remain one-clear-away.");
                    continue;
                }

                if (target.Coord.Row < floodStartRow)
                {
                    Assert.That(BoardHelpers.GetTile(state.Board, target.Coord), Is.TypeOf<TargetTile>(), $"{levelId} live target {target.TargetId} disappeared from the board.");
                }
            }
        }

        public static TileCoord FindAlternativeValidAction(GameState state, TileCoord excluded)
        {
            foreach (ActionInput candidate in EnumerateValidInputs(state))
            {
                if (candidate.TappedCoord != excluded)
                {
                    return candidate.TappedCoord;
                }
            }

            Assert.Fail($"Could not find a second branch action distinct from {excluded}.");
            return default;
        }

        public static ImmutableArray<ActionInput> EnumerateValidInputs(GameState state)
        {
            ImmutableArray<ActionInput>.Builder inputs = ImmutableArray.CreateBuilder<ActionInput>();
            for (int row = 0; row < state.Board.Height; row++)
            {
                for (int col = 0; col < state.Board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    ImmutableArray<TileCoord>? group = GroupOps.FindGroup(state.Board, coord);
                    if (group is null)
                    {
                        continue;
                    }

                    TileCoord canonical = CanonicalCoord(group.Value);
                    if (canonical == coord)
                    {
                        inputs.Add(new ActionInput(coord));
                    }
                }
            }

            return inputs.ToImmutable();
        }

        public static string[] ToFingerprints(IReadOnlyList<ScriptFrame> frames)
        {
            string[] values = new string[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                ScriptFrame frame = frames[i];
                values[i] = frame.Outcome.HasValue
                    ? frame.Fingerprint + "|" + frame.Outcome.Value
                    : frame.Fingerprint + "|initial";
            }

            return values;
        }

        public static ScriptFrame AssertTerminalOutcome(IReadOnlyList<ScriptFrame> frames, string expectedOutcome)
        {
            Assert.That(frames.Count, Is.GreaterThan(0));
            ScriptFrame finalFrame = frames[frames.Count - 1];
            Assert.That(finalFrame.Outcome.HasValue, Is.True, "Expected the script to reach a terminal action result.");
            Assert.That(finalFrame.Outcome!.Value.ToString(), Is.EqualTo(expectedOutcome));
            Assert.That(finalFrame.Outcome.Value, Is.Not.EqualTo(ActionOutcome.Ok));
            return finalFrame;
        }

        private static System.Collections.IEnumerator RunSolveInternal(
            SolveScriptJson solve,
            List<ScriptFrame> frames,
            int? seedOverride,
            bool allowInvalidInput)
        {
            int seed = seedOverride ?? solve.Seed;
            GameState state = LoadState(solve.LevelId, seed);
            AssertInvariants(solve.LevelId, state);
            frames.Add(new ScriptFrame(
                StepIndex: 0,
                Action: null,
                State: state,
                Outcome: null,
                Events: ImmutableArray<ActionEvent>.Empty,
                Fingerprint: Fingerprint(state)));

            yield return null;

            for (int i = 0; i < solve.Actions.Length; i++)
            {
                SolveActionJson action = solve.Actions[i];
                TileCoord tapped = new TileCoord(action.Row, action.Col);
                ActionResult result = Pipeline.RunAction(state, new ActionInput(tapped));

                if (!allowInvalidInput)
                {
                    Assert.That(result.Events, Has.None.TypeOf<InvalidInput>(), $"{solve.LevelId} action {i + 1} became invalid at {tapped}.");
                    Assert.That(result.State.ActionCount, Is.EqualTo(state.ActionCount + 1), $"{solve.LevelId} action {i + 1} did not increment action count.");
                }

                state = result.State;
                AssertInvariants(solve.LevelId, state);
                frames.Add(new ScriptFrame(
                    StepIndex: i + 1,
                    Action: tapped,
                    State: state,
                    Outcome: result.Outcome,
                    Events: result.Events,
                    Fingerprint: Fingerprint(state)));

                yield return null;
            }
        }

        private static TileCoord CanonicalCoord(ImmutableArray<TileCoord> group)
        {
            TileCoord best = group[0];
            for (int i = 1; i < group.Length; i++)
            {
                TileCoord current = group[i];
                if (current.Row < best.Row || (current.Row == best.Row && current.Col < best.Col))
                {
                    best = current;
                }
            }

            return best;
        }

        private static SolveScriptJson DeserializeSolve(string json)
        {
            EnsureJson();
            object? value = _deserializeMethod!.Invoke(null, new object?[] { json, typeof(SolveScriptJson), _jsonOptions });
            Assert.That(value, Is.TypeOf<SolveScriptJson>());
            return (SolveScriptJson)value!;
        }

        private static void EnsureJson()
        {
            if (_jsonAssembly is not null)
            {
                return;
            }

            lock (JsonGate)
            {
                if (_jsonAssembly is not null)
                {
                    return;
                }

                Assembly jsonAssembly = Assembly.Load(new AssemblyName("System.Text.Json"));
                Type serializerType = jsonAssembly.GetType("System.Text.Json.JsonSerializer", throwOnError: true)
                    ?? throw new InvalidOperationException("JsonSerializer type was not found.");
                Type optionsType = jsonAssembly.GetType("System.Text.Json.JsonSerializerOptions", throwOnError: true)
                    ?? throw new InvalidOperationException("JsonSerializerOptions type was not found.");
                object options = Activator.CreateInstance(optionsType)
                    ?? throw new InvalidOperationException("Could not create JsonSerializerOptions.");
                optionsType.GetProperty("PropertyNameCaseInsensitive")!.SetValue(options, true);

                _jsonAssembly = jsonAssembly;
                _jsonOptions = options;
                _deserializeMethod = serializerType.GetMethod(
                    "Deserialize",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(string), typeof(Type), optionsType },
                    modifiers: null)
                    ?? throw new MissingMethodException("Could not find JsonSerializer.Deserialize(string, Type, JsonSerializerOptions).");
            }
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
}
