using System.Net;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models.APIView;

namespace Azure.Sdk.Tools.Cli.Services.APIView;

public interface IAPIViewHttpService
{
    Task<string?> GetAsync(string endpoint, string operation, string environment = "production");
}

public class APIViewHttpService : IAPIViewHttpService
{
    private readonly IAPIViewAuthenticationService _authService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<APIViewHttpService> _logger;

    public APIViewHttpService(
        IHttpClientFactory httpClientFactory,
        IAPIViewAuthenticationService authService,
        ILogger<APIViewHttpService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _logger = logger;
    }

    public async Task<string?> GetAsync(string endpoint, string operation, string environment = "production")
    {
        try
        {
            string baseUrl = APIViewConfiguration.BaseUrlEndpoints[environment];
            HttpClient httpClient = _httpClientFactory.CreateClient();

            await _authService.ConfigureAuthenticationAsync(httpClient, environment);

            string requestUrl = $"{baseUrl}{endpoint}";
            using HttpResponseMessage response = await httpClient.GetAsync(requestUrl);
            if (response.StatusCode == HttpStatusCode.Found || response.StatusCode == HttpStatusCode.Redirect)
            {
                string? location = response.Headers.Location?.ToString();
                if (!string.IsNullOrEmpty(location) && (location.Contains("Login") || location.Contains("login")))
                {
                    _logger.LogError("Authentication required: Redirected to login page at {Location}", location);
                    AuthenticationErrorResponse errorResponse = _authService.CreateAuthenticationErrorResponse(
                        $"APIView requires authentication to access {operation}",
                        baseUrl: baseUrl);
                    return JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = true });
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("API call failed during {Operation} with status code {StatusCode}: {ReasonPhrase}",
                    operation, response.StatusCode, response.ReasonPhrase);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogError(
                        "Authentication failed. Ensure you have a valid GitHub token or Azure credentials.");
                }

                return null;
            }

            string content = await response.Content.ReadAsStringAsync();

            if (APIViewAuthenticationService.IsAuthenticationFailure(content))
            {
                _logger.LogError("Authentication required: Received login page instead of {Operation} data", operation);
                AuthenticationErrorResponse errorResponse = _authService.CreateAuthenticationErrorResponse(
                    $"APIView requires authentication to access {operation}",
                    baseUrl: baseUrl);
                return JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = true });
            }

            return content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during {Operation}", operation);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during {Operation}", operation);
            throw;
        }
    }
}
