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
        private string _regexValue = null;
        private string _groupForReplace = null;

        public HeaderRegexSanitizer(string key, string value = "Sanitized", string regex = null, string groupForReplace = null)
        {
            _targetKey = key;
            _newValue = value;
            _regexValue = regex;
            _groupForReplace = groupForReplace;
        }

        public override void SanitizeHeaders(IDictionary<string, string[]> headers)
        {
            // if there is a match
            if (headers.ContainsKey(_targetKey))
            {

                if(_regexValue == null)
                {
                    headers[_targetKey] = new string[] { _newValue };
                }
                // we only need to do a more complex replacement if one exists
                else
                {
                    var rx = new Regex(_regexValue);
                    var replacement = String.Empty;

                    if (_groupForReplace != null)
                    {
                        // do a targeted replace of just the specific group
                    }

                    headers[_targetKey] = new string[] { _newValue };
                }
            }
        }
    }
}
