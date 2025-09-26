using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models.APIView;
using Azure.Sdk.Tools.Cli.Services.APIView;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IAPIViewService
{
    Task<string?> GetRevisionContent(string apiRevisionId, string reviewId, string selectionType,
        string contentReturnType, string environment = "production");
    Task<string?> GetCommentsByRevisionAsync(string revisionId, string environment = "production");
    Task<AuthenticationStatus> CheckAuthenticationStatusAsync(string environment = "production");
    Task<AuthenticationGuidance> GetAuthenticationGuidanceAsync();
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

    public async Task<string?> GetRevisionContent(string apiRevisionId, string reviewId, string selectionType, string contentReturnType, string environment = "production")
    {
        string revisionContentEndpoint = $"/api/apirevisions/getRevisionContent?apiRevisionId={apiRevisionId}&reviewId={reviewId}&selectionType={selectionType}&contentReturnType={contentReturnType}";
        string? result = await _httpService.GetAsync(revisionContentEndpoint, "get revision content", environment);
        if (string.IsNullOrWhiteSpace(result))
        {
            _logger.LogWarning("Received empty response for revisions {ActiveRevisionId}", apiRevisionId);
            return null;
        }

        return result;
    }

    public async Task<AuthenticationStatus> CheckAuthenticationStatusAsync(string environment = "production")
    {
        return await _authService.CheckAuthenticationStatusAsync(environment);
    }

    public async Task<AuthenticationGuidance> GetAuthenticationGuidanceAsync()
    {
        string? token = await _authService.GetAuthenticationTokenAsync();
        return new AuthenticationGuidance
        {
            IsAuthenticated = !string.IsNullOrEmpty(token),
            CurrentTokenSource = "azure-credentials",
            Instructions = _authService.GetAuthenticationGuidance(),
            QuickSetup = "Run: az login"
        };
    }
}
