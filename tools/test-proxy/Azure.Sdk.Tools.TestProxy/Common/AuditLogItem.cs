using System;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class AuditLogItem
    {
        public string RecordingId { get; set; }

        public DateTime Timestamp { get; set; }

        public string Uri { get; set; }

        public string Verb { get; set; }

        public string Message { get; set; }

        public AuditLogItem(string recordingId, string requestUri, string requestMethod) {
            RecordingId = recordingId;
            Timestamp = DateTime.UtcNow;

            Uri = requestUri;
            Verb = requestMethod;
        }

        public string ToCsvString()
        {
            return $"{RecordingId},{Timestamp.ToString("o")},{Verb},{Uri},{Message}";
        }

        public AuditLogItem(string recordingId, string message)
        {
            RecordingId = recordingId;
            Timestamp = DateTime.UtcNow;

            Message = message;
        }

    }
}
