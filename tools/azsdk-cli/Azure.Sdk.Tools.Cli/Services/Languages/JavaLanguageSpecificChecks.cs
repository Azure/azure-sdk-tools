using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Configuration for a Maven operation execution.
/// </summary>
internal record JavaMavenOperation
{
    public required string OperationName { get; init; }
    public required string Goal { get; init; }
    public required string SuccessMessage { get; init; }
    public required string ErrorMessage { get; init; }
    public required string OutputKeyword { get; init; }
    public required string Guidance { get; init; }
}

/// <summary>
/// Java-specific implementation of language repository service.
/// Uses tools like Maven, Gradle, javac, etc. for Java development workflows.
/// </summary>
public class JavaLanguageSpecificChecks : ILanguageSpecificChecks
{
    private readonly IProcessHelper _processHelper;
    private readonly INpxHelper _npxHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<JavaLanguageSpecificChecks> _logger;

    public JavaLanguageSpecificChecks(
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,
        ILogger<JavaLanguageSpecificChecks> logger)
    {
        _processHelper = processHelper;
        _npxHelper = npxHelper;
        _gitHelper = gitHelper;
        _logger = logger;
    }

    public string SupportedLanguage => "Java";

    public async Task<CLICheckResponse> FormatCodeAsync(string packagePath, bool fix = false, CancellationToken cancellationToken = default)
    {
        var operation = new JavaMavenOperation
        {
            OperationName = "code formatting",
            Goal = fix ? "spotless:apply" : "spotless:check",
            SuccessMessage = fix ? "Code formatting applied successfully" : "Code formatting check passed - all files are properly formatted",
            ErrorMessage = fix ? "Code formatting failed to apply" : "Code formatting check failed - some files need formatting",
            OutputKeyword = "spotless",
            Guidance = fix ? 
                "Run 'mvn spotless:apply' to automatically format code, or check if spotless-maven-plugin is configured in the pom.xml" :
                "Run 'mvn spotless:apply' to fix formatting issues, or use --fix flag with this command"
        };

        return await ExecuteMavenOperationAsync(packagePath, operation, cancellationToken);
    }

    public async Task<CLICheckResponse> UpdateSnippetsAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        var operation = new JavaMavenOperation
        {
            OperationName = "snippet update",
            Goal = "codesnippet:update-codesnippet",
            SuccessMessage = "Code snippets updated successfully",
            ErrorMessage = "Code snippet update failed",
            OutputKeyword = "codesnippet",
            Guidance = "Check if com.azure.tools:codesnippet-maven-plugin is configured in the pom.xml. " +
                      "The plugin should be available for snippet processing in Azure SDK for Java projects."
        };

        return await ExecuteMavenOperationAsync(packagePath, operation, cancellationToken);
    }

    /// <summary>
    /// Executes a Maven operation with shared logic for Maven availability check, 
    /// pom.xml discovery, command execution, and error handling.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="operation">Configuration for the Maven operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the Maven operation</returns>
    private async Task<CLICheckResponse> ExecuteMavenOperationAsync(string packagePath, JavaMavenOperation operation, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting {OperationName} for Java project at: {PackagePath}", operation.OperationName, packagePath);

            // Check if Maven is available
            var mavenCheckResult = await _processHelper.Run(new("mvn", ["--version"], timeout: TimeSpan.FromSeconds(10)), cancellationToken);
            if (mavenCheckResult.ExitCode != 0)
            {
                _logger.LogError("Maven is not installed or not available in PATH");
                return new CLICheckResponse(1, "", $"Maven is not installed or not available in PATH. Please install Maven to use {operation.OperationName} functionality.");
            }

            _logger.LogInformation("Maven is available: {MavenVersion}", mavenCheckResult.Output.Split('\n')[0].Trim());

            // Find pom.xml in the package directory or its parents
            var pomPath = FindPomXml(packagePath);
            if (string.IsNullOrEmpty(pomPath))
            {
                _logger.LogError("No pom.xml found in {PackagePath} or its parent directories", packagePath);
                return new CLICheckResponse(1, "", $"No pom.xml found in {packagePath} or its parent directories. This doesn't appear to be a Maven project.");
            }

            var pomDirectory = Path.GetDirectoryName(pomPath);
            _logger.LogInformation("Using Maven project at: {PomDirectory}", pomDirectory);

            // Execute the Maven goal
            var command = "mvn";
            var args = new[] { operation.Goal, "-f", pomPath };

            _logger.LogInformation("Executing command: {Command} {Arguments}", command, string.Join(" ", args));
            var timeout = TimeSpan.FromMinutes(10); // Maven operations can take time
            var result = await _processHelper.Run(new(command, args, workingDirectory: pomDirectory, timeout: timeout), cancellationToken);

            if (result.ExitCode == 0)
            {
                _logger.LogInformation(operation.SuccessMessage);
                return new CLICheckResponse(result.ExitCode, operation.SuccessMessage);
            }
            else
            {
                _logger.LogWarning("{ErrorMessage} with exit code {ExitCode}", operation.ErrorMessage, result.ExitCode);
                
                // Extract useful error information from output
                var output = result.Output;
                if (output.Contains(operation.OutputKeyword))
                {
                    // If the output contains operation-specific information, include it
                    return new CLICheckResponse(result.ExitCode, output, operation.ErrorMessage);
                }
                else
                {
                    // Provide helpful guidance
                    return new CLICheckResponse(result.ExitCode, output, $"{operation.ErrorMessage}. {operation.Guidance}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {OperationName} for Java project at: {PackagePath}", operation.OperationName, packagePath);
            return new CLICheckResponse(1, "", $"Error during {operation.OperationName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Finds pom.xml file starting from the given directory and searching up the directory tree.
    /// </summary>
    /// <param name="startPath">The directory to start searching from</param>
    /// <returns>Path to pom.xml file, or null if not found</returns>
    private string? FindPomXml(string startPath)
    {
        var currentDir = new DirectoryInfo(startPath);
        
        while (currentDir != null)
        {
            var pomPath = Path.Combine(currentDir.FullName, "pom.xml");
            if (File.Exists(pomPath))
            {
                return pomPath;
            }
            currentDir = currentDir.Parent;
        }
        return null;
    }
}
