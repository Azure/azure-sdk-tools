using System.Text.Json.Serialization;

namespace Azure.Tools.GeneratorAgent.Models
{
    /// <summary>
    /// Represents metadata and content about a TypeSpec file for the get_typespec_file tool
    /// </summary>
    public class TypeSpecFileInfo
    {
        [JsonPropertyName("path")]
        public required string Path { get; set; }

        [JsonPropertyName("lines")]
        public int Lines { get; set; }

        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("sha256")]
        public required string Sha256 { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
