using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Python-specific implementation of language checks.
/// </summary>
public partial class PythonLanguageService : LanguageService
{
    public override async Task<PackageCheckResponse> UpdateSnippets(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting snippet update for Python project at: {PackagePath}", packagePath);

            // Find the repository root from the package path using GitHelper
            var repoRoot = gitHelper.DiscoverRepoRoot(packagePath);
            logger.LogInformation("Found repository root at: {RepoRoot}", repoRoot);

            // Construct path to the Python snippet updater script
            var scriptPath = Path.Combine(repoRoot, "eng", "tools", "azure-sdk-tools", "ci_tools", "snippet_update", "python_snippet_updater.py");

            // Check if the script exists
            if (!File.Exists(scriptPath))
            {
                logger.LogError("Python snippet updater script not found at: {ScriptPath}", scriptPath);
                return new PackageCheckResponse(1, "", $"Python snippet updater script not found at: {scriptPath}");
            }

            logger.LogInformation("Using Python snippet updater script: {ScriptPath}", scriptPath);

            // Check if Python is available
            var pythonCheckResult = await processHelper.Run(new("python", ["--version"], timeout: TimeSpan.FromSeconds(10)), cancellationToken);
            if (pythonCheckResult.ExitCode != 0)
            {
                logger.LogError("Python is not installed or not available in PATH");
                return new PackageCheckResponse(1, "", "Python is not installed or not available in PATH. Please install Python to use snippet update functionality.");
            }

            logger.LogInformation("Python is available: {PythonVersion}", pythonCheckResult.Output.Trim());

            // Run the Python snippet updater
            var command = "python";
            var args = new[] { scriptPath, packagePath };

            logger.LogInformation("Executing command: {Command} {Arguments}", command, string.Join(" ", args));
            var timeout = TimeSpan.FromMinutes(5);
            var result = await processHelper.Run(new(command, args, workingDirectory: packagePath, timeout: timeout), cancellationToken);

            if (result.ExitCode == 0)
            {
                logger.LogInformation("Snippet update completed successfully - all snippets are up to date");
                return new PackageCheckResponse(result.ExitCode, "All snippets are up to date");
            }
            else
            {
                logger.LogWarning("Snippet update detected out-of-date snippets with exit code {ExitCode}", result.ExitCode);
                return new PackageCheckResponse(result.ExitCode, result.Output, "Some snippets were updated or need attention");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating snippets for Python project at: {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error updating snippets: {ex.Message}");
        }
    }

    public override async Task<PackageCheckResponse> LintCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting code linting for Python project at: {PackagePath}", packagePath);
            var timeout = TimeSpan.FromMinutes(10); 

            // Run multiple linting tools
            var lintingTools = new[]
            {
                ("pylint", new[] { "azpysdk", "pylint", "--isolate", packagePath }),
                ("mypy", new[] { "azpysdk", "mypy", "--isolate", packagePath }),
            };

            logger.LogInformation("Starting {Count} linting tools in parallel", lintingTools.Length);

            // Create tasks for all linting tools to run in parallel
            var lintingTasks = lintingTools.Select(async tool =>
            {
                var (toolName, command) = tool;
                var result = await processHelper.Run(new(command[0], command.Skip(1).ToArray(), workingDirectory: packagePath, timeout: timeout), cancellationToken);
                return (toolName, result);
            });

            // Wait for all linting tools to complete
            var allResults = await Task.WhenAll(lintingTasks);

            // Analyze results
            var failedTools = allResults.Where(r => r.result.ExitCode != 0).ToList();

            if (failedTools.Count == 0)
            {
                logger.LogInformation("All linting tools completed successfully - no issues found");
                return new PackageCheckResponse(0, "All linting tools completed successfully - no issues found");
            }
            else
            {
                var failedToolNames = string.Join(", ", failedTools.Select(t => t.toolName));
                var combinedOutput = string.Join("\n\n", failedTools.Select(t => $"=== {t.toolName} ===\n{t.result.Output}"));
                
                logger.LogWarning("Linting found issues in {FailedCount}/{TotalCount} tools: {FailedTools}", 
                    failedTools.Count, allResults.Length, failedToolNames);
                
                return new PackageCheckResponse(1, combinedOutput, $"Linting issues found in: {failedToolNames}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running code linting for Python project at: {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error running code linting: {ex.Message}");
        }
    }

    public override async Task<PackageCheckResponse> FormatCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting code formatting for Python project at: {PackagePath}", packagePath);
            // Run azpysdk black
            var command = "azpysdk";
            var args = new[] { "black", "--isolate", packagePath };

            logger.LogInformation("Executing command: {Command} {Arguments}", command, string.Join(" ", args));
            var timeout = TimeSpan.FromMinutes(10);
            var result = await processHelper.Run(new(command, args, workingDirectory: packagePath, timeout: timeout), cancellationToken);

            if (result.ExitCode == 0)
            {
                logger.LogInformation("Code formatting completed successfully - no issues found");
                return new PackageCheckResponse(result.ExitCode, "Code formatting completed successfully - no issues found");
            }
            else
            {
                logger.LogWarning("Code formatting found issues with exit code {ExitCode}", result.ExitCode);
                return new PackageCheckResponse(result.ExitCode, result.Output, "Code formatting found issues that need attention");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running code formatting for Python project at: {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error running code formatting: {ex.Message}");
        }
    }

    public override async Task<PackageCheckResponse> ValidateReadme(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return await commonValidationHelpers.ValidateReadme(packagePath, fixCheckErrors, cancellationToken);
    }

    public override async Task<PackageCheckResponse> ValidateChangelog(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        var repoRoot = gitHelper.DiscoverRepoRoot(packagePath);
        var packageName = Path.GetFileName(packagePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return await commonValidationHelpers.ValidateChangelog(packageName, packagePath, fixCheckErrors, cancellationToken);
    }

    public override async Task<PackageCheckResponse> CheckSpelling(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        var repoRoot = gitHelper.DiscoverRepoRoot(packagePath);
        var relativePath = Path.GetRelativePath(repoRoot, packagePath);
        var spellingCheckPath = $"." + Path.DirectorySeparatorChar + relativePath + Path.DirectorySeparatorChar + "**";
        return await commonValidationHelpers.CheckSpelling(spellingCheckPath, packagePath, fixCheckErrors, cancellationToken);
    }
}
