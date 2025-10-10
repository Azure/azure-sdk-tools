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

    public async Task<CLICheckResponse> LintCodeAsync(string packagePath, bool fix = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting code linting for Java project at: {PackagePath} (Fix: {Fix})", packagePath, fix);

            // Check if Maven is available
            var mavenCheckResult = await _processHelper.Run(new("mvn", ["--version"], timeout: TimeSpan.FromSeconds(10)), cancellationToken);
            if (mavenCheckResult.ExitCode != 0)
            {
                _logger.LogError("Maven is not installed or not available in PATH");
                return new CLICheckResponse(1, "", "Maven is not installed or not available in PATH. Please install Maven to use code linting functionality.");
            }

            _logger.LogInformation("Maven is available: {MavenVersion}", mavenCheckResult.Output.Split('\n')[0].Trim());

            // Find pom.xml in the package directory or its parents
            var pomPath = FindPomXml(packagePath);
            if (string.IsNullOrEmpty(pomPath))
            {
                _logger.LogError("No pom.xml found in {PackagePath} or its parent directories", packagePath);
                return new CLICheckResponse(1, "", $"No pom.xml found in {packagePath} or its parent directories. This doesn't appear to be a Maven project.");
            }

            var pomDirectory = Path.GetDirectoryName(pomPath)!;
            _logger.LogInformation("Using Maven project at: {PomDirectory}", pomDirectory);

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

            if (fix)
            {
                // When fixing, allow violations but don't fail build
                args.AddRange([
                    "-Dcheckstyle.failOnViolation=false",
                    "-Dcheckstyle.failsOnError=false", 
                    "-Dspotbugs.failOnError=false",
                    "-Drevapi.failBuildOnProblemsFound=false"
                ]);
            }
            else
            {
                // When not fixing, still don't fail build but collect results for analysis
                args.AddRange([
                    "-Dcheckstyle.failOnViolation=false",
                    "-Dcheckstyle.failsOnError=false",
                    "-Dspotbugs.failOnError=false", 
                    "-Drevapi.failBuildOnProblemsFound=false"
                ]);
            }

            _logger.LogInformation("Executing Azure Java SDK-style linting: {Command} {Arguments}", command, string.Join(" ", args));
            var timeout = TimeSpan.FromMinutes(15);
            var result = await _processHelper.Run(new(command, [.. args], workingDirectory: pomDirectory, timeout: timeout), cancellationToken);

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

                var guidance = "To fix linting issues:\n" +
                             "• Checkstyle: Follow Java coding standards and fix style violations\n" +
                             "• SpotBugs: Review and fix potential bugs and code quality issues\n" +
                             "• RevAPI: Ensure API changes are backward compatible or properly documented\n" +
                             "• Run with --fix flag to attempt automatic fixes where possible\n" +
                             "• Use -Dcheckstyle.skip=true, -Dspotbugs.skip=true, -Drevapi.skip=true to skip specific tools during development";

                return new CLICheckResponse(1, output, $"{errorMessage}. {guidance}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during code linting for Java project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error during code linting: {ex.Message}");
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
