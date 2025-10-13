using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

public class LanguageSpecificResolver<T>(
    IGitHelper _gitHelper,
    IPowershellHelper _powershellHelper,
    ILogger<LanguageSpecificResolver<T>> _logger,
    [FromKeyedServices(SdkLanguage.DotNet)]
    T? dotnetService = default,
    [FromKeyedServices(SdkLanguage.Java)]
    T? javaService = default,
    [FromKeyedServices(SdkLanguage.Python)]
    T? pythonService = default,
    [FromKeyedServices(SdkLanguage.JavaScript)]
    T? javaScriptService = default,
    [FromKeyedServices(SdkLanguage.Go)]
    T? goService = default
    // If adding languages in future, add a corresponding entry here.
) : ILanguageSpecificResolver<T> where T : class
{
    public async Task<T?> Resolve(string packagePath, CancellationToken ct = default)
    {
        var language = await DetectLanguageAsync(packagePath, ct);

        return language switch
        {
            SdkLanguage.DotNet => dotnetService,
            SdkLanguage.Java => javaService,
            SdkLanguage.Python => pythonService,
            SdkLanguage.JavaScript => javaScriptService,
            SdkLanguage.Go => goService,
            // If adding languages in future, add a corresponding entry here.
            _ => default,
        };
    }

    private async Task<SdkLanguage?> DetectLanguageAsync(string packagePath, CancellationToken ct)
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

            var language = await ExtractLanguageFromFileAsync(languageSettingsPath, ct);
            if (!string.IsNullOrEmpty(language))
            {
                _logger.LogDebug("Detected language: {Language} from Language-Settings.ps1 at {Path}", language, languageSettingsPath);

                if (Enum.TryParse<SdkLanguage>(language, ignoreCase: true, out var parsedLanguage))
                {
                    return parsedLanguage;
                }
                else
                {
                    _logger.LogWarning("Unrecognized language '{Language}' in Language-Settings.ps1 at {Path}", language, languageSettingsPath);
                    return null;
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

    private async Task<string?> ExtractLanguageFromFileAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var options = new PowershellOptions([". '" + filePath.Replace("'", "''") + "'; Write-Output $Language"], 
                workingDirectory: Path.GetDirectoryName(filePath));

            var result = await _powershellHelper.Run(options, ct);

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