using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Rescue.LevelTelemetryTool;

namespace Rescue.LevelTelemetryTool.Tests
{
    public sealed class ReportTests
    {
        private static readonly string[] ExpectedSingleLevel = { "L01" };
        private static readonly string[] ExpectedRangeLevels = { "L00", "L01", "L02", "L03" };
        private static readonly string[] CombinedLevelAndRangeArgs = { "--level", "L01", "--range", "L00-L03" };
        private static readonly string[] ReversedRangeArgs = { "--range", "L03-L00" };

        private string _testDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "LevelTelemetryTests_" + Guid.NewGuid().ToString("N"));
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

        [Test]
        public void Run_Level_LoadsDefaultSamplesAndWritesReport()
        {
            string outputDir = Path.Combine(_testDir, "single");

            int exitCode = RunTelemetry(
                new[] { "--level", "L01", "--samples", "2", "--max-actions", "3", "--output", outputDir },
                out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            using JsonDocument document = ReadSingleReport(outputDir);
            JsonElement root = document.RootElement;
            Assert.That(ReadStringArray(root, "Levels"), Is.EqualTo(ExpectedSingleLevel));
            Assert.That(root.GetProperty("SamplesPerLevel").GetInt32(), Is.EqualTo(2));
            Assert.That(root.GetProperty("MaxActions").GetInt32(), Is.EqualTo(3));
            Assert.That(root.GetProperty("Runs").GetArrayLength(), Is.EqualTo(2));
        }

        [Test]
        public void Run_Range_ExpandsInclusiveLevels()
        {
            string outputDir = Path.Combine(_testDir, "range");

            int exitCode = RunTelemetry(
                new[] { "--range", "L00-L03", "--samples", "1", "--max-actions", "1", "--output", outputDir },
                out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            using JsonDocument document = ReadSingleReport(outputDir);
            JsonElement root = document.RootElement;
            Assert.That(ReadStringArray(root, "Levels"), Is.EqualTo(ExpectedRangeLevels));
            Assert.That(root.GetProperty("Runs").GetArrayLength(), Is.EqualTo(4));
        }

        [Test]
        public void Run_InvalidArgs_ReturnsUsageExitCode()
        {
            int combinedExitCode = RunTelemetry(CombinedLevelAndRangeArgs, out string combinedOutput);
            int reversedExitCode = RunTelemetry(ReversedRangeArgs, out string reversedOutput);

            Assert.That(combinedExitCode, Is.EqualTo(2), combinedOutput);
            Assert.That(combinedOutput, Does.Contain("Specify only one of --level or --range"));
            Assert.That(combinedOutput, Does.Contain("Usage:"));
            Assert.That(reversedExitCode, Is.EqualTo(2), reversedOutput);
            Assert.That(reversedOutput, Does.Contain("--range end must be greater than or equal to the start"));
            Assert.That(reversedOutput, Does.Contain("Usage:"));
        }

        [Test]
        public void Run_UnknownBot_ReturnsUsageExitCode()
        {
            int exitCode = RunTelemetry(
                new[] { "--level", "L01", "--bot", "unknown", "--output", Path.Combine(_testDir, "unknown") },
                out string output);

            Assert.That(exitCode, Is.EqualTo(2), output);
            Assert.That(output, Does.Contain("--bot must be one of"));
            Assert.That(output, Does.Contain("random_legal"));
            Assert.That(output, Does.Contain("greedy_clear"));
            Assert.That(output, Does.Contain("rescue_focused"));
            Assert.That(output, Does.Contain("dock_safe"));
            Assert.That(output, Does.Contain("Usage:"));
        }

        [Test]
        public void Run_SameInputs_ProducesDeterministicReportShape()
        {
            string firstOutputDir = Path.Combine(_testDir, "first");
            string secondOutputDir = Path.Combine(_testDir, "second");
            string[] firstArgs = { "--level", "L01", "--bot", "random_legal", "--samples", "2", "--max-actions", "5", "--output", firstOutputDir };
            string[] secondArgs = { "--level", "L01", "--bot", "random_legal", "--samples", "2", "--max-actions", "5", "--output", secondOutputDir };

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

        private static JsonDocument ReadSingleReport(string outputDir)
        {
            string[] reportPaths = Directory.GetFiles(outputDir, "*.json", SearchOption.TopDirectoryOnly);
            Assert.That(reportPaths.Length, Is.EqualTo(1), "Expected exactly one report file.");
            return JsonDocument.Parse(File.ReadAllText(reportPaths[0]));
        }

        private static string[] ReadStringArray(JsonElement root, string propertyName)
        {
            return root.GetProperty(propertyName)
                .EnumerateArray()
                .Select(static element => element.GetString() ?? string.Empty)
                .ToArray();
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
