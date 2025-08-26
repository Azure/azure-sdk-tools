using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

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
    Task<ILanguageSpecificChecks?> GetLanguageCheckAsync(string packagePath);
}

/// <summary>
/// Resolves the appropriate language-specific check implementation based on package contents.
/// Uses the Language-Settings.ps1 file in the repository to determine the language.
/// Uses composition pattern instead of inheritance.
/// </summary>
public class LanguageSpecificCheckResolver : ILanguageSpecificCheckResolver
{
    private readonly IEnumerable<ILanguageSpecificChecks> _languageChecks;
    private readonly IGitHelper _gitHelper;
    private readonly IPowershellHelper _powershellHelper;
    private readonly ILogger<LanguageSpecificCheckResolver> _logger;

    public LanguageSpecificCheckResolver(
        IEnumerable<ILanguageSpecificChecks> languageChecks,
        IGitHelper gitHelper,
        IPowershellHelper powershellHelper,
        ILogger<LanguageSpecificCheckResolver> logger)
    {
        _languageChecks = languageChecks ?? throw new ArgumentNullException(nameof(languageChecks));
        _gitHelper = gitHelper ?? throw new ArgumentNullException(nameof(gitHelper));
        _powershellHelper = powershellHelper ?? throw new ArgumentNullException(nameof(powershellHelper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the appropriate language-specific check service for the given package path.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <returns>Language-specific check service that can handle the package, or null if no handler is found</returns>
    public async Task<ILanguageSpecificChecks?> GetLanguageCheckAsync(string packagePath)
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
        var detectedLanguage = await DetectLanguageAsync(packagePath);
        
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

    private async Task<string?> DetectLanguageAsync(string packagePath)
    {
        try
        {
            var repositoryPath = _gitHelper.DiscoverRepoRoot(packagePath);
            if (string.IsNullOrEmpty(repositoryPath))
            {
                return null;
            }

            // Read Language-Settings.ps1 file to determine language
            var languageSettingsPath = Path.Combine(repositoryPath, "eng", "scripts", "Language-Settings.ps1");
            if (!File.Exists(languageSettingsPath))
            {
                _logger.LogWarning("Language-Settings.ps1 not found at {LanguageSettingsPath}", languageSettingsPath);
                return null;
            }

            var language = await ExtractLanguageFromFileAsync(languageSettingsPath);
            if (!string.IsNullOrEmpty(language))
            {
                _logger.LogDebug("Detected language: {Language} from Language-Settings.ps1 at {Path}", language, languageSettingsPath);
                return language;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting language for package path: {PackagePath}", packagePath);
            return null;
        }
    }

    private async Task<string?> ExtractLanguageFromFileAsync(string filePath)
    {
        try
        {
            var options = new PowershellOptions([". '" + filePath.Replace("'", "''") + "'; Write-Output $Language"], 
                workingDirectory: Path.GetDirectoryName(filePath));

            var result = await _powershellHelper.Run(options, CancellationToken.None);
            
            if (result.ExitCode != 0)
            {
                _logger.LogError("PowerShell execution failed for {FilePath}: {Output}", filePath, result.Output);
                return null;
            }

            var output = result.Output?.Trim();
            
            if (!string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("Extracted language '{Language}' from {FilePath}", output, filePath);
                return output;
            }

            _logger.LogWarning("No valid language value found in {FilePath}", filePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Language-Settings.ps1 file: {FilePath}", filePath);
            return null;
        }
    }

}