using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Matchers
{
    /// <summary>
    /// This matcher exposes the default matcher in a customizable way. Currently this merely includes enabling/disabling body match and adding additional excluded headers. The range of customizability will extend past these initial two.
    /// </summary>
    public class CustomDefaultMatcher : RecordMatcher
    {
        /// <summary>
        /// All optional settings are safely defaulted. This means that providing zero additional configuration will produce a sanitizer that is functionally identical to the default.
        /// </summary>
        /// <param name="compareBodies">Should the body value be compared during lookup operations?</param>
        /// <param name="excludedHeaders">A comma separated list of additional headers that should be excluded during matching. "Excluded" headers are entirely ignored. Unlike "ignored" headers, the presence (or lack of presence) of a header
        /// will not cause mismatch.</param>
        /// <param name="ignoredHeaders">A comma separated list of additional headers that should be ignored during matching. Any headers that are "ignored" will not do value comparison when matching. This means that if the recording has a header
        /// that isn't in the request, a test mismatch exception will be thrown noting the lack of header in the request. This also applies if the header is present in the request but not recording.</param>
        /// <param name="ignoreQueryOrdering">By default, the test-proxy does not sort query params before matching. Setting true will sort query params alphabetically before comparing URI.</param>
        /// <param name="ignoredQueryParameters">A comma separated list of query parameterse that should be ignored during matching.</param>
        public CustomDefaultMatcher(bool compareBodies = true, string excludedHeaders = "", string ignoredHeaders = "", bool ignoreQueryOrdering = false, string ignoredQueryParameters = "")
            : base(compareBodies: compareBodies, ignoreQueryOrdering: ignoreQueryOrdering)
        {
            const StringSplitOptions splitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;
            foreach(var exclusion in excludedHeaders.Split(",", splitOptions))
            {
                ExcludeHeaders.Add(exclusion);
            }

            foreach (var exclusion in ignoredHeaders.Split(",", splitOptions))
            {
                IgnoredHeaders.Add(exclusion);
            }

            foreach (var exclusion in ignoredQueryParameters.Split(",", splitOptions))
            {
                IgnoredQueryParameters.Add(exclusion);
            }
        }
    }
}
