using System.ComponentModel.DataAnnotations;

namespace Azure.Sdk.Tools.Cli.Options
{
  /// <summary>
  /// Configuration options for the AI completion service.
  /// </summary>
  public class AiCompletionOptions
  {
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "AiCompletion";

    /// <summary>
    /// The base URL of the AI completion endpoint.
    /// </summary>
    [Required]
    [Url]
    public string Endpoint { get; set; } = Environment.GetEnvironmentVariable(EndpointEnvironmentVariable) ?? string.Empty;

    /// <summary>
    /// Optional AI bot application client id for authentication. Can also be set via environment variable.
    /// </summary>
    public string? ClientId { get; set; } = Environment.GetEnvironmentVariable(AzureSDKBotClientIdEnvironmentVariable) ?? string.Empty;

    public string? AuthScope { get; set; } = Environment.GetEnvironmentVariable(AzureSDKBotScopeEnvironmentVariable) ?? string.Empty;

        /// <summary>
        /// Timeout for HTTP requests in seconds. Default is 30 seconds.
        /// </summary>
        [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Default tenant ID to use when not specified in requests.
    /// </summary>
    public string DefaultTenantId { get; set; } = "azure_sdk_qa_bot";

    /// <summary>
    /// Whether to enable request/response logging for debugging. Default is false.
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    /// <summary>
    /// User agent string to send with requests.
    /// </summary>
    public string UserAgent { get; set; } = "Azure-SDK-Tools-CLI";

    /// <summary>
    /// Environment variable name for the endpoint override.
    /// </summary>
    public const string EndpointEnvironmentVariable = "AI_COMPLETION_ENDPOINT";

    /// <summary>
    /// Environment variable name for the service application client ID.
    /// </summary>
    public const string AzureSDKBotClientIdEnvironmentVariable = "AI_COMPLETION_BOT_CLIENT_ID";

    /// <summary>
    /// Environment variable name for the service application authentication scope.
    /// </summary>
    public const string AzureSDKBotScopeEnvironmentVariable = "AI_COMPLETION_BOT_SCOPE";
  }
}
