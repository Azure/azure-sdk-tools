using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    public class HeaderRegexSanitizer : RecordedTestSanitizer
    {
        private string _targetKey;
        private string _newValue;
        private Regex _regex = null;
        private string _groupForReplace = null;

        public HeaderRegexSanitizer(string key, string value = "Sanitized", string regex = null, string groupForReplace = null)
        {
            _targetKey = key;
            _newValue = value;

            if (regex != null)
            {
                _regex = new Regex(regex, RegexOptions.Compiled);
            }

            if (groupForReplace != null)
            {
                _regex = new Regex(regex, RegexOptions.Compiled);
            }
        }

        public override void SanitizeHeaders(IDictionary<string, string[]> headers)
        {
            // if there is a match
            if (headers.ContainsKey(_targetKey))
            {
                // replace with a group
                headers[_targetKey] = new string[] { _newValue };
            }
        }
    }
}
