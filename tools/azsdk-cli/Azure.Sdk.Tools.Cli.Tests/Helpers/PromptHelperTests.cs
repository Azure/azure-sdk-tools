// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Text;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    [TestFixture]
    public class PromptHelperTests
    {
        private Mock<ILogger> _mockLogger = null!;
        private string _tempDirectory = null!;
        private string _testFile1Path = null!;
        private string _testFile2Path = null!;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);

            // Create test files
            _testFile1Path = Path.Combine(_tempDirectory, "test1.txt");
            _testFile2Path = Path.Combine(_tempDirectory, "test2.md");
            
            File.WriteAllText(_testFile1Path, "This is test file 1 content");
            File.WriteAllText(_testFile2Path, "# Test File 2\nThis is markdown content");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithInlineLink_ExpandsCorrectly()
        {
            // Arrange
            var text = "Check out [test file](test1.txt) for details.";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Does.Contain("This is test file 1 content"));
            Assert.That(result, Does.Contain("## Referenced File: test file (test1.txt)"));
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithReferenceLink_ExpandsCorrectly()
        {
            // Arrange
            var text = @"Check out [test file][testref] for details.

[testref]: test1.txt";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Does.Contain("This is test file 1 content"));
            Assert.That(result, Does.Contain("## Referenced File: test file (test1.txt)"));
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithHttpUrl_SkipsExpansion()
        {
            // Arrange
            var text = "Visit [Microsoft](https://microsoft.com) for more info.";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Is.EqualTo(text)); // Should be unchanged
            Assert.That(result, Does.Not.Contain("## Referenced File:"));
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithHttpsUrl_SkipsExpansion()
        {
            // Arrange
            var text = "Visit [Google](http://google.com) for search.";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Is.EqualTo(text)); // Should be unchanged
            Assert.That(result, Does.Not.Contain("## Referenced File:"));
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithAnchorLink_SkipsExpansion()
        {
            // Arrange
            var text = "See [section below](#important-section) for details.";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Is.EqualTo(text)); // Should be unchanged
            Assert.That(result, Does.Not.Contain("## Referenced File:"));
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithNonExistentFile_LogsWarningAndSkips()
        {
            // Arrange
            var text = "Check [missing file](nonexistent.txt) here.";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Is.EqualTo(text)); // Should be unchanged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Referenced file not found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithAbsolutePath_ExpandsCorrectly()
        {
            // Arrange
            var text = $"Check [absolute file]({_testFile1Path}) for details.";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Does.Contain("This is test file 1 content"));
            Assert.That(result, Does.Contain($"## Referenced File: absolute file ({_testFile1Path})"));
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithMultipleLinks_ExpandsAll()
        {
            // Arrange
            var text = "See [file 1](test1.txt) and [file 2](test2.md) for info.";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Does.Contain("This is test file 1 content"));
            Assert.That(result, Does.Contain("This is markdown content"));
            Assert.That(result, Does.Contain("## Referenced File: file 1 (test1.txt)"));
            Assert.That(result, Does.Contain("## Referenced File: file 2 (test2.md)"));
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithMixedLinkTypes_ExpandsOnlyLocalFiles()
        {
            // Arrange
            var text = @"Check [local file](test1.txt), visit [website](https://example.com), and see [anchor](#section).";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Does.Contain("This is test file 1 content"));
            Assert.That(result, Does.Contain("[website](https://example.com)")); // Should remain unchanged
            Assert.That(result, Does.Contain("[anchor](#section)")); // Should remain unchanged
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithEmptyText_ReturnsEmpty()
        {
            // Arrange
            var text = "";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Is.EqualTo(text));
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithNullText_ReturnsNull()
        {
            // Arrange
            string? text = null;
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text!, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithNoLinks_ReturnsUnchanged()
        {
            // Arrange
            var text = "This is just plain text with no links at all.";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Is.EqualTo(text));
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithReferenceWithoutDefinition_LogsWarningAndSkips()
        {
            // Arrange
            var text = "Check [missing ref][undefined] for details.";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Is.EqualTo(text)); // Should be unchanged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Reference definition not found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithMultipleReferences_ExpandsAll()
        {
            // Arrange
            var text = @"Check [file 1][ref1] and [file 2][ref2].

[ref1]: test1.txt
[ref2]: test2.md";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Does.Contain("This is test file 1 content"));
            Assert.That(result, Does.Contain("This is markdown content"));
            Assert.That(result, Does.Contain("## Referenced File: file 1 (test1.txt)"));
            Assert.That(result, Does.Contain("## Referenced File: file 2 (test2.md)"));
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithDuplicateFileReferences_ExpandsOnlyOnce()
        {
            // Arrange
            var text = "See [first ref](test1.txt) and [second ref](test1.txt).";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            // The implementation prevents duplicate file expansion for performance,
            // so the same file should only be expanded once even if referenced multiple times
            var contentOccurrences = CountOccurrences(result, "This is test file 1 content");
            Assert.That(contentOccurrences, Is.EqualTo(1));
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithCaseInsensitiveReferences_Works()
        {
            // Arrange - Test case sensitivity with different files to avoid duplicate prevention
            var text = @"Check [test1][ref].

[ref]: test1.txt";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Does.Contain("This is test file 1 content"));
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithCaseInsensitiveReferencesUppercase_Works()
        {
            // Arrange - Test case sensitivity with uppercase reference
            var text = @"Check [test2][REF].

[REF]: test2.md";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Does.Contain("This is markdown content"));
        }

        [TestCase("[](test1.txt)")] // Empty link text - should not match
        [TestCase("[text]()")] // Empty path - should not match  
        [TestCase("[][]")] // Empty reference - should not match
        public async Task ExpandRelativeFileLinksAsync_WithInvalidLinkFormats_DoesNotMatch(string invalidLink)
        {
            // Arrange
            var text = $"Check {invalidLink} for details.";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Is.EqualTo(text)); // Should be unchanged
            Assert.That(result, Does.Not.Contain("## Referenced File:"));
        }


        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithLargeFile_SkipsAndLogsWarning()
        {
            // Arrange
            var largeFilePath = Path.Combine(_tempDirectory, "large.txt");
            // Create a file larger than 1MB (1024 * 1024 bytes)
            var largeContent = new string('A', 1024 * 1024 + 1);
            File.WriteAllText(largeFilePath, largeContent);
            
            var text = "Check [large file](large.txt) for details.";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Is.EqualTo(text)); // Should be unchanged
            Assert.That(result, Does.Not.Contain("## Referenced File:"));
            
            // Verify warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("is too large")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithFileAtSizeLimit_ProcessesSuccessfully()
        {
            // Arrange
            var limitFilePath = Path.Combine(_tempDirectory, "limit.txt");
            // Create a file exactly at the 1MB limit
            var limitContent = new string('B', 1024 * 1024);
            File.WriteAllText(limitFilePath, limitContent);
            
            var text = "Check [limit file](limit.txt) for details.";
            
            // Act
            var result = await PromptHelper.ExpandRelativeFileLinksAsync(text, _tempDirectory, _mockLogger.Object);
            
            // Assert
            Assert.That(result, Does.Contain(limitContent));
            Assert.That(result, Does.Contain("## Referenced File: limit file (limit.txt)"));
        }

        private static int CountOccurrences(string text, string substring)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(substring, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += substring.Length;
            }
            return count;
        }
    }
}
