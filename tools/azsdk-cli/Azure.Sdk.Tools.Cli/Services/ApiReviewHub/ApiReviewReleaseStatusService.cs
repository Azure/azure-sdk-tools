using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.APIView;
using System.Net;

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

        try
        {
            var apiViewResult = await apiViewReleaseStatusService.GetReleaseStatusAsync(language, packageName, packageVersion, ct);
            result.ApiView = apiViewResult;

            if (apiViewResult.IsApproved || !IsSuccessfulStatusCode(result.ReviewHub.StatusCode))
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
            result.ApiView = new ApiViewReleaseStatusResult
            {
                StatusCode = GetStatusCode(ex),
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
}