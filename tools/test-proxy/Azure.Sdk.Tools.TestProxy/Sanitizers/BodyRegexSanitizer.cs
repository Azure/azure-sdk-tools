using Azure.Sdk.Tools.TestProxy.Common;
using System;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer operates on a RecordSession entry and applies itself to the Request and Response bodies contained therein.
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
        public BodyRegexSanitizer(string value = "Sanitized", string regex = null, string groupForReplace = null)
        {
            _newValue = value;
            _regexValue = regex;
            _groupForReplace = groupForReplace;
        }

        public override string SanitizeTextBody(string contentType, string body)
        {
            return StringSanitizer.SanitizeValue(body, _newValue, _regexValue, _groupForReplace);
        }


        public override byte[] SanitizeBody(string contentType, byte[] body)
        {
            return body;
        }
    }
}
