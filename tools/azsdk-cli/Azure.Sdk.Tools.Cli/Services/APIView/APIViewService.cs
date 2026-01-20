namespace Azure.Sdk.Tools.Cli.Services.APIView;

public interface IAPIViewService
{
    Task<string?> GetRevisionContent(string apiRevisionId, string reviewId, string contentReturnType, string environment);
    Task<string?> GetCommentsByRevisionAsync(string revisionId, string environment);
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

    public async Task<string?> GetCommentsByRevisionAsync(string revisionId, string environment)
    {
        string endpoint = $"/api/Comments/getRevisionComments?apiRevisionId={revisionId}";
        string? result = await _httpService.GetAsync(endpoint, environment);

        if (result == null)
        {
            _logger.LogWarning("No comments found for revision {RevisionId}", revisionId);
        }

        return result;
    }

    public async Task<string?> GetRevisionContent(string apiRevisionId, string reviewId, string contentReturnType, string environment)
    {
        string revisionContentEndpoint = $"/api/apirevisions/getRevisionContent?apiRevisionId={apiRevisionId}&reviewId={reviewId}&contentReturnType={contentReturnType}";
        string? result = await _httpService.GetAsync(revisionContentEndpoint, environment);
        if (string.IsNullOrWhiteSpace(result))
        {
            _logger.LogWarning("Received empty response for revisions {ActiveRevisionId}", apiRevisionId);
            return null;
        }

        return result;
    }
}
