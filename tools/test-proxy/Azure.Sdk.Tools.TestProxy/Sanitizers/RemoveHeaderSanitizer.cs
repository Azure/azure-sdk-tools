using Azure.Sdk.Tools.TestProxy.Common;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// A simple sanitizer that should be used to clean out one or multiple headers by their key. As could be determined by the description, this sanitizer only applies to the 
    /// request/response headers.
    /// </summary>
    public class RemoveHeaderSanitizer : RecordedTestSanitizer
    {
        private string[] _keysForRemoval;

        /// <summary>
        /// Removes headers from before saving a recording.
        /// </summary>
        /// <param name="headersForRemoval">A comma separated list. Should look like "Location, Transfer-Encoding" or something along those lines! Don't worry about whitespace
        /// between the commas separating each key. They will be ignored.</param>
        /// <param name="condition">
        /// A condition that dictates when this sanitizer applies to a request/response pair. The content of this key should be a JSON object that contains configuration keys. 
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'. Defaults to "apply always."
        /// </param>
        public RemoveHeaderSanitizer(string headersForRemoval, ApplyCondition condition = null)
        {
            Condition = condition;

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
