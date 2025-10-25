using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

/// <summary>
/// Base class for tools that need configuration-based operations, process execution, and common response patterns.
/// Provides shared functionality for configuration management, validation, response creation, and process execution.
/// </summary>
public abstract class ConfigBasedTool : MCPTool
{
    protected readonly ISpecGenSdkConfigHelper specGenSdkConfigHelper;
    protected readonly IProcessHelper? processHelper;
    protected readonly ILogger logger;

    protected ConfigBasedTool(
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        ILogger logger,
        IProcessHelper? processHelper = null)
    {
        this.specGenSdkConfigHelper = specGenSdkConfigHelper;
        this.logger = logger;
        this.processHelper = processHelper;
    }

    /// <summary>
    /// Creates a failure response with the specified error message.
    /// Works with any response type that inherits from CommandResponse.
    /// </summary>
    /// <typeparam name="T">The response type that inherits from CommandResponse.</typeparam>
    /// <param name="message">The error message to include in the response.</param>
    /// <returns>A response indicating failure.</returns>
    protected T CreateFailureResponse<T>(string message) where T : CommandResponse, new()
    {
        var response = new T();
        response.ResponseErrors = [message];
        
        // Set Result to "failed" for DefaultCommandResponse
        if (response is DefaultCommandResponse defaultResponse)
        {
            defaultResponse.Result = "failed";
        }
        
        return response;
    }

    /// <summary>
    /// Creates a success response with the specified message.
    /// Only works with DefaultCommandResponse. Tools using custom response types 
    /// should create their own success response methods.
    /// </summary>
    /// <param name="message">The success message to include in the response.</param>
    /// <param name="next_steps">An array of next steps to include in the response.</param>
    /// <returns>A DefaultCommandResponse indicating success.</returns>
    protected DefaultCommandResponse CreateSuccessResponse(string message, string[]? next_steps = null)
    {
        return new DefaultCommandResponse
        {
            Result = "succeeded",
            Message = message,
            NextSteps = next_steps?.ToList() ?? []
        };
    }

    /// <summary>
    /// Validates that the specified package path exists and is accessible.
    /// </summary>
    /// <typeparam name="T">The response type that inherits from CommandResponse.</typeparam>
    /// <param name="packagePath">The package path to validate.</param>
    /// <returns>A failure response if validation fails, null if validation passes.</returns>
    protected T? ValidatePackagePath<T>(string packagePath) where T : CommandResponse, new()
    {
        if (string.IsNullOrEmpty(packagePath))
        {
            return CreateFailureResponse<T>("Package path is required.");
        }

        if (!Directory.Exists(packagePath))
        {
            return CreateFailureResponse<T>($"Path does not exist: {packagePath}");
        }

        return null; // No validation errors
    }

    /// <summary>
    /// Validates that the specified file path exists.
    /// </summary>
    /// <typeparam name="T">The response type that inherits from CommandResponse.</typeparam>
    /// <param name="filePath">The file path to validate.</param>
    /// <param name="fileDescription">Description of the file for error messages (e.g., "configuration file").</param>
    /// <returns>A failure response if validation fails, null if validation passes.</returns>
    protected T? ValidateFilePath<T>(string filePath, string fileDescription = "file") where T : CommandResponse, new()
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return CreateFailureResponse<T>($"{fileDescription} path is required.");
        }

        if (!File.Exists(filePath))
        {
            return CreateFailureResponse<T>($"{fileDescription} does not exist: {filePath}");
        }

        return null; // No validation errors
    }

    /// <summary>
    /// Resolves a potentially relative path to an absolute path, using the base path as reference.
    /// </summary>
    /// <param name="path">The path to resolve (can be relative or absolute).</param>
    /// <param name="basePath">The base path to use for relative path resolution.</param>
    /// <returns>The resolved absolute path.</returns>
    protected virtual string ResolvePath(string path, string basePath)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(basePath, path));
    }

    /// <summary>
    /// Creates process options for script execution with custom parameters.
    /// Requires IProcessHelper to be provided in constructor.
    /// </summary>
    /// <param name="configType">The type of configuration to retrieve (e.g., build, changelog).</param>
    /// <param name="sdkRepoRoot">The root path of the SDK repository.</param>
    /// <param name="workingDirectory">The working directory for the process.</param>
    /// <param name="scriptParameters">Dictionary of parameters to pass to the script.</param>
    /// <param name="timeoutMinutes">Timeout in minutes for the process execution.</param>
    /// <returns>Configured ProcessOptions for execution.</returns>
    protected virtual async Task<ProcessOptions> CreateProcessOptions(
        ConfigType configType,
        string sdkRepoRoot,
        string workingDirectory,
        Dictionary<string, string> scriptParameters,
        int timeoutMinutes = 30)
    {
        if (processHelper == null)
        {
            throw new InvalidOperationException("ProcessHelper is required for process execution. Provide IProcessHelper in constructor.");
        }

        var (configContentType, configValue) = await specGenSdkConfigHelper.GetConfigurationAsync(sdkRepoRoot, configType);

        if (configContentType == ConfigContentType.Command)
        {
            return CreateCommandProcessOptions(configValue, workingDirectory, scriptParameters, timeoutMinutes);
        }
        else
        {
            return CreateScriptProcessOptions(sdkRepoRoot, configValue, workingDirectory, scriptParameters, timeoutMinutes);
        }
    }

    /// <summary>
    /// Creates ProcessOptions for command-based execution.
    /// </summary>
    /// <param name="configValue">The command configuration value.</param>
    /// <param name="workingDirectory">The working directory for the process.</param>
    /// <param name="variables">Variables for command substitution.</param>
    /// <param name="timeoutMinutes">Timeout in minutes for the process execution.</param>
    /// <returns>Configured ProcessOptions for command execution.</returns>
    private ProcessOptions CreateCommandProcessOptions(
        string configValue,
        string workingDirectory,
        Dictionary<string, string> variables,
        int timeoutMinutes)
    {
        var substitutedCommand = specGenSdkConfigHelper.SubstituteCommandVariables(configValue, variables);
        logger.LogInformation($"Executing command: {substitutedCommand}");

        var commandParts = specGenSdkConfigHelper.ParseCommand(substitutedCommand);
        if (commandParts.Length == 0)
        {
            throw new InvalidOperationException($"Invalid command: {substitutedCommand}");
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
    /// <param name="sdkRepoRoot">The root path of the SDK repository.</param>
    /// <param name="configValue">The script path configuration value.</param>
    /// <param name="workingDirectory">The working directory for the process.</param>
    /// <param name="scriptParameters">Parameters to pass to the PowerShell script.</param>
    /// <param name="timeoutMinutes">Timeout in minutes for the process execution.</param>
    /// <returns>Configured ProcessOptions for PowerShell script execution.</returns>
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
            throw new FileNotFoundException($"Script not found at: {fullScriptPath}");
        }

        logger.LogInformation($"Executing PowerShell script: {fullScriptPath}");

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
    /// Executes a process with common error handling and response formatting.
    /// Requires IProcessHelper to be provided in constructor.
    /// </summary>
    /// <param name="options">The process options for execution.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <param name="successMessage">Message to include on successful execution.</param>
    /// <param name="nextSteps">Array of next steps to include on successful execution.</param>
    /// <returns>A response indicating the result of the process execution.</returns>
    protected async Task<DefaultCommandResponse> ExecuteProcessAsync(
        ProcessOptions options,
        CancellationToken ct,
        string successMessage = "Process completed successfully.",
        string[]? nextSteps = null)
    {
        try
        {
            if (processHelper == null)
            {
                throw new InvalidOperationException("ProcessHelper is required for process execution. Provide IProcessHelper in constructor.");
            }

            logger.LogInformation("Executing process...");
            var result = await processHelper.Run(options, ct);
            var trimmedOutput = (result.Output ?? string.Empty).Trim();

            if (result.ExitCode != 0)
            {
                return CreateFailureResponse<DefaultCommandResponse>($"Process failed with exit code {result.ExitCode}. Output:\n{trimmedOutput}");
            }

            logger.LogInformation("Process execution completed successfully");
            return CreateSuccessResponse($"{successMessage} Output:\n{trimmedOutput}", nextSteps);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while executing process");
            return CreateFailureResponse<DefaultCommandResponse>($"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a PowerShell script with specified parameters.
    /// Requires IProcessHelper to be provided in constructor.
    /// </summary>
    /// <param name="scriptPath">Path to the PowerShell script.</param>
    /// <param name="parameters">Parameters to pass to the script.</param>
    /// <param name="workingDirectory">Working directory for script execution.</param>
    /// <param name="timeoutMinutes">Timeout in minutes for script execution.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A response indicating the result of the script execution.</returns>
    protected async Task<DefaultCommandResponse> ExecutePowerShellScriptAsync(
        string scriptPath,
        Dictionary<string, string> parameters,
        string workingDirectory,
        int timeoutMinutes,
        CancellationToken ct)
    {
        if (processHelper == null)
        {
            throw new InvalidOperationException("ProcessHelper is required for process execution. Provide IProcessHelper in constructor.");
        }

        var validationError = ValidateFilePath<DefaultCommandResponse>(scriptPath, "PowerShell script");
        if (validationError != null) 
        {
            return validationError;
        }

        // Convert parameters to script arguments
        var scriptArgs = new List<string>();
        foreach (var param in parameters)
        {
            scriptArgs.Add($"-{param.Key}");
            scriptArgs.Add(param.Value);
        }

        var options = new PowershellOptions(
            scriptPath,
            scriptArgs.ToArray(),
            logOutputStream: true,
            workingDirectory: workingDirectory,
            timeout: TimeSpan.FromMinutes(timeoutMinutes)
        );

        return await ExecuteProcessAsync(options, ct, "PowerShell script executed successfully.");
    }

    /// <summary>
    /// Executes a command with specified arguments.
    /// Requires IProcessHelper to be provided in constructor.
    /// </summary>
    /// <param name="executable">The executable to run.</param>
    /// <param name="arguments">Arguments to pass to the executable.</param>
    /// <param name="workingDirectory">Working directory for command execution.</param>
    /// <param name="timeoutMinutes">Timeout in minutes for command execution.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A response indicating the result of the command execution.</returns>
    protected async Task<DefaultCommandResponse> ExecuteCommandAsync(
        string executable,
        string[] arguments,
        string workingDirectory,
        int timeoutMinutes,
        CancellationToken ct)
    {
        if (processHelper == null)
        {
            throw new InvalidOperationException("ProcessHelper is required for process execution. Provide IProcessHelper in constructor.");
        }

        var options = new ProcessOptions(
            executable,
            arguments,
            logOutputStream: true,
            workingDirectory: workingDirectory,
            timeout: TimeSpan.FromMinutes(timeoutMinutes)
        );

        return await ExecuteProcessAsync(options, ct, "Command executed successfully.");
    }
}
