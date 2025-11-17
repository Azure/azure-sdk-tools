using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models.APIView;

namespace Azure.Sdk.Tools.Cli.Services.APIView;

public interface IAPIViewAuthenticationService
{
    Task ConfigureAuthenticationAsync(HttpClient httpClient, string environment = "production");
    AuthenticationErrorResponse CreateAuthenticationErrorResponse(string message, string revisionId = null, string activeRevisionId = null,
        string diffRevisionId = null, string baseUrl = null);
}

public class APIViewAuthenticationService : IAPIViewAuthenticationService
{
    private readonly ILogger<APIViewAuthenticationService> _logger;
    private readonly IAzureService _azureService;

    public APIViewAuthenticationService(IAzureService azureService,
        ILogger<APIViewAuthenticationService> logger)
    {
        _azureService = azureService;
        _logger = logger;
    }

    public async Task<string?> GetAuthenticationTokenAsync(string environment = "production")
    {
        try
        {
            var credential = _azureService.GetCredential();

            string scope = APIViewConfiguration.ApiViewScopes[environment];
            if (string.IsNullOrEmpty(scope))
            {
                _logger.LogWarning("Environment '{Environment}' not valid.", environment);
                return null;
            }

            TokenRequestContext tokenRequest = new([scope]);
            AccessToken? tokenResponse = await credential.GetTokenAsync(tokenRequest, CancellationToken.None);

            _logger.LogInformation("Successfully obtained Azure token with scope {Scope}", scope);
            return tokenResponse?.Token;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Azure token with scope {Scope}", APIViewConfiguration.ApiViewScopes.GetValueOrDefault(environment, "unknown"));
            return null;
        }
    }

    public async Task ConfigureAuthenticationAsync(HttpClient httpClient, string environment = "production")
    {
        string? token = await GetAuthenticationTokenAsync(environment);
        if (!string.IsNullOrEmpty(token))
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            httpClient.DefaultRequestHeaders.Add("X-API-Key", token);
        }

        httpClient.DefaultRequestHeaders.Add("User-Agent", APIViewConfiguration.UserAgent);
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        _logger.LogDebug("Request headers configured - Authorization: {HasAuth}, User-Agent: {UserAgent}",
            !string.IsNullOrEmpty(token) ? "Bearer [TOKEN]" : "None", APIViewConfiguration.UserAgent);
    }

    private string GetAuthenticationGuidance()
    {
        return @"Authentication is required to access APIView data. Please authenticate using Azure credentials:

**Azure CLI**:
   - Run: az login

The authentication service will automatically try Azure CLI";
    }

    public static bool IsAuthenticationFailure(string content)
    {
        return content.Contains("Account/Login") ||
               (content.Contains("login") && content.Contains("<html")) ||
               content.Contains("authentication required", StringComparison.OrdinalIgnoreCase);
    }

    public AuthenticationErrorResponse CreateAuthenticationErrorResponse(string message, string revisionId = null,
        string activeRevisionId = null, string diffRevisionId = null, string baseUrl = null)
    {
        return new AuthenticationErrorResponse
        {
            Error = "Authentication Required",
            Message = message,
            Guidance = GetAuthenticationGuidance(),
            RevisionId = revisionId,
            ActiveRevisionId = activeRevisionId,
            DiffRevisionId = diffRevisionId,
            LoginUrl = !string.IsNullOrEmpty(baseUrl) ? $"{baseUrl}/Account/Login" : null
        };
    }
}
