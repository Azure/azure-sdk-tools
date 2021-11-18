using System.Collections.Generic;
using System.Text;
using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Matchers
{
    public class HeaderLessMatcher : RecordMatcher
    {
        /// <summary>
        /// This matcher adjusts the "match" operation to ignore header differences when matching a request.
        /// </summary>
        public HeaderLessMatcher() : base(true)
        {
        }

        public override int CompareHeaderDictionaries(SortedDictionary<string, string[]> headers, SortedDictionary<string, string[]> entryHeaders, HashSet<string> ignoredHeaders, StringBuilder descriptionBuilder = null)
        {
            return 0;
        }
    }
}
