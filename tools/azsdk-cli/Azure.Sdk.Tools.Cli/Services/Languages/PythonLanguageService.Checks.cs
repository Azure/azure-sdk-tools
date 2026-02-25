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

            var repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, cancellationToken);
            var scriptPath = Path.Combine(repoRoot, "eng", "tools", "azure-sdk-tools", "ci_tools", "snippet_update", "python_snippet_updater.py");

            if (!File.Exists(scriptPath))
            {
                logger.LogError("Python snippet updater script not found at: {ScriptPath}", scriptPath);
                return new PackageCheckResponse(1, "", $"Python snippet updater script not found at: {scriptPath}");
            }

            var pythonCheckResult = await pythonHelper.Run(new PythonOptions("python", ["--version"]), cancellationToken);
            if (pythonCheckResult.ExitCode != 0)
            {
                logger.LogError("Python is not installed or not available in PATH");
                return new PackageCheckResponse(1, "", "Python is not installed or not available in PATH. Please install Python to use snippet update functionality.");
            }

            var result = await pythonHelper.Run(new PythonOptions("python", [scriptPath, packagePath], workingDirectory: packagePath), cancellationToken);

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

            var lintingTools = new[]
            {
                ("pylint", new PythonOptions("azpysdk", ["pylint", "--isolate", packagePath], workingDirectory: packagePath, timeout: TimeSpan.FromMinutes(3))),
                ("mypy", new PythonOptions("azpysdk", ["mypy", "--isolate", packagePath], workingDirectory: packagePath, timeout: TimeSpan.FromMinutes(3))),
            };

            logger.LogInformation("Starting {Count} linting tools in parallel", lintingTools.Length);

            var lintingTasks = lintingTools.Select(async tool =>
            {
                var (toolName, options) = tool;
                var result = await pythonHelper.Run(options, cancellationToken);
                return (toolName, result);
            });

            var allResults = await Task.WhenAll(lintingTasks);
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

    public override async Task<PackageCheckResponse> AnalyzeDependencies(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting dependency analysis for Python project at: {PackagePath}", packagePath);
            
            var result = await pythonHelper.Run(new PythonOptions("azpysdk", ["mindependency", "--isolate", packagePath], workingDirectory: packagePath, timeout: TimeSpan.FromMinutes(5)), cancellationToken);

            if (result.ExitCode == 0)
            {
                logger.LogInformation("Dependency analysis completed successfully - no issues found");
                return new PackageCheckResponse(result.ExitCode, "Dependency analysis completed successfully - all minimum dependencies are compatible");
            }
            else
            {
                logger.LogWarning("Dependency analysis found issues with exit code {ExitCode}", result.ExitCode);
                return new PackageCheckResponse(result.ExitCode, result.Output, "Dependency analysis found issues with minimum dependency versions");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running dependency analysis for Python project at: {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error running dependency analysis: {ex.Message}");
        }
    }

    public override async Task<PackageCheckResponse> FormatCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting code formatting for Python project at: {PackagePath}", packagePath);
            
            var result = await pythonHelper.Run(new PythonOptions("azpysdk", ["black", "--isolate", packagePath], workingDirectory: packagePath), cancellationToken);

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
        var repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, cancellationToken);
        var packageName = Path.GetFileName(packagePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return await commonValidationHelpers.ValidateChangelog(packageName, packagePath, fixCheckErrors, cancellationToken);
    }

    public override async Task<PackageCheckResponse> CheckSpelling(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        var repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, cancellationToken);
        var relativePath = Path.GetRelativePath(repoRoot, packagePath);
        var spellingCheckPath = $"." + Path.DirectorySeparatorChar + relativePath + Path.DirectorySeparatorChar + "**";
        return await commonValidationHelpers.CheckSpelling(spellingCheckPath, packagePath, fixCheckErrors, cancellationToken);
    }
}
