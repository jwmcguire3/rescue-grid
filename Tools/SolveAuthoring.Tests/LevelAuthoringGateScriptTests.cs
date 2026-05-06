using System;
using System.IO;
using NUnit.Framework;

namespace Rescue.SolveAuthoringTool.Tests
{
    public sealed class LevelAuthoringGateScriptTests
    {
        [Test]
        public void PowerShellGate_IncludesRequiredStagesInOrder()
        {
            string repoRoot = FindRepoRoot();
            string script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "verify-level-authoring.ps1"));

            AssertStageOrder(script);
            Assert.That(script, Does.Contain("[switch]$ContinueOnError"));
            Assert.That(script, Does.Contain("docs/level-packets/phase1.packet.json"));
        }

        [Test]
        public void BashGate_ExistsAndIncludesRequiredStagesInOrder()
        {
            string repoRoot = FindRepoRoot();
            string scriptPath = Path.Combine(repoRoot, "scripts", "verify-level-authoring.sh");

            Assert.That(File.Exists(scriptPath), Is.True);
            string script = File.ReadAllText(scriptPath);

            AssertStageOrder(script);
            Assert.That(script, Does.Contain("--continue-on-error"));
            Assert.That(script, Does.Contain("docs/level-packets/phase1.packet.json"));
        }

        private static void AssertStageOrder(string script)
        {
            string[] orderedStages =
            {
                "validate-all",
                "validate-phase1-all",
                "validate-brief-all",
                "readability-all",
                "design-report-all",
                "packet-design-report",
                "--verify-solves",
                "--verify-golden",
                "--verify-failpaths",
                "--compare-assistance-all",
                "packet-report",
                "--verify-acceptance",
            };

            int previousIndex = -1;
            for (int i = 0; i < orderedStages.Length; i++)
            {
                int index = script.IndexOf(orderedStages[i], StringComparison.Ordinal);
                Assert.That(index, Is.GreaterThan(previousIndex), $"Stage '{orderedStages[i]}' should appear after the previous gate stage.");
                previousIndex = index;
            }

            Assert.That(script, Does.Contain("summarize-all"));
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
