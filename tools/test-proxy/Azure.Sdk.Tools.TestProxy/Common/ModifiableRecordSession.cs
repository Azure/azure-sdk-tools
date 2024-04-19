using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class ModifiableRecordSession
    {
        public RecordMatcher CustomMatcher { get; set;}

        public RecordSession Session { get; }

        public ModifiableRecordSession(SanitizerDictionary sanitizerRegistry, string sessionId)
        {
            this.AppliedSanitizers = sanitizerRegistry.SessionSanitizers.ToList();
            this.SessionId = sessionId;
        }

        public ModifiableRecordSession(RecordSession session, SanitizerDictionary sanitizerRegistry, string sessionId)
        {
            Session = session;
            this.AppliedSanitizers = sanitizerRegistry.SessionSanitizers.ToList();
            this.SessionId = sessionId;
        }

        public string SessionId;

        public string Path { get; set; }

        public HttpClient Client { get; set; }

        public List<ResponseTransform> AdditionalTransforms { get; } = new List<ResponseTransform>();

        public List<string> AppliedSanitizers { get; set; } = new List<string>();
        public List<string> ForRemoval { get; } = new List<string>();

        public string SourceRecordingId { get; set; }

        public int PlaybackResponseTime { get; set; }

        public void ResetExtensions(SanitizerDictionary sanitizerDictionary)
        {
            AdditionalTransforms.Clear();
            AppliedSanitizers = new List<string>();
            AppliedSanitizers.AddRange(sanitizerDictionary.SessionSanitizers);
            ForRemoval.Clear();

            CustomMatcher = null;
            Client = null;
        }
    }
}
