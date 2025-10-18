using System.Text.Json.Serialization;

namespace Azure.Tools.GeneratorAgent.Models
{
    /// <summary>
    /// Represents a single change within a patch
    /// </summary>
    public class PatchChange
    {
        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("start_line")]
        public int StartLine { get; set; }

        [JsonPropertyName("end_line")]
        public int EndLine { get; set; }

        [JsonPropertyName("old_content")]
        public required string OldContent { get; set; }

        [JsonPropertyName("new_content")]
        public required string NewContent { get; set; }
    }
}