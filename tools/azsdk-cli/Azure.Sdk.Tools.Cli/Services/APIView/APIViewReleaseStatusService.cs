using System.Net;
using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;

namespace Azure.Sdk.Tools.Cli.Services.APIView;

public interface IAPIViewReleaseStatusService
{
    Task<ApiViewReleaseStatusResult> GetApprovalStatusAsync(string language, string packageName, string packageVersion, CancellationToken ct);
}

public class APIViewReleaseStatusService(
    IAPIViewHttpService apiViewHttpService,
    ILogger<APIViewReleaseStatusService> logger) : IAPIViewReleaseStatusService
{
    public async Task<ApiViewReleaseStatusResult> GetApprovalStatusAsync(string language, string packageName, string packageVersion, CancellationToken ct)
    {
        var apiViewLanguage = MapApiViewLanguage(language);
        if (apiViewLanguage == null)
        {
            throw new InvalidOperationException($"APIView release status does not support language '{language}'.");
        }

        var endpoint = $"/AutoReview/GetReviewStatus?language={Uri.EscapeDataString(apiViewLanguage)}&packageName={Uri.EscapeDataString(packageName)}&packageVersion={Uri.EscapeDataString(packageVersion)}";
        logger.LogInformation("Querying APIView release status for {packageName} {packageVersion}", packageName, packageVersion);

        var (_, statusCode) = await apiViewHttpService.GetAsync(endpoint, ct);
        return CreateResult(statusCode, packageName, packageVersion);
    }

    private static ApiViewReleaseStatusResult CreateResult(int statusCode, string packageName, string packageVersion)
    {
        return statusCode switch
        {
            (int)HttpStatusCode.OK => new ApiViewReleaseStatusResult
            {
                IsApproved = true,
                StatusCode = statusCode,
                Reason = "approved",
                Details = [$"APIView reports API approval for {packageName} {packageVersion}."]
            },
            (int)HttpStatusCode.Created => new ApiViewReleaseStatusResult
            {
                IsApproved = false,
                StatusCode = statusCode,
                Reason = "apiApprovalPending",
                Details = [$"APIView reports API approval is still pending for {packageName} {packageVersion}."]
            },
            (int)HttpStatusCode.Accepted => new ApiViewReleaseStatusResult
            {
                IsApproved = false,
                StatusCode = statusCode,
                Reason = "apiApprovalPending",
                Details = [$"APIView reports API approval is pending for {packageName} {packageVersion}."]
            },
            _ => throw new InvalidOperationException($"Unexpected APIView status code {statusCode} for {packageName} {packageVersion}.")
        };
    }

    private static string? MapApiViewLanguage(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "js" => "JavaScript",
            "csharp" => "C#",
            "java" => "Java",
            "python" => "Python",
            "go" => "Go",
            "rust" => "Rust",
            _ => null
        };
    }
}