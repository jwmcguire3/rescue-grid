using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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

        private string _testDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "LevelTelemetryBotTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }

        [TestCaseSource(nameof(BotNames))]
        public void Run_KnownBot_WritesReportForSelectedBot(string botName)
        {
            string outputDir = Path.Combine(_testDir, botName);

            int exitCode = RunTelemetry(
                new[] { "--level", "L01", "--bot", botName, "--samples", "1", "--max-actions", "1", "--output", outputDir },
                out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            string reportPath = GetSingleReportPath(outputDir);
            Assert.That(Path.GetFileName(reportPath), Does.StartWith(botName + "_"));
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(reportPath));
            Assert.That(document.RootElement.GetProperty("Bot").GetString(), Is.EqualTo(botName));
        }

        [TestCaseSource(nameof(BotNames))]
        public void Run_KnownBot_ProducesDeterministicRuns(string botName)
        {
            string firstOutputDir = Path.Combine(_testDir, botName + "_first");
            string secondOutputDir = Path.Combine(_testDir, botName + "_second");
            string[] firstArgs = { "--level", "L05", "--bot", botName, "--samples", "2", "--max-actions", "5", "--output", firstOutputDir };
            string[] secondArgs = { "--level", "L05", "--bot", botName, "--samples", "2", "--max-actions", "5", "--output", secondOutputDir };

            int firstExitCode = RunTelemetry(firstArgs, out string firstOutput);
            int secondExitCode = RunTelemetry(secondArgs, out string secondOutput);

            Assert.That(firstExitCode, Is.EqualTo(0), firstOutput);
            Assert.That(secondExitCode, Is.EqualTo(0), secondOutput);
            using JsonDocument first = ReadSingleReport(firstOutputDir);
            using JsonDocument second = ReadSingleReport(secondOutputDir);
            Assert.That(
                second.RootElement.GetProperty("Runs").GetRawText(),
                Is.EqualTo(first.RootElement.GetProperty("Runs").GetRawText()));
        }

        [Test]
        public void Run_NewPolicies_SelectDistinctFirstActionsOnStableLevel()
        {
            (int Row, int Col) greedy = RunFirstAction("greedy_clear");
            (int Row, int Col) rescue = RunFirstAction("rescue_focused");
            (int Row, int Col) dock = RunFirstAction("dock_safe");

            Assert.That(greedy, Is.EqualTo((5, 3)));
            Assert.That(rescue, Is.EqualTo((2, 3)));
            Assert.That(dock, Is.EqualTo((1, 0)));
            Assert.That(new[] { greedy, rescue, dock }.Distinct().Count(), Is.EqualTo(3));
        }

        private (int Row, int Col) RunFirstAction(string botName)
        {
            string outputDir = Path.Combine(_testDir, botName + "_first_action");

            int exitCode = RunTelemetry(
                new[] { "--level", "L05", "--bot", botName, "--samples", "1", "--max-actions", "1", "--output", outputDir },
                out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            using JsonDocument document = ReadSingleReport(outputDir);
            JsonElement action = document.RootElement
                .GetProperty("Runs")[0]
                .GetProperty("Actions")[0];
            return (action.GetProperty("Row").GetInt32(), action.GetProperty("Col").GetInt32());
        }

        private static JsonDocument ReadSingleReport(string outputDir)
        {
            return JsonDocument.Parse(File.ReadAllText(GetSingleReportPath(outputDir)));
        }

        private static string GetSingleReportPath(string outputDir)
        {
            string[] reportPaths = Directory.GetFiles(outputDir, "*.json", SearchOption.TopDirectoryOnly);
            Assert.That(reportPaths.Length, Is.EqualTo(1), "Expected exactly one report file.");
            return reportPaths[0];
        }

        private static int RunTelemetry(string[] args, out string output)
        {
            StringBuilder builder = new StringBuilder();
            TextWriter originalOut = Console.Out;
            TextWriter originalErr = Console.Error;

            Console.SetOut(new StringWriter(builder));
            Console.SetError(new StringWriter(builder));

            try
            {
                int exitCode = LevelTelemetryRunner.Run(args);
                output = builder.ToString();
                return exitCode;
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
            }
        }
    }
}
