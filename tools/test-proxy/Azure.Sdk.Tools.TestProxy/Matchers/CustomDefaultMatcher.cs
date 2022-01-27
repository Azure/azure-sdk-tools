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
        /// <param name="excludedHeaders">A comma separated list of additional headers that should be excluded during matching.</param>
        /// <param name="ignoredHeaders">A comma separated list of additional headers that should be ignored during matching.</param>
        /// <param name="ignoreQueryOrdering">By default, the test-proxy does not sort query params before matching. Setting true will sort query params alphabetically before comparing URI.</param>
        public CustomDefaultMatcher(bool compareBodies = true, string excludedHeaders = "", string ignoredHeaders = "", bool ignoreQueryOrdering = false) : base(compareBodies: compareBodies, ignoreQueryOrdering: ignoreQueryOrdering)
        {
            foreach(var exclusion in excludedHeaders.Split(",").Where(x => !string.IsNullOrEmpty(x.Trim())))
            {
                if (!ExcludeHeaders.Contains(exclusion)){
                    ExcludeHeaders.Add(exclusion);
                }
            }

            foreach (var exclusion in ignoredHeaders.Split(",").Where(x => !string.IsNullOrEmpty(x.Trim())))
            {
                if (!IgnoredHeaders.Contains(exclusion))
                {
                    IgnoredHeaders.Add(exclusion);
                }
            }
        }
    }
}
