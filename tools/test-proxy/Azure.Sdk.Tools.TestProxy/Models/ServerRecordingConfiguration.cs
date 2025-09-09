namespace Azure.Sdk.Tools.TestProxy.Models
{
    public enum UniversalRecordingMode
    {
        Record,     // implies requests that do not match /Admin should be directed to Record.HandleRequest()
        Playback,   // implies requests that do not match /Admin should be directed to Playback.HandleRequest()
        Azure       // implies the custom requirements for Azure SDK test recordings EG x-recording-upstream-base-uri and "standard" proxy mode
    }

    public class ServerRecordingConfiguration
    {
        public UniversalRecordingMode Mode { get; set; } = UniversalRecordingMode.Azure;
        public string RecordingId { get; set; }
    }
}
