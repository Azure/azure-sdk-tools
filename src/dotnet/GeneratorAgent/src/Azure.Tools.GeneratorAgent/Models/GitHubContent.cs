using System.Text.Json.Serialization;

namespace Azure.Tools.GeneratorAgent.Models
{
    /// <summary>
    /// GitHub API response model for directory contents
    /// </summary>
    internal class GitHubContent
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }
    }
}
