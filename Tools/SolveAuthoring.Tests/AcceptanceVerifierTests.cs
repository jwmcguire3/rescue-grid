using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Rescue.SolveAuthoringTool;

namespace Rescue.SolveAuthoringTool.Tests
{
    public sealed class AcceptanceVerifierTests
    {
        private string _repoRoot = string.Empty;
        private string _tempRoot = string.Empty;
        private string _manifestPath = string.Empty;
        private string _levelsDir = string.Empty;
        private string _briefsDir = string.Empty;
        private string _resourcesDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _repoRoot = FindRepoRoot();
            _tempRoot = Path.Combine(Path.GetTempPath(), "SolveAuthoringAcceptanceTests_" + Guid.NewGuid().ToString("N"));
            _manifestPath = Path.Combine(_tempRoot, "docs", "level-packets", "phase1.packet.json");
            _levelsDir = Path.Combine(_tempRoot, "Assets", "StreamingAssets", "Levels");
            _briefsDir = Path.Combine(_tempRoot, "docs", "level-briefs");
            _resourcesDir = Path.Combine(_tempRoot, "Assets", "Resources", "Levels");

            Directory.CreateDirectory(Path.GetDirectoryName(_manifestPath)!);
            Directory.CreateDirectory(_levelsDir);
            Directory.CreateDirectory(_briefsDir);
            Directory.CreateDirectory(_resourcesDir);

            File.Copy(RepoPath("Assets", "StreamingAssets", "Levels", "L00.json"), Path.Combine(_levelsDir, "L00.json"));
            File.Copy(RepoPath("docs", "level-briefs", "L00.brief.json"), Path.Combine(_briefsDir, "L00.brief.json"));
            File.Copy(RepoPath("Assets", "Resources", "Levels", "L00.solve.json"), Path.Combine(_resourcesDir, "L00.solve.json"));
            File.Copy(RepoPath("Assets", "Resources", "Levels", "L00.golden.json"), Path.Combine(_resourcesDir, "L00.golden.json"));
            WriteManifest();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }

        [Test]
        public void VerifyAcceptance_AllManifestExpectedFilesPresent_Passes()
        {
            int exitCode = RunAcceptance(out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(output, Does.Contain("L00:"));
            Assert.That(output, Does.Contain("Golden: expected Win, got Win -> PASS"));
            Assert.That(output, Does.Contain("Acceptance summary: 1/1 level(s) passed."));
        }

        [Test]
        public void VerifyAcceptance_MissingManifestExpectedGolden_Fails()
        {
            File.Delete(Path.Combine(_resourcesDir, "L00.golden.json"));

            int exitCode = RunAcceptance(out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("Golden JSON: FAIL"));
            Assert.That(output, Does.Contain("Golden: expected <missing>, got NotRun -> FAIL"));
            Assert.That(output, Does.Contain("Failed level ids: L00"));
        }

        [Test]
        public void VerifyAcceptance_MissingBrief_Fails()
        {
            File.Delete(Path.Combine(_briefsDir, "L00.brief.json"));

            int exitCode = RunAcceptance(out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("Brief JSON: FAIL"));
            Assert.That(output, Does.Contain("Readability warnings: SKIP"));
            Assert.That(output, Does.Contain("Failed level ids: L00"));
        }

        [Test]
        public void VerifyAcceptance_CoreValidationError_Fails()
        {
            MutateJson(Path.Combine(_levelsDir, "L00.json"), root =>
            {
                root["board"]!["width"] = 99;
            });

            int exitCode = RunAcceptance(out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("Core validation: FAIL"));
            Assert.That(output, Does.Contain("board.tiles.width"));
            Assert.That(output, Does.Contain("Failed level ids: L00"));
        }

        [Test]
        public void VerifyAcceptance_GoldenMismatch_Fails()
        {
            MutateJson(Path.Combine(_resourcesDir, "L00.golden.json"), root =>
            {
                root["expectedOutcome"] = "Loss";
            });

            int exitCode = RunAcceptance(out string output);

            Assert.That(exitCode, Is.EqualTo(1), output);
            Assert.That(output, Does.Contain("Golden: expected Loss, got Win -> FAIL"));
            Assert.That(output, Does.Contain("final outcome mismatch"));
        }

        [Test]
        public void VerifyAcceptance_Phase1PolicyWarning_DoesNotFailByDefault()
        {
            MutateJson(Path.Combine(_levelsDir, "L00.json"), root =>
            {
                root["dock"]!["jamEnabled"] = true;
            });

            int exitCode = RunAcceptance(out string output);

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(output, Does.Contain("Phase 1 policy warnings: WARN"));
            Assert.That(output, Does.Contain("phase1.dockJamLevel"));
            Assert.That(output, Does.Contain("Acceptance summary: 1/1 level(s) passed."));
        }

        [Test]
        public void VerifyLevelAuthoringScript_IncludesAcceptanceGate()
        {
            string script = File.ReadAllText(RepoPath("scripts", "verify-level-authoring.ps1"));

            Assert.That(script, Does.Contain("--verify-golden"));
            Assert.That(script, Does.Contain("--verify-acceptance"));
            Assert.That(script.IndexOf("--verify-acceptance", StringComparison.Ordinal), Is.GreaterThan(script.IndexOf("--verify-golden", StringComparison.Ordinal)));
        }

        private int RunAcceptance(out string output)
        {
            string[] args =
            {
                "--verify-acceptance",
                "--manifest",
                _manifestPath,
                "--levels-dir",
                _levelsDir,
                "--briefs-dir",
                _briefsDir,
                "--resources-dir",
                _resourcesDir,
            };

            string originalCurrentDirectory = Directory.GetCurrentDirectory();
            TextWriter originalOut = Console.Out;
            TextWriter originalErr = Console.Error;
            StringBuilder builder = new StringBuilder();

            Directory.SetCurrentDirectory(_tempRoot);
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

        private void WriteManifest()
        {
            File.WriteAllText(_manifestPath, @"{
  ""packetId"": ""phase1-test"",
  ""displayName"": ""Phase 1 Acceptance Test Packet"",
  ""firstLevelId"": ""L00"",
  ""lastLevelId"": ""L00"",
  ""expectedLevelIds"": [""L00""],
  ""ruleTeachLevelIds"": [""L00""],
  ""dockJamLevelIds"": [],
  ""staticVineIntroLevelIds"": [],
  ""debrisPoolBands"": [
    {
      ""firstLevelId"": ""L00"",
      ""lastLevelId"": ""L00"",
      ""debrisTypePoolSize"": 5
    }
  ],
  ""waterIntervalMinimum"": 6,
  ""notes"": ""Test fixture manifest.""
}");
        }

        private static void MutateJson(string path, Action<JsonNode> mutate)
        {
            JsonNode root = JsonNode.Parse(File.ReadAllText(path)) ?? throw new InvalidOperationException($"Could not parse '{path}'.");
            mutate(root);
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private string RepoPath(params string[] parts)
        {
            string path = _repoRoot;
            for (int i = 0; i < parts.Length; i++)
            {
                path = Path.Combine(path, parts[i]);
            }

            return path;
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
