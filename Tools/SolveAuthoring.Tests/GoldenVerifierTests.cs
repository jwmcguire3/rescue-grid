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
    public sealed class GoldenVerifierTests
    {
        private static readonly string[] VerifyGoldenArgs = { "--verify-golden" };
        private static readonly string[] OutputLineSeparators = { "\r\n", "\n" };

        private string _repoRoot = string.Empty;
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _repoRoot = FindRepoRoot();
            _tempDir = Path.Combine(Path.GetTempPath(), "SolveAuthoringGoldenTests_" + Guid.NewGuid().ToString("N"));
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
        public void VerifyGolden_AllCurrentGoldenFiles_Passes()
        {
            int exitCode = RunSolveAuthoring(VerifyGoldenArgs, out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(output, Does.Contain("L00.golden.json: expected Win, got Win -> PASS"));
            Assert.That(output, Does.Contain("L01.golden.json: expected Win, got Win -> PASS"));
            Assert.That(output, Does.Contain("L02.golden.json: expected Win, got Win -> PASS"));
            Assert.That(output, Does.Contain("L03.golden.json: expected Win, got Win -> PASS"));
        }

        [Test]
        public void VerifyGolden_TargetedFiles_VerifiesOnlyThoseFiles()
        {
            string l00 = GoldenPath("L00");
            string l03 = GoldenPath("L03");

            int exitCode = RunSolveAuthoring(new[] { "--verify-golden", l00, l03 }, out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(output, Does.Contain("L00.golden.json: expected Win, got Win -> PASS"));
            Assert.That(output, Does.Contain("L03.golden.json: expected Win, got Win -> PASS"));
            Assert.That(output, Does.Not.Contain("L01.golden.json"));
            Assert.That(output, Does.Not.Contain("L02.golden.json"));
        }

        [Test]
        public void VerifyGolden_MissingRequiredProperty_FailsWithClearLine()
        {
            string path = WriteMutatedGolden("missing-path-type.golden.json", "L00", root => root.AsObject().Remove("pathType"));

            int exitCode = RunSolveAuthoring(new[] { "--verify-golden", path }, out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(CountFailLines(output), Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("missing-path-type.golden.json: expected <unknown>, got NotRun -> FAIL"));
            Assert.That(output, Does.Contain("missing required property 'pathType'"));
        }

        [Test]
        public void VerifyGolden_WrongEventOrder_Fails()
        {
            string path = WriteMutatedGolden("wrong-event-order.golden.json", "L00", root =>
            {
                root["expectedEventsInOrder"] = new JsonArray("Won", "TargetOneClearAway");
            });

            int exitCode = RunSolveAuthoring(new[] { "--verify-golden", path }, out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("wrong-event-order.golden.json: expected Win, got Win -> FAIL"));
            Assert.That(output, Does.Contain("expected event 'TargetOneClearAway' was not found in order"));
        }

        [Test]
        public void VerifyGolden_ExtractionOrderMismatch_Fails()
        {
            string path = WriteMutatedGolden("wrong-extraction-order.golden.json", "L03", root =>
            {
                root["expectedExtractionOrder"] = new JsonArray("1", "0");
            });

            int exitCode = RunSolveAuthoring(new[] { "--verify-golden", path }, out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("wrong-extraction-order.golden.json: expected Win, got Win -> FAIL"));
            Assert.That(output, Does.Contain("expected extraction order [1,0], got [0,1]"));
        }

        [Test]
        public void VerifyGolden_MaxActionsTooLow_FailsBeforeReplay()
        {
            string path = WriteMutatedGolden("max-actions-too-low.golden.json", "L00", root =>
            {
                root["maxActions"] = 1;
            });

            int exitCode = RunSolveAuthoring(new[] { "--verify-golden", path }, out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("max-actions-too-low.golden.json: expected Win, got NotRun -> FAIL"));
            Assert.That(output, Does.Contain("actions used 2 exceeds maxActions 1"));
        }

        [Test]
        public void VerifyGolden_InvalidAction_FailsWithStepAndCoord()
        {
            string path = WriteMutatedGolden("invalid-action.golden.json", "L00", root =>
            {
                JsonArray actions = root["actions"]!.AsArray();
                JsonObject firstAction = actions[0]!.AsObject();
                firstAction["row"] = 0;
                firstAction["col"] = 0;
            });

            int exitCode = RunSolveAuthoring(new[] { "--verify-golden", path }, out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("invalid-action.golden.json: expected Win, got Ok -> FAIL"));
            Assert.That(output, Does.Contain("step 1 invalid input at 0,0"));
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

        private string WriteMutatedGolden(string fileName, string levelId, Action<JsonNode> mutate)
        {
            string json = File.ReadAllText(GoldenPath(levelId));
            JsonNode root = JsonNode.Parse(json) ?? throw new InvalidOperationException("Golden fixture could not be parsed.");
            mutate(root);

            string path = Path.Combine(_tempDir, fileName);
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return path;
        }

        private string GoldenPath(string levelId)
        {
            return Path.Combine(_repoRoot, "Assets", "Resources", "Levels", levelId + ".golden.json");
        }

        private static int CountFailLines(string output)
        {
            return output.Split(OutputLineSeparators, StringSplitOptions.None)
                .Count(static line => line.Contains(" -> FAIL", StringComparison.Ordinal));
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
