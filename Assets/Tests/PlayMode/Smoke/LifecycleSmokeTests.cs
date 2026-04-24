using System.Collections.Generic;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using UnityEngine.TestTools;

namespace Rescue.PlayMode.Tests.Smoke
{
    public sealed class LifecycleSmokeTests
    {
        [UnityTest]
        public System.Collections.IEnumerator EveryMainLevelSolveScriptReachesItsExpectedTerminalOutcome()
        {
            IReadOnlyList<string> levelIds = SmokeTestHarness.MainLevelIds;
            for (int i = 0; i < levelIds.Count; i++)
            {
                SolveScriptJson solve = SmokeTestHarness.LoadSolve(levelIds[i]);
                List<ScriptFrame> frames = new List<ScriptFrame>();

                yield return SmokeTestHarness.RunSolveStrict(solve, frames);

                ScriptFrame finalFrame = SmokeTestHarness.AssertTerminalOutcome(frames, solve.ExpectedOutcome);
                Assert.That(frames.Count - 1, Is.EqualTo(solve.Actions.Length), $"{solve.LevelId} did not consume the full solve script.");

                bool sawTerminalEvent = false;
                for (int eventIndex = 0; eventIndex < finalFrame.Events.Length; eventIndex++)
                {
                    if (finalFrame.Events[eventIndex] is Won or Lost)
                    {
                        sawTerminalEvent = true;
                        break;
                    }
                }

                Assert.That(sawTerminalEvent, Is.True, $"{solve.LevelId} terminal action should emit a terminal event.");
            }
        }
    }
}
