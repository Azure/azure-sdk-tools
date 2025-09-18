using System.Text.Json;
using Azure.Sdk.Tools.Cli.Services.APIView;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IAPIViewService
{
    Task<string?> GetCommentsByRevisionAsync(string revisionId, string environment = "production", string? authToken = null);
    Task<string?> GetRevisionCodeTokenFile(string activeRevisionId, string environment = "production", string? authToken = null);
    Task<string?> GetRevisionContentText(string revisionId, string environment = "production", string? authToken = null);
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

    public async Task<string?> GetCommentsByRevisionAsync(string revisionId, string environment = "production", string? authToken = null)
    {
        string endpoint = $"/api/Comments/getRevisionComments?apiRevisionId={revisionId}";
        string? result = await _httpService.GetAsync(endpoint, "comments", environment, authToken);

        if (result == null)
        {
            _logger.LogWarning("No comments found for revision {RevisionId}", revisionId);
        }

        return result;
    }

    public async Task<string?> GetRevisionCodeTokenFile(string activeRevisionId, string environment = "production", string? authToken = null)
    {
        string reviewIdEndpoint = $"/api/apirevisions/{activeRevisionId}/getReviewId";
        string? reviewId = await _httpService.GetAsync(reviewIdEndpoint, "get review ID", environment, authToken);

        string endpoint = $"/api/reviews/{reviewId}/content?activeApiRevisionId={activeRevisionId}";
        string? result = await _httpService.GetAsync(endpoint, "get revision content", environment, authToken);

        if (string.IsNullOrWhiteSpace(result))
        {
            _logger.LogWarning("Received empty response for revisions {ActiveRevisionId}", activeRevisionId);
            return null;
        }

        return result;
    }

    public async Task<string?> GetRevisionContentText(string revisionId, string environment = "production", string? authToken = null)
    {
        string endpoint = $"/api/apirevisions/getRevisionText?apiRevisionId={revisionId}";
        string? result = await _httpService.GetAsync(endpoint, "latest revision", environment, authToken);

        if (result == null)
        {
            _logger.LogWarning("No content found for revision= {RevisionId}", revisionId);
        }

        return result;
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
