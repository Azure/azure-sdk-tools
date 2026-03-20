using NUnit.Framework;

namespace Azure.Sdk.Tools.SnippetGenerator.TestData
{
    public class MarkdownOnlySnippets
    {
        [Test]
        public void MarkdownOnlyIndentation()
        {
            #region Snippet:MarkdownOnlyIndentation
            //@@ var blobClient = new BlobClient(
            //@@     new Uri("https://example.com"),
            //@@         credential);
            var blobClient = new object();
            #endregion
        }

        [Test]
        public void MarkdownOnlyNoSpace()
        {
            #region Snippet:MarkdownOnlyNoSpace
            //@@var x = 1;
            //@@var y = 2;
            var blobClient = new object();
            #endregion
        }
    }
}
