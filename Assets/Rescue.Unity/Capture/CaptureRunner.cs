using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;
using Rescue.Content;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rescue.Unity.Capture
{
    public sealed class CaptureRunner : MonoBehaviour
    {
        private const string CaptureArgument = "-capture-l15";
        private const string PreviewArgument = "-capture-preview";
        private const string SolveResourcePath = "Levels/L15.solve";
        private const string CaptureReportFileName = "L15.capture.json";

        private static CaptureRunner? _instance;

        private bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!ShouldRunCapture())
            {
                return;
            }

            if (_instance is not null)
            {
                return;
            }

            GameObject host = new GameObject("CaptureRunner");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<CaptureRunner>();
        }

        private void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;

            try
            {
                CaptureSolveJson solve = LoadSolve();
                if (HasArgument(PreviewArgument))
                {
                    Debug.Log($"[CaptureRunner] Preview {solve.LevelId} seed {solve.Seed}: {FormatActions(solve.Actions)}");
                }

                CaptureRunResult result = RunSolve(solve);
                WriteCaptureReport(result);
                Debug.Log($"[CaptureRunner] SUCCESS {result.LevelId} seed {result.Seed}: {FormatActions(result.Actions)} -> {result.Outcome}");
                for (int i = 0; i < result.StepEvents.Length; i++)
                {
                    string eventSummary = string.Join(", ", result.StepEvents[i].EventTypes);
                    Debug.Log($"[CaptureRunner] Step {i + 1} events: {eventSummary}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaptureRunner] FAILURE {ex}");
                ExitWithFailure();
            }
        }

        private static bool ShouldRunCapture()
        {
            if (HasArgument(CaptureArgument))
            {
                return true;
            }

#if CAPTURE_BUILD
            if (Application.isMobilePlatform)
            {
                return true;
            }
#endif

            return false;
        }

        private static bool HasArgument(string argument)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static CaptureSolveJson LoadSolve()
        {
            TextAsset? asset = Resources.Load<TextAsset>(SolveResourcePath);
            if (asset is null)
            {
                throw new InvalidOperationException($"Capture solve asset '{SolveResourcePath}' was not found.");
            }

            CaptureSolveJson? solve = JsonSerializer.Deserialize<CaptureSolveJson>(
                asset.text,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });

            if (solve is null)
            {
                throw new InvalidOperationException("Capture solve JSON could not be deserialized.");
            }

            if (!string.Equals(solve.LevelId, "L15", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Expected capture solve for L15 but found '{solve.LevelId}'.");
            }

            return solve;
        }

        private static CaptureRunResult RunSolve(CaptureSolveJson solve)
        {
            GameState state = Loader.LoadLevel(solve.LevelId, solve.Seed);
            ImmutableArray<CaptureStepResult>.Builder stepResults = ImmutableArray.CreateBuilder<CaptureStepResult>(solve.Actions.Length);

            for (int i = 0; i < solve.Actions.Length; i++)
            {
                CaptureActionJson action = solve.Actions[i];
                TileCoord tappedCoord = new TileCoord(action.Row, action.Col);
                ActionResult result = Pipeline.RunAction(state, new ActionInput(tappedCoord), new RunOptions(RecordSnapshot: false));

                for (int eventIndex = 0; eventIndex < result.Events.Length; eventIndex++)
                {
                    if (result.Events[eventIndex] is InvalidInput invalid)
                    {
                        throw new InvalidOperationException(
                            $"Capture solve diverged at step {i + 1} on {tappedCoord}: invalid input {invalid.Reason}.");
                    }
                }

                ImmutableArray<string>.Builder eventTypes = ImmutableArray.CreateBuilder<string>(result.Events.Length);
                for (int eventIndex = 0; eventIndex < result.Events.Length; eventIndex++)
                {
                    eventTypes.Add(result.Events[eventIndex].GetType().Name);
                }

                stepResults.Add(new CaptureStepResult(
                    StepIndex: i + 1,
                    Row: action.Row,
                    Col: action.Col,
                    Outcome: result.Outcome.ToString(),
                    EventTypes: eventTypes.ToImmutable()));

                state = result.State;
            }

            ActionOutcome finalOutcome = solve.ExpectedOutcome switch
            {
                "Win" => ActionOutcome.Win,
                "LossDockOverflow" => ActionOutcome.LossDockOverflow,
                "LossWaterOnTarget" => ActionOutcome.LossWaterOnTarget,
                _ => throw new InvalidOperationException($"Unsupported expected outcome '{solve.ExpectedOutcome}'."),
            };

            if (state.ExtractedTargetOrder.Length != state.Targets.Length)
            {
                throw new InvalidOperationException(
                    $"Capture solve did not extract every target. Extracted {state.ExtractedTargetOrder.Length}/{state.Targets.Length}.");
            }

            if (stepResults.Count == 0 || !string.Equals(stepResults[^1].Outcome, finalOutcome.ToString(), StringComparison.Ordinal))
            {
                string actualOutcome = stepResults.Count == 0 ? "None" : stepResults[^1].Outcome;
                throw new InvalidOperationException(
                    $"Capture solve ended with '{actualOutcome}' instead of '{finalOutcome}'.");
            }

            return new CaptureRunResult(
                solve.LevelId,
                solve.Seed,
                solve.Actions,
                stepResults.ToImmutable(),
                finalOutcome.ToString(),
                state.ExtractedTargetOrder);
        }

        private static void WriteCaptureReport(CaptureRunResult result)
        {
            string directory = Path.Combine(Application.persistentDataPath, "capture");
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, CaptureReportFileName);
            string json = JsonSerializer.Serialize(
                new CaptureReportJson(
                    result.LevelId,
                    result.Seed,
                    result.Outcome,
                    result.Actions,
                    result.StepEvents,
                    CopyExtractedTargetOrder(result.ExtractedTargetOrder)),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                });

            File.WriteAllText(path, json, Encoding.UTF8);
            Debug.Log($"[CaptureRunner] Wrote capture report to {path}");
        }

        private static string FormatActions(IReadOnlyList<CaptureActionJson> actions)
        {
            List<string> formatted = new List<string>(actions.Count);
            for (int i = 0; i < actions.Count; i++)
            {
                formatted.Add($"{actions[i].Row},{actions[i].Col}");
            }

            return string.Join(" -> ", formatted);
        }

        private static string[] CopyExtractedTargetOrder(ImmutableArray<string> extractedTargetOrder)
        {
            string[] copy = new string[extractedTargetOrder.Length];
            for (int i = 0; i < extractedTargetOrder.Length; i++)
            {
                copy[i] = extractedTargetOrder[i];
            }

            return copy;
        }

        private static void ExitWithFailure()
        {
#if UNITY_EDITOR
            EditorApplication.Exit(1);
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            Application.Quit(1);
            Environment.Exit(1);
#else
            Application.Quit();
#endif
        }

        private sealed record CaptureSolveJson(
            string LevelId,
            int Seed,
            int AlternateSeed,
            string ExpectedOutcome,
            bool ExpectAlternateSeedDivergence,
            CaptureActionJson[] Actions);

        private sealed record CaptureActionJson(int Row, int Col);

        private sealed record CaptureStepResult(
            int StepIndex,
            int Row,
            int Col,
            string Outcome,
            ImmutableArray<string> EventTypes);

        private sealed record CaptureRunResult(
            string LevelId,
            int Seed,
            IReadOnlyList<CaptureActionJson> Actions,
            ImmutableArray<CaptureStepResult> StepEvents,
            string Outcome,
            ImmutableArray<string> ExtractedTargetOrder);

        private sealed record CaptureReportJson(
            string LevelId,
            int Seed,
            string Outcome,
            IReadOnlyList<CaptureActionJson> Actions,
            ImmutableArray<CaptureStepResult> Steps,
            string[] ExtractedTargetOrder);
    }
}
