using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AiCompletion;
using Azure.Sdk.Tools.Cli.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.Cli.Services
{
    /// <summary>
    /// Implementation of the AI completion service.
    /// </summary>
    public class AiCompletionService : IAiCompletionService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiCompletionService> _logger;
        private readonly AiCompletionOptions _options;
        private readonly JsonSerializerOptions _jsonOptions;

        public AiCompletionService(
            HttpClient httpClient,
            ILogger<AiCompletionService> logger,
            IOptions<AiCompletionOptions> options)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = _options.EnableDebugLogging
            };

            ConfigureHttpClient();
        }

        public async Task<CompletionResponse> SendCompletionRequestAsync(
            CompletionRequest request,
            string? apiKey = null,
            string? endpoint = null,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!ValidateRequest(request))
            {
                throw new ArgumentException("Request validation failed", nameof(request));
            }

            try
            {
                // Get endpoint and API key with environment variable override support
                endpoint = GetEffectiveEndpoint(endpoint);
                apiKey = GetEffectiveApiKey(apiKey);

                var requestUri = new Uri(new Uri(endpoint), "/completion");

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);

                if (!string.IsNullOrEmpty(apiKey))
                {
                    httpRequest.Headers.Add("X-API-KEY", apiKey);
                }

                httpRequest.Content = JsonContent.Create(request, options: _jsonOptions);

                if (_options.EnableDebugLogging)
                {
                    var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
                    _logger.LogDebug("Sending AI completion request to {Endpoint}: {Request}",
                        requestUri, requestJson);
                }
                else
                {
                    _logger.LogInformation("Sending AI completion request to {Endpoint} with question length: {Length}",
                        requestUri, request.Message.Content.Length);
                }

                var response = await _httpClient.SendAsync(httpRequest, cancellationToken)
                    .ConfigureAwait(false);

                return await HandleHttpResponse(response, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("AI completion request was cancelled");
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling AI completion endpoint");
                throw new InvalidOperationException($"Failed to call AI completion endpoint: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Request to AI completion endpoint timed out");
                throw new InvalidOperationException("Request to AI completion endpoint timed out", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize AI completion response");
                throw new InvalidOperationException($"Invalid response format from AI completion endpoint: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling AI completion endpoint");
                throw new InvalidOperationException($"Unexpected error calling AI completion endpoint: {ex.Message}", ex);
            }
        }

        public bool ValidateRequest(CompletionRequest request)
        {
            if (request == null)
            {
                return false;
            }

            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

            if (!isValid)
            {
                foreach (var result in validationResults)
                {
                    _logger.LogWarning("Request validation failed: {Error}", result.ErrorMessage);
                }
            }

            // Additional business logic validation
            if (string.IsNullOrWhiteSpace(request.Message.Content))
            {
                _logger.LogWarning("Request validation failed: Message content cannot be empty");
                return false;
            }

            if (request.TopK.HasValue && (request.TopK.Value < 1 || request.TopK.Value > 100))
            {
                _logger.LogWarning("Request validation failed: TopK must be between 1 and 100");
                return false;
            }

            return isValid;
        }

        private async Task<CompletionResponse> HandleHttpResponse(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadFromJsonAsync<CompletionResponse>(
                    _jsonOptions, cancellationToken).ConfigureAwait(false);

                if (responseContent == null)
                {
                    throw new InvalidOperationException("Received null response from AI completion endpoint");
                }

                if (_options.EnableDebugLogging)
                {
                    _logger.LogDebug("Received AI completion response with ID: {Id}, HasResult: {HasResult}, AnswerLength: {Length}",
                        responseContent.Id, responseContent.HasResult, responseContent.Answer.Length);
                }
                else
                {
                    _logger.LogInformation("Received AI completion response with ID: {Id}, HasResult: {HasResult}",
                        responseContent.Id, responseContent.HasResult);
                }

                return responseContent;
            }

            // Handle error responses
            await HandleErrorResponse(response, cancellationToken).ConfigureAwait(false);

            // This should never be reached due to exception throwing above
            throw new InvalidOperationException($"Unexpected response status: {response.StatusCode}");
        }

        private async Task HandleErrorResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var errorResponse = JsonSerializer.Deserialize<CompletionErrorResponse>(content, _jsonOptions);
                if (errorResponse != null)
                {
                    _logger.LogError("AI completion API returned error: {Error} - {Message} (Code: {Code})",
                        errorResponse.Error, errorResponse.Message, errorResponse.Code);

                    throw new InvalidOperationException(
                        $"AI completion API error: {errorResponse.Error} - {errorResponse.Message}");
                }
            }
            catch (JsonException)
            {
                // If we can't parse the error response, log the raw content
                _logger.LogError("AI completion API returned error status {StatusCode} with content: {Content}",
                    response.StatusCode, content);
            }

            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new InvalidOperationException("Unauthorized: Invalid or missing API key"),
                HttpStatusCode.BadRequest => new ArgumentException($"Bad request: {content}"),
                HttpStatusCode.TooManyRequests => new InvalidOperationException("Rate limit exceeded. Please try again later."),
                HttpStatusCode.InternalServerError => new InvalidOperationException("AI completion service is experiencing issues"),
                _ => new InvalidOperationException($"AI completion request failed with status {response.StatusCode}: {content}")
            };
        }

        private void ConfigureHttpClient()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            if (!string.IsNullOrEmpty(_options.UserAgent))
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
            }

            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        private string GetEffectiveEndpoint(string? override_endpoint)
        {
            // Priority: parameter override > environment variable > configuration
            if (!string.IsNullOrEmpty(override_endpoint))
            {
                return override_endpoint;
            }

            var envEndpoint = Environment.GetEnvironmentVariable(_options.EndpointEnvironmentVariable);
            if (!string.IsNullOrEmpty(envEndpoint))
            {
                return envEndpoint;
            }

            if (string.IsNullOrEmpty(_options.Endpoint))
            {
                throw new InvalidOperationException(
                    $"AI completion endpoint not configured. Set via configuration, " +
                    $"environment variable '{_options.EndpointEnvironmentVariable}', or parameter override.");
            }

            return _options.Endpoint;
        }

        private string? GetEffectiveApiKey(string? override_apiKey)
        {
            // Priority: parameter override > environment variable > configuration
            if (!string.IsNullOrEmpty(override_apiKey))
            {
                return override_apiKey;
            }

            var envApiKey = Environment.GetEnvironmentVariable(_options.ApiKeyEnvironmentVariable);
            if (!string.IsNullOrEmpty(envApiKey))
            {
                return envApiKey;
            }

            return _options.ApiKey;
        }
    }
}
