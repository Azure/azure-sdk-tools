using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer operates on a RecordSession entry and applies itself to the headers containered therein.
    /// </summary>
    public class HeaderRegexSanitizer : RecordedTestSanitizer
    {
        private string _targetKey;
        private string _newValue;
        private string _regexValue = null;
        private string _groupForReplace = null;

        /// <summary>
        /// Can be used for multiple purposes:
        ///     1) To replace a key with a specific value, do not set "regex" value.
        ///     2) To do a simple regex replace operation, define arguments "key", "value", and "regex"
        ///     3) To do a targeted substitution of a specific group, define all arguments "key", "value", and "regex"
        /// </summary>
        /// <param name="key">The name of the header we're operating against.</param>
        /// <param name="value">The substitution or whole new header value, depending on "regex" setting.</param>
        /// <param name="regex">A regex. Can be defined as a simple regex replace OR if groupForReplace is set, a subsitution operation.</param>
        /// <param name="groupForReplace">The capture group that needs to be operated upon. Do not set if you're invoking a simple replacement operation.</param>
        public HeaderRegexSanitizer(string key, string value = "Sanitized", string regex = null, string groupForReplace = null)
        {
            _targetKey = key;
            _newValue = value;
            _regexValue = regex;
            _groupForReplace = groupForReplace;
        }

        public override void SanitizeHeaders(IDictionary<string, string[]> headers)
        {
            if (headers.ContainsKey(_targetKey))
            {
                var originalValue = headers[_targetKey][0];

                var replacement = StringSanitizer.SanitizeValue(originalValue, _newValue, _regexValue, _groupForReplace);

                headers[_targetKey] = new string[] { replacement };
            }
        }
    }
}
