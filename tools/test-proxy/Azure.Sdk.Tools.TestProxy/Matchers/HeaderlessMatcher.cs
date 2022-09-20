using System.Collections.Generic;
using System.Text;
using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Matchers
{
    public class HeaderlessMatcher : RecordMatcher
    {
        /// <summary>
        /// This matcher adjusts the "match" operation to ignore header differences when matching a request. Be aware that wholly ignoring headers during matching might incur unexpected issues down the line.
        /// </summary>
        public HeaderlessMatcher() : base(true)
        {
        }

        public override int CompareHeaderDictionaries(SortedDictionary<string, string[]> headers, SortedDictionary<string, string[]> entryHeaders, HashSet<string> ignoredHeaders, HashSet<string> excludedHeaders, StringBuilder descriptionBuilder = null)
        {
            return 0;
        }
    }
}
