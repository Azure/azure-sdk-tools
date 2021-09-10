using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Matchers
{
    public class BodilessMatcher : RecordMatcher
    {
        /// <summary>
        /// This matcher adjusts the "match" operation to EXCLUDE the body when matching a request to a recording's entries.
        /// </summary>
        public BodilessMatcher() : base(false) { }
    }
}
