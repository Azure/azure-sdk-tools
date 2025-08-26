using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Interface for language repository service operations.
/// </summary>
public interface ILanguageChecks
{
    /// <summary>
    /// Analyzes dependencies for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the dependency analysis</returns>
    Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct);

    /// <summary>
    /// Validates the changelog for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the changelog validation</returns>
    Task<CLICheckResponse> ValidateChangelogAsync(string packagePath, CancellationToken ct);

    /// <summary>
    /// Validates the README for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <returns>Result of the README validation</returns>
    Task<CLICheckResponse> ValidateReadmeAsync(string packagePath);

    /// <summary>
    /// Checks spelling in the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <returns>Result of the spelling check</returns>
    Task<CLICheckResponse> CheckSpellingAsync(string packagePath);

    /// <summary>
    /// Gets the SDK package path for the given repository and package path.
    /// </summary>
    /// <param name="repo">Repository root path</param>
    /// <param name="packagePath">Package path</param>
    /// <returns>SDK package path</returns>
    string GetSDKPackagePath(string repo, string packagePath);
}

/// <summary>
/// Implementation of language repository service.
/// </summary>
public class LanguageChecks : ILanguageChecks 
{
    private readonly IProcessHelper _processHelper;
    private readonly INpxHelper _npxHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<LanguageChecks> _logger;
    private readonly ILanguageSpecificCheckResolver _languageSpecificCheckResolver;

    public LanguageChecks(IProcessHelper processHelper, INpxHelper npxHelper, IGitHelper gitHelper, ILogger<LanguageChecks> logger, ILanguageSpecificCheckResolver languageSpecificCheckResolver)
    {
        _processHelper = processHelper;
        _npxHelper = npxHelper;
        _gitHelper = gitHelper;
        _logger = logger;
        _languageSpecificCheckResolver = languageSpecificCheckResolver;
    }

    /// <summary>
    /// Validates package path and discovers repository root.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>Repository root path if successful, or CLICheckResponse with error if validation fails</returns>
    private (string? repoRoot, CLICheckResponse? errorResponse) ValidatePackageAndDiscoverRepo(string packagePath)
    {
        if (!Directory.Exists(packagePath))
        {
            return (null, new CLICheckResponse(1, "", $"Package path does not exist: {packagePath}"));
        }

        // Find the SDK repository root by looking for common repository indicators
        var packageRepoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
        if (string.IsNullOrEmpty(packageRepoRoot))
        {
            return (null, new CLICheckResponse(1, "", $"Could not find repository root from package path: {packagePath}"));
        }

        return (packageRepoRoot, null);
    }

    public virtual async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct)
    {
        var languageSpecificCheck = _languageSpecificCheckResolver.GetLanguageCheck(packagePath);
        
        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new CLICheckResponse(
                exitCode: 1, 
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }
        
        return await languageSpecificCheck.AnalyzeDependenciesAsync(packagePath, ct);
    }

    public virtual async Task<CLICheckResponse> ValidateChangelogAsync(string packagePath, CancellationToken ct)
    {
        return await ValidateChangelogCommonAsync(packagePath, ct);
    }

    public virtual async Task<CLICheckResponse> ValidateReadmeAsync(string packagePath)
    {
        return await ValidateReadmeCommonAsync(packagePath);
    }

    public virtual async Task<CLICheckResponse> CheckSpellingAsync(string packagePath)
    {
        return await CheckSpellingCommonAsync(packagePath);
    }

    /// <summary>
    /// Common changelog validation implementation that works for most Azure SDK languages.
    /// Uses the PowerShell script from eng/common/scripts/Verify-ChangeLog.ps1.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<CLICheckResponse> ValidateChangelogCommonAsync(string packagePath, CancellationToken ct)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var (packageRepoRoot, errorResponse) = ValidatePackageAndDiscoverRepo(packagePath);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            // Construct the path to the PowerShell script in the SDK repository
            var scriptPath = Path.Combine(packageRepoRoot, "eng", "common", "scripts", "Verify-ChangeLog.ps1");

            if (!File.Exists(scriptPath))
            {
                return new CLICheckResponse(1, "", $"PowerShell script not found at expected location: {scriptPath}");
            }

            var command = "pwsh";
            var args = new[] { "-File", scriptPath, "-PackageName", this.GetSDKPackagePath(packageRepoRoot, packagePath) };

            // Use a longer timeout for changelog validation - 5 minutes should be sufficient
            var timeout = TimeSpan.FromMinutes(5);
            var processResult = await _processHelper.Run(new(command, args, timeout: timeout, workingDirectory: packagePath), ct);
            stopwatch.Stop();

            return CLICheckResponse.CreateResponseFromProcessResult(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateChangelogCommonAsync");
            return new CLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Common README validation implementation that works for most Azure SDK languages.
    /// Uses the PowerShell script from eng/common/scripts/Verify-Readme.ps1.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<CLICheckResponse> ValidateReadmeCommonAsync(string packagePath)
    {
        try
        {
            var (packageRepoRoot, errorResponse) = ValidatePackageAndDiscoverRepo(packagePath);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            // Construct the path to the PowerShell script in the SDK repository
            var scriptPath = Path.Combine(packageRepoRoot, Constants.ENG_COMMON_SCRIPTS_PATH, "Verify-Readme.ps1");
            
            if (!File.Exists(scriptPath))
            {
                return new CLICheckResponse(1, "", $"PowerShell script not found at expected location: {scriptPath}");
            }

            // Construct the path to the doc settings file
            var settingsPath = Path.Combine(packageRepoRoot, "eng", ".docsettings.yml");
            
            if (!File.Exists(settingsPath))
            {
                return new CLICheckResponse(1, "", $"Doc settings file not found at expected location: {settingsPath}");
            }

            var command = "pwsh";
            var args = new[] { 
                "-File", scriptPath, 
                "-SettingsPath", settingsPath,
                "-ScanPaths", packagePath,
            };

            var timeout = TimeSpan.FromMinutes(10);
            var processResult = await _processHelper.Run(new(command, args, timeout: timeout, workingDirectory: packagePath), ct: default);

            return CLICheckResponse.CreateResponseFromProcessResult(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateReadmeCommonAsync");
            return new CLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Common spelling check implementation that works for most Azure SDK languages.
    /// Uses cspell directly to check spelling in the package directory.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<CLICheckResponse> CheckSpellingCommonAsync(string packagePath)
    {
        try
        {
            var (packageRepoRoot, errorResponse) = ValidatePackageAndDiscoverRepo(packagePath);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            // Construct the path to the cspell config file
            var cspellConfigPath = Path.Combine(packageRepoRoot, ".vscode", "cspell.json");
            
            if (!File.Exists(cspellConfigPath))
            {
                return new CLICheckResponse(1, "", $"Cspell config file not found at expected location: {cspellConfigPath}");
            }

            // Convert absolute path to relative path from repo root
            var relativePath = Path.GetRelativePath(packageRepoRoot, packagePath);


            var npxOptions = new NpxOptions( 
                null, 
                ["cspell", "lint", "--config", cspellConfigPath, "--root", packageRepoRoot, $"." + Path.DirectorySeparatorChar + relativePath + Path.DirectorySeparatorChar + "**"], 
                logOutputStream: true 
            ); 
            var processResult = await _npxHelper.Run(npxOptions, ct: default);
            return CLICheckResponse.CreateResponseFromProcessResult(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckSpellingCommonAsync");
            return new CLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
    }

    public virtual string GetSDKPackagePath(string repo, string packagePath)
    {
        return Path.GetFileName(packagePath);
    }
}
