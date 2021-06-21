using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer applies at the session level, just before saving a recording to disk.
    /// 
    /// It cleans out all request/response pairs that match an oauth regex in their URI.
    /// </summary>
    public class OAuthResponseSanitizer : RecordedTestSanitizer
    {
        public static Regex rx = new Regex("/oauth2(?:/v2.0)?/token");

        /// <summary>
        /// There are no customizations available for this sanitizer.
        /// </summary>
        public OAuthResponseSanitizer() { }

        public override void Sanitize(RecordSession session)
        {
            session.Entries.RemoveAll(x => rx.IsMatch(x.RequestUri));
        }
    }
}
