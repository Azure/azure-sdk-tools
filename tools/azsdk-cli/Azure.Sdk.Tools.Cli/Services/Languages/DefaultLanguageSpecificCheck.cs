using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Default implementation for packages where no specific language can be detected.
/// Provides basic validation that works for any package type.
/// </summary>
public class DefaultLanguageSpecificCheck : ILanguageSpecificCheck
{
    private readonly ILogger<DefaultLanguageSpecificCheck> _logger;

    public DefaultLanguageSpecificCheck(ILogger<DefaultLanguageSpecificCheck> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string SupportedLanguage => "Default";

    public bool CanHandle(string packagePath)
    {
        // This is the fallback - it can handle any package, but has lower priority
        return Directory.Exists(packagePath);
    }

    public async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running default dependency analysis for package at {PackagePath}", packagePath);

        try
        {
            var issues = new List<string>();

            // Perform basic directory structure validation
            if (!Directory.Exists(packagePath))
            {
                return new CLICheckResponse(1, "", $"Package directory does not exist: {packagePath}");
            }

            // Check if directory is empty
            var files = Directory.GetFiles(packagePath, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                issues.Add("Package directory is empty");
            }

            // Check for common files that might indicate issues
            var commonFiles = Directory.GetFiles(packagePath, "*", SearchOption.TopDirectoryOnly);
            if (commonFiles.Length == 0)
            {
                issues.Add("No files found in package root directory");
            }

            // Check for executable permissions issues (basic check)
            try
            {
                var dirInfo = new DirectoryInfo(packagePath);
                var hasReadAccess = dirInfo.Exists && dirInfo.GetFiles().Length >= 0;
                if (!hasReadAccess)
                {
                    issues.Add("Cannot read package directory contents");
                }
            }
            catch (UnauthorizedAccessException)
            {
                issues.Add("Insufficient permissions to access package directory");
            }

            var statusDetails = issues.Any() 
                ? string.Join("\n", issues) 
                : "Basic dependency analysis completed successfully (no language-specific checks available)";
            var exitCode = issues.Any() ? 1 : 0;

            await Task.CompletedTask;

            return new CLICheckResponse(exitCode, statusDetails, 
                exitCode == 0 
                    ? "Basic dependency validation passed" 
                    : "Basic dependency validation found issues");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during default dependency analysis");
            return new CLICheckResponse(1, ex.ToString(), $"Default dependency analysis failed: {ex.Message}");
        }
    }
}