using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;

namespace Azure.Sdk.Tools.Cli.Services.APIView;

public interface IAPIViewAuthenticationService
{
    Task<string?> GetAuthenticationTokenAsync(string? providedToken = null, string environment = "production");
    Task ConfigureAuthenticationAsync(HttpClient httpClient, string environment = "production", string? providedToken = null);
    Task<string> CheckAuthenticationStatusAsync(string? endpoint = null);
    string GetAuthenticationGuidance();
    string GetTokenSource(string? token);
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

    public async Task<string?> GetAuthenticationTokenAsync(string? providedToken = null,
        string environment = "production")
    {
        if (!string.IsNullOrEmpty(providedToken))
        {
            return providedToken;
        }

        string? githubToken = GetGitHubToken();
        if (!string.IsNullOrEmpty(githubToken))
        {
            _logger.LogDebug("Using GitHub token for authentication");
            return githubToken;
        }

        try
        {
            DefaultAzureCredential credential = new();
            string scope = APIViewConfiguration.ApiViewScopes[environment];
            
            if (string.IsNullOrEmpty(scope))
            {
                _logger.LogWarning("Environment not valid.");
                return null;
            }

            try
            {
                TokenRequestContext tokenRequest = new([scope]);
                AccessToken tokenResponse = await credential.GetTokenAsync(tokenRequest);

                _logger.LogInformation("Successfully obtained Azure token with scope {Scope}", scope);
                return tokenResponse.Token;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get Azure token with scope {Scope}: {Message}", scope, ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get any Azure token, no authentication available");
        }

        _logger.LogWarning(
            "No authentication token available. APIView requests may fail if authentication is required.");
        return null;
    }

    public async Task ConfigureAuthenticationAsync(HttpClient httpClient, string environment = "production",
        string? providedToken = null)
    {
        string? token = await GetAuthenticationTokenAsync(providedToken, environment);
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
        return @"Authentication is required to access APIView data. Please choose one of these options:

1. **GitHub Token**:
   - Set environment variable: GITHUB_TOKEN=your_token_here
   - Or run: gh auth login

2. **Azure CLI**:
   - Run: az login
   - Ensure you have access to APIView resources

3. **Provide token directly**:
   - Pass authToken parameter to the MCP tool

To get a GitHub token:
1. Go to https://github.com/settings/tokens
2. Generate a new token with 'repo' and 'read:org' scopes
3. Set it as GITHUB_TOKEN environment variable";
    }

    public string GetTokenSource(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "none";
        }

        string? githubToken = Environment.GetEnvironmentVariable(APIViewConfiguration.GitHubTokenEnvironmentVariable);
        if (!string.IsNullOrEmpty(githubToken) && githubToken == token)
        {
            return "environment-variable";
        }

        try
        {
            DefaultAzureCredential credential = new();
            return "azure-managed-identity";
        }
        catch
        {
            return "github-cli-or-provided";
        }
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

    public async Task<string> CheckAuthenticationStatusAsync(string? environmentOption = null)
    {
        string environment = environmentOption ?? "production";
        string baseUrl = APIViewConfiguration.BaseUrlEndpoints[environment];
        string? token = await GetAuthenticationTokenAsync(null, environment);
        
        bool hasToken = !string.IsNullOrEmpty(token);
        bool isAuthenticationWorking = false;
        string? authenticationError = null;

        if (hasToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                await ConfigureAuthenticationAsync(httpClient, environment, token);
                
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
            tokenSource = GetTokenSource(token),
            endpoint = baseUrl,
            authenticationError,
            guidance = isAuthenticationWorking ? "Authentication working successfully" : GetAuthenticationGuidance()
        };

        return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
    }
    private string? GetGitHubToken()
    {
        string? githubToken = Environment.GetEnvironmentVariable(APIViewConfiguration.GitHubTokenEnvironmentVariable);
        if (!string.IsNullOrEmpty(githubToken))
        {
            return githubToken;
        }

        try
        {
            Process process = new();
            process.StartInfo.FileName = "gh";
            process.StartInfo.Arguments = "auth status --show-token";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();
            string output = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                Match match = Regex.Match(output, APIViewConfiguration.GitHubTokenRegex);
                if (match.Success)
                {
                    return match.Groups["token"]?.Value.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get GitHub token from gh CLI");
        }

        return null;
    }
}
