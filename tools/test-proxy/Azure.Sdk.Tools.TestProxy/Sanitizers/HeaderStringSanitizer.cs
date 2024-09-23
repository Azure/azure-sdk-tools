using Azure.Sdk.Tools.TestProxy.Common;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer operates on a RecordSession entry and applies value replacement to the headers contained therein. This sanitizer ONLY applies to the request/response headers, 
    /// body and URI are left untouched.
    /// </summary>
    public class HeaderStringSanitizer : RecordedTestSanitizer
    {
        private string _targetKey;
        private string _newValue;
        private string _targetValue;

        /// <summary>
        /// Applies a simple value replacement for a target header key. If it does not exist, no actions will be taken.
        /// </summary>
        /// <param name="key">The name of the header we're operating against.</param>
        /// <param name="target">A target string. This could contain special regex characters like "?()+*" but they will be treated as a literal.</param>
        /// <param name="value">The substitution value.</param>
        /// <param name="condition">
        /// A condition that dictates when this sanitizer applies to a request/response pair. The content of this key should be a JSON object that contains configuration keys. 
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'. Defaults to "apply always."
        /// </param>
        public HeaderStringSanitizer(string key, string target, string value = "Sanitized", ApplyCondition condition = null)
        {
            _targetKey = key;
            _newValue = value;
            _targetValue = target;
            Condition = condition;
        }

        public override void SanitizeHeaders(IDictionary<string, string[]> headers)
        {
            if (headers.ContainsKey(_targetKey))
            {
                // Accessing 0th key safe due to the fact that we force header values in without splitting them on ;. 
                // We do this because letting .NET split and then reassemble header values introduces a space into the header itself
                // Ex: "application/json;odata=minimalmetadata" with .NET default header parsing becomes "application/json; odata=minimalmetadata"
                // Given this breaks signature verification, we have to avoid it.
                headers[_targetKey] = headers[_targetKey].Select(x => StringSanitizer.ReplaceValue(inputValue: x, targetValue: _targetValue, replacementValue: _newValue)).ToArray();
            }
        }
    }
}
