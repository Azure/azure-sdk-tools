using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Service for creating process options based on configuration and executing processes with standardized response handling.
/// </summary>
public class ProcessConfigurationService : IProcessConfigurationService
{
    private readonly ISpecGenSdkConfigHelper specGenSdkConfigHelper;
    private readonly IProcessHelper processHelper;
    private readonly ILogger<ProcessConfigurationService> logger;
    private readonly IResponseFactory responseFactory;

    public ProcessConfigurationService(
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IProcessHelper processHelper,
        ILogger<ProcessConfigurationService> logger,
        IResponseFactory responseFactory)
    {
        this.specGenSdkConfigHelper = specGenSdkConfigHelper;
        this.processHelper = processHelper;
        this.logger = logger;
        this.responseFactory = responseFactory;
    }

    /// <inheritdoc />
    public ProcessOptions CreateProcessOptionsAsync(
        SpecGenSdkConfigContentType configContentType,
        string configValue,
        string sdkRepoRoot,
        string workingDirectory,
        Dictionary<string, string> parameters,
        int timeoutMinutes = 5)
    {
        if (configContentType == SpecGenSdkConfigContentType.Command)
        {
            return CreateCommandProcessOptions(configValue, workingDirectory, parameters, timeoutMinutes);
        }
        else
        {
            return CreateScriptProcessOptions(sdkRepoRoot, configValue, workingDirectory, parameters, timeoutMinutes);
        }
    }

    /// <inheritdoc />
    public async Task<PackageOperationResponse> ExecuteProcessAsync(
        ProcessOptions options,
        CancellationToken ct,
        PackageInfo? packageInfo = null,
        string successMessage = "Process completed successfully.",
        string[]? nextSteps = null)
    {
        try
        {
            logger.LogInformation("Executing process...");
            var result = await processHelper.Run(options, ct);
            var trimmedOutput = (result.Output ?? string.Empty).Trim();

            if (result.ExitCode != 0)
            {
                return responseFactory.CreateFailureResponse($"Process failed with exit code {result.ExitCode}. Output:\n{trimmedOutput}", packageInfo);
            }

            logger.LogInformation("Process execution completed successfully");
            return responseFactory.CreateSuccessResponse($"{successMessage} Output:\n{trimmedOutput}", packageInfo, nextSteps);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while executing process");
            return responseFactory.CreateFailureResponse($"An error occurred: {ex.Message}", packageInfo);
        }
    }

    /// <summary>
    /// Creates ProcessOptions for command-based execution.
    /// </summary>
    private ProcessOptions CreateCommandProcessOptions(
        string configValue,
        string workingDirectory,
        Dictionary<string, string> variables,
        int timeoutMinutes)
    {
        var substitutedCommand = specGenSdkConfigHelper.SubstituteCommandVariables(configValue, variables);
        logger.LogInformation("Executing command: {SubstitutedCommand}", substitutedCommand);

        var commandParts = specGenSdkConfigHelper.ParseCommand(substitutedCommand);
        if (commandParts.Length == 0)
        {
            logger.LogWarning("Invalid command: {SubstitutedCommand}", substitutedCommand);
            return null;
        }

        return new ProcessOptions(
            commandParts[0],
            commandParts.Skip(1).ToArray(),
            logOutputStream: true,
            workingDirectory: workingDirectory,
            timeout: TimeSpan.FromMinutes(timeoutMinutes)
        );
    }

    /// <summary>
    /// Creates ProcessOptions for PowerShell script execution.
    /// </summary>
    private ProcessOptions CreateScriptProcessOptions(
        string sdkRepoRoot,
        string configValue,
        string workingDirectory,
        Dictionary<string, string> scriptParameters,
        int timeoutMinutes)
    {
        var fullScriptPath = ResolvePath(configValue, sdkRepoRoot);

        if (!File.Exists(fullScriptPath))
        {
            logger.LogWarning("Script not found at: {FullScriptPath}", fullScriptPath);
            return null;
        }

        logger.LogInformation("Executing PowerShell script: {FullScriptPath}", fullScriptPath);

        // Convert dictionary to PowerShell parameter array
        var scriptArgs = new List<string>();
        foreach (var param in scriptParameters)
        {
            scriptArgs.Add($"-{param.Key}");
            scriptArgs.Add(param.Value);
        }

        return new PowershellOptions(
            fullScriptPath,
            scriptArgs.ToArray(),
            logOutputStream: true,
            workingDirectory: workingDirectory,
            timeout: TimeSpan.FromMinutes(timeoutMinutes)
        );
    }

    /// <summary>
    /// Resolves a potentially relative path to an absolute path, using the base path as reference.
    /// </summary>
    private string ResolvePath(string path, string basePath)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(basePath, path));
    }
}
