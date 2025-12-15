using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// JavaScript-specific implementation of language repository service.
/// Uses tools like npm, yarn, node, eslint, etc. for JavaScript development workflows.
/// </summary>
public partial class JavaScriptLanguageService : LanguageService
{    

    public async Task<PackageCheckResponse> ValidateSamplesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            var result = await processHelper.Run(new(
                    "pnpm",
                    ["run", "build:samples"],
                    workingDirectory: packagePath
                ),
                ct
            );

            if (result.ExitCode != 0)
            {
                logger.LogError("'pnpm run build:samples' failed with exit code {ExitCode}", result.ExitCode);
                return new PackageCheckResponse(result)
                {
                    NextSteps = ["Review the error output and attempt to resolve the issue."]
                };
            }

            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating samples for JavaScript project at: {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error validating samples: {ex.Message}");
        }
    }
    
    public override async Task<PackageCheckResponse> UpdateSnippets(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await processHelper.Run(new(
                    "pnpm",
                    ["run", "update-snippets"],
                    workingDirectory: packagePath
                ),
                cancellationToken
            );

            if (result.ExitCode != 0)
            {
                logger.LogError("'pnpm run update-snippets' failed with exit code {ExitCode}", result.ExitCode);
                return new PackageCheckResponse(result)
                {
                    NextSteps = ["Review the error output and attempt to resolve the issue."]
                };
            }

            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating snippets for JavaScript project at: {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error updating snippets: {ex.Message}");
        }
    }

    public override async Task<PackageCheckResponse> LintCode(string packagePath, bool fix = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var subcommand = fix ? "lint:fix" : "lint";

            var result = await processHelper.Run(new(
                    "pnpm",
                    ["run", subcommand],
                    workingDirectory: packagePath
                ),
                cancellationToken
            );

            if (result.ExitCode != 0)
            {
                logger.LogError(
                    "'pnpm run {Subcommand}' failed with exit code {ExitCode}",
                    subcommand,
                    result.ExitCode);

                var nextSteps = fix ? "Review the linting errors and fix them manually." : "Run this tool in fix mode to automatically fix some of the errors.";

                return new PackageCheckResponse(result)
                {
                    NextSteps = [nextSteps]
                };
            }

            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error linting JavaScript project at: {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error linting code: {ex.Message}");
        }
    }

    public override async Task<PackageCheckResponse> FormatCode(string packagePath, bool fix = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var subcommand = fix ? "format" : "check-format";
            var result = await processHelper.Run(new(
                    "pnpm",
                    ["run", subcommand],
                    workingDirectory: packagePath
                ),
                cancellationToken
            );

            if (result.ExitCode != 0)
            {
                logger.LogError(
                    "'pnpm run {Subcommand}' failed with exit code {ExitCode}",
                    subcommand,
                    result.ExitCode);
                var nextSteps = fix ? "Review the error output and attempt to resolve the issue." : "Run this tool in fix mode to fix the formatting.";
                return new PackageCheckResponse(result)
                {
                    NextSteps = [nextSteps]
                };
            }

            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error formatting JavaScript project at: {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error formatting code: {ex.Message}");
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
                logger.LogWarning(ex, "Failed to parse package.json at {PackageJsonPath}. Falling back to directory name.", packageJsonPath);
            }
        }

        // Fallback to directory name if package.json reading fails
        return Path.GetFileName(packagePath);
    }

    public override async Task<PackageCheckResponse> ValidateReadme(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return await commonValidationHelpers.ValidateReadme(packagePath, fixCheckErrors, cancellationToken);
    }

    public override async Task<PackageCheckResponse> ValidateChangelog(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        var repoRoot = gitHelper.DiscoverRepoRoot(packagePath);
        var packageName = await GetSDKPackageName(repoRoot, packagePath, cancellationToken);
        return await commonValidationHelpers.ValidateChangelog(packageName, packagePath, fixCheckErrors, cancellationToken);
    }
}
