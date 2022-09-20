using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.Text;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer operates on a RecordSession entry and applies value replacement to the Request and Response bodies contained therein. It ONLY operates on the request/response bodies. Not header or URIs.
    /// </summary>
    public class BodyStringSanitizer : RecordedTestSanitizer
    {
        private string _newValue;
        private string _targetValue;

        /// <summary>
        /// This sanitizer offers regex replace within a returned body. Specifically, this means regex applying to the raw JSON. If you are attempting to simply 
        /// replace a specific key, the BodyKeySanitizer is probably the way to go. Regardless, there are examples present in SanitizerTests.cs.
        /// </summary>
        /// <param name="target">A target string. This could contain special regex characters like "?()+*" but they will be treated as a literal.</param>
        /// <param name="value">The substitution value.</param>
        /// <param name="condition">
        /// A condition that dictates when this sanitizer applies to a request/response pair. The content of this key should be a JSON object that contains configuration keys. 
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'. Defaults to "apply always."
        /// </param>
        public BodyStringSanitizer(string target, string value = "Sanitized", ApplyCondition condition = null)
        {
            _targetValue = target;
            _newValue = value;
            Condition = condition;
        }

        public override string SanitizeTextBody(string contentType, string body)
        {
            return StringSanitizer.ReplaceValue(inputValue: body, targetValue: _targetValue, replacementValue: _newValue);
        }
    }
}
