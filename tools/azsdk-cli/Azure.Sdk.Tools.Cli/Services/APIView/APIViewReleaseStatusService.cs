using System.Net;
using System.Text.RegularExpressions;
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
        var result = CreateResult(statusCode, packageName, packageVersion);

        // Workaround preserving the legacy release-pipeline behavior until API Review Hub
        // owns release gating: beta releases require package-name approval, but not API approval.
        if (IsBetaVersion(apiViewLanguage, packageVersion) && result.PackageNameApproved && !result.IsApproved)
        {
            result.IsApproved = true;
            result.Reason = "reviewNotRequired";
            result.Details = [$"APIView review is not required for beta package {packageName} {packageVersion}; package-name approval is complete."];
        }

        return result;
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

    private static bool IsBetaVersion(string language, string packageVersion)
    {
        var publicVersion = packageVersion.Split('+', 2)[0];
        if (publicVersion.Contains("-beta", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!language.Equals("Python", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Regex.IsMatch(
            publicVersion,
            @"\Av?(?:[0-9]+!)?[0-9]+(?:\.[0-9]+)*[-_.]?(?:beta|b)[-_.]?[0-9]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}