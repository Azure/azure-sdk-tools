using System.IO;
using System.Linq;
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
    /// Detects the primary language of a repository based on the README.md header.
    /// Looks for patterns like "Azure SDK for .NET", "Azure SDK for Python", etc.
    /// </summary>
    /// <param name="repositoryPath">Path to the repository root</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    /// <returns>Detected language string</returns>
    public static string DetectLanguage(string repositoryPath, ILogger logger)
    {        
        // Try to detect from README.md header at repository root
        var readmePath = Path.Combine(repositoryPath, "README.md");
        logger.LogInformation($"README path: {readmePath}");
        if (!File.Exists(readmePath))
        {
            logger.LogDebug("README.md not found, language detection unsuccessful");
            return "unknown";
        }

        logger.LogDebug("Found README.md file at: {ReadmePath}", readmePath);
        try
        {
            var content = File.ReadAllText(readmePath);

            // Look for "Azure SDK for X" pattern in the first line of README
            var firstLine = content.Split('\n').FirstOrDefault()?.Trim().ToLowerInvariant() ?? "";

            // Extract the language from the first line
            if (firstLine.Contains("python"))
            {
                logger.LogInformation("Detected language: python from README.md header");
                return "python";
            }
            if (firstLine.Contains("javascript"))
            {
                logger.LogInformation("Detected language: javascript from README.md header");
                return "javascript";
            }
            if (firstLine.Contains(".net"))
            {
                logger.LogInformation("Detected language: dotnet from README.md header");
                return "dotnet";
            }
            if (firstLine.Contains("java"))
            {
                logger.LogInformation("Detected language: java from README.md header");
                return "java";
            }
            if (firstLine.Contains("go"))
            {
                logger.LogInformation("Detected language: go from README.md header");
                return "go";
            }

            logger.LogWarning("No recognized language found in README.md header");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read README.md file");
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
