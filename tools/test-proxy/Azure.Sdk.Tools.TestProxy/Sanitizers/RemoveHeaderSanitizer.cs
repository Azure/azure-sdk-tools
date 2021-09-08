using Azure.Sdk.Tools.TestProxy.Common;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// A simple sanitizer that should be used to clean out one or multiple headers by their key.
    /// </summary>
    public class RemoveHeaderSanitizer : RecordedTestSanitizer
    {
        private string[] _keysForRemoval;

        /// <summary>
        /// Removes headers from before saving a recording.
        /// </summary>
        /// <param name="headersForRemoval">A comma separated list. Should look like "Location, Transfer-Encoding" or something along those lines! Don't worry about whitespace
        /// between the commas separating each key. They will be ignored.</param>
        public RemoveHeaderSanitizer(string headersForRemoval)
        {
            _keysForRemoval = headersForRemoval.Split(",").Select(x => x.Trim()).ToArray();
        }

        public override void SanitizeHeaders(IDictionary<string, string[]> headers)
        {
            foreach (var headerKey in _keysForRemoval)
            {
                if (headers.ContainsKey(headerKey))
                {
                    headers.Remove(headerKey);
                }

            }
        }
    }
}
