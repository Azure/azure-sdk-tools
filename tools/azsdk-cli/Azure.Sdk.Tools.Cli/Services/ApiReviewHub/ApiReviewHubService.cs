using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;

namespace Azure.Sdk.Tools.Cli.Services.ApiReviewHub;

public interface IApiReviewHubService
{
    Task<ApiReviewHubRequestReviewPullRequestResult> RequestReviewPullRequestAsync(
        ReviewPullRequestCreationRequest request,
        string endpoint,
        bool waitForCompletion,
        TimeSpan pollInterval,
        CancellationToken ct);

    Task<ApiReviewHubReleaseGateResult> GetReleaseGateStatusAsync(
        string endpoint,
        string language,
        string packageName,
        string packageVersion,
        string apiHash,
        CancellationToken ct);
}

public class ApiReviewHubService(
    IHttpClientFactory httpClientFactory,
    IAzureService azureService,
    ILogger<ApiReviewHubService> logger) : IApiReviewHubService
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<ApiReviewHubRequestReviewPullRequestResult> RequestReviewPullRequestAsync(
        ReviewPullRequestCreationRequest request,
        string endpoint,
        bool waitForCompletion,
        TimeSpan pollInterval,
        CancellationToken ct)
    {
        endpoint = endpoint.TrimEnd('/');
        var httpClient = httpClientFactory.CreateClient(nameof(ApiReviewHubService));
        await ConfigureAuthorizationAsync(httpClient, endpoint, ct);

        logger.LogInformation("Requesting API Review Hub review PR for {packageName} from {endpoint}", request.PackageName, endpoint);
        var accepted = await PostJsonAsync<ReviewPullRequestCreationAcceptedResponse>(httpClient, $"{endpoint}/api/review-prs", request, ct);
        var result = new ApiReviewHubRequestReviewPullRequestResult
        {
            OperationId = accepted.OperationId,
            Status = accepted.Status
        };

        if (!waitForCompletion)
        {
            return result;
        }

        var startedAt = DateTimeOffset.UtcNow;
        var loggedPipelineUrl = false;
        while (true)
        {
            var operation = await GetJsonAsync<OperationStatus>(httpClient, $"{endpoint}/api/operations/{accepted.OperationId}", ct);
            result.Status = operation.Status;
            result.Operation = operation;
            LogOperationProgress(operation, startedAt, ref loggedPipelineUrl);

            if (string.Equals(operation.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            if (string.Equals(operation.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(operation.FailureReason ?? $"API Review Hub operation {operation.OperationId} failed.");
            }

            await Task.Delay(pollInterval, ct);
        }
    }

    public async Task<ApiReviewHubReleaseGateResult> GetReleaseGateStatusAsync(
        string endpoint,
        string language,
        string packageName,
        string packageVersion,
        string apiHash,
        CancellationToken ct)
    {
        endpoint = endpoint.TrimEnd('/');
        var httpClient = httpClientFactory.CreateClient(nameof(ApiReviewHubService));
        await ConfigureAuthorizationAsync(httpClient, endpoint, ct);

        var uriBuilder = new UriBuilder($"{endpoint}/api/releases/check-gate");
        var query = new List<string>
        {
            $"language={Uri.EscapeDataString(language)}",
            $"packageName={Uri.EscapeDataString(packageName)}",
            $"version={Uri.EscapeDataString(packageVersion)}"
        };
        if (!string.IsNullOrWhiteSpace(apiHash))
        {
            query.Add($"apiHash={Uri.EscapeDataString(apiHash)}");
        }

        uriBuilder.Query = string.Join("&", query);
        logger.LogInformation("Querying API Review Hub release gate for {packageName} {packageVersion}", packageName, packageVersion);
        return await GetJsonAsync<ApiReviewHubReleaseGateResult>(httpClient, uriBuilder.Uri.ToString(), ct);
    }

    private void LogOperationProgress(OperationStatus operation, DateTimeOffset startedAt, ref bool loggedPipelineUrl)
    {
        if (!loggedPipelineUrl && !string.IsNullOrWhiteSpace(operation.PipelineUrl))
        {
            logger.LogInformation("API Review Hub build: {pipelineUrl}", operation.PipelineUrl);
            loggedPipelineUrl = true;
        }

        logger.LogInformation(
            "API Review Hub operation {operationId} status: {status} (elapsed {elapsed}).",
            operation.OperationId,
            operation.Status,
            FormatElapsed(DateTimeOffset.UtcNow - startedAt));
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"h\:mm\:ss")
            : elapsed.ToString(@"m\:ss");
    }

    private async Task ConfigureAuthorizationAsync(HttpClient httpClient, string endpoint, CancellationToken ct)
    {
        var tokenScope = $"{GetAppIdUri(endpoint)}/.default";
        var token = await azureService.GetCredential().GetTokenAsync(new TokenRequestContext([tokenScope]), ct);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static async Task<T> PostJsonAsync<T>(HttpClient httpClient, string url, object body, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(body, serializerOptions), Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(url, content, ct);
        return await ReadResponseAsync<T>(response, ct);
    }

    private static async Task<T> GetJsonAsync<T>(HttpClient httpClient, string url, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(url, ct);
        return await ReadResponseAsync<T>(response, ct);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"API Review Hub request failed with status {(int)response.StatusCode}: {content}");
        }

        var value = JsonSerializer.Deserialize<T>(content, serializerOptions);
        if (value == null)
        {
            throw new InvalidOperationException("API Review Hub returned an empty response.");
        }

        return value;
    }

    private static string GetAppIdUri(string endpoint)
    {
        var host = new Uri(endpoint).Host;
        var siteName = host.Split('.', 2)[0];
        const string prefix = "api-review-hub";

        if (!siteName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unable to derive API Review Hub Entra App ID URI from endpoint host {host}.");
        }

        var environmentSuffix = siteName[prefix.Length..];
        return $"api://apireviewhub{environmentSuffix}";
    }
}