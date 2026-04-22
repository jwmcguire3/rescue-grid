using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Rescue.Core.State;
using Rescue.Replay;

namespace ReplayTool
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                Options options = Options.Parse(args);
                ReplayResult replay = ReplayRunner.ReplaySession(options.SessionPath);

                if (options.ExportPath is not null)
                {
                    ExportReplay(replay, options.ExportPath);
                }

                if (options.StepIndex.HasValue)
                {
                    return PrintStep(replay, options.StepIndex.Value);
                }

                PrintSummary(replay, options.ExportPath);
                return replay.Verified ? 0 : 2;
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                PrintUsage();
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static int PrintStep(ReplayResult replay, int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= replay.Frames.Length)
            {
                Console.Error.WriteLine($"Step {stepIndex} is out of range. Valid steps: 0-{replay.Frames.Length - 1}.");
                return 1;
            }

            ReplayFrame frame = replay.Frames[stepIndex];
            Console.WriteLine(BuildFrameSummary(replay, frame));
            return frame.RngStateVerified is false ? 2 : 0;
        }

        private static void PrintSummary(ReplayResult replay, string? exportPath)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Replay session: {replay.SessionJsonlPath}");
            builder.AppendLine($"Level: {replay.LevelId}");
            builder.AppendLine($"Seed: {replay.Seed}");
            builder.AppendLine($"Frames: {replay.Frames.Length}");
            builder.AppendLine($"Verified: {replay.Verified}");

            ReplayFrame finalFrame = replay.FinalFrame;
            builder.AppendLine($"Final outcome: {finalFrame.Outcome?.ToString() ?? "initial"}");
            builder.AppendLine($"Final action index: {finalFrame.ActionIndex}");
            builder.AppendLine($"Final RNG: {finalFrame.State.RngState}");

            if (exportPath is not null)
            {
                builder.AppendLine($"Exported: {exportPath}");
            }

            if (!replay.Verified)
            {
                for (int i = 1; i < replay.Frames.Length; i++)
                {
                    ReplayFrame frame = replay.Frames[i];
                    if (frame.RngStateVerified is false)
                    {
                        builder.AppendLine($"First verification failure: step {frame.FrameIndex} -> {frame.VerificationMessage}");
                        break;
                    }
                }
            }

            Console.WriteLine(builder.ToString().TrimEnd());
        }

        private static string BuildFrameSummary(ReplayResult replay, ReplayFrame frame)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Replay session: {replay.SessionJsonlPath}");
            builder.AppendLine($"Frame: {frame.FrameIndex}/{replay.Frames.Length - 1}");
            builder.AppendLine($"Action index: {frame.ActionIndex}");
            builder.AppendLine($"Input: {FormatInput(frame)}");
            builder.AppendLine($"Outcome: {frame.Outcome?.ToString() ?? "initial"}");
            builder.AppendLine($"RNG verified: {frame.RngStateVerified?.ToString() ?? "n/a"}");
            if (!string.IsNullOrWhiteSpace(frame.VerificationMessage))
            {
                builder.AppendLine($"Verification: {frame.VerificationMessage}");
            }

            builder.AppendLine($"Dock: {FormatDock(frame.State.Dock)}");
            builder.AppendLine($"Water: flooded={frame.State.Water.FloodedRows}, untilRise={frame.State.Water.ActionsUntilRise}, interval={frame.State.Water.RiseInterval}, pause={frame.State.Water.PauseUntilFirstAction}");
            builder.AppendLine($"Vine: sinceClear={frame.State.Vine.ActionsSinceLastClear}, threshold={frame.State.Vine.GrowthThreshold}, pending={FormatCoord(frame.State.Vine.PendingGrowthTile)}");
            builder.AppendLine("Board:");
            string[] rows = FormatBoard(frame.State.Board);
            for (int i = 0; i < rows.Length; i++)
            {
                builder.AppendLine($"  {rows[i]}");
            }

            return builder.ToString().TrimEnd();
        }

        private static void ExportReplay(ReplayResult replay, string exportPath)
        {
            string? directory = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            ReplayExport export = new ReplayExport(
                replay.LevelId,
                replay.Seed,
                replay.Verified,
                BuildFrames(replay.Frames));

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
            File.WriteAllText(exportPath, JsonSerializer.Serialize(export, options));
        }

        private static FrameExport[] BuildFrames(System.Collections.Immutable.ImmutableArray<ReplayFrame> frames)
        {
            FrameExport[] exported = new FrameExport[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                ReplayFrame frame = frames[i];
                exported[i] = new FrameExport(
                    frame.FrameIndex,
                    frame.ActionIndex,
                    FormatInput(frame),
                    frame.Outcome?.ToString(),
                    frame.RngStateVerified,
                    frame.VerificationMessage,
                    FormatBoard(frame.State.Board),
                    FormatDock(frame.State.Dock),
                    frame.State.Water.FloodedRows,
                    frame.State.Water.ActionsUntilRise,
                    frame.State.Vine.ActionsSinceLastClear,
                    frame.State.RngState.S0,
                    frame.State.RngState.S1,
                    frame.State.ActionCount);
            }

            return exported;
        }

        private static string[] FormatBoard(Board board)
        {
            string[] rows = new string[board.Height];
            for (int row = 0; row < board.Height; row++)
            {
                StringBuilder builder = new StringBuilder();
                for (int col = 0; col < board.Width; col++)
                {
                    if (col > 0)
                    {
                        builder.Append(' ');
                    }

                    builder.Append(TileCode(BoardHelpers.GetTile(board, new TileCoord(row, col))));
                }

                rows[row] = builder.ToString();
            }

            return rows;
        }

        private static string FormatDock(Dock dock)
        {
            List<string> values = new List<string>(dock.Slots.Length);
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                values.Add(dock.Slots[i]?.ToString() ?? ".");
            }

            return string.Join(" ", values);
        }

        private static string FormatInput(ReplayFrame frame)
        {
            return frame.Input.HasValue
                ? $"{frame.Input.Value.TappedCoord.Row},{frame.Input.Value.TappedCoord.Col}"
                : "none";
        }

        private static string FormatCoord(TileCoord? coord)
        {
            return coord.HasValue ? $"{coord.Value.Row},{coord.Value.Col}" : "none";
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

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  scripts/replay.sh --session path/to/session.jsonl");
            Console.Error.WriteLine("  scripts/replay.sh --session path/to/session.jsonl --step 12");
            Console.Error.WriteLine("  scripts/replay.sh --session path/to/session.jsonl --export out.json");
        }

        private sealed record ReplayExport(
            string LevelId,
            int Seed,
            bool Verified,
            FrameExport[] Frames);

        private sealed record FrameExport(
            int FrameIndex,
            int ActionIndex,
            string Input,
            string? Outcome,
            bool? RngStateVerified,
            string? VerificationMessage,
            string[] Board,
            string Dock,
            int FloodedRows,
            int ActionsUntilRise,
            int VineActionsSinceClear,
            uint RngS0,
            uint RngS1,
            int GameActionCount);

        private sealed class Options
        {
            public string SessionPath { get; private init; } = string.Empty;

            public int? StepIndex { get; private init; }

            public string? ExportPath { get; private init; }

            public static Options Parse(string[] args)
            {
                string? sessionPath = null;
                int? stepIndex = null;
                string? exportPath = null;

                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    switch (arg)
                    {
                        case "--session":
                            sessionPath = RequireValue(args, ref i, "--session");
                            break;

                        case "--step":
                            stepIndex = int.Parse(RequireValue(args, ref i, "--step"));
                            break;

                        case "--export":
                            exportPath = RequireValue(args, ref i, "--export");
                            break;

                        default:
                            throw new ArgumentException($"Unknown argument: {arg}");
                    }
                }

                if (string.IsNullOrWhiteSpace(sessionPath))
                {
                    throw new ArgumentException("Missing required --session path.");
                }

                return new Options
                {
                    SessionPath = sessionPath,
                    StepIndex = stepIndex,
                    ExportPath = exportPath,
                };
            }

            private static string RequireValue(string[] args, ref int index, string argName)
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {argName}.");
                }

                index++;
                return args[index];
            }
        }
    }
}
