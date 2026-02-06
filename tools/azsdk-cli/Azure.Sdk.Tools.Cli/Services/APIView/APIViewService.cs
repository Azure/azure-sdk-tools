using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.APIView;

public interface IAPIViewService
{
    Task<string?> GetRevisionContent(string apiRevisionId, string reviewId, string contentReturnType);
    Task<string?> GetCommentsByRevisionAsync(string revisionId);
    Task<string?> GetMetadata(string revisionId);
    Task<ResolvePackageResponse?> Resolve(string url);
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

    public async Task<string?> GetCommentsByRevisionAsync(string revisionId)
    {
        string endpoint = $"/api/Comments/getRevisionComments?apiRevisionId={revisionId}";
        string? result = await _httpService.GetAsync(endpoint);

        if (result == null)
        {
            _logger.LogWarning("No comments found for revision {RevisionId}", revisionId);
        }

        return result;
    }

    public async Task<string?> GetRevisionContent(string apiRevisionId, string reviewId, string contentReturnType)
    {
        string revisionContentEndpoint = $"/api/apirevisions/getRevisionContent?apiRevisionId={apiRevisionId}&reviewId={reviewId}&contentReturnType={contentReturnType}";
        string? result = await _httpService.GetAsync(revisionContentEndpoint);
        if (string.IsNullOrWhiteSpace(result))
        {
            _logger.LogWarning("Received empty response for revisions {ActiveRevisionId}", apiRevisionId);
            return null;
        }

        return result;
    }

    public async Task<string?> GetMetadata(string revisionId)
    {
        string endpoint = $"/api/reviews/metadata?revisionId={revisionId}";
        string? result = await _httpService.GetAsync(endpoint);

        if (result == null)
        {
            _logger.LogWarning("No metadata found for revision {RevisionId}", revisionId);
        }

        return result;
    }

    public async Task<ResolvePackageResponse?> Resolve(string url)
    {
        string endpoint = $"/api/reviews/resolve?link={url}";
        string? result = await _httpService.GetAsync(endpoint);

        if (result == null)
        {
            _logger.LogWarning("Failed to resolve URL {Url}", url);
            return null;
        }

        try
        {
            var resolveResponse = JsonSerializer.Deserialize<ResolvePackageResponse>(result);
            return resolveResponse;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize resolve response for URL {Url}", url);
            return null;
        }
    }
}
