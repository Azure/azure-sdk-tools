using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

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
    private readonly ILogger<JavaLanguageSpecificChecks> _logger;

    // Maven operation timeouts
    private static readonly TimeSpan MavenFormatTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MavenLintTimeout = TimeSpan.FromMinutes(15);

    // Common NextSteps messages
    private static readonly string[] mavenInstallationNextSteps = [
        "Install Apache Maven from https://maven.apache.org/download.cgi",
        "Ensure Maven is added to your system PATH environment variable",
        "Verify installation by running 'mvn --version' in terminal"
    ];

    private static readonly string[] pomNotFoundNextSteps = [
        "Ensure you're running this command from within a Maven project directory",
        "Verify that a pom.xml file exists in the package directory"
    ];

    private static readonly string[] exceptionHandlingNextSteps = [
        "Check that Maven and Java are properly installed and configured",
        "Verify that the project's pom.xml is valid and not corrupted",
        "Check Maven logs for more detailed error information"
    ];

    public JavaLanguageSpecificChecks(
        IProcessHelper processHelper,
        ILogger<JavaLanguageSpecificChecks> logger)
    {
        _processHelper = processHelper;
        _logger = logger;
    }

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

            // Validate Maven and POM prerequisites
            var pomPath = Path.Combine(packagePath, "pom.xml");
            var prerequisiteCheck = await ValidateMavenPrerequisitesAsync(packagePath, pomPath, cancellationToken);
            if (prerequisiteCheck != null)
            {
<<<<<<< HEAD
                return prerequisiteCheck;
=======
                _logger.LogError("Maven is not installed or not available in PATH");
                return new CLICheckResponse(1, "", $"Maven is not installed or not available in PATH. Please install Maven to use {operation.OperationName} functionality.");
>>>>>>> ea577eddf (Implement update code-snippets for Java)
            }


            // Execute the Maven goal
            var command = "mvn";
            var args = new[] { operation.Goal, "-f", pomPath };

            var result = await _processHelper.Run(new(command, args, workingDirectory: packagePath, timeout: MavenFormatTimeout), cancellationToken);

            if (result.ExitCode == 0)
            {
                _logger.LogInformation(operation.SuccessMessage);
                return new CLICheckResponse(result.ExitCode, operation.SuccessMessage);
            }
            else
            {
<<<<<<< HEAD
                var errorMessage = fix ? "Code formatting failed to apply" : "Code formatting check failed - some files need formatting";
                _logger.LogWarning("{ErrorMessage} with exit code {ExitCode}", errorMessage, result.ExitCode);

                var output = result.Output;
                var nextSteps = fix ?
                    "Review the error output and check if spotless-maven-plugin is properly configured in the pom.xml" :
                    "Run with --fix flag to automatically format code, or run 'mvn spotless:apply' manually";

                return new CLICheckResponse(result.ExitCode, output, errorMessage)
                {
                    NextSteps = [nextSteps]
                };
=======
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
>>>>>>> ea577eddf (Implement update code-snippets for Java)
            }
        }
        catch (Exception ex)
        {
<<<<<<< HEAD
            _logger.LogError(ex, "Error during code formatting for Java project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error during code formatting: {ex.Message}")
            {
                NextSteps = [.. exceptionHandlingNextSteps]
            };
        }
    }

    public async Task<CLICheckResponse> LintCodeAsync(string packagePath, bool fix = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting code linting for Java project at: {PackagePath} (Fix: {Fix})", packagePath, fix);

            // Validate Maven and POM prerequisites
            var pomPath = Path.Combine(packagePath, "pom.xml");
            var prerequisiteCheck = await ValidateMavenPrerequisitesAsync(packagePath, pomPath, cancellationToken);
            if (prerequisiteCheck != null)
            {
                return prerequisiteCheck;
            }

            // Use Azure SDK approach: mvn install with linting properties (based on run-and-validate-linting.yml)
            // This matches the Azure SDK for Java pipeline which runs linting during install phase
            var command = "mvn";
            var args = new List<string>
            {
                "install",
                "--no-transfer-progress",
                "-DskipTests",
                "-Dgpg.skip",
                "-Dmaven.javadoc.skip=true",
                "-Dcodesnippet.skip=true",
                "-Dspotless.apply.skip=true",
                "-Dshade.skip=true",
                "-Dmaven.antrun.skip=true",
                "-am",
                "-f", pomPath
            };

            // Configure linting behavior to match Azure SDK pipeline
            // Note: There's no automated "fix" mode for linting - all tools require manual review
            args.AddRange([
                "-Dcheckstyle.failOnViolation=false",
                "-Dcheckstyle.failsOnError=false",
                "-Dspotbugs.failOnError=false",
                "-Drevapi.failBuildOnProblemsFound=false"
            ]);

            var result = await _processHelper.Run(new(command, [.. args], workingDirectory: packagePath, timeout: MavenLintTimeout), cancellationToken);

            // Parse Maven output to determine which linting tools found issues
            var output = result.Output;
            var lintingResults = ParseLintingResults(output, result.ExitCode);

            var failedTools = lintingResults.Where(r => r.HasIssues).ToList();
            var passedTools = lintingResults.Where(r => !r.HasIssues).ToList();

            if (failedTools.Count == 0)
            {
                var message = result.ExitCode == 0
                    ? $"Code linting passed - All tools successful: {string.Join(", ", passedTools.Select(t => t.Tool))}"
                    : "Code linting completed, but build had other issues. Check Maven output for details.";
                _logger.LogInformation(message);
                return new CLICheckResponse(result.ExitCode, message);
            }
            else
            {
                var failedToolNames = string.Join(", ", failedTools.Select(t => t.Tool));
                var passedToolNames = passedTools.Count > 0 ? string.Join(", ", passedTools.Select(t => t.Tool)) : "None";

                var errorMessage = $"Code linting found issues - Tools with issues: {failedToolNames}. Clean tools: {passedToolNames}";
                _logger.LogWarning(errorMessage);

                var nextSteps = new List<string>();
                if (failedTools.Any(t => t.Tool == "Checkstyle"))
                {
                    nextSteps.Add("Checkstyle: Review and manually fix code quality violations - no auto-fix available");
                }
                if (failedTools.Any(t => t.Tool == "SpotBugs"))
                {
                    nextSteps.Add("SpotBugs: Review and manually fix potential bugs - no auto-fix available");
                }
                if (failedTools.Any(t => t.Tool == "RevAPI"))
                {
                    nextSteps.Add("RevAPI: Manually review API compatibility - requires design decisions");
                }

                nextSteps.Add("Review the linting errors and fix them manually - no auto-fix available");
                nextSteps.Add("Use -Dcheckstyle.skip=true, -Dspotbugs.skip=true, -Drevapi.skip=true to skip specific tools during development");

                return new CLICheckResponse(result.ExitCode, output, errorMessage)
                {
                    NextSteps = nextSteps
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during code linting for Java project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error during code linting: {ex.Message}")
            {
                NextSteps = [.. exceptionHandlingNextSteps, "Verify that the project's pom.xml is valid and contains required linting plugins"]
            };
=======
            _logger.LogError(ex, "Error during {OperationName} for Java project at: {PackagePath}", operation.OperationName, packagePath);
            return new CLICheckResponse(1, "", $"Error during {operation.OperationName}: {ex.Message}");
>>>>>>> ea577eddf (Implement update code-snippets for Java)
        }
    }

    /// <summary>
    /// Parses Maven output to determine which linting tools found issues.
    /// Based on Azure SDK for Java pipeline patterns that run linting during install phase.
    /// </summary>
    /// <param name="output">Maven command output</param>
    /// <param name="exitCode">Maven command exit code</param>
    /// <returns>List of linting results per tool</returns>
    private List<(string Tool, bool HasIssues)> ParseLintingResults(string output, int exitCode)
    {
        var results = new List<(string Tool, bool HasIssues)>();

        // Check for Checkstyle execution and results
        var checkstyleRan = output.Contains("checkstyle:") && output.Contains(":check");
        var checkstyleHasIssues = false;
        if (checkstyleRan)
        {
            // Look for explicit success indicators - if not found, assume there are issues
            var checkstyleSuccess = output.Contains("You have 0 Checkstyle violations.") ||
                                   output.Contains("Audit done."); // Sometimes just shows "Audit done." for clean runs
            checkstyleHasIssues = !checkstyleSuccess;
        }
        results.Add(("Checkstyle", checkstyleHasIssues));

        // Check for SpotBugs execution and results
        var spotbugsRan = output.Contains("spotbugs:") && output.Contains(":check");
        var spotbugsHasIssues = false;
        if (spotbugsRan)
        {
            // Look for explicit success indicators - SpotBugs reports success patterns
            var spotbugsSuccess = (output.Contains("BugInstance size is 0") && output.Contains("No errors/warnings found")) ||
                                 output.Contains("Error size is 0") && output.Contains("No errors/warnings found");
            spotbugsHasIssues = !spotbugsSuccess;
        }
        results.Add(("SpotBugs", spotbugsHasIssues));

        // Check for RevAPI execution and results
        var revapiRan = output.Contains("revapi:") && output.Contains(":check");
        var revapiHasIssues = false;
        if (revapiRan)
        {
            // Look for explicit success indicators
            var revapiSuccess = output.Contains("API checks completed without failures.");
            revapiHasIssues = !revapiSuccess;
        }
        results.Add(("RevAPI", revapiHasIssues));

        _logger.LogInformation("Linting results parsed: Checkstyle ran={CheckstyleRan} issues={CheckstyleIssues}, SpotBugs ran={SpotBugsRan} issues={SpotBugsIssues}, RevAPI ran={RevapiRan} issues={RevapiIssues}",
            checkstyleRan, checkstyleHasIssues, spotbugsRan, spotbugsHasIssues, revapiRan, revapiHasIssues);

        return results;
    }

    /// <summary>
    /// Validates Maven installation and POM.xml existence for both formatting and linting operations.
    /// </summary>
    /// <param name="packagePath">The package directory path</param>
    /// <param name="pomPath">The path to the pom.xml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CLICheckResponse with error details if validation fails, null if validation passes</returns>
    private async Task<CLICheckResponse?> ValidateMavenPrerequisitesAsync(string packagePath, string pomPath, CancellationToken cancellationToken)
    {
        // Check if Maven is available
        var mavenCheckResult = await _processHelper.Run(new("mvn", ["--version"], timeout: TimeSpan.FromSeconds(10)), cancellationToken);
        if (mavenCheckResult.ExitCode != 0)
        {
            _logger.LogError("Maven is not installed or not available in PATH");
            return new CLICheckResponse(mavenCheckResult.ExitCode, "", "Maven is not installed or not available in PATH.")
            {
                NextSteps = [.. mavenInstallationNextSteps]
            };
        }

        _logger.LogInformation("Maven is available: {MavenVersion}", mavenCheckResult.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0].Trim());

        // Check for pom.xml in the package directory
        if (!File.Exists(pomPath))
        {
            _logger.LogError("No pom.xml found in {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"No pom.xml found in {packagePath}. This doesn't appear to be a Maven project.")
            {
                NextSteps = [.. pomNotFoundNextSteps]
            };
        }

        _logger.LogInformation("Using Maven project at: {PackagePath}", packagePath);
        return null; // No error, prerequisites are valid
    }
}
