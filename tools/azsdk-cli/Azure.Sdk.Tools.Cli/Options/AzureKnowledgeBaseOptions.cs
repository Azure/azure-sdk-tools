using System.ComponentModel.DataAnnotations;

namespace Azure.Sdk.Tools.Cli.Options
{
  /// <summary>
  /// Configuration options for the AI completion service.
  /// </summary>
  public class AzureKnowledgeBaseOptions
  {
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "AzureKnowledgeBase";

    /// <summary>
    /// The base URL of the Azure Knowledge base service endpoint.
    /// </summary>
    [Required]
    [Url]
    public string Endpoint { get; set; } = Environment.GetEnvironmentVariable(EndpointEnvironmentVariable) ?? string.Empty;

    /// <summary>
    /// Optional Azure Knowledge base service application client id for authentication. Can also be set via environment variable.
    /// </summary>
    public string? ClientId { get; set; } = Environment.GetEnvironmentVariable(AzureKBClientIdEnvironmentVariable) ?? string.Empty;

    /// <summary>
    /// Optional scope for authentication. Can also be set via environment variable.
    /// </summary>
    public string? AuthScope { get; set; } = Environment.GetEnvironmentVariable(AzureKBScopeEnvironmentVariable) ?? string.Empty;

    /// <summary>
    /// Timeout for HTTP requests in seconds. Default is 30 seconds.
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 300;

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
    public const string EndpointEnvironmentVariable = "AZURE_KB_ENDPOINT";

    /// <summary>
    /// Environment variable name for the service application client ID.
    /// </summary>
    public const string AzureKBClientIdEnvironmentVariable = "AZURE_KB_CLIENT_ID";

    /// <summary>
    /// Environment variable name for the service application authentication scope.
    /// </summary>
    public const string AzureKBScopeEnvironmentVariable = "AZURE_KB_SCOPE";
  }
}
