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
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Optional API key for authentication. Can also be set via environment variable.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Timeout for HTTP requests in seconds. Default is 30 seconds.
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts for failed requests. Default is 3.
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Default tenant ID to use when not specified in requests.
    /// </summary>
    public string DefaultTenantId { get; set; } = "azure_sdk_qa_bot";

    /// <summary>
    /// Default TopK value for searches. Default is 10.
    /// </summary>
    [Range(1, 100)]
    public int DefaultTopK { get; set; } = 10;

    /// <summary>
    /// Whether to enable request/response logging for debugging. Default is false.
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    /// <summary>
    /// User agent string to send with requests.
    /// </summary>
    public string UserAgent { get; set; } = "Azure-SDK-Tools-CLI";

    /// <summary>
    /// Environment variable name for the API key override.
    /// </summary>
    public string ApiKeyEnvironmentVariable { get; set; } = "AI_COMPLETION_API_KEY";

    /// <summary>
    /// Environment variable name for the endpoint override.
    /// </summary>
    public string EndpointEnvironmentVariable { get; set; } = "AI_COMPLETION_ENDPOINT";
  }
}
