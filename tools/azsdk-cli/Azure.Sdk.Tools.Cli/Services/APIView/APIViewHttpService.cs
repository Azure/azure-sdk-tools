using System.Net;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models.APIView;

namespace Azure.Sdk.Tools.Cli.Services.APIView;

public interface IAPIViewHttpService
{
    void ConfigureEnvironment(string environment);
    Task<(string? content, int statusCode)> GetAsync(string endpoint, CancellationToken ct);
    Task<(string? content, int statusCode)> PostAsync(string endpoint, CancellationToken ct);
}

public class APIViewHttpService : IAPIViewHttpService
{
    private readonly IAPIViewAuthenticationService _authService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<APIViewHttpService> _logger;

    private string _environment = "production";
    private string _baseUrl = APIViewConfiguration.BaseUrlEndpoints["production"];

    private HttpClient? _cachedClient;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private string? _cachedEnvironment;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(50); 

    public APIViewHttpService(
        IHttpClientFactory httpClientFactory,
        IAPIViewAuthenticationService authService,
        ILogger<APIViewHttpService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _logger = logger;
    }

    public void ConfigureEnvironment(string environment)
    {
        // Skip invalidation if the environment hasn't changed
        if (_environment == environment)
        {
            return;
        }
        _environment = environment;
        _baseUrl = APIViewConfiguration.BaseUrlEndpoints[environment];
        // Invalidate cached client since auth scopes differ per environment
        _cachedClient = null;
        _cacheExpiry = DateTime.MinValue;
    }

    public async Task<string?> GetAsync(string endpoint, string environment)
    {
        string baseUrl = _baseUrl;
        HttpClient httpClient = await GetOrCreateAuthenticatedClientAsync(ct);

        string requestUrl = $"{baseUrl}{endpoint}";
        using HttpResponseMessage response = await httpClient.GetAsync(requestUrl, ct);

        string content = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            string baseUrl = APIViewConfiguration.BaseUrlEndpoints[environment];
            HttpClient httpClient = await GetOrCreateAuthenticatedClientAsync(environment);

            string requestUrl = $"{baseUrl}{endpoint}";
            using HttpResponseMessage response = await httpClient.GetAsync(requestUrl);
            if (response.StatusCode == HttpStatusCode.Found || response.StatusCode == HttpStatusCode.Redirect)
            {
                string? location = response.Headers.Location?.ToString();
                if (!string.IsNullOrEmpty(location) && (location.Contains("Login") || location.Contains("login")))
                {
                    _logger.LogError("Authentication required: Redirected to login page at {Location}", location);
                    AuthenticationErrorResponse errorResponse = _authService.CreateAuthenticationErrorResponse(
                        $"APIView requires authentication to access {endpoint}",
                        baseUrl: baseUrl);
                    return JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = true });
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("API call failed during {Endpoint} with status code {StatusCode}: {ReasonPhrase}",
                    endpoint, response.StatusCode, response.ReasonPhrase);

    public async Task<(string? content, int statusCode)> PostAsync(string endpoint, CancellationToken ct)
    {
        string baseUrl = _baseUrl;
        HttpClient httpClient = await GetOrCreateAuthenticatedClientAsync(ct);

        string requestUrl = $"{baseUrl}{endpoint}";
        using HttpResponseMessage response = await httpClient.PostAsync(requestUrl, new StringContent(string.Empty), ct);

            string content = await response.Content.ReadAsStringAsync();

            if (APIViewAuthenticationService.IsAuthenticationFailure(content))
            {
                _logger.LogError("Authentication required: Received login page instead of {Endpoint} data", endpoint);
                AuthenticationErrorResponse errorResponse = _authService.CreateAuthenticationErrorResponse(
                    $"APIView requires authentication to access {endpoint}",
                    baseUrl: baseUrl);
                return JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = true });
            }

            return content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during {Endpoint} call", endpoint);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during {Endpoint} call", endpoint);
            throw;
        }
    }

    private async Task<HttpClient> GetOrCreateAuthenticatedClientAsync(string environment)
    {
        if (_cachedClient != null && 
            _cachedEnvironment == environment && 
            DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedClient;
        }

        await _cacheLock.WaitAsync();
        try
        {
            if (_cachedClient != null && 
                _cachedEnvironment == environment && 
                DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedClient;
            }
            
            HttpClient newClient = _httpClientFactory.CreateClient();
            await _authService.ConfigureAuthenticationAsync(newClient, environment);
            
            _cachedClient = newClient;
            _cachedEnvironment = environment;
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
                        
            return _cachedClient;
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}
