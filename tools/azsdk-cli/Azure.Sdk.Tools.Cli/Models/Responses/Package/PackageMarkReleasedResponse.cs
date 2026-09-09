using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public class PackageMarkReleasedResponse : CommandResponse
{
    [JsonIgnore]
    public string PackageName { get; set; } = string.Empty;

    [JsonIgnore]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("api_review_hub")]
    public JsonElement? ApiReviewHub { get; set; }

    [JsonPropertyName("api_view")]
    public JsonElement? ApiView { get; set; }

    [JsonIgnore]
    public bool ApiReviewHubSucceeded { get; set; }

    [JsonIgnore]
    public bool ApiReviewHubSkipped { get; set; }

    [JsonIgnore]
    public string ApiReviewHubMessage { get; set; } = string.Empty;

    [JsonIgnore]
    public bool ApiViewSucceeded { get; set; }

    [JsonIgnore]
    public string ApiViewMessage { get; set; } = string.Empty;

    protected override string Format()
    {
        StringBuilder output = new();
        output.AppendLine($"Package: {PackageName} {Version}");
        string apiReviewHubStatus = ApiReviewHubSkipped ? "SKIPPED" : ApiReviewHubSucceeded ? "SUCCEEDED" : "FAILED";
        output.AppendLine($"API Review Hub: {apiReviewHubStatus} - {ApiReviewHubMessage}");
        output.AppendLine($"APIView: {(ApiViewSucceeded ? "SUCCEEDED" : "FAILED")} - {ApiViewMessage}");
        return output.ToString();
    }

    public override string ToString()
    {
        string output = Format().TrimEnd();
        return SupportChannel == null
            ? output
            : $"{output}{Environment.NewLine}{SupportChannel}";
    }
}