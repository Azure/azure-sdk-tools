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
    Task<CLICheckResponse> ValidateReadmeAsync(string packagePath, CancellationToken ct);

    /// <summary>
    /// Checks spelling in the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fix">Whether to attempt to automatically fix spelling issues where supported by cspell</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the spelling check</returns>
    Task<CLICheckResponse> CheckSpellingAsync(string packagePath, bool fix = false, CancellationToken ct = default);

    /// <summary>
    /// Updates code snippets in the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the snippet update operation</returns>
    Task<CLICheckResponse> UpdateSnippetsAsync(string packagePath, CancellationToken ct = default);

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
        var languageSpecificCheck = await _languageSpecificCheckResolver.GetLanguageCheckAsync(packagePath);

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

    public virtual async Task<CLICheckResponse> ValidateReadmeAsync(string packagePath, CancellationToken ct = default)
    {
        return await ValidateReadmeCommonAsync(packagePath, ct);
    }

    public virtual async Task<CLICheckResponse> CheckSpellingAsync(string packagePath, bool fix = false, CancellationToken ct = default)
    {
        return await CheckSpellingCommonAsync(packagePath, fix, ct);
    }

    public virtual async Task<CLICheckResponse> UpdateSnippetsAsync(string packagePath, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificCheckResolver.GetLanguageCheckAsync(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new CLICheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }

        return await languageSpecificCheck.UpdateSnippetsAsync(packagePath, ct);
    }

    /// <summary>
    /// Common changelog validation implementation that works for most Azure SDK languages.
    /// Uses the PowerShell script from eng/common/scripts/Verify-ChangeLog.ps1.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
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

            return new CLICheckResponse(processResult);
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
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<CLICheckResponse> ValidateReadmeCommonAsync(string packagePath, CancellationToken ct = default)
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
            var processResult = await _processHelper.Run(new(command, args, timeout: timeout, workingDirectory: packagePath), ct: ct);

            return new CLICheckResponse(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateReadmeCommonAsync");
            return new CLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Common spelling check implementation that checks for spelling issues and optionally applies fixes.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="fix">Whether to attempt to automatically fix spelling issues where supported by cspell</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<CLICheckResponse> CheckSpellingCommonAsync(string packagePath, bool fix = false, CancellationToken ct = default)
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
            var processResult = await _npxHelper.Run(npxOptions, ct: ct);

            // If fix is requested and there are spelling issues, provide fix guidance
            if (fix && processResult.ExitCode != 0 && !string.IsNullOrWhiteSpace(processResult.Output))
            {
                // Build an LLM prompt that instructs the model to either fix typos or add legitimate terms to cspell.json
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("You are an automated spelling helper. You will be provided with the output from cspell lint for a code repository.");
                sb.AppendLine("For each reported token, decide whether it is a typo that should be corrected, or a legitimate technical/proper term that should be added to the cspell dictionary (the cspell.json 'words' list).");
                sb.AppendLine();
                sb.AppendLine("Requirements:");
                sb.AppendLine("1) Output ONLY valid JSON: an array of objects. Each object must contain the following fields:");
                sb.AppendLine("   - file: path to the file containing the token (relative to repository root)");
                sb.AppendLine("   - line: the line number where the token appears");
                sb.AppendLine("   - original: the original token as reported by cspell");
                sb.AppendLine("   - recommendation: an object with these fields:");
                sb.AppendLine("       * action: one of 'fix' or 'ignore' ('fix' means propose a replacement, 'ignore' means add to cspell.json words)");
                sb.AppendLine("       * replacement: (string) present only if action == 'fix' — the corrected token or replacement text");
                sb.AppendLine("       * justification: (string) a brief one-line justification for the recommendation");
                sb.AppendLine();
                sb.AppendLine("2) If you recommend 'ignore', ensure you explain why this word should be kept (e.g. product name, acronym, code identifier, or language-specific term).");
                sb.AppendLine("3) Keep replacements minimal and preserve code formatting and casing.");
                sb.AppendLine();
                sb.AppendLine("cspell lint output:");
                sb.AppendLine(processResult.Output);
                sb.AppendLine();
                sb.AppendLine("Return the JSON array now.");

                var prompt = sb.ToString();

                var response = new CLICheckResponse(processResult.ExitCode, prompt);
                response.NextSteps = new List<string>
                {
                    "Send the returned JSON prompt to your LLM to obtain fix/ignore recommendations.",
                    "For 'ignore' recommendations, add the word to the repository's .vscode/cspell.json 'words' list.",
                    "For 'fix' recommendations, apply the suggested replacements as a patch and review before committing."
                };

                return response;
            }

            return new CLICheckResponse(processResult);
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
