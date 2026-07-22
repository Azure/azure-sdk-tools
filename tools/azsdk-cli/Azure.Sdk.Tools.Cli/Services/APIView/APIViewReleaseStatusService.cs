using System.Net;
using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;

namespace Azure.Sdk.Tools.Cli.Services.APIView;

public interface IAPIViewReleaseStatusService
{
    Task<ApiViewReleaseStatusResult> GetReleaseStatusAsync(string language, string packageName, string packageVersion, CancellationToken ct);
}

public class APIViewReleaseStatusService(
    IAPIViewHttpService apiViewHttpService,
    ILogger<APIViewReleaseStatusService> logger) : IAPIViewReleaseStatusService
{
    public async Task<ApiViewReleaseStatusResult> GetReleaseStatusAsync(string language, string packageName, string packageVersion, CancellationToken ct)
    {
        var mappedLanguage = MapLanguage(language);
        if (mappedLanguage == null)
        {
            throw new InvalidOperationException($"APIView release status does not support language '{language}'.");
        }

        var endpoint = $"/AutoReview/GetReviewStatus?language={Uri.EscapeDataString(mappedLanguage)}&packageName={Uri.EscapeDataString(packageName)}&packageVersion={Uri.EscapeDataString(packageVersion)}";
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
                PackageNameApproved = true,
                StatusCode = statusCode,
                Reason = "approved",
                Details = [$"APIView reports API approval for {packageName} {packageVersion}."]
            },
            (int)HttpStatusCode.Created => new ApiViewReleaseStatusResult
            {
                IsApproved = false,
                PackageNameApproved = true,
                StatusCode = statusCode,
                Reason = "packageNameApproved",
                Details = [$"APIView reports package-name approval, but API approval is still pending for {packageName} {packageVersion}."]
            },
            (int)HttpStatusCode.Accepted => new ApiViewReleaseStatusResult
            {
                IsApproved = false,
                PackageNameApproved = false,
                StatusCode = statusCode,
                Reason = "packageNamePending",
                Details = [$"APIView reports neither API approval nor package-name approval for {packageName} {packageVersion}."]
            },
            _ => throw new InvalidOperationException($"Unexpected APIView status code {statusCode} for {packageName} {packageVersion}.")
        };
    }

    private static string? MapLanguage(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "javascript" => "JavaScript",
            "dotnet" => "C#",
            "java" => "Java",
            "python" => "Python",
            _ => null
        };
    }
}