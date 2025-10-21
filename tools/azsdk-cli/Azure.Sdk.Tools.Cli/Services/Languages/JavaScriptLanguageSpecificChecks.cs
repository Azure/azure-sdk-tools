using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// JavaScript-specific implementation of language repository service.
/// Uses tools like npm, yarn, node, eslint, etc. for JavaScript development workflows.
/// </summary>
public class JavaScriptLanguageSpecificChecks : ILanguageSpecificChecks
{
    private readonly IProcessHelper _processHelper;
    private readonly INpxHelper _npxHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<JavaScriptLanguageSpecificChecks> _logger;

    public JavaScriptLanguageSpecificChecks(
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,
        ILogger<JavaScriptLanguageSpecificChecks> logger)
    {
        _processHelper = processHelper;
        _npxHelper = npxHelper;
        _gitHelper = gitHelper;
        _logger = logger;
    }

    public async Task<CLICheckResponse> ValidateSamplesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            var result = await _processHelper.Run(new(
                    "pnpm",
                    ["run", "build:samples"],
                    workingDirectory: packagePath
                ),
                ct
            );

            if (result.ExitCode != 0)
            {
                _logger.LogError("'pnpm run build:samples' failed with exit code {ExitCode}", result.ExitCode);
                return new CLICheckResponse(result)
                {
                    NextSteps = ["Review the error output and attempt to resolve the issue."]
                };
            }

            return new CLICheckResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating samples for JavaScript project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error validating samples: {ex.Message}");
        }
    }
    
    public async Task<CLICheckResponse> UpdateSnippetsAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _processHelper.Run(new(
                    "pnpm",
                    ["run", "update-snippets"],
                    workingDirectory: packagePath
                ),
                cancellationToken
            );

            if (result.ExitCode != 0)
            {
                _logger.LogError("'pnpm run update-snippets' failed with exit code {ExitCode}", result.ExitCode);
                return new CLICheckResponse(result)
                {
                    NextSteps = ["Review the error output and attempt to resolve the issue."]
                };
            }

            return new CLICheckResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating snippets for JavaScript project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error updating snippets: {ex.Message}");
        }
    }

    public async Task<CLICheckResponse> LintCodeAsync(string packagePath, bool fix = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var subcommand = fix ? "lint:fix" : "lint";

            var result = await _processHelper.Run(new(
                    "pnpm",
                    ["run", subcommand],
                    workingDirectory: packagePath
                ),
                cancellationToken
            );

            if (result.ExitCode != 0)
            {
                _logger.LogError($"'pnpm run {subcommand}' failed with exit code {result.ExitCode}");

                var nextSteps = fix ? "Review the linting errors and fix them manually." : "Run this tool in fix mode to automatically fix some of the errors.";

                return new CLICheckResponse(result)
                {
                    NextSteps = [nextSteps]
                };
            }

            return new CLICheckResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linting JavaScript project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error linting code: {ex.Message}");
        }
    }

    public async Task<CLICheckResponse> FormatCodeAsync(string packagePath, bool fix = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var subcommand = fix ? "format" : "check-format";
            var result = await _processHelper.Run(new(
                    "pnpm",
                    ["run", subcommand],
                    workingDirectory: packagePath
                ),
                cancellationToken
            );

            if (result.ExitCode != 0)
            {
                _logger.LogError($"'pnpm run {subcommand}' failed with exit code {result.ExitCode}");
                var nextSteps = fix ? "Review the error output and attempt to resolve the issue." : "Run this tool in fix mode to fix the formatting.";
                return new CLICheckResponse(result)
                {
                    NextSteps = [nextSteps]
                };
            }

            return new CLICheckResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting JavaScript project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error formatting code: {ex.Message}");
        }
    }

    public async Task<string> GetSDKPackageName(string repo, string packagePath, CancellationToken cancellationToken = default)
    {
        // For JavaScript packages, read the package name from package.json
        var packageJsonPath = Path.Combine(packagePath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            try
            {
                var packageJsonContent = await File.ReadAllTextAsync(packageJsonPath, cancellationToken);
                using var document = JsonDocument.Parse(packageJsonContent);
                if (document.RootElement.TryGetProperty("name", out var nameProperty))
                {
                    var packageName = nameProperty.GetString();
                    if (!string.IsNullOrEmpty(packageName))
                    {
                        return packageName;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse package.json at {PackageJsonPath}. Falling back to directory name.", packageJsonPath);
            }
        }

        // Fallback to directory name if package.json reading fails
        return Path.GetFileName(packagePath);
    }

    public Task<string> GetSpellingCheckPath(string packageRepoRoot, string packagePath)
    {
        var relativePath = Path.GetRelativePath(packageRepoRoot, packagePath);
        var defaultPath = $"." + Path.DirectorySeparatorChar + relativePath + Path.DirectorySeparatorChar + "review" + Path.DirectorySeparatorChar + "*.md";
        return Task.FromResult(defaultPath);
    }
}
