using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;

namespace Azure.Sdk.Tools.Cli.Services.ApiReviewHub;

public interface IApiReviewHubService
{
    Task<OperationStatus> RequestReviewPullRequestAsync(
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
        string repoOwner,
        CancellationToken ct);

    Task<ApiReviewHubMarkReleasedResult> MarkPackageReleasedAsync(
        string language,
        string packageName,
        string packageVersion,
        string apiHash,
        string repositoryOwner,
        CancellationToken ct,
        bool dryRun = false);
}

public class ApiReviewHubService(
    IHttpClientFactory httpClientFactory,
    IAzureService azureService,
    ILogger<ApiReviewHubService> logger,
    TimeProvider? timeProvider = null,
    TimeSpan? operationTimeout = null) : IApiReviewHubService
{
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromMinutes(30);
    private const string DefaultEndpoint = "https://api-review-hub.azurewebsites.net";

    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly TimeSpan _operationTimeout = operationTimeout ?? DefaultOperationTimeout;

    public async Task<OperationStatus> RequestReviewPullRequestAsync(
        ReviewPullRequestCreationRequest request,
        string endpoint,
        bool waitForCompletion,
        TimeSpan pollInterval,
        CancellationToken ct)
    {
        endpoint = endpoint.TrimEnd('/');
        var httpClient = httpClientFactory.CreateClient(nameof(ApiReviewHubService));
        var authorization = await GetAuthorizationAsync(endpoint, ct);

        logger.LogInformation("Requesting API Review Hub review PR for {packageName} from {endpoint}", request.PackageName, endpoint);
        ReviewPullRequestCreationAcceptedResponse accepted;
        try
        {
            accepted = await PostJsonAsync<ReviewPullRequestCreationAcceptedResponse>(httpClient, $"{endpoint}/api/review-prs", request, authorization, ct);
        }
        catch (ApiReviewHubRequestException ex) when (string.Equals(ex.ErrorCode, "reviewPullRequestAlreadyExists", StringComparison.Ordinal))
        {
            var serverMessage = TryGetErrorMessage(ex.Content) ?? $"An API Review Hub review PR already exists for {request.PackageName}.";
            logger.LogDebug("{message}", serverMessage);

            return new OperationStatus
            {
                Status = "succeeded",
                PackageName = request.PackageName,
                Message = serverMessage,
                ReviewPullRequest = TryGetErrorReviewPullRequest(ex.Content)
            };
        }

        if (!waitForCompletion)
        {
            return new OperationStatus { OperationId = accepted.OperationId, Status = accepted.Status };
        }

        var startedAt = _timeProvider.GetUtcNow();
        var loggedPipelineUrl = false;
        while (true)
        {
            var operation = await GetJsonAsync<OperationStatus>(httpClient, $"{endpoint}/api/operations/{accepted.OperationId}", authorization, ct);
            LogOperationProgress(operation, startedAt, ref loggedPipelineUrl);

            if (string.Equals(operation.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                return operation;
            }

            if (string.Equals(operation.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(operation.FailureReason ?? $"API Review Hub operation {operation.OperationId} failed.");
            }

            if (_timeProvider.GetUtcNow() - startedAt >= _operationTimeout)
            {
                throw new TimeoutException($"API Review Hub operation {operation.OperationId} timed out after {FormatElapsed(_timeProvider.GetUtcNow() - startedAt)} with status '{operation.Status}'.");
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
        string repoOwner,
        CancellationToken ct)
    {
        endpoint = endpoint.TrimEnd('/');
        var httpClient = httpClientFactory.CreateClient(nameof(ApiReviewHubService));
        var authorization = await GetAuthorizationAsync(endpoint, ct);

        var uriBuilder = new UriBuilder($"{endpoint}/api/releases/check-approval");
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

        if (!string.IsNullOrWhiteSpace(repoOwner))
        {
            query.Add($"repoOwner={Uri.EscapeDataString(repoOwner)}");
        }

        uriBuilder.Query = string.Join("&", query);
        logger.LogInformation("Querying API Review Hub release gate for {packageName} {packageVersion}", packageName, packageVersion);
        var result = await GetJsonAsync<ApiReviewHubReleaseGateResult>(httpClient, uriBuilder.Uri.ToString(), authorization, ct);
        return result;
    }

    public async Task<ApiReviewHubMarkReleasedResult> MarkPackageReleasedAsync(
        string language,
        string packageName,
        string packageVersion,
        string apiHash,
        string repositoryOwner,
        CancellationToken ct,
        bool dryRun = false)
    {
        var httpClient = httpClientFactory.CreateClient(nameof(ApiReviewHubService));
        var authorization = await GetAuthorizationAsync(DefaultEndpoint, ct);
        var request = new MarkPackageReleasedRequest
        {
            Language = language,
            PackageName = packageName,
            Version = packageVersion,
            ApiHash = apiHash,
            RepoOwner = repositoryOwner,
            ReleasedOn = _timeProvider.GetUtcNow(),
            DryRun = dryRun
        };

        logger.LogInformation("Marking {packageName} {packageVersion} as released in API Review Hub", packageName, packageVersion);
        return await PostJsonAsync<ApiReviewHubMarkReleasedResult>(httpClient, $"{DefaultEndpoint}/api/releases/mark-released", request, authorization, ct);
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
            FormatElapsed(_timeProvider.GetUtcNow() - startedAt));
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"h\:mm\:ss")
            : elapsed.ToString(@"m\:ss");
    }

    private async Task<AuthenticationHeaderValue> GetAuthorizationAsync(string endpoint, CancellationToken ct)
    {
        var tokenScope = $"{GetAppIdUri(endpoint)}/.default";
        var token = await azureService.GetCredential().GetTokenAsync(new TokenRequestContext([tokenScope]), ct);
        return new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static async Task<T> PostJsonAsync<T>(HttpClient httpClient, string url, object body, AuthenticationHeaderValue authorization, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(body, serializerOptions), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        request.Headers.Authorization = authorization;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, ct);
        return await ReadResponseAsync<T>(response, ct);
    }

    private static async Task<T> GetJsonAsync<T>(HttpClient httpClient, string url, AuthenticationHeaderValue authorization, CancellationToken ct) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = authorization;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, ct);
        var value = await ReadResponseAsync<T>(response, ct);

        if (value is ApiReviewHubReleaseGateResult releaseGateResult)
        {
            releaseGateResult.StatusCode = (int)response.StatusCode;
        }

        return value;
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorCode = TryGetErrorCode(content);
            throw new ApiReviewHubRequestException(
                $"API Review Hub request failed with status {(int)response.StatusCode}: {content}",
                response.StatusCode,
                errorCode,
                content);
        }

        var value = JsonSerializer.Deserialize<T>(content, serializerOptions);
        if (value == null)
        {
            throw new InvalidOperationException("API Review Hub returned an empty response.");
        }

        return value;
    }

    private static string? TryGetErrorCode(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.GetProperty("error").GetProperty("code").GetString();
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static string? TryGetErrorMessage(string? content)
    {
        return TryGetErrorProperty(content, "message", out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static JsonElement? TryGetErrorReviewPullRequest(string? content)
    {
        return TryGetErrorProperty(content, "reviewPullRequest", out var property)
            ? property.Clone()
            : null;
    }

    private static bool TryGetErrorProperty(string? content, string propertyName, out JsonElement property)
    {
        property = default;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("error", out var error)
                || error.ValueKind != JsonValueKind.Object
                || !error.TryGetProperty(propertyName, out var parsedProperty))
            {
                return false;
            }

            property = parsedProperty.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string? value)
    {
        value = null;

        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static string GetAppIdUri(string endpoint)
    {
        var host = new Uri(endpoint).Host;
        if (!host.EndsWith(".azurewebsites.net", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"API Review Hub endpoint host is not allowed: {host}.");
        }

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

internal class ApiReviewHubRequestException(string message, System.Net.HttpStatusCode statusCode, string? errorCode, string? content)
    : HttpRequestException(message, null, statusCode)
{
    public string? ErrorCode { get; } = errorCode;
    public string? Content { get; } = content;
}