using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.APIView;

public class APIViewMarkReleasedResult
{
    [JsonPropertyName("reviewId")]
    public required string ReviewId { get; set; }

    [JsonPropertyName("revisionId")]
    public required string RevisionId { get; set; }

    [JsonPropertyName("packageName")]
    public required string PackageName { get; set; }

    [JsonPropertyName("language")]
    public required string Language { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }

    [JsonPropertyName("isReleased")]
    public bool IsReleased { get; set; }

    [JsonPropertyName("releasedOn")]
    public DateTimeOffset? ReleasedOn { get; set; }
}