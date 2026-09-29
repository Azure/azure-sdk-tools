// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// General use sanitizer for cleaning URIs via regex.
    /// </summary>
    public class UriRegexSanitizer : RecordedTestSanitizer
    {
        private readonly string _newValue;
        private readonly string _groupForReplace = null;

        private readonly Regex _regex;

        /// <summary>
        /// Runs a regex replace on the member of your choice.
        /// </summary>
        /// <param name="value">The substitution value.</param>
        /// <param name="regex">A regex. Can be defined as a simple regex replace OR if groupForReplace is set, a subsitution operation.</param>
        /// <param name="groupForReplace">The capture group that needs to be operated upon. Do not set if you're invoking a simple replacement operation.</param>
        /// <param name="condition">
        /// A condition that dictates when this sanitizer applies to a request/response pair. The content of this key should be a JSON object that contains configuration keys.
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'. Defaults to "apply always."
        /// </param>
        public UriRegexSanitizer(string value = "Sanitized", string regex = null, string groupForReplace = null, ApplyCondition condition = null)
            : this(GetRegex(regex), value, groupForReplace, condition)
        {
        }

        internal UriRegexSanitizer(Regex regex, string value = "Sanitized", string groupForReplace = null, ApplyCondition condition = null)
        {
            _scope = SanitizerScope.Uri;
            _newValue = value;
            _groupForReplace = groupForReplace;

            Condition = condition;

            _regex = regex;
        }

        public override string SanitizeUri(string uri)
        {
            return StringSanitizer.SanitizeValue(uri, _newValue, _regex, _groupForReplace);
        }
    }
}
