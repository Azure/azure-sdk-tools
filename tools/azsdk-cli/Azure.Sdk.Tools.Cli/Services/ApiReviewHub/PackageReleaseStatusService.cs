using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.APIView;
using System.Net;

namespace Azure.Sdk.Tools.Cli.Services.ApiReviewHub;

public interface IPackageReleaseStatusService
{
    Task<PackageReleaseStatusResult> GetApprovalStatusAsync(string endpoint, string language, string packageName, string packageVersion, string apiHash, string repoOwner, CancellationToken ct);
}

public class PackageReleaseStatusService(
    IApiReviewHubService apiReviewHubService,
    IAPIViewReleaseStatusService apiViewReleaseStatusService,
    ILogger<PackageReleaseStatusService> logger) : IPackageReleaseStatusService
{
    public async Task<PackageReleaseStatusResult> GetApprovalStatusAsync(string endpoint, string language, string packageName, string packageVersion, string apiHash, string repoOwner, CancellationToken ct)
    {
        var result = new PackageReleaseStatusResult();

        try
        {
            var reviewHubResult = await apiReviewHubService.GetReleaseGateStatusAsync(endpoint, language, packageName, packageVersion, apiHash, repoOwner, ct);
            reviewHubResult.StatusCode ??= (int)HttpStatusCode.OK;
            result.ReviewHub = reviewHubResult;
            result.IsApproved = reviewHubResult.IsApproved;
            result.FinalSource = "ApiReviewHub";
            result.Reason = reviewHubResult.Reason ?? (reviewHubResult.IsApproved ? "approved" : "notApproved");
            if (reviewHubResult.IsApproved)
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "API Review Hub release status lookup failed for {packageName} {packageVersion}", packageName, packageVersion);
            result.ReviewHub = new ApiReviewHubReleaseGateResult
            {
                StatusCode = GetStatusCode(ex),
                Reason = "queryFailed",
                Error = ex.Message
            };
        }

        if (!IsApiViewLanguageSupported(language))
        {
            result.ApiView = new ApiViewReleaseStatusResult
            {
                IsApproved = false,
                PackageNameApproved = false,
                Reason = "languageNotSupported",
                Details = [$"APIView does not support {language} for release gating."]
            };

            if (!IsSuccessfulStatusCode(result.ReviewHub.StatusCode) || IsReviewHubNotApplicable(result.ReviewHub))
            {
                result.IsApproved = false;
                result.FinalSource = "None";
                result.Reason = "apiViewLanguageNotSupported";
            }

            return result;
        }

        try
        {
            var apiViewResult = await apiViewReleaseStatusService.GetApprovalStatusAsync(language, packageName, packageVersion, ct);
            result.ApiView = apiViewResult;

            if (apiViewResult.IsApproved || !IsSuccessfulStatusCode(result.ReviewHub.StatusCode) || IsReviewHubNotApplicable(result.ReviewHub))
            {
                result.IsApproved = apiViewResult.IsApproved;
                result.FinalSource = "APIView";
                result.Reason = apiViewResult.Reason;
            }

            return result;
        }
        catch (Exception ex)
        {
            var apiViewStatusCode = GetStatusCode(ex);
            if (IsReviewHubNotApplicable(result.ReviewHub) && apiViewStatusCode == (int)HttpStatusCode.NotFound)
            {
                logger.LogInformation("APIView review not found for {packageName} {packageVersion} when Review Hub is not applicable.", packageName, packageVersion);
                result.ApiView = new ApiViewReleaseStatusResult
                {
                    StatusCode = apiViewStatusCode,
                    IsApproved = false,
                    PackageNameApproved = false,
                    Reason = "reviewNotFound",
                    Details = [$"APIView review is not found for {packageName} {packageVersion}."]
                };
                result.IsApproved = false;
                result.FinalSource = "APIView";
                result.Reason = "reviewNotFound";
                return result;
            }

            logger.LogWarning(ex, "APIView release status fallback failed for {packageName} {packageVersion}", packageName, packageVersion);
            result.ApiView = new ApiViewReleaseStatusResult
            {
                StatusCode = apiViewStatusCode,
                Reason = "queryFailed",
                Error = ex.Message
            };
            result.IsApproved = false;
            result.FinalSource = "None";
            result.Reason = "queryFailed";
            return result;
        }
    }

    private static int? GetStatusCode(Exception ex) =>
        ex is HttpRequestException httpRequestException && httpRequestException.StatusCode.HasValue
            ? (int)httpRequestException.StatusCode.Value
            : null;

    private static bool IsSuccessfulStatusCode(int? statusCode) =>
        statusCode is >= 200 and < 300;

    private static bool IsApiViewLanguageSupported(string language) =>
        language.Equals("js", StringComparison.OrdinalIgnoreCase)
        || language.Equals("csharp", StringComparison.OrdinalIgnoreCase)
        || language.Equals("java", StringComparison.OrdinalIgnoreCase)
        || language.Equals("python", StringComparison.OrdinalIgnoreCase)
        || language.Equals("go", StringComparison.OrdinalIgnoreCase)
        || language.Equals("rust", StringComparison.OrdinalIgnoreCase);

    private static bool IsReviewHubNotApplicable(ApiReviewHubReleaseGateResult reviewHubResult) =>
        string.Equals(reviewHubResult.Reason, "repositoryNotSupported", StringComparison.OrdinalIgnoreCase);
}