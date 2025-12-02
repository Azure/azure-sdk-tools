using System.Text.Json.Serialization;

namespace Azure.Tools.GeneratorAgent.Models
{
    /// <summary>
    /// Represents a patch request from the agent to modify a TypeSpec file
    /// </summary>
    public class PatchRequest
    {
        [JsonPropertyName("file")]
        public required string File { get; set; }

        [JsonPropertyName("from_version")]
        public int FromVersion { get; set; }

        [JsonPropertyName("reason")]
        public required string Reason { get; set; }

        [JsonPropertyName("changes")]
        public required List<PatchChange> Changes { get; set; }
    }
}