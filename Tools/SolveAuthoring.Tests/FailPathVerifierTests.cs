using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Rescue.SolveAuthoringTool;

namespace Rescue.SolveAuthoringTool.Tests
{
    public sealed class FailPathVerifierTests
    {
        private static readonly string[] OutputLineSeparators = { "\r\n", "\n" };

        private string _repoRoot = string.Empty;
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _repoRoot = FindRepoRoot();
            _tempDir = Path.Combine(Path.GetTempPath(), "SolveAuthoringFailPathTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Test]
        public void VerifyFailPath_ValidReplay_Passes()
        {
            string path = WriteFailPath("valid.fail.json", ValidFailPathJson());

            int exitCode = RunSolveAuthoring(new[] { "--verify-failpaths", path }, out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(output, Does.Contain("valid.fail.json: expected LossDockOverflow, got LossDockOverflow -> PASS"));
        }

        [Test]
        public void VerifyFailPath_ExpectedOutcomeMismatch_Fails()
        {
            string path = WriteMutatedFailPath("outcome-mismatch.fail.json", root =>
            {
                root["expectedOutcome"] = "LossWaterOnTarget";
                root["expectedLossReason"] = "LossWaterOnTarget";
            });

            int exitCode = RunSolveAuthoring(new[] { "--verify-failpaths", path }, out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("outcome-mismatch.fail.json: expected LossWaterOnTarget, got LossDockOverflow -> FAIL"));
            Assert.That(output, Does.Contain("final outcome mismatch"));
        }

        [Test]
        public void VerifyFailPath_InvalidAction_FailsWithStepAndCoord()
        {
            string path = WriteMutatedFailPath("invalid-action.fail.json", root =>
            {
                JsonArray actions = RequireArray(root, "actions");
                JsonObject firstAction = RequireObject(actions[0]);
                firstAction["row"] = 0;
                firstAction["col"] = 1;
            });

            int exitCode = RunSolveAuthoring(new[] { "--verify-failpaths", path }, out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("invalid-action.fail.json: expected LossDockOverflow, got Ok -> FAIL"));
            Assert.That(output, Does.Contain("step 1 invalid input at 0,1"));
        }

        [Test]
        public void VerifyFailPath_MissingRequiredProperty_FailsClearly()
        {
            string path = WriteMutatedFailPath("missing-path-type.fail.json", root => root.AsObject().Remove("pathType"));

            int exitCode = RunSolveAuthoring(new[] { "--verify-failpaths", path }, out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(CountFailLines(output), Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("missing-path-type.fail.json: expected <unknown>, got NotRun -> FAIL"));
            Assert.That(output, Does.Contain("missing required property 'pathType'"));
        }

        [Test]
        public void VerifyFailPath_ExpectedEventsInOrder_IsChecked()
        {
            string path = WriteMutatedFailPath("wrong-event-order.fail.json", root =>
            {
                root["expectedEventsInOrder"] = new JsonArray("Lost", "TargetOneClearAway");
            });

            int exitCode = RunSolveAuthoring(new[] { "--verify-failpaths", path }, out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("wrong-event-order.fail.json: expected LossDockOverflow, got LossDockOverflow -> FAIL"));
            Assert.That(output, Does.Contain("expected event 'TargetOneClearAway' was not found in order"));
        }

        private int RunSolveAuthoring(string[] args, out string output)
        {
            string originalCurrentDirectory = Directory.GetCurrentDirectory();
            TextWriter originalOut = Console.Out;
            TextWriter originalErr = Console.Error;
            StringBuilder builder = new StringBuilder();

            Directory.SetCurrentDirectory(_repoRoot);
            Console.SetOut(new StringWriter(builder));
            Console.SetError(new StringWriter(builder));

            try
            {
                int exitCode = SolveAuthoringRunner.Run(args);
                output = builder.ToString();
                return exitCode;
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
                Directory.SetCurrentDirectory(originalCurrentDirectory);
            }
        }

        private string WriteMutatedFailPath(string fileName, Action<JsonNode> mutate)
        {
            JsonNode root = JsonNode.Parse(ValidFailPathJson()) ?? throw new InvalidOperationException("Fail path fixture could not be parsed.");
            mutate(root);
            return WriteFailPath(fileName, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private string WriteFailPath(string fileName, string json)
        {
            string path = Path.Combine(_tempDir, fileName);
            File.WriteAllText(path, json);
            return path;
        }

        private static JsonArray RequireArray(JsonNode root, string propertyName)
        {
            JsonNode? value = root[propertyName];
            return value as JsonArray ?? throw new InvalidOperationException($"Expected '{propertyName}' to be an array.");
        }

        private static JsonObject RequireObject(JsonNode? value)
        {
            return value as JsonObject ?? throw new InvalidOperationException("Expected JSON object.");
        }

        private static int CountFailLines(string output)
        {
            return output.Split(OutputLineSeparators, StringSplitOptions.None)
                .Count(static line => line.Contains(" -> FAIL", StringComparison.Ordinal));
        }

        private static string ValidFailPathJson()
        {
            return @"{
  ""levelId"": ""L03"",
  ""seed"": 1,
  ""pathType"": ""expected_fail"",
  ""expectedOutcome"": ""LossDockOverflow"",
  ""expectedLossReason"": ""LossDockOverflow"",
  ""maxActions"": 5,
  ""actions"": [
    { ""row"": 0, ""col"": 4, ""intent"": ""Chase the visually easier upper target side first instead of the water-near rescue."" },
    { ""row"": 5, ""col"": 4, ""intent"": ""Keep clearing away from the urgent lower puppy."" },
    { ""row"": 4, ""col"": 3, ""intent"": ""Add dock residue while still avoiding the lower route."" },
    { ""row"": 4, ""col"": 5, ""intent"": ""Push the dock into acute pressure."" },
    { ""row"": 6, ""col"": 4, ""intent"": ""Overflow the dock before rescuing the urgent lower puppy."" }
  ],
  ""expectedEventsInOrder"": [
    ""TargetOneClearAway"",
    ""DockOverflowTriggered"",
    ""Lost""
  ],
  ""notes"": ""Verified current L03 wrong-priority dock failure path.""
}";
        }

        private static string FindRepoRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory is not null)
            {
                string levelsPath = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "Levels");
                if (Directory.Exists(levelsPath))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not find repository root from test directory.");
        }
    }
}
