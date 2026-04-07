using Azure.Sdk.Tools.Cli.Configuration;

namespace Azure.Sdk.Tools.Cli.Services.APIView;

public interface IAPIViewHttpService
{
    Task<(string? content, int statusCode)> GetAsync(string endpoint, CancellationToken ct);
    Task<(string? content, int statusCode)> PostAsync(string endpoint, CancellationToken ct);
}

public class APIViewHttpService : IAPIViewHttpService
{
    private readonly IAPIViewAuthenticationService _authService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<APIViewHttpService> _logger;
    private readonly string _environment;
    private readonly string _baseUrl;

    private HttpClient? _cachedClient;
    private DateTime _cacheExpiry = DateTime.MinValue;
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
        _environment = Environment.GetEnvironmentVariable("APIVIEW_ENVIRONMENT") ?? "production";
        _baseUrl = APIViewConfiguration.BaseUrlEndpoints[_environment];
    }

    public async Task<(string? content, int statusCode)> GetAsync(string endpoint, CancellationToken ct)
    {
        HttpClient httpClient = await GetOrCreateAuthenticatedClientAsync(ct);

        string requestUrl = $"{_baseUrl}{endpoint}";
        using HttpResponseMessage response = await httpClient.GetAsync(requestUrl, ct);

        string content = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            return (content, (int)response.StatusCode);
        }

        string truncated = content.Length > 500 ? content[..500] + "...[truncated]" : content;
        _logger.LogError("API call failed: GET {Endpoint} returned {StatusCode}: {Content}",
            endpoint, response.StatusCode, truncated);
        throw new HttpRequestException(
            $"GET {endpoint} failed with status code {(int)response.StatusCode}",
            null, response.StatusCode);

    }

    public async Task<(string? content, int statusCode)> PostAsync(string endpoint, CancellationToken ct)
    {
        HttpClient httpClient = await GetOrCreateAuthenticatedClientAsync(ct);

        string requestUrl = $"{_baseUrl}{endpoint}";
        using HttpResponseMessage response = await httpClient.PostAsync(requestUrl, new StringContent(string.Empty), ct);

        string content = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            return (content, (int)response.StatusCode);
        }

        string truncated = content.Length > 500 ? content[..500] + "...[truncated]" : content;
        _logger.LogError("API call failed: POST {Endpoint} returned {StatusCode}: {Content}",
            endpoint, response.StatusCode, truncated);
        throw new HttpRequestException(
            $"POST {endpoint} failed with status code {(int)response.StatusCode}",
            null, response.StatusCode);

    }

    private async Task<HttpClient> GetOrCreateAuthenticatedClientAsync(CancellationToken ct)
    {
        if (_cachedClient != null &&
            DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedClient;
        }

        await _cacheLock.WaitAsync(ct);
        try
        {
            if (_cachedClient != null &&
                DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedClient;
            }

            HttpClient newClient = _httpClientFactory.CreateClient();
            await _authService.ConfigureAuthenticationAsync(newClient, _environment, ct);

            _cachedClient = newClient;
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);

            return _cachedClient;
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}
