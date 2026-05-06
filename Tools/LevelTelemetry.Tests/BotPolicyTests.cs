using System.Linq;
using NUnit.Framework;
using Rescue.LevelTelemetryTool;

namespace Rescue.LevelTelemetryTool.Tests
{
    public sealed class BotPolicyTests
    {
        private static readonly string[] BotNames =
        {
            "random_legal",
            "greedy_clear",
            "rescue_focused",
            "dock_safe",
        };

        [TestCaseSource(nameof(BotNames))]
        public void RunBot_KnownBot_ProducesDeterministicRun(string botName)
        {
            LevelTelemetryRunner.BotRunResult first = LevelTelemetryRunner.RunBot("L05", 1, botName, 5);
            LevelTelemetryRunner.BotRunResult second = LevelTelemetryRunner.RunBot("L05", 1, botName, 5);

            Assert.That(second.Actions.Select(static action => (action.Row, action.Col)), Is.EqualTo(first.Actions.Select(static action => (action.Row, action.Col))));
            Assert.That(second.TerminalReason, Is.EqualTo(first.TerminalReason));
            Assert.That(second.EventTypeNames, Is.EqualTo(first.EventTypeNames));
        }

        [Test]
        public void RunBot_NewPolicies_SelectDistinctFirstActionsOnStableLevel()
        {
            (int Row, int Col) greedy = RunFirstAction("greedy_clear");
            (int Row, int Col) rescue = RunFirstAction("rescue_focused");
            (int Row, int Col) dock = RunFirstAction("dock_safe");

            Assert.That(greedy, Is.EqualTo((5, 3)));
            Assert.That(rescue, Is.EqualTo((2, 3)));
            Assert.That(dock, Is.EqualTo((1, 0)));
            Assert.That(new[] { greedy, rescue, dock }.Distinct().Count(), Is.EqualTo(3));
        }

        private static (int Row, int Col) RunFirstAction(string botName)
        {
            LevelTelemetryRunner.BotRunResult run = LevelTelemetryRunner.RunBot("L05", 1, botName, 1);
            LevelTelemetryRunner.ActionReport action = run.Actions[0];
            return (action.Row, action.Col);
        }
    }
}
