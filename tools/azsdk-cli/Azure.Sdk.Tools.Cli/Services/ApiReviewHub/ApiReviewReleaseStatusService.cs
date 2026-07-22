using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.APIView;

namespace Azure.Sdk.Tools.Cli.Services.ApiReviewHub;

public interface IApiReviewReleaseStatusService
{
    Task<ApiReviewReleaseStatusResult> GetReleaseStatusAsync(string endpoint, string language, string packageName, string packageVersion, string apiHash, CancellationToken ct);
}

public class ApiReviewReleaseStatusService(
    IApiReviewHubService apiReviewHubService,
    IAPIViewReleaseStatusService apiViewReleaseStatusService,
    ILogger<ApiReviewReleaseStatusService> logger) : IApiReviewReleaseStatusService
{
    public async Task<ApiReviewReleaseStatusResult> GetReleaseStatusAsync(string endpoint, string language, string packageName, string packageVersion, string apiHash, CancellationToken ct)
    {
        var result = new ApiReviewReleaseStatusResult();

        try
        {
            var reviewHubResult = await apiReviewHubService.GetReleaseGateStatusAsync(endpoint, language, packageName, packageVersion, apiHash, ct);
            result.ReviewHub = new ApiReviewStatusSourceResult<ApiReviewHubReleaseGateResult>
            {
                Succeeded = true,
                Result = reviewHubResult
            };
            result.IsApproved = reviewHubResult.Allowed;
            result.FinalSource = "ApiReviewHub";
            result.Reason = reviewHubResult.Reason ?? (reviewHubResult.Allowed ? "approved" : "notApproved");
            if (reviewHubResult.Allowed)
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "API Review Hub release status lookup failed for {packageName} {packageVersion}", packageName, packageVersion);
            result.ReviewHub = new ApiReviewStatusSourceResult<ApiReviewHubReleaseGateResult>
            {
                Succeeded = false,
                Error = ex.Message
            };
        }

        try
        {
            var apiViewResult = await apiViewReleaseStatusService.GetReleaseStatusAsync(language, packageName, packageVersion, ct);
            result.ApiView = new ApiReviewStatusSourceResult<ApiViewReleaseStatusResult>
            {
                Succeeded = true,
                Result = apiViewResult
            };

            if (apiViewResult.IsApproved || !result.ReviewHub.Succeeded)
            {
                result.IsApproved = apiViewResult.IsApproved;
                result.FinalSource = "APIView";
                result.Reason = apiViewResult.Reason;
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "APIView release status fallback failed for {packageName} {packageVersion}", packageName, packageVersion);
            result.ApiView = new ApiReviewStatusSourceResult<ApiViewReleaseStatusResult>
            {
                Succeeded = false,
                Error = ex.Message
            };
            result.IsApproved = false;
            result.FinalSource = "None";
            result.Reason = "queryFailed";
            return result;
        }
    }
}