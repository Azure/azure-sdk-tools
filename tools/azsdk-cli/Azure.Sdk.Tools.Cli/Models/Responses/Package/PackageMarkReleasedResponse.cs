using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public class ReleaseBackendResult
{
    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }
}

public class PackageMarkReleasedResponse : PackageResponseBase
{
    [JsonPropertyName("api_hash")]
    public required string ApiHash { get; set; }

    [JsonPropertyName("api_review_hub")]
    public required ReleaseBackendResult ApiReviewHub { get; set; }

    [JsonPropertyName("api_view")]
    public required ReleaseBackendResult ApiView { get; set; }

    protected override string Format()
    {
        StringBuilder output = new();
        output.AppendLine($"Package: {PackageName} {Version}");
        output.AppendLine($"API hash: {ApiHash}");
        output.AppendLine($"API Review Hub: {(ApiReviewHub.Succeeded ? "SUCCEEDED" : "FAILED")} - {ApiReviewHub.Message}");
        output.AppendLine($"APIView: {(ApiView.Succeeded ? "SUCCEEDED" : "FAILED")} - {ApiView.Message}");
        return output.ToString();
    }
}