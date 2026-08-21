
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models.APIView;
namespace Azure.Sdk.Tools.Cli.Services.APIView;

using System.Net;

public interface IAPIViewService
{
    Task<string?> GetRevisionContent(string apiRevisionId, string reviewId, string contentReturnType, CancellationToken ct);
    Task<string?> GetCommentsByRevisionAsync(string revisionId, CancellationToken ct);
    Task<string?> GetMetadata(string revisionId, CancellationToken ct);
    Task<string?> Resolve(string url, CancellationToken ct);

    /// <summary>
    /// Creates or updates an API review from a CI pipeline build.
    /// Optionally tracks package versions, release tags, and source branches.
    /// </summary>
    Task<(string? content, int statusCode)> CreateCIReviewAsync(
        string buildId, string artifactName, string originalFilePath, string reviewFilePath,
        string repoName, string packageName, string project,
        string? label = null, bool compareAllRevisions = false, string? packageVersion = null,
        bool setReleaseTag = false, string? packageType = null, string? sourceBranch = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the canonical APIView review URL for a given package and language.
    /// </summary>
    Task<string?> GetReviewUrlByPackageAsync(string packageName, string language, string? version, CancellationToken ct);

    Task<APIViewMarkReleasedResult> MarkPackageReleasedAsync(
        string packageName,
        string language,
        string version,
        CancellationToken ct = default,
        bool dryRun = false);

    /// <summary>
    /// Submits API surface text for automated Copilot review.
    /// </summary>
    Task<(string? content, int statusCode)> StartCopilotReviewAsync(
        string apiText, string? language = null, string? baseApiText = null,
        string? outline = null, string? existingCommentsJson = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the status or results of a Copilot review job.
    /// </summary>
    Task<(string? content, int statusCode)> GetCopilotReviewAsync(
        string jobId, CancellationToken ct = default);
}

public class APIViewService : IAPIViewService
{
    private readonly IAPIViewHttpService _httpService;
    private readonly ILogger<APIViewService> _logger;

    public APIViewService(
        IAPIViewHttpService httpService,
        ILogger<APIViewService> logger)
    {
        _httpService = httpService;
        _logger = logger;
    }

    public async Task<string?> GetCommentsByRevisionAsync(string revisionId, CancellationToken ct)
    {
        string endpoint = $"/api/Comments/getRevisionComments?apiRevisionId={revisionId}";
        (string? result, _) = await _httpService.GetAsync(endpoint, ct);

        if (result == null)
        {
            _logger.LogWarning("No comments found for revision {RevisionId}", revisionId);
        }

        return result;
    }

    public async Task<string?> GetRevisionContent(string apiRevisionId, string reviewId, string contentReturnType, CancellationToken ct)
    {
        string revisionContentEndpoint = $"/api/apirevisions/getRevisionContent?apiRevisionId={apiRevisionId}&reviewId={reviewId}&contentReturnType={contentReturnType}";
        (string? result, _) = await _httpService.GetAsync(revisionContentEndpoint, ct);
        if (string.IsNullOrWhiteSpace(result))
        {
            _logger.LogWarning("Received empty response for revisions {ActiveRevisionId}", apiRevisionId);
            return null;
        }

        return result;
    }

    public async Task<string?> GetMetadata(string revisionId, CancellationToken ct)
    {
        string endpoint = $"/api/reviews/metadata?revisionId={revisionId}";
        (string? result, _) = await _httpService.GetAsync(endpoint, ct);

        if (result == null)
        {
            _logger.LogWarning("No metadata found for revision {RevisionId}", revisionId);
        }

        return result;
    }

    public async Task<string?> Resolve(string url, CancellationToken ct)
    {
        string endpoint = $"/api/reviews/resolve?link={url}";
        (string? result, _) = await _httpService.GetAsync(endpoint, ct);

        if (result == null)
        {
            _logger.LogWarning("Failed to resolve URL {Url}", url);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<(string? content, int statusCode)> CreateCIReviewAsync(
        string buildId,
        string artifactName,
        string originalFilePath,
        string reviewFilePath,
        string repoName,
        string packageName,
        string project,
        string? label = null,
        bool compareAllRevisions = false,
        string? packageVersion = null,
        bool setReleaseTag = false,
        string? packageType = null,
        string? sourceBranch = null,
        CancellationToken ct = default
    ) {
        var queryParams = new List<string>
        {
            $"buildId={Uri.EscapeDataString(buildId)}",
            $"artifactName={Uri.EscapeDataString(artifactName)}",
            $"originalFilePath={Uri.EscapeDataString(originalFilePath)}",
            $"reviewFilePath={Uri.EscapeDataString(reviewFilePath)}",
            $"repoName={Uri.EscapeDataString(repoName)}",
            $"packageName={Uri.EscapeDataString(packageName)}",
            $"project={Uri.EscapeDataString(project)}",
            $"compareAllRevisions={(compareAllRevisions ? "true" : "false")}"
        };

        if (!string.IsNullOrEmpty(label))
        {
            queryParams.Add($"label={Uri.EscapeDataString(label)}");
        }

        if (!string.IsNullOrEmpty(packageVersion))
        {
            queryParams.Add($"packageVersion={Uri.EscapeDataString(packageVersion)}");
        }

        if (setReleaseTag)
        {
            queryParams.Add("setReleaseTag=true");
        }

        if (!string.IsNullOrEmpty(packageType))
        {
            queryParams.Add($"packageType={Uri.EscapeDataString(packageType)}");
        }

        if (!string.IsNullOrEmpty(sourceBranch))
        {
            queryParams.Add($"sourceBranch={Uri.EscapeDataString(sourceBranch)}");
        }

        string endpoint = $"/autoreview/create?{string.Join("&", queryParams)}";

        return await _httpService.PostAsync(endpoint, ct);
    }

    public async Task<APIViewMarkReleasedResult> MarkPackageReleasedAsync(
        string packageName,
        string language,
        string version,
        CancellationToken ct = default,
        bool dryRun = false)
    {
        string endpoint = $"/api/reviews/mark-released?packageName={Uri.EscapeDataString(packageName)}&language={Uri.EscapeDataString(language)}&version={Uri.EscapeDataString(version)}&dryRun={dryRun.ToString().ToLowerInvariant()}";
        (string? content, _) = await _httpService.PostAsync(endpoint, ct);
        return JsonSerializer.Deserialize<APIViewMarkReleasedResult>(content ?? string.Empty)
            ?? throw new InvalidDataException("APIView returned an empty mark-released response.");
    }

    /// <inheritdoc />
    public async Task<string?> GetReviewUrlByPackageAsync(string packageName, string language, string? version, CancellationToken ct)
    {
        var queryParams = new List<string>
        {
            $"package={Uri.EscapeDataString(packageName)}",
            $"language={Uri.EscapeDataString(language)}",
            "redirect=false"
        };

        if (!string.IsNullOrEmpty(version))
        {
            queryParams.Add($"version={Uri.EscapeDataString(version)}");
        }

        string endpoint = $"/review?{string.Join("&", queryParams)}";

        try
        {
            (string? result, _) = await _httpService.GetAsync(endpoint, ct);

            if (string.IsNullOrWhiteSpace(result))
            {
                return null;
            }

            var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(result);
            return json.GetProperty("url").GetString();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<(string? content, int statusCode)> StartCopilotReviewAsync(
        string apiText,
        string? language = null,
        string? baseApiText = null,
        string? outline = null,
        string? existingCommentsJson = null,
        CancellationToken ct = default
    )
    {
        var payload = new Dictionary<string, object?> { ["target"] = apiText };

        if (!string.IsNullOrEmpty(language))
        {
            payload["language"] = language;
        }

        if (!string.IsNullOrEmpty(baseApiText))
        {
            payload["base"] = baseApiText;
        }

        if (!string.IsNullOrEmpty(outline))
        {
            payload["outline"] = outline;
        }

        if (!string.IsNullOrEmpty(existingCommentsJson))
        {
            using var existingCommentsDoc = JsonDocument.Parse(existingCommentsJson);
            if (existingCommentsDoc.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("existingCommentsJson must be a JSON array.", nameof(existingCommentsJson));
            }
            payload["existingComments"] = existingCommentsDoc.RootElement.Clone();
        }

        string jsonBody = JsonSerializer.Serialize(payload);
        return await _httpService.PostAsync("/api/reviews/start-copilot-review-job", jsonBody, ct);
    }

    /// <inheritdoc />
    public async Task<(string? content, int statusCode)> GetCopilotReviewAsync(string jobId, CancellationToken ct = default)
    {
        string endpoint = $"/api/reviews/get-copilot-review-job/{Uri.EscapeDataString(jobId)}";
        return await _httpService.GetAsync(endpoint, ct);
    }
}
