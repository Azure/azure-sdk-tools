namespace Azure.Sdk.Tools.TestProxy.Models
{
    public enum UniversalRecordingMode
    {
        Record,     // Requests that do not match /Admin should be directed to Record.HandleRequest()
        Playback,   // Requests that do not match /Admin should be directed to Playback.HandleRequest()
        Azure       // Azure mode, where direction to /Record/HandleRequest() or /Playback/HandleRequest() is determined by x-recording-mode header presence
    }

    public class ServerRecordingConfiguration
    {
        public UniversalRecordingMode Mode { get; set; } = UniversalRecordingMode.Azure;
        public string RecordingId { get; set; }
    }
}
