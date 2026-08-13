using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.APIView;

public class APIViewMarkReleasedResult
{
    [JsonPropertyName("reviewId")]
    public required string ReviewId { get; set; }

    [JsonPropertyName("revisionId")]
    public required string RevisionId { get; set; }

    [JsonPropertyName("isReleased")]
    public bool IsReleased { get; set; }

    [JsonPropertyName("releasedOn")]
    public DateTimeOffset? ReleasedOn { get; set; }
}