using System.Collections.Generic;
using NUnit.Framework;
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
                Assert.That(finalFrame.Events, Has.Some.TypeOf<Rescue.Core.Pipeline.Lost>(), $"{solve.LevelId} terminal action should emit a terminal event.");
            }
        }
    }
}
