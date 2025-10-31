using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    [TestFixture]
    public class PromptHelperTests
    {
        private ILogger _logger;

        [SetUp]
        public void SetUp()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<PromptHelperTests>();
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_StandaloneUsage_WorksCorrectly()
        {
            // Arrange
            var testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(testDir);

            try
            {
                var docsFile = Path.Combine(testDir, "sample-docs.md");
                var docsContent = @"# Test Documentation

This is a sample documentation file that can be referenced from prompts.

## Example Code

```csharp
public async Task<string> SampleMethod()
{
    return ""Hello from sample!"";
}
```

## Best Practices

- Always use async/await for file operations
- Include proper error handling
- Use cancellation tokens for long-running operations";

                var promptFile = Path.Combine(testDir, "test-prompt.md");
                var promptContent = @"Generate a comprehensive sample that demonstrates Azure Blob Storage operations.

Please refer to [Sample Documentation](sample-docs.md) for coding best practices.

The sample should include:
- Upload operations
- Download operations  
- Error handling";

                await File.WriteAllTextAsync(docsFile, docsContent);
                await File.WriteAllTextAsync(promptFile, promptContent);

                var prompt = await File.ReadAllTextAsync(promptFile);

                // Act
                var expandedPrompt = await PromptHelper.ExpandRelativeFileLinksAsync(prompt, testDir, _logger);

                // Assert
                Assert.That(expandedPrompt, Contains.Substring("## Referenced File: Sample Documentation (sample-docs.md)"));
                Assert.That(expandedPrompt, Contains.Substring("Always use async/await for file operations"));
                Assert.That(expandedPrompt, Does.Not.Contain("[Sample Documentation](sample-docs.md)"));
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }

        [Test]
        public async Task ExpandRelativeFileLinksAsync_WithMultipleLinks_ExpandsAll()
        {
            // Arrange  
            var testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(testDir);

            try
            {
                var file1 = Path.Combine(testDir, "doc1.md");
                var file2 = Path.Combine(testDir, "doc2.md");
                await File.WriteAllTextAsync(file1, "Content from doc1");
                await File.WriteAllTextAsync(file2, "Content from doc2");

                var prompt = "See [Doc 1](doc1.md) and [Doc 2](doc2.md) for details.";

                // Act
                var result = await PromptHelper.ExpandRelativeFileLinksAsync(prompt, testDir, _logger);

                // Assert
                Assert.That(result, Contains.Substring("Content from doc1"));
                Assert.That(result, Contains.Substring("Content from doc2"));
                Assert.That(result, Contains.Substring("## Referenced File: Doc 1 (doc1.md)"));
                Assert.That(result, Contains.Substring("## Referenced File: Doc 2 (doc2.md)"));
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }
    }
}