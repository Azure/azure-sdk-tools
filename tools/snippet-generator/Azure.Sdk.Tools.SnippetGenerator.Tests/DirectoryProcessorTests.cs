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
    }
}
