using NUnit.Framework;

namespace Azure.Sdk.Tools.SnippetGenerator.TestData
{
    public class Snippets
    {
        [Test]
        public void EmptySnippet()
        {
#if SHOULD_NEVER_BE_DEFINED
            #region Snippet:EmptySnippet
            throw new NotImplementedException();
            #endregion
#endif
        }
    }
}
