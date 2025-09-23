using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.Cli.Configuration;

namespace Azure.Sdk.Tools.Cli.Services.APIView;

public interface IAPIViewAuthenticationService
{
    Task<string?> GetAuthenticationTokenAsync(string environment = "production");
    Task ConfigureAuthenticationAsync(HttpClient httpClient, string environment = "production");
    Task<string> CheckAuthenticationStatusAsync(string? endpoint = null);
    string GetAuthenticationGuidance();
    bool IsAuthenticationFailure(string content);
    string CreateAuthenticationErrorResponse(string message, string revisionId = null, string activeRevisionId = null,
        string diffRevisionId = null, string baseUrl = null);
}

public class APIViewAuthenticationService : IAPIViewAuthenticationService
{
    private readonly ILogger<APIViewAuthenticationService> _logger;

    public APIViewAuthenticationService(ILogger<APIViewAuthenticationService> logger)
    {
        _logger = logger;
    }

    public async Task<string?> GetAuthenticationTokenAsync(string environment = "production")
    {
        try
        {
            var credential = new ChainedTokenCredential(
                new AzureCliCredential(),
                new AzurePowerShellCredential(),
                new DefaultAzureCredential());
                
            string scope = APIViewConfiguration.ApiViewScopes[environment];
            
            if (string.IsNullOrEmpty(scope))
            {
                _logger.LogWarning("Environment '{Environment}' not valid.", environment);
                return null;
            }

            TokenRequestContext tokenRequest = new([scope]);
            AccessToken tokenResponse = await credential.GetTokenAsync(tokenRequest);

            _logger.LogInformation("Successfully obtained Azure token with scope {Scope}", scope);
            return tokenResponse.Token;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Azure token with scope {Scope}: {Message}", 
                APIViewConfiguration.ApiViewScopes.GetValueOrDefault(environment, "unknown"), ex.Message);
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

    public string GetAuthenticationGuidance()
    {
        return @"Authentication is required to access APIView data. Please authenticate using Azure credentials:

**Azure CLI**:
   - Run: az login
   - Ensure you have access to APIView resources

**Azure PowerShell**:
   - Run: Connect-AzAccount
   - Ensure you have access to APIView resources

**Managed Identity** (when running in Azure):
   - Authentication will happen automatically
   - Ensure the managed identity has appropriate APIView permissions

The authentication service will automatically try Azure CLI, Azure PowerShell, and managed identity credentials in that order.";
    }

    public async Task<string> CheckAuthenticationStatusAsync(string? environmentOption = null)
    {
        string environment = environmentOption ?? "production";
        string baseUrl = APIViewConfiguration.BaseUrlEndpoints[environment];
        string? token = await GetAuthenticationTokenAsync(environment);
        
        bool hasToken = !string.IsNullOrEmpty(token);
        bool isAuthenticationWorking = false;
        string? authenticationError = null;

        if (hasToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                await ConfigureAuthenticationAsync(httpClient, environment);
                
                string testUrl = $"{baseUrl}/api/authtest/status";
                using var response = await httpClient.GetAsync(testUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    isAuthenticationWorking = responseContent.Contains("\"IsAuthenticated\":true", StringComparison.OrdinalIgnoreCase);
                    
                    if (!isAuthenticationWorking)
                    {
                        authenticationError = "Token exists but authentication failed - token may be invalid or expired";
                    }
                }
                else
                {
                    authenticationError = $"Authentication test failed with status {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                authenticationError = $"Authentication test failed: {ex.Message}";
            }
        }

        var status = new
        {
            hasToken,
            isAuthenticationWorking,
            tokenSource = "azure-credentials",
            endpoint = baseUrl,
            authenticationError,
            guidance = isAuthenticationWorking ? "Authentication working successfully" : GetAuthenticationGuidance()
        };

        return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
    }

    public bool IsAuthenticationFailure(string content)
    {
        return content.Contains("Account/Login") ||
               (content.Contains("login") && content.Contains("<html")) ||
               content.Contains("authentication required", StringComparison.OrdinalIgnoreCase);
    }

    public string CreateAuthenticationErrorResponse(string message, string revisionId = null,
        string activeRevisionId = null, string diffRevisionId = null, string baseUrl = null)
    {
        string guidance = GetAuthenticationGuidance();

        var errorResponse = new
        {
            error = "Authentication Required",
            message,
            guidance,
            revisionId,
            activeRevisionId,
            diffRevisionId,
            loginUrl = !string.IsNullOrEmpty(baseUrl) ? $"{baseUrl}/Account/Login" : null
        };

        return JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = true });
    }
}
