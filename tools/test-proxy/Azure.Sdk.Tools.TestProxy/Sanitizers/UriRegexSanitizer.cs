using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// General use sanitizer for cleaning URIs via regex.
    /// </summary>
    public class UriRegexSanitizer : RecordedTestSanitizer
    {
        private string _newValue;
        private string _regexValue = null;
        private string _groupForReplace = null;


        /// <summary>
        /// Runs a regex replace on the member of your choice.
        /// </summary>
        /// <param name="value">The substitution value.</param>
        /// <param name="regex">A regex. Can be defined as a simple regex replace OR if groupForReplace is set, a subsitution operation.</param>
        /// <param name="groupForReplace">The capture group that needs to be operated upon. Do not set if you're invoking a simple replacement operation.</param>
        public UriRegexSanitizer(string value = "Sanitized", string regex = null, string groupForReplace = null)
        {
            _regexValue = regex;
            _newValue = value;
            _groupForReplace = groupForReplace;
        }

        public override string SanitizeUri(string uri)
        {
            return StringSanitizer.SanitizeValue(uri, _newValue, _regexValue, _groupForReplace);
        }
    }
}
