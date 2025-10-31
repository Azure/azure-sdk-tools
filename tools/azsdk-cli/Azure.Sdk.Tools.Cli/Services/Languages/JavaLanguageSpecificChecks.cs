using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

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
    private static readonly TimeSpan MavenSnippetTimeout = TimeSpan.FromMinutes(5);

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

    public async Task<CLICheckResponse> FormatCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting code formatting for Java project at: {PackagePath}", packagePath);

            // Validate Maven and POM prerequisites
            var pomPath = Path.Combine(packagePath, "pom.xml");
            var prerequisiteCheck = await ValidateMavenPrerequisitesAsync(packagePath, pomPath, cancellationToken);
            if (prerequisiteCheck != null)
            {
                return prerequisiteCheck;
            }


            // Determine the Maven goal based on fix parameter
            var goal = fixCheckErrors ? "spotless:apply" : "spotless:check";
            var command = "mvn";
            var args = new[] { goal, "-f", pomPath };

            var result = await _processHelper.Run(new(command, args, workingDirectory: packagePath, timeout: MavenFormatTimeout), cancellationToken);

            if (result.ExitCode == 0)
            {
                var successMessage = fixCheckErrors
                    ? "Code formatting applied successfully"
                    : "Code formatting check passed - all files are properly formatted";
                _logger.LogInformation("{Message}", successMessage);
                return new CLICheckResponse(result.ExitCode, successMessage);
            }
            else
            {
                var errorMessage = fixCheckErrors ? "Code formatting failed to apply" : "Code formatting check failed - some files need formatting";
                _logger.LogWarning("{ErrorMessage} with exit code {ExitCode}", errorMessage, result.ExitCode);

                var output = result.Output;
                var nextSteps = fixCheckErrors ?
                    "Review the error output and check if spotless-maven-plugin is properly configured in the pom.xml" :
                    "Run with --fix flag to automatically format code, or run 'mvn spotless:apply' manually";

                return new CLICheckResponse(result.ExitCode, output, errorMessage)
                {
                    NextSteps = [nextSteps]
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during code formatting for Java project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error during code formatting: {ex.Message}")
            {
                NextSteps = [.. exceptionHandlingNextSteps]
            };
        }
    }

    public async Task<CLICheckResponse> LintCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting code linting for Java project at: {PackagePath} (Fix: {Fix})", packagePath, fixCheckErrors);

            // Validate Maven and POM prerequisites
            var pomPath = Path.Combine(packagePath, "pom.xml");
            var prerequisiteCheck = await ValidateMavenPrerequisitesAsync(packagePath, pomPath, cancellationToken);
            if (prerequisiteCheck != null)
            {
                return prerequisiteCheck;
            }

            // Use mvn install with ALL linting tools in fail-safe mode
            // This follows the "accumulate all errors" pattern instead of failing fast
            // The -am flag ensures parent POMs are built/resolved automatically
            var command = "mvn";
            var args = new List<string>
            {
                "install",
                "--no-transfer-progress",
                "-DskipTests",
                "-Dgpg.skip",
                "-DtrimStackTrace=false",
                "-Dmaven.javadoc.skip=false",
                "-Dcodesnippet.skip=true",
                "-Dspotless.skip=false",
                "-Djacoco.skip=true",
                "-Dshade.skip=true",
                "-Dmaven.antrun.skip=true",
                "-am",
                "-f", pomPath
            };

            // Configure ALL linting tools in fail-safe mode - accumulate errors instead of failing fast
            args.AddRange([
                "-Dcheckstyle.failOnViolation=false",
                "-Dcheckstyle.failsOnError=false",
                "-Dspotbugs.failOnError=false",
                "-Drevapi.failBuildOnProblemsFound=false"
                // Note: Javadoc doesn't have a failOnError flag - it contributes to build exit code
            ]);

            var result = await _processHelper.Run(new(command, [.. args], workingDirectory: packagePath, timeout: MavenLintTimeout), cancellationToken);

            // Parse Maven output to determine which linting tools found issues
            var output = result.Output;
            var lintingResults = ParseLintingResults(output);

            var failedTools = lintingResults.Where(r => r.HasIssues).ToList();
            var passedTools = lintingResults.Where(r => !r.HasIssues).ToList();

            if (failedTools.Count == 0)
            {
                if (result.ExitCode == 0)
                {
                    var passedToolNames = string.Join(", ", passedTools.Select(t => t.Tool));
                    var successMessage = $"Code linting passed - All tools successful: {passedToolNames}";
                    _logger.LogInformation("Code linting passed - All tools successful: {PassedToolNames}", passedToolNames);
                    return new CLICheckResponse(result.ExitCode, successMessage);
                }

                const string otherIssuesMessage = "Code linting completed, but build had other issues. Check Maven output for details.";
                _logger.LogInformation("Code linting completed, but build had other issues. Check Maven output for details.");
                return new CLICheckResponse(result.ExitCode, otherIssuesMessage);
            }
            else
            {
                var failedToolNames = string.Join(", ", failedTools.Select(t => t.Tool));
                var passedToolNames = passedTools.Count > 0 ? string.Join(", ", passedTools.Select(t => t.Tool)) : "None";

                var errorMessage = $"Code linting found issues - Tools with issues: {failedToolNames}. Clean tools: {passedToolNames}";
                _logger.LogWarning(
                    "Code linting found issues - Tools with issues: {FailedToolNames}. Clean tools: {PassedToolNames}",
                    failedToolNames,
                    passedToolNames);

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
                if (failedTools.Any(t => t.Tool == "Javadoc"))
                {
                    nextSteps.Add("Javadoc: Fix javadoc comments, missing documentation, or invalid HTML in docstrings");
                }

                nextSteps.Add("Review the linting errors and fix them manually - no auto-fix available");
                nextSteps.Add("Use -Dcheckstyle.skip=true, -Dspotbugs.skip=true, -Drevapi.skip=true, -Dmaven.javadoc.skip=true to skip specific tools during development");

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
        }
    }

    public async Task<CLICheckResponse> UpdateSnippetsAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting code snippet update for Java project at: {PackagePath}", packagePath);

            // Validate Maven and POM prerequisites
            var pomPath = Path.Combine(packagePath, "pom.xml");
            var prerequisiteCheck = await ValidateMavenPrerequisitesAsync(packagePath, pomPath, cancellationToken);
            if (prerequisiteCheck != null)
            {
                return prerequisiteCheck;
            }

            // Use Azure SDK approach: mvn com.azure.tools:codesnippet-maven-plugin:update-codesnippet
            var command = "mvn";
            var args = new[] { "com.azure.tools:codesnippet-maven-plugin:update-codesnippet", "-f", pomPath };

            var result = await _processHelper.Run(new(command, args, workingDirectory: packagePath, timeout: MavenSnippetTimeout), cancellationToken);

            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Code snippets updated successfully");
                return new CLICheckResponse(result.ExitCode, "Code snippets updated successfully");
            }
            else
            {
                _logger.LogWarning("Code snippet update failed with exit code {ExitCode}", result.ExitCode);

                var output = result.Output;
                return new CLICheckResponse(result.ExitCode, output, "Code snippet update failed - some snippets may be outdated or missing")
                {
                    NextSteps = ["Ensure that code snippets in documentation match the actual code implementation, or check if codesnippet-maven-plugin is configured in the pom.xml"]
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during code snippet update for Java project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error during code snippet update: {ex.Message}")
            {
                NextSteps = [.. exceptionHandlingNextSteps]
            };
        }
    }

    public async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting dependency analysis for Java project at: {PackagePath}", packagePath);

            // Validate Maven and POM prerequisites
            var pomPath = Path.Combine(packagePath, "pom.xml");
            var prerequisiteCheck = await ValidateMavenPrerequisitesAsync(packagePath, pomPath, cancellationToken);
            if (prerequisiteCheck != null)
            {
                return prerequisiteCheck;
            }

            // Azure SDK for Java uses BOM-based dependency management
            return await AnalyzeDependencyTreeAsync(packagePath, pomPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dependency analysis for Java project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error during dependency analysis: {ex.Message}")
            {
                NextSteps = [.. exceptionHandlingNextSteps]
            };
        }
    }

    /// <summary>
    /// Analyzes the Maven dependency tree for conflicts, duplicates, and issues.
    /// Uses 'mvn dependency:tree -Dverbose' as recommended in Azure SDK for Java troubleshooting.
    /// </summary>
    /// <param name="packagePath">The package directory path</param>
    /// <param name="pomPath">The path to the pom.xml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis results</returns>
    private async Task<CLICheckResponse> AnalyzeDependencyTreeAsync(string packagePath, string pomPath, CancellationToken cancellationToken)
    {
        var command = "mvn";
        var args = new[] { "dependency:tree", "-Dverbose", "-f", pomPath };

        var result = await _processHelper.Run(new(command, args, workingDirectory: packagePath, timeout: TimeSpan.FromMinutes(5)), cancellationToken);

        var output = result.Output;
        
        // Simple check - if Maven succeeded and no conflict indicators, it's success
        if (output.Contains("[INFO] BUILD SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            const string successMessage = "Dependency analysis completed - no conflicts detected";
            _logger.LogInformation("Dependency analysis completed - no conflicts detected");
            return new CLICheckResponse(0, successMessage);
        }

        const string errorMessage = "Dependency analysis found issues - check Maven output for conflicts or build errors";
        _logger.LogWarning("Dependency analysis found issues - check Maven output for conflicts or build errors");

        return new CLICheckResponse(1, output, errorMessage)
        {
            NextSteps = [
                "Add Azure SDK BOM to dependencyManagement section in pom.xml: https://github.com/Azure/azure-sdk-for-java/tree/main/sdk/boms/azure-sdk-bom",
                "Remove version numbers from Azure SDK dependencies - let BOM manage versions",
                "Review verbose dependency tree output to identify specific conflicts"
            ]
        };
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

    /// <summary>
    /// Parses Maven output to determine which linting tools found issues.
    /// Based on Azure SDK for Java pipeline patterns that run linting during install phase.
    /// </summary>
    /// <param name="output">Maven command output</param>
    /// <returns>List of linting results per tool</returns>
    private static List<(string Tool, bool HasIssues)> ParseLintingResults(string output)
    {
        return [
            ("Checkstyle", HasCheckstyleIssues(output)),
            ("SpotBugs", HasSpotBugsIssues(output)), 
            ("RevAPI", HasRevapiIssues(output)),
            ("Javadoc", HasJavadocIssues(output))
        ];
    }

    private static bool HasCheckstyleIssues(string output) =>
        output.Contains("Checkstyle violations", StringComparison.OrdinalIgnoreCase) && 
        !output.Contains("You have 0 Checkstyle violations.", StringComparison.OrdinalIgnoreCase) && 
        !output.Contains("Audit done.", StringComparison.OrdinalIgnoreCase);

    private static bool HasSpotBugsIssues(string output) =>
        output.Contains("BugInstance size is", StringComparison.OrdinalIgnoreCase) && 
        !output.Contains("BugInstance size is 0", StringComparison.OrdinalIgnoreCase);

    private static bool HasRevapiIssues(string output) =>
        output.Contains("API problems detected", StringComparison.OrdinalIgnoreCase) && 
        !output.Contains("API checks completed without failures.", StringComparison.OrdinalIgnoreCase);

    private static bool HasJavadocIssues(string output) =>
        output.Contains("Error while generating Javadoc:", StringComparison.OrdinalIgnoreCase) ||
        (output.Contains("maven-javadoc-plugin", StringComparison.OrdinalIgnoreCase) && 
         output.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase) &&
         !output.Contains("Building jar:", StringComparison.OrdinalIgnoreCase) && 
         !output.Contains("-javadoc.jar", StringComparison.OrdinalIgnoreCase));
}
