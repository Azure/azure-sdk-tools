using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// General use sanitizer for cleaning URIs via straightforward string replacement.
    /// </summary>
    public class UriStringSanitizer : RecordedTestSanitizer
    {
        private string _newValue;
        private string _targetValue;

        /// <summary>
        /// Runs a simple string replacement against the request/response URIs.
        /// </summary>
        /// <param name="value">The substitution value.</param>
        /// <param name="target">A target string. This could contain special regex characters like "?()+*" but they will be treated as a literal.</param>
        /// <param name="condition">
        /// A condition that dictates when this sanitizer applies to a request/response pair. The content of this key should be a JSON object that contains configuration keys. 
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'. Defaults to "apply always."
        /// </param>
        public UriStringSanitizer(string target, string value = "Sanitized", ApplyCondition condition = null)
        {
            _targetValue = target;
            _newValue = value;
            Condition = condition;
        }

        public override string SanitizeUri(string uri)
        {
            return StringSanitizer.ReplaceValue(inputValue: uri, targetValue: _targetValue, replacementValue: _newValue);
        }
    }
}
