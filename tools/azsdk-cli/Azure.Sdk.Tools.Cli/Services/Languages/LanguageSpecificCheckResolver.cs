using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Interface for resolving language-specific check implementations.
/// </summary>
public interface ILanguageSpecificCheckResolver
{
    /// <summary>
    /// Gets the appropriate language-specific check service for the given package path.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <returns>Language-specific check service that can handle the package, or null if no handler is found</returns>
    ILanguageSpecificChecks? GetLanguageCheck(string packagePath);
}

/// <summary>
/// Resolves the appropriate language-specific check implementation based on package contents.
/// Uses composition pattern instead of inheritance.
/// </summary>
public class LanguageSpecificCheckResolver : ILanguageSpecificCheckResolver
{
    private readonly IEnumerable<ILanguageSpecificChecks> _languageChecks;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<LanguageSpecificCheckResolver> _logger;

    private static readonly Dictionary<string, string> RepositoryLanguageMapping = new()
    {
        { "azure-sdk-for-python", "Python" },
        { "azure-sdk-for-java", "Java" },
        { "azure-sdk-for-js", "JavaScript" },
        { "azure-sdk-for-go", "Go" },
        { "azure-sdk-for-net", "Dotnet" }
    };

    public LanguageSpecificCheckResolver(
        IEnumerable<ILanguageSpecificChecks> languageChecks,
        IGitHelper gitHelper,
        ILogger<LanguageSpecificCheckResolver> logger)
    {
        _languageChecks = languageChecks ?? throw new ArgumentNullException(nameof(languageChecks));
        _gitHelper = gitHelper ?? throw new ArgumentNullException(nameof(gitHelper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the appropriate language-specific check service for the given package path.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <returns>Language-specific check service that can handle the package, or null if no handler is found</returns>
    public ILanguageSpecificChecks? GetLanguageCheck(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new ArgumentException("Package path cannot be null or empty", nameof(packagePath));
        }

        if (!Directory.Exists(packagePath))
        {
            throw new ArgumentException($"Package path does not exist: {packagePath}", nameof(packagePath));
        }

        _logger.LogDebug("Resolving language-specific check for package at {PackagePath}", packagePath);

        // Use repository detection service to identify the language
        var detectedLanguage = DetectLanguage(packagePath);
        
        if (string.IsNullOrEmpty(detectedLanguage))
        {
            _logger.LogWarning("No language detected for package at {PackagePath}", packagePath);
            return null;
        }

        // Find the appropriate language-specific check implementation
        var specificLanguageCheck = _languageChecks.FirstOrDefault(check => 
            string.Equals(check.SupportedLanguage, detectedLanguage, StringComparison.OrdinalIgnoreCase));
        
        if (specificLanguageCheck != null)
        {
            _logger.LogInformation("Selected {Language} check service for package at {PackagePath}", 
                specificLanguageCheck.SupportedLanguage, packagePath);
            return specificLanguageCheck;
        }

        _logger.LogWarning("No language-specific check service found for detected language '{Language}' at package path {PackagePath}", 
            detectedLanguage, packagePath);
        return null;
    }

    private string? DetectLanguage(string packagePath)
    {
        try
        {
            var repositoryPath = _gitHelper.DiscoverRepoRoot(packagePath);
            if (string.IsNullOrEmpty(repositoryPath))
            {
                return null;
            }

            var repoName = Path.GetFileName(repositoryPath?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.ToLowerInvariant();
            if (string.IsNullOrEmpty(repoName))
            {
                return null;
            }

            foreach (var (repoPattern, language) in RepositoryLanguageMapping)
            {
                if (repoName.Contains(repoPattern))
                {
                    _logger.LogDebug("Detected language: {Language} from repository name: {RepoName}", language, repoName);
                    return language;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting language for package path: {PackagePath}", packagePath);
            return null;
        }
    }

}