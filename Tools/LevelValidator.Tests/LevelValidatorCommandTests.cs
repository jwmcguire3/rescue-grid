using System;
using System.IO;
using NUnit.Framework;

namespace LevelValidator.Tests
{
    public sealed class LevelValidatorCommandTests
    {
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
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
    }
}
