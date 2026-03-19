using NUnit.Framework;

namespace Azure.Sdk.Tools.SnippetGenerator.TestData
{
    public class MarkdownOnlySnippets
    {
        [Test]
        public void MarkdownOnlyIndentation()
        {
            #region Snippet:MarkdownOnlyIndentation
            //@@var blobClient = new BlobClient(
            //@@    new Uri("https://example.com"),
            //@@        credential);
            var blobClient = new object();
            #endregion
        }
    }
}
