using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    /// <param name="repositoryPath">Absolute path to the repository root</param>
    /// <param name="logger">Optional logger instance for diagnostics</param>
    /// <returns>Language-specific repository service</returns>
    public static ILanguageRepoService CreateService(string repositoryPath, ILogger? logger = null)
    {
        logger.LogInformation($"Create service for repository at: {repositoryPath}");
        if (string.IsNullOrWhiteSpace(repositoryPath))
            throw new ArgumentException("Repository path cannot be null or empty", nameof(repositoryPath));

        if (!Directory.Exists(repositoryPath))
            throw new DirectoryNotFoundException($"Repository path does not exist: {repositoryPath}");

        var detectedLanguage = DetectLanguage(repositoryPath, logger);

        return detectedLanguage switch
        {
            "python" => new PythonLanguageRepoService(repositoryPath, logger),
            "javascript" => new JavaScriptLanguageRepoService(repositoryPath),
            "dotnet" => new DotNetLanguageRepoService(repositoryPath),
            "go" => new GoLanguageRepoService(repositoryPath),
            "java" => new JavaLanguageRepoService(repositoryPath),
            _ => new LanguageRepoService(repositoryPath) // Base implementation for unsupported languages
        };
    }

    /// <summary>
    /// Detects the primary language of a repository based on file patterns and configuration files.
    /// First checks for Language-Settings.ps1 as mentioned in the gist, then falls back to file-based detection.
    /// </summary>
    /// <param name="repositoryPath">Path to the repository root</param>
    /// <param name="logger">Optional logger instance for diagnostics</param>
    /// <returns>Detected language string</returns>
    public static string DetectLanguage(string repositoryPath, ILogger? logger = null)
    {        
        // First, try to detect from eng/scripts/Language-Settings.ps1 as mentioned in the gist
        var languageSettingsPath = Path.Combine(repositoryPath, "eng", "scripts", "Language-Settings.ps1");
        logger.LogInformation($"Language settings path {languageSettingsPath}");
        if (File.Exists(languageSettingsPath))
        {
            logger?.LogDebug("Found Language-Settings.ps1 file at: {LanguageSettingsPath}", languageSettingsPath);
            try
            {
                var content = File.ReadAllText(languageSettingsPath);

                // Look for language indicators in the PowerShell file
                if (content.Contains("python", StringComparison.OrdinalIgnoreCase))
                {
                    logger?.LogInformation("Detected language: python from Language-Settings.ps1");
                    return "python";
                }
                if (content.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("js", StringComparison.OrdinalIgnoreCase))
                {
                    logger?.LogInformation("Detected language: javascript from Language-Settings.ps1");
                    return "javascript";
                }
                if (content.Contains("dotnet", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains(".net", StringComparison.OrdinalIgnoreCase))
                {
                    logger?.LogInformation("Detected language: csharp from Language-Settings.ps1");
                    return "csharp";
                }
                if (content.Contains("java", StringComparison.OrdinalIgnoreCase))
                {
                    logger?.LogInformation("Detected language: java from Language-Settings.ps1");
                    return "java";
                }
                if (content.Contains("go", StringComparison.OrdinalIgnoreCase))
                {
                    logger?.LogInformation("Detected language: go from Language-Settings.ps1");
                    return "go";
                }

                logger?.LogWarning("No recognized language found in Language-Settings.ps1");
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to read Language-Settings.ps1 file, falling back to file-based detection");
                // Fall through to file-based detection if reading the settings file fails
            }
        }
        else
        {
            logger?.LogDebug("Language-Settings.ps1 not found, language detection unsuccessful");
        }
        
        logger?.LogWarning("Could not detect repository language, returning 'unknown'");
        return "unknown";
    }    /// <summary>
    /// Gets a list of all supported languages.
    /// </summary>
    /// <returns>Array of supported language strings</returns>
    public static string[] GetSupportedLanguages()
    {
        return new[] { "python", "javascript", "dotnet", "go", "java" };
    }
}
