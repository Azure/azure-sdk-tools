using System.Text.Json;
using Azure.Sdk.Tools.Cli.Services.APIView;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IAPIViewService
{
    Task<string?> GetCommentByRevisionAsync(string revisionId, string environment = "production", string? authToken = null);
    Task<string?> GetRevisionContent(string reviewId, string activeRevisionId, string? diffRevisionId, string environment = "production", string? authToken = null);
    Task<string?> ListReviewVersions(string reviewId, string environment = "production", string? authToken = null);
    Task<string?> GetLatestRevisionAsync(string reviewId, string environment = "production", string? authToken = null);
    Task<string> CheckAuthenticationStatusAsync(string environment = "production");
    Task<string> GetAuthenticationGuidanceAsync();
}

public class APIViewService : IAPIViewService
{
    private readonly IAPIViewAuthenticationService _authService;
    private readonly IAPIViewHttpService _httpService;
    private readonly ILogger<APIViewService> _logger;

    public APIViewService(
        IAPIViewHttpService httpService,
        IAPIViewAuthenticationService authService,
        ILogger<APIViewService> logger)
    {
        _httpService = httpService;
        _authService = authService;
        _logger = logger;
    }

    public async Task<string?> GetCommentByRevisionAsync(string revisionId, string environment = "production", string? authToken = null)
    {
        try
        {
            string endpoint = $"/api/Comments/getRevisionComments?apiRevisionId={revisionId}";
            string? result = await _httpService.GetAsync(endpoint, "comments", environment, authToken);

            if (result == null)
            {
                _logger.LogWarning("No comments found for revision {RevisionId}", revisionId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get comments for revision {RevisionId}", revisionId);
            return null;
        }
    }

    public async Task<string?> GetRevisionContent(string reviewId, string activeRevisionId, string? diffRevisionId, string environment = "production", string? authToken = null)
    {
        try
        {
            string endpoint = $"/api/reviews/{reviewId}/content?activeApiRevisionId={activeRevisionId}&diffApiRevisionId={diffRevisionId}";
            string? result = await _httpService.GetAsync(endpoint, "get revision content", environment, authToken);

            if (string.IsNullOrWhiteSpace(result))
            {
                _logger.LogWarning("Received empty response for revisions {ActiveRevisionId} - {DiffRevisionId}",
                    activeRevisionId, diffRevisionId);
                return null;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get diff between revisions: {ActiveRevisionId} - {DiffRevisionId}",
                activeRevisionId, diffRevisionId);
            return null;
        }
    }

    public async Task<string?> ListReviewVersions(string reviewId, string environment = "production", string? authToken = null)
    {
        try
        {
            string endpoint = $"/api/apirevisions/{reviewId}/getReviewVersions";
            string? result = await _httpService.GetAsync(endpoint, "review revisions", environment, authToken);

            if (result == null)
            {
                _logger.LogWarning("No revisions found for review {ReviewId}", reviewId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get revisions for review {ReviewId}", reviewId);
            return null;
        }
    }

    public async Task<string?> GetLatestRevisionAsync(string reviewId, string environment = "production", string? authToken = null)
    {
        try
        {
            string endpoint = $"/api/apirevisions/getRevisionText?reviewId={reviewId}&selectionType=Latest";
            string? result = await _httpService.GetAsync(endpoint, "latest revision", environment, authToken);

            if (result == null)
            {
                _logger.LogWarning("No latest revision found for review {ReviewId}", reviewId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest revision for review {ReviewId}", reviewId);
            return null;
        }
    }

    public async Task<string> CheckAuthenticationStatusAsync(string environment = "production")
    {
        return await _authService.CheckAuthenticationStatusAsync(environment);
    }

    public async Task<string> GetAuthenticationGuidanceAsync()
    {
        string? token = await _authService.GetAuthenticationTokenAsync();
        var guidance = new
        {
            isAuthenticated = !string.IsNullOrEmpty(token),
            currentTokenSource = _authService.GetTokenSource(token),
            instructions = _authService.GetAuthenticationGuidance(),
            quickSetup = new
            {
                githubToken = "Set environment variable: GITHUB_TOKEN=your_token_here",
                azureCli = "Run: az login",
                githubCli = "Run: gh auth login"
            }
        };

        return JsonSerializer.Serialize(guidance, new JsonSerializerOptions { WriteIndented = true });
    }
}
