using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    public class RegexUriReplace : RecordedTestSanitizer
    {
        private Regex _regex;
        private string _newValue;
        
        public RegexUriReplace(string regex, string value = "Sanitized")
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
