using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// General use sanitizer for cleaning URIs via regex.
    /// </summary>
    public class UriRegexSanitizer : RecordedTestSanitizer
    {
        private Regex _regex;
        private string _newValue;
        
        /// <summary>
        /// Runs a regex replace on the member of your choice.
        /// </summary>
        /// <param name="regex">The regex used to replace.</param>
        /// <param name="value">The substituted value.</param>
        public UriRegexSanitizer(string regex, string value = "Sanitized")
        {
            _regex = new Regex(regex, RegexOptions.Compiled);
            _newValue = value;
        }

        public override string SanitizeUri(string uri)
        {
            return _regex.Replace(uri, _newValue);
        }
    }
}
