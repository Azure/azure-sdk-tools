using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Services
{
    public class CopilotHttpService : ICopilotHttpService
    {
        private readonly string _endpoint;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ICopilotAuthenticationService _authService;

        public CopilotHttpService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ICopilotAuthenticationService authService)
        {
            _endpoint = configuration["CopilotServiceEndpoint"];
            _httpClientFactory = httpClientFactory;
            _authService = authService;
        }

        public async Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken = default)
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, path, body: null, cancellationToken);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<TResponse>(responseBody);
        }

        public async Task<TResponse> PostAsync<TResponse>(string path, object body, CancellationToken cancellationToken = default)
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Post, path, body, cancellationToken);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<TResponse>(responseBody);
        }

        public async Task PostAsync(string path, object body, CancellationToken cancellationToken = default)
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Post, path, body, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body = null, CancellationToken cancellationToken = default)
        {
            string url = BuildUrl(path);
            using var request = new HttpRequestMessage(method, url);
            if (body != null)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json");
            }
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", await _authService.GetAccessTokenAsync(cancellationToken));

            HttpClient client = _httpClientFactory.CreateClient();
            return await client.SendAsync(request, cancellationToken);
        }

        private string BuildUrl(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return _endpoint;
            }
            return $"{_endpoint.TrimEnd('/')}/{path.TrimStart('/')}";
        }
    }
}
