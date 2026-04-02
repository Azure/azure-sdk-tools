using System;
using System.IO;
using NUnit.Framework;

namespace Azure.Sdk.Tools.SnippetGenerator.Tests
{
    public class DirectoryProcessorTests
    {
        [Test]
        public void DirectoryProcessorValidatesEmptySnippets()
        {
            var path = Path.Join(TestContext.CurrentContext.TestDirectory, "TestData");
            var files = new string[] { Path.Join(path, "EmptySnippet.md") };
            var sut = new DirectoryProcessor(path);

            InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.ProcessAsync(files));
            StringAssert.Contains("Snippet 'Snippet:EmptySnippet' is empty", ex.Message);
        }

        [Test]
        public async System.Threading.Tasks.Task MarkdownOnlyPrefixPreservesIndentation()
        {
            var path = Path.Join(TestContext.CurrentContext.TestDirectory, "TestData");
            var mdFile = Path.Join(path, "MarkdownOnlyIndentation.md");

            // Reset the markdown file to its original state
            File.WriteAllText(mdFile, "```C# Snippet:MarkdownOnlyIndentation\n```\n");

            var sut = new DirectoryProcessor(path);
            await sut.ProcessAsync(new[] { mdFile });

            var result = File.ReadAllText(mdFile);

            // The //@@-prefixed lines should preserve their relative indentation
            // Three distinct levels: 0 spaces, 4 spaces, 8 spaces
            StringAssert.Contains("var blobClient = new BlobClient(", result);
            StringAssert.Contains("    new Uri(", result);
            StringAssert.Contains("        credential);", result);
        }

        [Test]
        public async System.Threading.Tasks.Task MarkdownOnlyPrefixWorksWithoutSeparatorSpace()
        {
            var path = Path.Join(TestContext.CurrentContext.TestDirectory, "TestData");
            var mdFile = Path.Join(path, "MarkdownOnlyNoSpace.md");

            // Reset the markdown file to its original state
            File.WriteAllText(mdFile, "```C# Snippet:MarkdownOnlyNoSpace\n```\n");

            var sut = new DirectoryProcessor(path);
            await sut.ProcessAsync(new[] { mdFile });

            var result = File.ReadAllText(mdFile);

            // Without separator space, //@@content should still work
            StringAssert.Contains("var x = 1;", result);
            StringAssert.Contains("var y = 2;", result);
        }
    }
}
