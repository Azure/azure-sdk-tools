using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Service for creating process options based on configuration and executing processes with standardized response handling.
/// </summary>
public interface IProcessConfigurationService
{
    /// <summary>
    /// Creates process options based on configuration type and parameters.
    /// </summary>
    /// <param name="configType">The type of configuration content type.</param>
    /// <param name="configValue">The configuration value (e.g., command or script path).</param>
    /// <param name="sdkRepoRoot">The root path of the SDK repository.</param>
    /// <param name="workingDirectory">The working directory for the process.</param>
    /// <param name="parameters">Dictionary of parameters to pass to the script/command.</param>
    /// <param name="timeoutMinutes">Timeout in minutes for the process execution.</param>
    /// <returns>Configured ProcessOptions for execution.</returns>
    ProcessOptions CreateProcessOptionsAsync(
        SpecGenSdkConfigContentType configType,
        string configValue,
        string sdkRepoRoot,
        string workingDirectory, 
        Dictionary<string, string> parameters,
        int timeoutMinutes = 30);

    /// <summary>
    /// Executes a process with common error handling and response formatting.
    /// </summary>
    /// <param name="options">The process options for execution.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <param name="packageInfo">Optional package information to include in the response.</param>
    /// <param name="successMessage">Message to include on successful execution.</param>
    /// <param name="nextSteps">Array of next steps to include on successful execution.</param>
    /// <returns>A response indicating the result of the process execution.</returns>
    Task<PackageOperationResponse> ExecuteProcessAsync(
        ProcessOptions options,
        CancellationToken ct,
        PackageInfo? packageInfo = null,
        string successMessage = "Process completed successfully.",
        string[]? nextSteps = null);
}
