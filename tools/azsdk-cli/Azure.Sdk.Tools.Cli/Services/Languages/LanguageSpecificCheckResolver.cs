using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Resolves the appropriate language-specific check implementation based on package contents.
/// Uses composition pattern instead of inheritance.
/// </summary>
public class LanguageSpecificCheckResolver
{
    private readonly IEnumerable<ILanguageSpecificChecks> _languageChecks;
    private readonly ILogger<LanguageSpecificCheckResolver> _logger;

    public LanguageSpecificCheckResolver(
        IEnumerable<ILanguageSpecificChecks> languageChecks,
        ILogger<LanguageSpecificCheckResolver> logger)
    {
        _languageChecks = languageChecks ?? throw new ArgumentNullException(nameof(languageChecks));
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

        // Find specific language implementations
        var specificLanguageCheck = _languageChecks.FirstOrDefault(check => check.CanHandle(packagePath));
        
        if (specificLanguageCheck != null)
        {
            _logger.LogInformation("Selected {Language} check service for package at {PackagePath}", 
                specificLanguageCheck.SupportedLanguage, packagePath);
            return specificLanguageCheck;
        }

        _logger.LogWarning("No language-specific check service found for package at {PackagePath}", packagePath);
        return null;
    }

    /// <summary>
    /// Gets all available language-specific check services.
    /// </summary>
    /// <returns>Collection of all available language check services</returns>
    public IEnumerable<ILanguageSpecificChecks> GetAllLanguageChecks() => _languageChecks;

}