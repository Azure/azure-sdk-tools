using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Python-specific implementation of language checks.
/// </summary>
public partial class PythonLanguageService : LanguageService
{
    // Common NextSteps messages for Python tool issues
    private const string VerifySetupNextStepInstruction = "Run 'azsdk_verify_setup' from the azure-sdk-for-python repo root to auto-install required Python tools";

    public override async Task<PackageCheckResponse> UpdateSnippets(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting snippet update for Python project at: {PackagePath}", packagePath);

            var result = await pythonHelper.Run(new PythonOptions("azpysdk", ["update_snippet", "--isolate", packagePath], workingDirectory: packagePath), cancellationToken);

            if (result.ExitCode == 0)
            {
                logger.LogInformation("Snippet update completed successfully - all snippets are up to date");
                return new PackageCheckResponse(result.ExitCode, "All snippets are up to date");
            }
            else
            {
                logger.LogWarning("Snippet update detected out-of-date snippets with exit code {ExitCode}", result.ExitCode);
                return new PackageCheckResponse(result.ExitCode, result.Output, "Some snippets were updated or need attention")
                {
                    NextSteps = [
                        "Review the snippet update output to identify if any snippets need manual changes"
                    ]
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating snippets for Python project at: {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error updating snippets: {ex.Message}")
            {
                NextSteps = [VerifySetupNextStepInstruction]
            };
        }
    }

    public override async Task<PackageCheckResponse> LintCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting code linting for Python project at: {PackagePath}", packagePath);

            // azpysdk returns exit code 0 when the target resolves to no Python packages.
            // Validate upfront so we don't report a false pass for an invalid package path.
            var hasSetupPy = File.Exists(Path.Combine(packagePath, "setup.py"));
            var hasPyProjectToml = File.Exists(Path.Combine(packagePath, "pyproject.toml"));
            if (!hasSetupPy && !hasPyProjectToml)
            {
                var errorMessage = $"Package path is not a Python package root: {packagePath}. Expected setup.py or pyproject.toml.";
                logger.LogError("{ErrorMessage}", errorMessage);
                return new PackageCheckResponse(1, errorMessage, errorMessage);
            }

            var timeout = TimeSpan.FromMinutes(3);
            var lintingTools = new[]
            {
                ("pylint", new PythonOptions("azpysdk", ["pylint", packagePath], workingDirectory: packagePath, timeout: timeout)),
                ("mypy", new PythonOptions("azpysdk", ["mypy", packagePath], workingDirectory: packagePath, timeout: timeout)),
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

                var nextSteps = new List<string>();
                if (failedTools.Any(t => t.toolName == "pylint"))
                {
                    nextSteps.Add("pylint: Review and manually fix code quality violations.");
                }
                if (failedTools.Any(t => t.toolName == "mypy"))
                {
                    nextSteps.Add("mypy: Review and manually fix type annotation issues.");
                }
                nextSteps.Add(VerifySetupNextStepInstruction);

                return new PackageCheckResponse(1, combinedOutput, $"Linting issues found in: {failedToolNames}")
                {
                    NextSteps = nextSteps
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running code linting for Python project at: {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error running code linting: {ex.Message}")
            {
                NextSteps = [VerifySetupNextStepInstruction]
            };
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
                return new PackageCheckResponse(result.ExitCode, result.Output, "Dependency analysis found issues with minimum dependency versions")
                {
                    NextSteps = [
                        "Review and update the minimum dependency versions declared in setup.py or pyproject.toml",
                        "Ensure that all dependent packages are compatible with the declared minimum versions",
                    ]
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running dependency analysis for Python project at: {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error running dependency analysis: {ex.Message}")
            {
                NextSteps = [VerifySetupNextStepInstruction]
            };
        }
    }

    public override async Task<PackageCheckResponse> FormatCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting code formatting for Python project at: {PackagePath}", packagePath);

            var blackArgs = new[] { "black", "--isolate", packagePath };

            var result = await pythonHelper.Run(new PythonOptions("azpysdk", blackArgs, workingDirectory: packagePath), cancellationToken);

            if (result.ExitCode == 0)
            {
                logger.LogInformation("Code formatting completed successfully - no issues found");
                return new PackageCheckResponse(result.ExitCode, "Code formatting completed successfully - no issues found");
            }
            else
            {
                logger.LogWarning("Code formatting failed to apply with exit code {ExitCode}", result.ExitCode);

                return new PackageCheckResponse(result.ExitCode, result.Output, "Code formatting failed to apply")
                {
                    NextSteps = ["Review the error output - some formatting issues could not be auto-fixed by black"]
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running code formatting for Python project at: {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error running code formatting: {ex.Message}")
            {
                NextSteps = [VerifySetupNextStepInstruction]
            };
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
        return await commonValidationHelpers.CheckSpelling(packagePath, fixCheckErrors, cancellationToken);
    }
}
