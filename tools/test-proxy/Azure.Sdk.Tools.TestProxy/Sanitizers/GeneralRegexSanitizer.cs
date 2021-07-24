﻿using Azure.Sdk.Tools.TestProxy.Common;
using System.Collections.Generic;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer operates on a RecordSession entry and applies itself to the Request and Response bodies contained therein. This "general" sanitizer applies the configured regex replacement
    /// to headers, body, and URI. 
    /// </summary>
    public class GeneralRegexSanitizer : RecordedTestSanitizer
    {
        private string _newValue;
        private string _regexValue = null;
        private string _groupForReplace = null;

        private BodyRegexSanitizer _bodySanitizer;
        private UriRegexSanitizer _uriSanitizer;

        /// <summary>
        /// This sanitizer offers a general regex replace across request/response Body, Headers, and URI. For the body, this means regex applying to the raw JSON. 
        /// to 
        /// </summary>
        /// <param name="value">The substitution value.</param>
        /// <param name="regex">A regex. Can be defined as a simple regex replace OR if groupForReplace is set, a subsitution operation.</param>
        /// <param name="groupForReplace">The capture group that needs to be operated upon. Do not set if you're invoking a simple replacement operation.</param>
        public GeneralRegexSanitizer(string value = "Sanitized", string regex = ".*", string groupForReplace = null)
        {
            _newValue = value;
            _regexValue = regex;
            _groupForReplace = groupForReplace;

            _bodySanitizer = new BodyRegexSanitizer(value, regex, groupForReplace);
            _uriSanitizer = new UriRegexSanitizer(value, regex, groupForReplace);
        }

        public override void SanitizeHeaders(IDictionary<string, string[]> headers)
        {
            foreach (var headerKey in headers.Keys)
            {
                // Accessing 0th key safe due to the fact that we force header values in without splitting them on ;. 
                // We do this because letting .NET split and then reassemble header values introduces a space into the header itself
                // Ex: "application/json;odata=minimalmetadata" with .NET default header parsing becomes "application/json; odata=minimalmetadata"
                // Given this breaks signature verification, we have to avoid it.
                var originalValue = headers[headerKey][0];

                var replacement = StringSanitizer.SanitizeValue(originalValue, _newValue, _regexValue, _groupForReplace);

                headers[headerKey][0] = replacement;
            }
        }

        public override string SanitizeUri(string uri)
        {
            return _uriSanitizer.SanitizeUri(uri);
        }

        public override string SanitizeTextBody(string contentType, string body)
        {
            return _bodySanitizer.SanitizeTextBody(contentType, body);
        }

        public override byte[] SanitizeBody(string contentType, byte[] body)
        {
            return _bodySanitizer.SanitizeBody(contentType, body);
        }
    }
}
