using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

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
        try
        {
            _logger.LogInformation("Starting code formatting for Java project at: {PackagePath}", packagePath);

            // Check if Maven is available
            var mavenCheckResult = await _processHelper.Run(new("mvn", ["--version"], timeout: TimeSpan.FromSeconds(10)), cancellationToken);
            if (mavenCheckResult.ExitCode != 0)
            {
                _logger.LogError("Maven is not installed or not available in PATH");
                return new CLICheckResponse(1, "", "Maven is not installed or not available in PATH. Please install Maven to use code formatting functionality.");
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

            // Determine the Maven goal based on fix parameter
            var goal = fix ? "spotless:apply" : "spotless:check";
            var command = "mvn";
            var args = new[] { goal, "-f", pomPath };

            _logger.LogInformation("Executing command: {Command} {Arguments}", command, string.Join(" ", args));
            var timeout = TimeSpan.FromMinutes(10); // Maven operations can take time
            var result = await _processHelper.Run(new(command, args, workingDirectory: pomDirectory, timeout: timeout), cancellationToken);

            if (result.ExitCode == 0)
            {
                var message = fix ? "Code formatting applied successfully" : "Code formatting check passed - all files are properly formatted";
                _logger.LogInformation(message);
                return new CLICheckResponse(result.ExitCode, message);
            }
            else
            {
                var errorMessage = fix ? "Code formatting failed to apply" : "Code formatting check failed - some files need formatting";
                _logger.LogWarning("{ErrorMessage} with exit code {ExitCode}", errorMessage, result.ExitCode);
                
                // Extract useful error information from output
                var output = result.Output;
                if (output.Contains("spotless"))
                {
                    // If the output contains spotless-related information, include it
                    return new CLICheckResponse(result.ExitCode, output, errorMessage);
                }
                else
                {
                    // Provide helpful guidance
                    var guidance = fix ? 
                        "Run 'mvn spotless:apply' to automatically format code, or check if spotless-maven-plugin is configured in the pom.xml" :
                        "Run 'mvn spotless:apply' to fix formatting issues, or use --fix flag with this command";
                    return new CLICheckResponse(result.ExitCode, output, $"{errorMessage}. {guidance}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting code for Java project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error formatting code: {ex.Message}");
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
