using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Factory service for creating language-specific repository services.
/// Detects the language of a repository and returns the appropriate service implementation.
/// </summary>
public class LanguageRepoServiceFactory
{
    /// <summary>
    /// Creates the appropriate language repository service based on the detected language.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="processHelper">Process helper for running commands</param>
    /// <param name="gitHelper">Git helper instance for repository operations</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    /// <returns>Language-specific repository service</returns>
    public static ILanguageRepoService CreateService(string packagePath, IProcessHelper processHelper, IGitHelper gitHelper, ILogger logger)
    {
        logger.LogInformation($"Create service for package at: {packagePath}");
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new ArgumentException("Package path cannot be null or empty", nameof(packagePath));
        }

        if (!Directory.Exists(packagePath))
        {
            throw new DirectoryNotFoundException($"Package path does not exist: {packagePath}");
        }

        // Discover the repository root from the project path
        var repoRootPath = gitHelper.DiscoverRepoRoot(packagePath);
        logger.LogInformation($"Discovered repository root: {repoRootPath}");
        
        // Create language service using factory (detects language automatically)
        logger.LogInformation($"Creating language service for repository at: {repoRootPath}");
        var detectedLanguage = DetectLanguage(repoRootPath, logger);

        return detectedLanguage switch
        {
            "python" => new PythonLanguageRepoService(packagePath, processHelper, gitHelper, logger),
            "javascript" => new JavaScriptLanguageRepoService(packagePath, processHelper),
            "dotnet" => new DotNetLanguageRepoService(packagePath, processHelper),
            "go" => new GoLanguageRepoService(packagePath, processHelper, logger as ILogger<GoLanguageRepoService>),
            "java" => new JavaLanguageRepoService(packagePath, processHelper),
            _ => new LanguageRepoService(packagePath, processHelper) // Base implementation for unsupported languages
        };
    }

    /// <summary>
    /// Detects the primary language of a repository based on file patterns and configuration files.
    /// First checks for Language-Settings.ps1, then falls back to file-based detection.
    /// </summary>
    /// <param name="repositoryPath">Path to the repository root</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    /// <returns>Detected language string</returns>
    public static string DetectLanguage(string repositoryPath, ILogger logger)
    {        
        // First, try to detect from eng/scripts/Language-Settings.ps1
        var languageSettingsPath = Path.Combine(repositoryPath, "eng", "scripts", "Language-Settings.ps1");
        logger.LogInformation($"Language settings path {languageSettingsPath}");
        if (!File.Exists(languageSettingsPath))
        {
            logger.LogDebug("Language-Settings.ps1 not found, language detection unsuccessful");
            return "unknown";
        }

        logger.LogDebug("Found Language-Settings.ps1 file at: {LanguageSettingsPath}", languageSettingsPath);
        try
        {
            var content = File.ReadAllText(languageSettingsPath);

            // Look for language indicators in the PowerShell file
            if (content.Contains("python", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Detected language: python from Language-Settings.ps1");
                return "python";
            }
            if (content.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("js", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Detected language: javascript from Language-Settings.ps1");
                return "javascript";
            }
            if (content.Contains("dotnet", StringComparison.OrdinalIgnoreCase) ||
                content.Contains(".net", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Detected language: dotnet from Language-Settings.ps1");
                return "dotnet";
            }
            if (content.Contains("java", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Detected language: java from Language-Settings.ps1");
                return "java";
            }
            if (content.Contains("go", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Detected language: go from Language-Settings.ps1");
                return "go";
            }

            logger.LogWarning("No recognized language found in Language-Settings.ps1");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read Language-Settings.ps1 file");
            return "unknown";
        }
        return "unknown";
    }
    
    /// <summary>
    /// Gets a list of all supported languages.
    /// </summary>
    /// <returns>Array of supported language strings</returns>
    public static string[] GetSupportedLanguages()
    {
        return new[] { "python", "javascript", "dotnet", "go", "java" };
    }
}
