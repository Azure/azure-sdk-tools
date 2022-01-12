using Azure.Sdk.Tools.TestProxy.Common;
using System.Collections.Generic;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer operates on a RecordSession entry and applies itself to the Request and Response bodies contained therein. This "general" sanitizer applies the configured string value replacement
    /// to headers, body, and URI. 
    /// </summary>
    public class GeneralStringSanitizer : RecordedTestSanitizer
    {
        private string _newValue;
        private string _targetValue;

        private BodyStringSanitizer _bodySanitizer;
        private UriStringSanitizer _uriSanitizer;

        /// <summary>
        /// This sanitizer offers a value replace across request/response Body, Headers, and URI. For the body, this means a string replacement applied directly to the raw JSON. 
        /// </summary>
        /// <param name="value">The substitution value.</param>
        /// <param name="target">A target string. This could contain special regex characters like "?()+*" but they will be treated as a literal.</param>
        /// <param name="condition">
        /// A condition that dictates when this sanitizer applies to a request/response pair. The content of this key should be a JSON object that contains configuration keys. 
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'. Defaults to "apply always."
        /// </param>
        public GeneralStringSanitizer(string target, string value = "Sanitized", ApplyCondition condition = null)
        {
            _targetValue = target;
            _newValue = value;
            Condition = condition;

            _bodySanitizer = new BodyStringSanitizer(target, value, condition);
            _uriSanitizer = new UriStringSanitizer(target, value, condition);
        }

        public override void SanitizeHeaders(IDictionary<string, string[]> headers)
        {
            foreach (var headerKey in headers.Keys)
            {
                // Accessing 0th key safe due to the fact that we force header values in without splitting them on ;. 
                // We do this because letting .NET split and then reassemble header values introduces a space into the header itself
                // Ex: "application/json;odata=minimalmetadata" with .NET default header parsing becomes "application/json; odata=minimalmetadata"
                // Given this breaks signature verification, we have to avoid it.
                var originalValue = headers[headerKey][0];

                var replacement = StringSanitizer.ReplaceValue(inputValue: originalValue, targetValue: _targetValue, replacementValue: _newValue);

                headers[headerKey][0] = replacement;
            }
        }

        public override string SanitizeUri(string uri)
        {
            return _uriSanitizer.SanitizeUri(uri);
        }

        public override string SanitizeTextBody(string contentType, string body)
        {
            return _bodySanitizer.SanitizeTextBody(contentType, body);
        }

        public override byte[] SanitizeBody(string contentType, byte[] body)
        {
            return _bodySanitizer.SanitizeBody(contentType, body);
        }
    }
}
