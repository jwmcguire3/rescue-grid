#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.IO;
using Rescue.Unity.Presentation;
using UnityEngine;

namespace Rescue.Unity.Diagnostics
{
    public sealed class AndroidWhiteoutCommandBridge : MonoBehaviour
    {
        private const string CommandFileName = "whiteout-command.txt";
        private const float PollIntervalSeconds = 0.25f;
        private string lastCommandText = string.Empty;
        private float nextPollAt;

        public static void EnsureInstance()
        {
            if (FindAnyObjectByType<AndroidWhiteoutCommandBridge>() is not null)
            {
                return;
            }

            GameObject host = new GameObject(nameof(AndroidWhiteoutCommandBridge));
            DontDestroyOnLoad(host);
            host.AddComponent<AndroidWhiteoutCommandBridge>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[AndroidWhiteoutCommand] path='{ResolveCommandPath()}'");
        }

        private void Update()
        {
            if (Time.unscaledTime < nextPollAt)
            {
                return;
            }

            nextPollAt = Time.unscaledTime + PollIntervalSeconds;
            TryProcessCommandFile();
        }

        private void TryProcessCommandFile()
        {
            string commandPath = ResolveCommandPath();
            if (!File.Exists(commandPath))
            {
                return;
            }

            string commandText;
            try
            {
                commandText = File.ReadAllText(commandPath).Trim();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[AndroidWhiteoutCommand] could not read command file: {exception.Message}");
                return;
            }

            if (string.IsNullOrWhiteSpace(commandText) || string.Equals(commandText, lastCommandText, StringComparison.Ordinal))
            {
                return;
            }

            lastCommandText = commandText;
            if (!TryParseCommand(commandText, out string levelId, out int acceptedMoves))
            {
                Debug.LogWarning($"[AndroidWhiteoutCommand] invalid command '{commandText}'. Expected '<levelId>,<acceptedMoves>'.");
                return;
            }

            StartCoroutine(RunCommand(levelId, acceptedMoves, commandText));
        }

        private System.Collections.IEnumerator RunCommand(string levelId, int acceptedMoves, string commandText)
        {
            PlayableLevelSession? session = PlayableLevelSession.EnsureForActiveGameScene();
            if (session is null)
            {
                Debug.LogWarning($"[AndroidWhiteoutCommand] no active {nameof(PlayableLevelSession)} for '{commandText}'.");
                yield break;
            }

            session.LoadLevel(levelId, session.Seed);
            yield return null;
            yield return new WaitForEndOfFrame();

            int appliedMoves = 0;
            for (int i = 0; i < acceptedMoves; i++)
            {
                if (!session.TryFindFirstDiagnosticMove(out Rescue.Core.State.TileCoord coord, out int groupSize))
                {
                    Debug.LogWarning(
                        $"[AndroidWhiteoutCommand] level={levelId} stopped at {appliedMoves}/{acceptedMoves}; no valid group found.");
                    break;
                }

                bool applied = session.TryRunDiagnosticAction(coord);
                Debug.Log(
                    $"[AndroidWhiteoutCommand] level={levelId} diagnosticMove={i + 1} coord=({coord.Row},{coord.Col}) groupSize={groupSize} applied={applied}");
                if (!applied)
                {
                    break;
                }

                appliedMoves++;
                yield return null;
                yield return new WaitForEndOfFrame();
            }

            AndroidWhiteoutDiagnostics.LogLevelVisualState(levelId);
            int actionCount = session.CurrentState?.ActionCount ?? -1;
            Debug.Log(
                $"[AndroidWhiteoutCommand] ready level={levelId} requestedMoves={acceptedMoves} appliedMoves={appliedMoves} actionCount={actionCount}");
        }

        private static bool TryParseCommand(string commandText, out string levelId, out int acceptedMoves)
        {
            levelId = string.Empty;
            acceptedMoves = 0;
            string[] parts = commandText.Split(new[] { ',', ':', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            levelId = parts[0].Trim().ToUpperInvariant();
            return levelId.Length == 3
                && levelId[0] == 'L'
                && int.TryParse(levelId.Substring(1), out _)
                && int.TryParse(parts[1], out acceptedMoves)
                && acceptedMoves >= 0;
        }

        private static string ResolveCommandPath()
        {
            return Path.Combine(Application.persistentDataPath, CommandFileName);
        }
    }
}
#endif
