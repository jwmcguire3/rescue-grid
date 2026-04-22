using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Telemetry;

namespace Rescue.Replay
{
    public sealed record ReplayFrame(
        int FrameIndex,
        int ActionIndex,
        ActionInput? Input,
        GameState State,
        ActionOutcome? Outcome,
        ImmutableArray<ActionEvent> Events,
        bool? RngStateVerified,
        string? VerificationMessage = null);

    public sealed record ReplayResult(
        string SessionJsonlPath,
        string LevelId,
        int Seed,
        ImmutableArray<ITelemetryEvent> SessionEvents,
        ImmutableArray<ReplayFrame> Frames)
    {
        public ReplayFrame InitialFrame => Frames[0];

        public ReplayFrame FinalFrame => Frames[^1];

        public bool Verified
        {
            get
            {
                for (int i = 0; i < Frames.Length; i++)
                {
                    bool? verified = Frames[i].RngStateVerified;
                    if (verified.HasValue && !verified.Value)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    public static class ReplayRunner
    {
        private const int DefaultLossRetentionCap = 20;

        public static ReplayResult ReplaySession(string sessionJsonlPath)
        {
            return ReplaySession(sessionJsonlPath, DefaultLevelLoader);
        }

        public static ReplayResult ReplaySession(
            string sessionJsonlPath,
            Func<string, int, GameState> levelLoader)
        {
            if (string.IsNullOrWhiteSpace(sessionJsonlPath))
            {
                throw new ArgumentException("Session path is required.", nameof(sessionJsonlPath));
            }

            if (levelLoader is null)
            {
                throw new ArgumentNullException(nameof(levelLoader));
            }

            ImmutableArray<ITelemetryEvent> sessionEvents = LoadSessionEvents(sessionJsonlPath);
            LevelStartEvent levelStart = RequireLevelStart(sessionEvents);
            ImmutableArray<ActionTakenEvent> actions = GetActionEvents(sessionEvents);
            int seed = checked((int)levelStart.Seed);
            GameState currentState = levelLoader(levelStart.LevelId, seed);

            ImmutableArray<ReplayFrame>.Builder frames = ImmutableArray.CreateBuilder<ReplayFrame>(actions.Length + 1);
            frames.Add(new ReplayFrame(
                FrameIndex: 0,
                ActionIndex: 0,
                Input: null,
                State: currentState,
                Outcome: null,
                Events: ImmutableArray<ActionEvent>.Empty,
                RngStateVerified: null));

            for (int i = 0; i < actions.Length; i++)
            {
                ActionTakenEvent action = actions[i];
                ActionResult result = Pipeline.RunAction(currentState, new ActionInput(action.Input), new RunOptions(RecordSnapshot: false));
                bool verified = result.State.RngState == action.RngStateAfter;
                string? verificationMessage = verified
                    ? null
                    : $"Logged rngStateAfter {action.RngStateAfter} did not match replayed {result.State.RngState}.";

                frames.Add(new ReplayFrame(
                    FrameIndex: i + 1,
                    ActionIndex: action.ActionIndex,
                    Input: new ActionInput(action.Input),
                    State: result.State,
                    Outcome: result.Outcome,
                    Events: result.Events,
                    RngStateVerified: verified,
                    VerificationMessage: verificationMessage));

                currentState = result.State;
            }

            return new ReplayResult(
                sessionJsonlPath,
                levelStart.LevelId,
                seed,
                sessionEvents,
                frames.ToImmutable());
        }

        public static ImmutableArray<TrajectoryDiff> CompareTrajectories(ReplayResult a, ReplayResult b)
        {
            if (a is null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (b is null)
            {
                throw new ArgumentNullException(nameof(b));
            }

            if (!string.Equals(a.LevelId, b.LevelId, StringComparison.Ordinal))
            {
                return ImmutableArray.Create(new TrajectoryDiff(
                    TrajectoryDiffKind.LevelIdMismatch,
                    FrameIndex: 0,
                    Message: "Replay level ids differ.",
                    Expected: a.LevelId,
                    Actual: b.LevelId));
            }

            if (a.Seed != b.Seed)
            {
                return ImmutableArray.Create(new TrajectoryDiff(
                    TrajectoryDiffKind.SeedMismatch,
                    FrameIndex: 0,
                    Message: "Replay seeds differ.",
                    Expected: a.Seed.ToString(),
                    Actual: b.Seed.ToString()));
            }

            int sharedFrames = Math.Min(a.Frames.Length, b.Frames.Length);
            for (int i = 0; i < sharedFrames; i++)
            {
                ReplayFrame expected = a.Frames[i];
                ReplayFrame actual = b.Frames[i];

                if (expected.ActionIndex != actual.ActionIndex)
                {
                    return ImmutableArray.Create(new TrajectoryDiff(
                        TrajectoryDiffKind.ActionIndexMismatch,
                        i,
                        "Action indices diverged.",
                        expected.ActionIndex.ToString(),
                        actual.ActionIndex.ToString()));
                }

                if (expected.Input != actual.Input)
                {
                    return ImmutableArray.Create(new TrajectoryDiff(
                        TrajectoryDiffKind.InputMismatch,
                        i,
                        "Replay inputs diverged.",
                        expected.Input?.TappedCoord.ToString(),
                        actual.Input?.TappedCoord.ToString()));
                }

                if (expected.Outcome != actual.Outcome)
                {
                    return ImmutableArray.Create(new TrajectoryDiff(
                        TrajectoryDiffKind.OutcomeMismatch,
                        i,
                        "Replay outcomes diverged.",
                        expected.Outcome?.ToString(),
                        actual.Outcome?.ToString()));
                }

                string expectedFingerprint = TrajectoryFormatter.Fingerprint(expected.State);
                string actualFingerprint = TrajectoryFormatter.Fingerprint(actual.State);
                if (!string.Equals(expectedFingerprint, actualFingerprint, StringComparison.Ordinal))
                {
                    return ImmutableArray.Create(new TrajectoryDiff(
                        TrajectoryDiffKind.StateMismatch,
                        i,
                        "Replay states diverged.",
                        TrajectoryFormatter.SummarizeFrame(expected),
                        TrajectoryFormatter.SummarizeFrame(actual)));
                }

                if (expected.RngStateVerified != actual.RngStateVerified)
                {
                    return ImmutableArray.Create(new TrajectoryDiff(
                        TrajectoryDiffKind.RngVerificationMismatch,
                        i,
                        "Replay RNG verification diverged.",
                        expected.RngStateVerified?.ToString(),
                        actual.RngStateVerified?.ToString()));
                }
            }

            if (a.Frames.Length != b.Frames.Length)
            {
                return ImmutableArray.Create(new TrajectoryDiff(
                    TrajectoryDiffKind.FrameCountMismatch,
                    sharedFrames,
                    "Replay frame counts differ.",
                    a.Frames.Length.ToString(),
                    b.Frames.Length.ToString()));
            }

            return ImmutableArray<TrajectoryDiff>.Empty;
        }

        public static string CaptureLossSession(
            string sessionJsonlPath,
            string levelId,
            int seed,
            string lossesDirectoryPath,
            DateTimeOffset timestamp,
            int retentionCap = DefaultLossRetentionCap)
        {
            if (string.IsNullOrWhiteSpace(sessionJsonlPath))
            {
                throw new ArgumentException("Session path is required.", nameof(sessionJsonlPath));
            }

            if (string.IsNullOrWhiteSpace(levelId))
            {
                throw new ArgumentException("Level id is required.", nameof(levelId));
            }

            if (string.IsNullOrWhiteSpace(lossesDirectoryPath))
            {
                throw new ArgumentException("Losses directory is required.", nameof(lossesDirectoryPath));
            }

            if (!File.Exists(sessionJsonlPath))
            {
                throw new FileNotFoundException("Session log was not found.", sessionJsonlPath);
            }

            Directory.CreateDirectory(lossesDirectoryPath);

            string safeLevelId = SanitizeFileComponent(levelId);
            string fileName = $"{safeLevelId}_{seed}_{timestamp:yyyyMMdd_HHmmss_fff}.jsonl";
            string destinationPath = Path.Combine(lossesDirectoryPath, fileName);
            File.Copy(sessionJsonlPath, destinationPath, overwrite: true);

            if (retentionCap > 0)
            {
                EnforceRetention(lossesDirectoryPath, retentionCap);
            }

            return destinationPath;
        }

        private static void EnforceRetention(string lossesDirectoryPath, int retentionCap)
        {
            string[] files = Directory.GetFiles(lossesDirectoryPath, "*.jsonl", SearchOption.TopDirectoryOnly);
            Array.Sort(files, (left, right) =>
                File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left)));

            for (int i = retentionCap; i < files.Length; i++)
            {
                File.Delete(files[i]);
            }
        }

        private static ImmutableArray<ITelemetryEvent> LoadSessionEvents(string sessionJsonlPath)
        {
            if (!File.Exists(sessionJsonlPath))
            {
                throw new FileNotFoundException("Session log was not found.", sessionJsonlPath);
            }

            ImmutableArray<ITelemetryEvent>.Builder events = ImmutableArray.CreateBuilder<ITelemetryEvent>();
            string[] lines = File.ReadAllLines(sessionJsonlPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                ITelemetryEvent? telemetryEvent = JsonSerializer.Deserialize<ITelemetryEvent>(
                    line,
                    TelemetryJsonConverter.OuterOptions);
                if (telemetryEvent is not null)
                {
                    events.Add(telemetryEvent);
                }
            }

            return events.ToImmutable();
        }

        private static LevelStartEvent RequireLevelStart(ImmutableArray<ITelemetryEvent> sessionEvents)
        {
            for (int i = 0; i < sessionEvents.Length; i++)
            {
                if (sessionEvents[i] is LevelStartEvent levelStart)
                {
                    return levelStart;
                }
            }

            throw new InvalidOperationException("Replay session did not include a level_start event.");
        }

        private static ImmutableArray<ActionTakenEvent> GetActionEvents(ImmutableArray<ITelemetryEvent> sessionEvents)
        {
            ImmutableArray<ActionTakenEvent>.Builder actions = ImmutableArray.CreateBuilder<ActionTakenEvent>();
            for (int i = 0; i < sessionEvents.Length; i++)
            {
                if (sessionEvents[i] is ActionTakenEvent action)
                {
                    actions.Add(action);
                }
            }

            return actions.ToImmutable();
        }

        private static GameState DefaultLevelLoader(string levelId, int seed)
        {
            string levelPath = ResolveLevelPath(levelId);
            string json = File.ReadAllText(levelPath);
            LevelJson level = ContentJson.DeserializeLevel(json);
            return Loader.LoadLevel(level, seed);
        }

        private static string ResolveLevelPath(string levelId)
        {
            string relativePath = Path.Combine("Assets", "StreamingAssets", "Levels", levelId + ".json");

            string[] roots =
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
            };

            for (int i = 0; i < roots.Length; i++)
            {
                string? candidate = FindAncestorCandidate(roots[i], relativePath);
                if (candidate is not null)
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException($"Could not resolve level '{levelId}' beneath Assets/StreamingAssets/Levels.");
        }

        private static string? FindAncestorCandidate(string startPath, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                return null;
            }

            DirectoryInfo? current = new DirectoryInfo(Path.GetFullPath(startPath));
            while (current is not null)
            {
                string candidate = Path.Combine(current.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            return null;
        }

        private static string SanitizeFileComponent(string value)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] buffer = value.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (Array.IndexOf(invalidChars, buffer[i]) >= 0)
                {
                    buffer[i] = '_';
                }
            }

            return new string(buffer);
        }
    }
}
