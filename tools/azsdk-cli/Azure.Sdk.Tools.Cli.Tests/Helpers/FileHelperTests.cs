// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    [TestFixture]
    public class FileHelperTests
    {
        [Test]
        [TestCase("src/test/file.cs", new[] { "**/test/**" }, true)]
        [TestCase("src/test/subfolder/file.cs", new[] { "**/test/**" }, true)]
        [TestCase("src/main/file.cs", new[] { "**/test/**" }, false)]
        [TestCase("file.tmp", new[] { "*.tmp" }, true)]
        [TestCase("file.cs", new[] { "*.tmp" }, false)]
        [TestCase("bin/Debug/file.dll", new[] { "bin/**" }, true)]
        [TestCase("obj/Release/file.obj", new[] { "obj/**" }, true)]
        [TestCase("src/file.cs", new[] { "bin/**", "obj/**" }, false)]
        [TestCase("bin/file.dll", new[] { "bin/**", "obj/**" }, true)]
        [TestCase("node_modules/package/file.js", new[] { "**/node_modules/**" }, true)]
        [TestCase("src/node_modules/file.js", new[] { "**/node_modules/**" }, true)]
        [TestCase("src/file.js", new[] { "**/node_modules/**" }, false)]
        [TestCase("Bin/Debug/file.dll", new[] { "bin/**" }, true)]
        [TestCase("BIN/DEBUG/FILE.DLL", new[] { "bin/**" }, true)]
        [TestCase("test.tmp", new[] { "*.tmp", "*.bak", "**/test/**" }, true)]
        [TestCase("test.bak", new[] { "*.tmp", "*.bak", "**/test/**" }, true)]
        [TestCase("src/test/file.cs", new[] { "*.tmp", "*.bak", "**/test/**" }, true)]
        [TestCase("src/main/file.cs", new[] { "*.tmp", "*.bak", "**/test/**" }, false)]
        public void IsMatchingExcludePattern_ShouldReturnExpectedResult(string filePath, string[] patterns, bool expected)
        {
            var result = FileHelper.IsMatchingExcludePattern(filePath, patterns);

            Assert.That(result, Is.EqualTo(expected), 
                $"Path '{filePath}' with patterns [{string.Join(", ", patterns)}] should return {expected} but returned {result}");
        }

        [Test]
        public void IsMatchingExcludePattern_WithEmptyPatterns_ShouldReturnFalse()
        {
            var filePath = "any/file.cs";
            var patterns = Array.Empty<string>();

            var result = FileHelper.IsMatchingExcludePattern(filePath, patterns);

            Assert.That(result, Is.False);
        }

        [Test]
        public void IsMatchingExcludePattern_WithNullPatterns_ShouldReturnFalse()
        {
            var filePath = "any/file.cs";
            string[]? patterns = null;

            var result = FileHelper.IsMatchingExcludePattern(filePath, patterns!);

            Assert.That(result, Is.False);
        }

        [Test]
        public void IsMatchingExcludePattern_WithEmptyFilePath_ShouldReturnFalse()
        {
            var filePath = "";
            var patterns = new[] { "*.tmp" };

            var result = FileHelper.IsMatchingExcludePattern(filePath, patterns);

            Assert.That(result, Is.False);
        }
    }
}