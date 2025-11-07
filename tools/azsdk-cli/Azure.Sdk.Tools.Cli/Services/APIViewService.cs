using Azure.Sdk.Tools.Cli.Services.APIView;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IAPIViewService
{
    Task<string?> GetRevisionContent(string apiRevisionId, string reviewId, string contentReturnType, string environment = "production");
    Task<string?> GetCommentsByRevisionAsync(string revisionId, string environment = "production");
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

    public async Task<string?> GetCommentsByRevisionAsync(string revisionId, string environment = "production")
    {
        string endpoint = $"/api/Comments/getRevisionComments?apiRevisionId={revisionId}";
        string? result = await _httpService.GetAsync(endpoint, "comments", environment);

        if (result == null)
        {
            _logger.LogWarning("No comments found for revision {RevisionId}", revisionId);
        }

        return result;
    }

    public async Task<string?> GetRevisionContent(string apiRevisionId, string reviewId, string contentReturnType, string environment = "production")
    {
        string revisionContentEndpoint = $"/api/apirevisions/getRevisionContent?apiRevisionId={apiRevisionId}&reviewId={reviewId}&contentReturnType={contentReturnType}";
        string? result = await _httpService.GetAsync(revisionContentEndpoint, "get revision content", environment);
        if (string.IsNullOrWhiteSpace(result))
        {
            _logger.LogWarning("Received empty response for revisions {ActiveRevisionId}", apiRevisionId);
            return null;
        }

        return result;
    }
}
