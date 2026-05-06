using System;
using System.IO;
using NUnit.Framework;
using Rescue.Content;

namespace LevelValidator.Tests
{
    public sealed class LevelValidatorCommandTests
    {
        private string _repoRoot = string.Empty;
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _repoRoot = FindRepoRoot();
            _tempDir = Path.Combine(Path.GetTempPath(), "LevelValidatorTests_" + Guid.NewGuid().ToString("N"));
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
        public void PreviewSvg_InvalidLevel_ReturnsFailureAndDoesNotWriteOutput()
        {
            string levelPath = Path.Combine(_tempDir, "invalid.json");
            string outputPath = Path.Combine(_tempDir, "invalid.svg");
            File.WriteAllText(levelPath, "{}");

            int exitCode = global::LevelValidatorRunner.Run(new[] { "preview-svg", levelPath, outputPath });

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(File.Exists(outputPath), Is.False);
        }

        [Test]
        public void WriteReview_NewFile_GeneratesReviewMarkdown()
        {
            string outputPath = Path.Combine(_tempDir, "L03.review.md");

            int exitCode = global::LevelValidatorRunner.Run(new[]
            {
                "write-review",
                RepoPath("Assets", "StreamingAssets", "Levels", "L03.json"),
                RepoPath("docs", "level-briefs", "L03.brief.json"),
                outputPath,
            });

            string markdown = File.ReadAllText(outputPath);
            Assert.That(exitCode, Is.EqualTo(0), markdown);
            Assert.That(markdown, Does.Contain("# L03 - Rescue Order Arrives"));
            Assert.That(markdown, Does.Contain(LevelReviewWriter.AutoStartMarker));
            Assert.That(markdown, Does.Contain(LevelReviewWriter.AutoEndMarker));
            Assert.That(markdown, Does.Contain("## Brief Summary"));
            Assert.That(markdown, Does.Contain("## Automated Status"));
            Assert.That(markdown, Does.Contain("- Core validation:"));
            Assert.That(markdown, Does.Contain("- Phase 1 policy:"));
            Assert.That(markdown, Does.Contain("- Brief conformance: PASS"));
            Assert.That(markdown, Does.Contain("- Readability/density:"));
            Assert.That(markdown, Does.Contain("- Solve path:"));
            Assert.That(markdown, Does.Contain("- Golden path:"));
            Assert.That(markdown, Does.Contain("- Fail path:"));
            Assert.That(markdown, Does.Contain("- Assistance comparison:"));
            Assert.That(markdown, Does.Contain("## Key Metrics"));
            Assert.That(markdown, Does.Contain("- Density:"));
            Assert.That(markdown, Does.Contain("- Target count: 2"));
            Assert.That(markdown, Does.Contain("- Water interval: 6"));
            Assert.That(markdown, Does.Contain("- Assistance chance: 0.7"));
            Assert.That(markdown, Does.Contain("- Legal starting groups:"));
            Assert.That(markdown, Does.Contain("## Designer Checklist"));
            Assert.That(markdown, Does.Contain("- [ ] First move readable?"));
            Assert.That(markdown, Does.Contain("- [ ] Level ends emotionally on rescue?"));
            Assert.That(markdown, Does.Contain("## Decision"));
            Assert.That(markdown, Does.Contain("- [ ] Accepted"));
            Assert.That(markdown, Does.Contain("- [ ] Needs revision"));
            Assert.That(markdown, Does.Contain("- [ ] Cut"));
            Assert.That(markdown, Does.Contain("## Notes"));
        }

        [Test]
        public void WriteReview_ExistingManualNotes_PreservesManualContent()
        {
            string outputPath = Path.Combine(_tempDir, "L03.review.md");
            File.WriteAllText(
                outputPath,
                "# L03 - Old Title" + Environment.NewLine
                + Environment.NewLine
                + LevelReviewWriter.AutoStartMarker + Environment.NewLine
                + "old generated content" + Environment.NewLine
                + LevelReviewWriter.AutoEndMarker + Environment.NewLine
                + Environment.NewLine
                + "## Decision" + Environment.NewLine
                + Environment.NewLine
                + "- [ ] Accepted" + Environment.NewLine
                + "- [x] Needs revision" + Environment.NewLine
                + "- [ ] Cut" + Environment.NewLine
                + Environment.NewLine
                + "## Notes" + Environment.NewLine
                + Environment.NewLine
                + "Manual designer note.");

            int exitCode = global::LevelValidatorRunner.Run(new[]
            {
                "write-review",
                RepoPath("Assets", "StreamingAssets", "Levels", "L03.json"),
                RepoPath("docs", "level-briefs", "L03.brief.json"),
                outputPath,
            });

            string markdown = File.ReadAllText(outputPath);
            Assert.That(exitCode, Is.EqualTo(0), markdown);
            Assert.That(markdown, Does.Contain("- [x] Needs revision"));
            Assert.That(markdown, Does.Contain("Manual designer note."));
            Assert.That(markdown, Does.Contain("## Automated Status"));
        }

        [Test]
        public void WriteReview_ExistingAutoSection_ReplacesAutoContent()
        {
            string outputPath = Path.Combine(_tempDir, "L03.review.md");
            File.WriteAllText(
                outputPath,
                "# L03 - Rescue Order Arrives" + Environment.NewLine
                + Environment.NewLine
                + LevelReviewWriter.AutoStartMarker + Environment.NewLine
                + "stale automated status" + Environment.NewLine
                + LevelReviewWriter.AutoEndMarker + Environment.NewLine
                + Environment.NewLine
                + "## Notes" + Environment.NewLine
                + Environment.NewLine
                + "Keep me.");

            int exitCode = global::LevelValidatorRunner.Run(new[]
            {
                "write-review",
                RepoPath("Assets", "StreamingAssets", "Levels", "L03.json"),
                RepoPath("docs", "level-briefs", "L03.brief.json"),
                outputPath,
            });

            string markdown = File.ReadAllText(outputPath);
            Assert.That(exitCode, Is.EqualTo(0), markdown);
            Assert.That(markdown, Does.Not.Contain("stale automated status"));
            Assert.That(markdown, Does.Contain("## Automated Status"));
            Assert.That(markdown, Does.Contain("Keep me."));
        }

        [Test]
        public void WriteReview_MissingGoldenAndFailPath_SurfacesWarnings()
        {
            string root = CreateReviewRoot("L03");
            string outputPath = Path.Combine(root, "docs", "level-reviews", "L03.review.md");

            int exitCode = global::LevelValidatorRunner.Run(new[]
            {
                "write-review",
                Path.Combine(root, "Assets", "StreamingAssets", "Levels", "L03.json"),
                Path.Combine(root, "docs", "level-briefs", "L03.brief.json"),
                outputPath,
            });

            string markdown = File.ReadAllText(outputPath);
            Assert.That(exitCode, Is.EqualTo(0), markdown);
            Assert.That(markdown, Does.Contain("- Golden path: WARN - Golden path file was not found"));
            Assert.That(markdown, Does.Contain("- Fail path: WARN - Fail path file was not found"));
        }

        [Test]
        public void WriteReviewAll_WritesExpectedFiles()
        {
            string root = CreateReviewRoot("L00", "L01");
            string reviewsDir = Path.Combine(root, "docs", "level-reviews");

            int exitCode = global::LevelValidatorRunner.Run(new[]
            {
                "write-review-all",
                Path.Combine(root, "Assets", "StreamingAssets", "Levels"),
                Path.Combine(root, "docs", "level-briefs"),
                reviewsDir,
            });

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(File.Exists(Path.Combine(reviewsDir, "L00.review.md")), Is.True);
            Assert.That(File.Exists(Path.Combine(reviewsDir, "L01.review.md")), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(reviewsDir, "L00.review.md")), Does.Contain("# L00 -"));
            Assert.That(File.ReadAllText(Path.Combine(reviewsDir, "L01.review.md")), Does.Contain("# L01 -"));
        }

        private string CreateReviewRoot(params string[] levelIds)
        {
            string root = Path.Combine(_tempDir, "review-root-" + Guid.NewGuid().ToString("N"));
            string levelsDir = Path.Combine(root, "Assets", "StreamingAssets", "Levels");
            string resourcesDir = Path.Combine(root, "Assets", "Resources", "Levels");
            string briefsDir = Path.Combine(root, "docs", "level-briefs");
            Directory.CreateDirectory(levelsDir);
            Directory.CreateDirectory(resourcesDir);
            Directory.CreateDirectory(briefsDir);
            for (int i = 0; i < levelIds.Length; i++)
            {
                string levelId = levelIds[i];
                File.Copy(RepoPath("Assets", "StreamingAssets", "Levels", levelId + ".json"), Path.Combine(levelsDir, levelId + ".json"));
                File.Copy(RepoPath("docs", "level-briefs", levelId + ".brief.json"), Path.Combine(briefsDir, levelId + ".brief.json"));
            }

            return root;
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
