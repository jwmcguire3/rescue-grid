using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Rescue.PlayMode.Tests.Smoke
{
    public sealed class RetrySmokeTests
    {
        [UnityTest]
        public System.Collections.IEnumerator SameSeedReplayIsIdenticalAndAlternateSeedDivergesWhenAuthoredTo()
        {
            IReadOnlyList<string> levelIds = SmokeTestHarness.MainLevelIds;
            for (int i = 0; i < levelIds.Count; i++)
            {
                SolveScriptJson solve = SmokeTestHarness.LoadSolve(levelIds[i]);

                List<ScriptFrame> baseline = new List<ScriptFrame>();
                yield return SmokeTestHarness.RunSolveStrict(solve, baseline);
                SmokeTestHarness.AssertTerminalOutcome(baseline, solve.ExpectedOutcome);

                List<ScriptFrame> sameSeedReplay = new List<ScriptFrame>();
                yield return SmokeTestHarness.RunSolveStrict(solve, sameSeedReplay, solve.Seed);
                SmokeTestHarness.AssertTerminalOutcome(sameSeedReplay, solve.ExpectedOutcome);

                Assert.That(
                    SmokeTestHarness.ToFingerprints(sameSeedReplay),
                    Is.EqualTo(SmokeTestHarness.ToFingerprints(baseline)).AsCollection,
                    $"{solve.LevelId} trajectory drifted after reset with the same seed.");

                List<ScriptFrame> alternateSeedReplay = new List<ScriptFrame>();
                yield return SmokeTestHarness.RunSolveLoose(solve, alternateSeedReplay, solve.AlternateSeed);

                if (solve.ExpectAlternateSeedDivergence)
                {
                    Assert.That(
                        SmokeTestHarness.ToFingerprints(alternateSeedReplay),
                        Is.Not.EqualTo(SmokeTestHarness.ToFingerprints(baseline)).AsCollection,
                        $"{solve.LevelId} should diverge when replayed with alternate seed {solve.AlternateSeed}.");
                }
                else
                {
                    Assert.That(
                        SmokeTestHarness.ToFingerprints(alternateSeedReplay),
                        Is.EqualTo(SmokeTestHarness.ToFingerprints(baseline)).AsCollection,
                        $"{solve.LevelId} unexpectedly diverged under alternate seed {solve.AlternateSeed}.");
                }
            }
        }
    }
}
