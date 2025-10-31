using System.ComponentModel;
using System.Threading;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Prompts;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using ModelContextProtocol.Protocol;

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
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix dependency issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the dependency analysis</returns>
    Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Validates the changelog for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix changelog issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the changelog validation</returns>
    Task<CLICheckResponse> ValidateChangelogAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Validates the README for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix README issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the README validation</returns>
    Task<CLICheckResponse> ValidateReadmeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Checks spelling in the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix spelling issues where supported by cspell</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the spelling check</returns>
    Task<CLICheckResponse> CheckSpellingAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Updates code snippets in the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix snippet issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the snippet update operation</returns>
    Task<CLICheckResponse> UpdateSnippetsAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Lints code in the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to automatically fix linting issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the code linting operation</returns>
    Task<CLICheckResponse> LintCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Formats code in the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to automatically apply code formatting</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the code formatting operation</returns>
    Task<CLICheckResponse> FormatCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Checks AOT compatibility for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the AOT compatibility check</returns>
    Task<CLICheckResponse> CheckAotCompat(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Checks generated code for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the generated code check</returns>
    Task<CLICheckResponse> CheckGeneratedCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);
    
    /// <summary>
    /// Validates samples for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the sample validation</returns>
    Task<CLICheckResponse> ValidateSamplesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);
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
    private readonly ILanguageSpecificResolver<ILanguageSpecificChecks> _languageSpecificChecks;
    private readonly IMicroagentHostService _microagentHostService;

    public LanguageChecks(IProcessHelper processHelper, INpxHelper npxHelper, IGitHelper gitHelper, ILogger<LanguageChecks> logger, ILanguageSpecificResolver<ILanguageSpecificChecks> languageSpecificChecks, IMicroagentHostService microagentHostService)
    {
        _processHelper = processHelper;
        _npxHelper = npxHelper;
        _gitHelper = gitHelper;
        _logger = logger;
        _languageSpecificChecks = languageSpecificChecks;
        _microagentHostService = microagentHostService;
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

    public virtual async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new CLICheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }

        return await languageSpecificCheck.AnalyzeDependenciesAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<CLICheckResponse> ValidateChangelogAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        return await ValidateChangelogCommonAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<CLICheckResponse> ValidateReadmeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        return await ValidateReadmeCommonAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<CLICheckResponse> CheckSpellingAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        return await CheckSpellingCommonAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<CLICheckResponse> UpdateSnippetsAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new CLICheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }

        return await languageSpecificCheck.UpdateSnippetsAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<CLICheckResponse> LintCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new CLICheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }

        return await languageSpecificCheck.LintCodeAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<CLICheckResponse> FormatCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new CLICheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }

        return await languageSpecificCheck.FormatCodeAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<CLICheckResponse> ValidateSamplesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new CLICheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }
        return await languageSpecificCheck.ValidateSamplesAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<CLICheckResponse> CheckAotCompat(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new CLICheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }
        return await languageSpecificCheck.CheckAotCompat(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<CLICheckResponse> CheckGeneratedCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new CLICheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }

        return await languageSpecificCheck.CheckGeneratedCode(packagePath, fixCheckErrors, ct);
    }

    /// <summary>
    /// Common changelog validation implementation that works for most Azure SDK languages.
    /// Uses the PowerShell script from eng/common/scripts/Verify-ChangeLog.ps1.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix changelog issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<CLICheckResponse> ValidateChangelogCommonAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var (packageRepoRoot, errorResponse) = ValidatePackageAndDiscoverRepo(packagePath);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);
            if (languageSpecificCheck == null)
            {
                return new CLICheckResponse(1, "", $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.");
            }

            // Construct the path to the PowerShell script in the SDK repository
            var scriptPath = Path.Combine(packageRepoRoot, "eng", "common", "scripts", "Verify-ChangeLog.ps1");

            if (!File.Exists(scriptPath))
            {
                return new CLICheckResponse(1, "", $"PowerShell script not found at expected location: {scriptPath}");
            }

            var command = "pwsh";
            var args = new[] { "-File", scriptPath, "-PackageName", await languageSpecificCheck.GetSDKPackageName(packageRepoRoot, packagePath, ct) };

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
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix README issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<CLICheckResponse> ValidateReadmeCommonAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
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
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix spelling issues where supported by cspell</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<CLICheckResponse> CheckSpellingCommonAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
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

            // If fix is requested and there are spelling issues, use Microagent to automatically apply fixes
            if (fixCheckErrors && processResult.ExitCode != 0 && !string.IsNullOrWhiteSpace(processResult.Output))
            {
                try
                {
                    var fixResult = await RunSpellingFixMicroagent(packageRepoRoot, processResult.Output, ct);
                    return new CLICheckResponse(0, fixResult.Summary);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running spelling fix microagent");
                    return new CLICheckResponse(processResult.ExitCode, processResult.Output, ex.Message);
                }
            }

            return new CLICheckResponse(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckSpellingCommonAsync");
            return new CLICheckResponse(1, "", ex.Message);
        }
    }

    /// <summary>
    /// Result of the spelling fix microagent operation.
    /// </summary>
    public record SpellingFixResult(
        [property: Description("Summary of the operations performed")] string Summary
    );

    /// <summary>
    /// Runs a microagent to automatically fix spelling issues by either correcting typos or adding legitimate terms to cspell.json.
    /// </summary>
    /// <param name="repoRoot">Repository root path</param>
    /// <param name="cspellOutput">Output from cspell lint command</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the spelling fix operation</returns>
    private async Task<SpellingFixResult> RunSpellingFixMicroagent(string repoRoot, string cspellOutput, CancellationToken ct)
    {
        var spellingTemplate = new SpellingValidationTemplate(cspellOutput, repoRoot);
        var agent = new Microagent<SpellingFixResult>
        {
            Instructions = spellingTemplate.BuildPrompt(),
            MaxToolCalls = 10,
            Model = "gpt-4",
            Tools = new IAgentTool[]
            {
                new ReadFileTool(repoRoot),
                new WriteFileTool(repoRoot),
                new UpdateCspellWordsTool(repoRoot)
            }
        };

        return await _microagentHostService.RunAgentToCompletion(agent, ct);
    }
}
