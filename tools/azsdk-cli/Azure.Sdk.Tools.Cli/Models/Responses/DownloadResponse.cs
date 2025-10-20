using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response model for file download operations
/// </summary>
public class DownloadResponse : CommandResponse
{
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("downloaded_count")]
    public int DownloadedCount { get; set; }

    [JsonPropertyName("total_files")]
    public int TotalFiles { get; set; }

    public override string ToString()
    {
        return ToString(Message);
    }
}
