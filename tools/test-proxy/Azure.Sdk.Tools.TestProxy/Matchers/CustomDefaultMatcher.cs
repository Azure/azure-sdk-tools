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
        /// Both compareBodies and nonDefaultHeaderExclusions are safely defaulted for this constructor. This means that providing neither of them will produce a matcher that is functionally identical to the default.
        /// </summary>
        /// <param name="compareBodies">Should the body value be compared during lookup operations?</param>
        /// <param name="nonDefaultHeaderExclusions">A comma separated list of additional headers that should be excluded during matching.</param>
        public CustomDefaultMatcher(bool compareBodies = true, string nonDefaultHeaderExclusions = "") : base(compareBodies: compareBodies)
        {
            foreach(var exclusion in nonDefaultHeaderExclusions.Split(",").Where(x => !string.IsNullOrEmpty(x.Trim())))
            {
                if (!ExcludeHeaders.Contains(exclusion)){
                    ExcludeHeaders.Add(exclusion);
                }
            }
        }
    }
}
