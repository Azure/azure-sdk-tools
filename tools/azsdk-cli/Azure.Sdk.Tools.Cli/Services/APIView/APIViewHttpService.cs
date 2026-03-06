using Azure.Sdk.Tools.Cli.Configuration;

namespace Azure.Sdk.Tools.Cli.Services.APIView;

public interface IAPIViewHttpService
{
    Task<(string? content, int statusCode)> GetAsync(string endpoint);
    Task<(string? content, int statusCode)> PostAsync(string endpoint);
}

public class APIViewHttpService : IAPIViewHttpService
{
    private readonly IAPIViewAuthenticationService _authService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<APIViewHttpService> _logger;
    
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

    private static string GetEnvironment()
    {
        return Environment.GetEnvironmentVariable("APIVIEW_ENVIRONMENT") ?? "production";
    }

    public async Task<(string? content, int statusCode)> GetAsync(string endpoint)
    {
        try
        {
            string environment = GetEnvironment();
            string baseUrl = APIViewConfiguration.BaseUrlEndpoints[environment];
            HttpClient httpClient = await GetOrCreateAuthenticatedClientAsync(environment);

            string requestUrl = $"{baseUrl}{endpoint}";
            using HttpResponseMessage response = await httpClient.GetAsync(requestUrl);

            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                string truncated = content.Length > 500 ? content[..500] + "...[truncated]" : content;
                _logger.LogError("API call failed during GET {Endpoint} with status code {StatusCode}: {Content}",
                    endpoint, response.StatusCode, truncated);
            }

            return (content, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during GET {Endpoint}", endpoint);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GET {Endpoint}", endpoint);
            throw;
        }
    }

    public async Task<(string? content, int statusCode)> PostAsync(string endpoint)
    {
        try
        {
            string environment = GetEnvironment();
            string baseUrl = APIViewConfiguration.BaseUrlEndpoints[environment];
            HttpClient httpClient = await GetOrCreateAuthenticatedClientAsync(environment);

            string requestUrl = $"{baseUrl}{endpoint}";
            using HttpResponseMessage response = await httpClient.PostAsync(requestUrl, new StringContent(string.Empty));

            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                string truncated = content.Length > 500 ? content[..500] + "...[truncated]" : content;
                _logger.LogError("API call failed during POST {Endpoint} with status code {StatusCode}: {Content}",
                    endpoint, response.StatusCode, truncated);
            }

            return (content, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during POST {Endpoint}", endpoint);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during POST {Endpoint}", endpoint);
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
