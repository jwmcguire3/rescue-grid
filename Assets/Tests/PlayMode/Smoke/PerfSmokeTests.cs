using System.Diagnostics;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using UnityEngine.TestTools;

namespace Rescue.PlayMode.Tests.Smoke
{
    public sealed class PerfSmokeTests
    {
        [UnityTest]
        public System.Collections.IEnumerator RepresentativeLevelLoadStaysUnderCoarseThreshold()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var state = SmokeTestHarness.LoadState("L15", SmokeTestHarness.LoadSolve("L15").Seed);
            stopwatch.Stop();

            yield return null;

            SmokeTestHarness.AssertInvariants("L15", state);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(500), "Level load regressed past the coarse smoke threshold.");
        }

        [UnityTest]
        public System.Collections.IEnumerator RepresentativeActionStaysUnderCoarseThreshold()
        {
            SolveScriptJson solve = SmokeTestHarness.LoadSolve("L15");
            var state = SmokeTestHarness.LoadState("L15", solve.Seed);
            var input = new ActionInput(new Rescue.Core.State.TileCoord(solve.Actions[0].Row, solve.Actions[0].Col));

            Stopwatch stopwatch = Stopwatch.StartNew();
            ActionResult result = Pipeline.RunAction(state, input);
            stopwatch.Stop();

            yield return null;

            SmokeTestHarness.AssertInvariants("L15", result.State);
            Assert.That(result.Events, Has.None.TypeOf<InvalidInput>(), "Representative action became invalid.");
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(250), "Action resolution regressed past the coarse smoke threshold.");
        }
    }
}
