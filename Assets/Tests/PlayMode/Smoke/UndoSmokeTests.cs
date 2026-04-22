using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Undo;
using UnityEngine.TestTools;

namespace Rescue.PlayMode.Tests.Smoke
{
    public sealed class UndoSmokeTests
    {
        [UnityTest]
        public System.Collections.IEnumerator UndoRestoresInitialStateAndConsumesThePlayerFacingCharge()
        {
            SolveScriptJson solve = SmokeTestHarness.LoadSolve("L01");
            var initial = SmokeTestHarness.LoadState(solve.LevelId, solve.Seed);
            string initialFingerprint = SmokeTestHarness.Fingerprint(initial);
            ActionResult firstAction = Pipeline.RunAction(initial, new ActionInput(new Rescue.Core.State.TileCoord(solve.Actions[0].Row, solve.Actions[0].Col)));

            yield return null;

            Assert.That(UndoGuard.CanUndo(firstAction.State, firstAction.Snapshot), Is.True);
            var restored = UndoGuard.PerformUndo(firstAction.State, firstAction.Snapshot!);

            yield return null;

            Assert.That(SmokeTestHarness.Fingerprint(restored), Is.EqualTo(SmokeTestHarness.Fingerprint(initial with { UndoAvailable = false })));
            Assert.That(SmokeTestHarness.Fingerprint(initial), Is.EqualTo(initialFingerprint));
            Assert.That(restored.UndoAvailable, Is.False);
        }

        [UnityTest]
        public System.Collections.IEnumerator UndoThenAlternateBranchMatchesFreshSecondBranchOnly()
        {
            SolveScriptJson solve = SmokeTestHarness.LoadSolve("L01");
            var initial = SmokeTestHarness.LoadState(solve.LevelId, solve.Seed);
            var firstAction = Pipeline.RunAction(initial, new ActionInput(new Rescue.Core.State.TileCoord(solve.Actions[0].Row, solve.Actions[0].Col)));
            var restored = UndoGuard.PerformUndo(firstAction.State, firstAction.Snapshot!);
            Rescue.Core.State.TileCoord alternateCoord = SmokeTestHarness.FindAlternativeValidAction(restored, new Rescue.Core.State.TileCoord(solve.Actions[0].Row, solve.Actions[0].Col));

            yield return null;

            ActionResult branched = Pipeline.RunAction(restored, new ActionInput(alternateCoord), new RunOptions(RecordSnapshot: false));
            ActionResult expected = Pipeline.RunAction(initial with { UndoAvailable = false }, new ActionInput(alternateCoord), new RunOptions(RecordSnapshot: false));

            yield return null;

            Assert.That(SmokeTestHarness.Fingerprint(branched.State), Is.EqualTo(SmokeTestHarness.Fingerprint(expected.State)));
            Assert.That(branched.Outcome, Is.EqualTo(expected.Outcome));
            Assert.That(branched.State.ActionCount, Is.EqualTo(1));
        }

        [UnityTest]
        public System.Collections.IEnumerator SecondUndoAttemptIsRejectedCleanlyAfterTheChargeIsSpent()
        {
            SolveScriptJson solve = SmokeTestHarness.LoadSolve("L01");
            var initial = SmokeTestHarness.LoadState(solve.LevelId, solve.Seed);
            var firstAction = Pipeline.RunAction(initial, new ActionInput(new Rescue.Core.State.TileCoord(solve.Actions[0].Row, solve.Actions[0].Col)));
            var restored = UndoGuard.PerformUndo(firstAction.State, firstAction.Snapshot!);
            Rescue.Core.State.TileCoord alternateCoord = SmokeTestHarness.FindAlternativeValidAction(restored, new Rescue.Core.State.TileCoord(solve.Actions[0].Row, solve.Actions[0].Col));

            yield return null;

            ActionResult secondAction = Pipeline.RunAction(restored, new ActionInput(alternateCoord));

            yield return null;

            Assert.That(restored.UndoAvailable, Is.False);
            Assert.That(UndoGuard.CanUndo(secondAction.State, secondAction.Snapshot), Is.False);
        }
    }
}
