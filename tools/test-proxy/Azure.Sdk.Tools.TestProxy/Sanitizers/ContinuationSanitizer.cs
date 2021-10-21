using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// Used to sanitize "session" variables. A "session" variable is one that is returned as a result of Response A, then used as INPUT of Response B. 
    /// 
    /// The value inserted defaults to a new guid if one is not provided. This sanitizer applies at the session level, and looks at the HEADERS of the request/response pairs.
    /// </summary>
    public class ContinuationSanitizer : RecordedTestSanitizer
    {
        private string _targetKey;
        private string _method;
        private bool _resetAfterFirst;

        private Dictionary<string, Delegate> _updateMethod = new Dictionary<string, Delegate>()
        {
            { "guid", new Func<string>(GuidReplacer) }
        };

        public static string GuidReplacer()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// This sanitizer is applied at the session level, and is used to anonymize private keys in response/request pairs.
        /// 
        /// For instance, request/response pair A has a RESPONSE that contains a "sessionId" header whos value must be present in the REQUEST of the next request/response pair B.
        /// 
        /// This sanitizer supports "all further requests get this the value of this first key" as well as as the more standard "response value gets used in next request" that is described 
        /// in the scenario before. 
        /// 
        /// Defaults to maintaining same key for rest of recording.
        /// </summary>
        /// <param name="key">The name of the header whos value will be replaced from response -> next request.</param>
        /// <param name="method">The method by which the value of the targeted key will be replaced. Currently only supports generating a new guid for the value replacement..</param>
        /// <param name="resetAfterFirst">Do we need multiple pairs replaced? Or do we want to stop after the first set.</param>
        public ContinuationSanitizer(string key, string method, string resetAfterFirst = "true")
        {
            _targetKey = key;
            _method = method;
            _resetAfterFirst = bool.Parse(resetAfterFirst);
        }

        // iterate through each entry.
        // if a RESPONSE issues a token with a name we expect, sanitize it.
        // use it on the next instance of a REQUEST with the same header value present.
        public override void Sanitize(RecordSession session)
        {
            string newValue = "";

            // iterate across the entries. looking at responses first for a member that may need to be anonymized, 
            // when one is found, the next REQUEST should have this same key modified.
            // currently will invoke the creation function once per encounter of response -> request.
            for (int i = 0; i < session.Entries.Count; i++)
            {
                var currentEntry = session.Entries[i];
                
                if (currentEntry.Response.Headers.ContainsKey(_targetKey) && String.IsNullOrWhiteSpace(newValue))
                {
                    newValue = (string)_updateMethod[_method].DynamicInvoke();
                    currentEntry.Response.Headers[_targetKey] = new string[] { newValue };

                    continue;
                }

                if (currentEntry.Request.Headers.ContainsKey(_targetKey) && !String.IsNullOrWhiteSpace(newValue))
                {
                    currentEntry.Request.Headers[_targetKey] = new string[] { newValue };

                    if (_resetAfterFirst)
                    {
                        newValue = "";
                    }
                }
            }

        }
    }
}
