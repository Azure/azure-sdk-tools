using Azure.Sdk.Tools.TestProxy.Common;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer operates on a RecordSession entry and applies itself to the headers contained therein. This sanitizer ONLY applies to the request/response headers, 
    /// body and URI are left untouched.
    /// </summary>
    public class HeaderRegexSanitizer : RecordedTestSanitizer
    {
        private string _targetKey;
        private string _newValue;
        private string _regexValue = null;
        private string _groupForReplace = null;

        /// <summary>
        /// Can be used for multiple purposes:
        ///     1) To replace a key with a specific value, do not set "regex" value.
        ///     2) To do a simple regex replace operation, define arguments "key", "value", and "regex"
        ///     3) To do a targeted substitution of a specific group, define all arguments "key", "value", and "regex"
        /// </summary>
        /// <param name="key">The name of the header we're operating against.</param>
        /// <param name="value">The substitution or whole new header value, depending on "regex" setting.</param>
        /// <param name="regex">A regex. Can be defined as a simple regex replace OR if groupForReplace is set, a subsitution operation.</param>
        /// <param name="groupForReplace">The capture group that needs to be operated upon. Do not set if you're invoking a simple replacement operation.</param>
        /// <param name="condition">
        /// A condition that dictates when this sanitizer applies to a request/response pair. The content of this key should be a JSON object that contains configuration keys. 
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'. Defaults to "apply always."
        /// </param>
        public HeaderRegexSanitizer(string key, string value = "Sanitized", string regex = ".+", string groupForReplace = null, ApplyCondition condition = null)
        {
            _targetKey = key;
            _newValue = value;
            _regexValue = regex;
            _groupForReplace = groupForReplace;
            Condition = condition;

            StringSanitizer.ConfirmValidRegex(regex);
        }

        public override void SanitizeHeaders(IDictionary<string, string[]> headers)
        {
            if (headers.ContainsKey(_targetKey))
            {
                // Accessing 0th key safe due to the fact that we force header values in without splitting them on ;. 
                // We do this because letting .NET split and then reassemble header values introduces a space into the header itself
                // Ex: "application/json;odata=minimalmetadata" with .NET default header parsing becomes "application/json; odata=minimalmetadata"
                // Given this breaks signature verification, we have to avoid it.
                headers[_targetKey] = headers[_targetKey].Select(x => StringSanitizer.SanitizeValue(x, _newValue, _regexValue, _groupForReplace)).ToArray();
            }
        }
    }
}
