using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.Text;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer operates on a RecordSession entry and applies regex replacement to the Request and Response bodies contained therein. It ONLY operates on the request/response bodies. Not header or URIs.
    /// </summary>
    public class BodyRegexSanitizer : RecordedTestSanitizer
    {
        private string _newValue;
        private string _regexValue = null;
        private string _groupForReplace = null;

        /// <summary>
        /// This sanitizer offers regex replace within a returned body. Specifically, this means regex applying to the raw JSON. If you are attempting to simply 
        /// replace a specific key, the BodyKeySanitizer is probably the way to go. Regardless, there are examples present in SanitizerTests.cs.
        /// to 
        /// </summary>
        /// <param name="value">The substitution value.</param>
        /// <param name="regex">A regex. Can be defined as a simple regex replace OR if groupForReplace is set, a subsitution operation.</param>
        /// <param name="groupForReplace">The capture group that needs to be operated upon. Do not set if you're invoking a simple replacement operation.</param>
        /// <param name="condition">
        /// A condition that dictates when this sanitizer applies to a request/response pair. The content of this key should be a JSON object that contains configuration keys. 
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'. Defaults to "apply always."
        /// </param>
        public BodyRegexSanitizer(string value = "Sanitized", string regex = null, string groupForReplace = null, ApplyCondition condition = null)
        {
            _newValue = value;
            _regexValue = regex;
            _groupForReplace = groupForReplace;
            Condition = condition;

            StringSanitizer.ConfirmValidRegex(regex);
        }

        public override string SanitizeTextBody(string contentType, string body)
        {
            return StringSanitizer.SanitizeValue(body, _newValue, _regexValue, _groupForReplace);
        }
    }
}
