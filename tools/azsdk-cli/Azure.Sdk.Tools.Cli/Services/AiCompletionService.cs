using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AiCompletion;
using Azure.Sdk.Tools.Cli.Options;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

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
        private readonly IPublicClientApplication? _msalApp;
        private AuthenticationInfo? _token;
        private readonly IList<string> scopes = new List<string>() { "api://azure-sdk-qa-bot/token" };
        private readonly string authUrl = "https://login.microsoftonline.com/organizations/";

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

            if (string.IsNullOrEmpty(_options.Endpoint) || string.IsNullOrEmpty(_options.ClientId))
            {
                _logger.LogError("Neither the completion API endpoint nor the application client ID has been specified.");
                throw new ArgumentException("Neither the completion API endpoint nor the application client ID has been specified.");
            } else
            {
                _logger.LogInformation("Ai completion service endpoint: {Endpoint}, client id: {ClientId}", _options.Endpoint, _options.ClientId);
                
                var builder = PublicClientApplicationBuilder
                .Create(_options.ClientId)
                .WithAuthority(authUrl)
                .WithDefaultRedirectUri(); // Use MSAL's default redirect URI for public clients

                _msalApp = builder.Build();

                // Configure persistent token cache
                ConfigureTokenCache(_msalApp);
            }

            ConfigureHttpClient();
        }

        public async Task<CompletionResponse> SendCompletionRequestAsync(
            CompletionRequest request,
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
                var requestUri = new Uri(new Uri(_options.Endpoint), "/completion");

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);

                if (_token == null || DateTime.UtcNow > _token.ExpiresOn)
                {
                    _token = await RetriveAiCompletionAccessTokenAsync(cancellationToken);
                }
                
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
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

            return isValid;
        }
        private async Task<AuthenticationInfo> RetriveAiCompletionAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (_msalApp != null)
            {
                var accounts = await _msalApp.GetAccountsAsync();
                AuthenticationResult? authResult = null;
                if (accounts.Any())
                {
                    try
                    {
                        authResult = await _msalApp.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                            .ExecuteAsync(cancellationToken);
                    }
                    catch (MsalUiRequiredException ex)
                    {
                        _logger.LogError("Silent authentication failed, will use interactive: {Message}", ex.Message);
                    }
                }
                else
                {
                    _logger.LogInformation("No cached accounts found, will use interactive authentication.");
                }

                if (authResult == null)
                {
                    _logger.LogInformation("Prompting for interactive authentication");
                    authResult = await _msalApp.AcquireTokenInteractive(scopes)
                        .ExecuteAsync(cancellationToken);
                    _logger.LogInformation("Interactive authentication completed successfully");
                }
                if (authResult == null)
                {
                    _logger.LogError("Failed to authenticate.");
                    throw new Exception("Authentication Failure.");
                }
                return new AuthenticationInfo(authResult.AccessToken, authResult.ExpiresOn);
            } else
            {
                throw new Exception("No IPublicClientApplication instance");
            }
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

        private void ConfigureTokenCache(IPublicClientApplication app)
        {
            // Configure in-memory token cache for the lifetime of the MCP server process
            _logger.LogDebug("Configuring in-memory token cache for MSAL");

            // Set up token cache callbacks with detailed logging
            app.UserTokenCache.SetBeforeAccess(notificationArgs =>
            {
                _logger.LogDebug("Token cache access - HasStateChanged: {HasStateChanged}, SuggestedCacheKey: {SuggestedCacheKey}",
                    notificationArgs.HasStateChanged, notificationArgs.SuggestedCacheKey);
            });

            app.UserTokenCache.SetAfterAccess(notificationArgs =>
            {
                _logger.LogDebug("Token cache after access - HasStateChanged: {HasStateChanged}", notificationArgs.HasStateChanged);
                if (notificationArgs.HasStateChanged)
                {
                    _logger.LogInformation("Token cache has been updated with new authentication data");
                }
            });
        }
    }

    class AuthenticationInfo
    {
        public AuthenticationInfo(string token, DateTimeOffset expiresOn) {
            AccessToken = token;
            ExpiresOn = expiresOn;
        }
        public string AccessToken { get; set; }
        public DateTimeOffset ExpiresOn { get; set; }
    }
}
