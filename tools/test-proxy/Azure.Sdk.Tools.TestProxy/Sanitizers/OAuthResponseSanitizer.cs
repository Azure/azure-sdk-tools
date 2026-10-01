// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer applies at the session level, just before saving a recording to disk.
    /// 
    /// It cleans out all request/response pairs that match an oauth regex in their URI.
    /// </summary>
    public class OAuthResponseSanitizer : RecordedTestSanitizer
    {
        /// <summary>
        /// There are no customizations available for this sanitizer.
        /// </summary>
        public OAuthResponseSanitizer() { }

        public override void Sanitize(RecordSession session)
        {
            session.Entries.RemoveAll(x => SharedRegexes.OAuth2Token().IsMatch(x.RequestUri));
        }
    }
}
