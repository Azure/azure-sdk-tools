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
    Task<PackageCheckResponse> AnalyzeDependenciesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Validates the changelog for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix changelog issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the changelog validation</returns>
    Task<PackageCheckResponse> ValidateChangelogAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Validates the README for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix README issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the README validation</returns>
    Task<PackageCheckResponse> ValidateReadmeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Checks spelling in the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix spelling issues where supported by cspell</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the spelling check</returns>
    Task<PackageCheckResponse> CheckSpellingAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Updates code snippets in the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix snippet issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the snippet update operation</returns>
    Task<PackageCheckResponse> UpdateSnippetsAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Lints code in the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to automatically fix linting issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the code linting operation</returns>
    Task<PackageCheckResponse> LintCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Formats code in the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to automatically apply code formatting</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the code formatting operation</returns>
    Task<PackageCheckResponse> FormatCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Checks AOT compatibility for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the AOT compatibility check</returns>
    Task<PackageCheckResponse> CheckAotCompat(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

    /// <summary>
    /// Checks generated code for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the generated code check</returns>
    Task<PackageCheckResponse> CheckGeneratedCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);
    
    /// <summary>
    /// Validates samples for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the sample validation</returns>
    Task<PackageCheckResponse> ValidateSamplesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);
}

/// <summary>
/// Implementation of language repository service.
/// </summary>
public class LanguageChecks : ILanguageChecks
{
    private readonly ILogger<LanguageChecks> _logger;
    private readonly ILanguageSpecificResolver<ILanguageSpecificChecks> _languageSpecificChecks;

    public LanguageChecks(ILogger<LanguageChecks> logger, ILanguageSpecificResolver<ILanguageSpecificChecks> languageSpecificChecks)
    {
        _logger = logger;
        _languageSpecificChecks = languageSpecificChecks;
        _microagentHostService = microagentHostService;
    }

    /// <summary>
    /// Validates package path and discovers repository root.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>Repository root path if successful, or PackageCheckResponse with error if validation fails</returns>
    private (string? repoRoot, PackageCheckResponse? errorResponse) ValidatePackageAndDiscoverRepo(string packagePath)
    {
        if (!Directory.Exists(packagePath))
        {
            return (null, new PackageCheckResponse(1, "", $"Package path does not exist: {packagePath}"));
        }

        // Find the SDK repository root by looking for common repository indicators
        var packageRepoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
        if (string.IsNullOrEmpty(packageRepoRoot))
        {
            return (null, new PackageCheckResponse(1, "", $"Could not find repository root from package path: {packagePath}"));
        }

        return (packageRepoRoot, null);
    }

    public virtual async Task<PackageCheckResponse> AnalyzeDependenciesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new PackageCheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }

        return await languageSpecificCheck.AnalyzeDependenciesAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<PackageCheckResponse> ValidateChangelogAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
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

        return await languageSpecificCheck.ValidateChangelogAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<PackageCheckResponse> ValidateReadmeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
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

        return await languageSpecificCheck.ValidateReadmeAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<PackageCheckResponse> CheckSpellingAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
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

        return await languageSpecificCheck.CheckSpellingAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<PackageCheckResponse> UpdateSnippetsAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new PackageCheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }

        return await languageSpecificCheck.UpdateSnippetsAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<PackageCheckResponse> LintCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new PackageCheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }

        return await languageSpecificCheck.LintCodeAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<PackageCheckResponse> FormatCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new PackageCheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }

        return await languageSpecificCheck.FormatCodeAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<PackageCheckResponse> ValidateSamplesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new PackageCheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }
        return await languageSpecificCheck.ValidateSamplesAsync(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<PackageCheckResponse> CheckAotCompat(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new PackageCheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }
        return await languageSpecificCheck.CheckAotCompat(packagePath, fixCheckErrors, ct);
    }

    public virtual async Task<PackageCheckResponse> CheckGeneratedCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);

        if (languageSpecificCheck == null)
        {
            _logger.LogError("No language-specific check handler found for package at {PackagePath}. Supported languages may not include this package type.", packagePath);
            return new PackageCheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }

        return await languageSpecificCheck.CheckGeneratedCode(packagePath, fixCheckErrors, ct);
    }


}

/// <summary>
/// Provides common helper methods for language-specific checks that can be optionally used
/// by language implementations. Each language can choose to call these helpers or implement
/// their own custom logic.
/// </summary>
public static class CommonLanguageHelpers
{
    /// <summary>
    /// Common changelog validation implementation that works for most Azure SDK languages.
    /// Uses the PowerShell script from eng/common/scripts/Verify-ChangeLog.ps1.
    /// </summary>
    /// <param name="languageChecks">The language-specific checks instance</param>
    /// <param name="processHelper">Process helper for running commands</param>
    /// <param name="gitHelper">Git helper for repository operations</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix changelog issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<PackageCheckResponse> ValidateChangelogCommonAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var (packageRepoRoot, errorResponse) = ValidatePackageAndDiscoverRepo(packagePath, gitHelper);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            var languageSpecificCheck = await _languageSpecificChecks.Resolve(packagePath);
            if (languageSpecificCheck == null)
            {
                return new PackageCheckResponse(1, "", $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.");
            }

            // Construct the path to the PowerShell script in the SDK repository
            var scriptPath = Path.Combine(packageRepoRoot, "eng", "common", "scripts", "Verify-ChangeLog.ps1");

            if (!File.Exists(scriptPath))
            {
                return new PackageCheckResponse(1, "", $"PowerShell script not found at expected location: {scriptPath}");
            }

            var command = "pwsh";
            var args = new[] { "-File", scriptPath, "-PackageName", await languageChecks.GetSDKPackageName(packageRepoRoot, packagePath, ct) };

            // Use a longer timeout for changelog validation - 5 minutes should be sufficient
            var timeout = TimeSpan.FromMinutes(5);
            var processResult = await processHelper.Run(new(command, args, timeout: timeout, workingDirectory: packagePath), ct);
            stopwatch.Stop();

            return new PackageCheckResponse(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateChangelogCommonAsync");
            return new PackageCheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Common README validation implementation that works for most Azure SDK languages.
    /// Uses the PowerShell script from eng/common/scripts/Verify-Readme.ps1.
    /// </summary>
    /// <param name="processHelper">Process helper for running commands</param>
    /// <param name="gitHelper">Git helper for repository operations</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix README issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<PackageCheckResponse> ValidateReadmeCommonAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            var (packageRepoRoot, errorResponse) = ValidatePackageAndDiscoverRepo(packagePath, gitHelper);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            // Construct the path to the PowerShell script in the SDK repository
            var scriptPath = Path.Combine(packageRepoRoot, Constants.ENG_COMMON_SCRIPTS_PATH, "Verify-Readme.ps1");

            if (!File.Exists(scriptPath))
            {
                return new PackageCheckResponse(1, "", $"PowerShell script not found at expected location: {scriptPath}");
            }

            // Construct the path to the doc settings file
            var settingsPath = Path.Combine(packageRepoRoot, "eng", ".docsettings.yml");

            if (!File.Exists(settingsPath))
            {
                return new PackageCheckResponse(1, "", $"Doc settings file not found at expected location: {settingsPath}");
            }

            var command = "pwsh";
            var args = new[] {
                "-File", scriptPath,
                "-SettingsPath", settingsPath,
                "-ScanPaths", packagePath,
            };

            var timeout = TimeSpan.FromMinutes(10);
            var processResult = await processHelper.Run(new(command, args, timeout: timeout, workingDirectory: packagePath), ct);

            return new PackageCheckResponse(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateReadmeCommonAsync");
            return new PackageCheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Common spelling check implementation that checks for spelling issues and optionally applies fixes.
    /// </summary>
    /// <param name="languageChecks">The language-specific checks instance</param>
    /// <param name="processHelper">Process helper for running commands</param>
    /// <param name="npxHelper">NPX helper for running Node.js tools</param>
    /// <param name="gitHelper">Git helper for repository operations</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="microagentHostService">Microagent host service for AI-powered fixes</param>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix spelling issues where supported by cspell</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<PackageCheckResponse> CheckSpellingCommonAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            var (packageRepoRoot, errorResponse) = ValidatePackageAndDiscoverRepo(packagePath, gitHelper);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            // Construct the path to the cspell config file
            var cspellConfigPath = Path.Combine(packageRepoRoot, ".vscode", "cspell.json");

            if (!File.Exists(cspellConfigPath))
            {
                return new PackageCheckResponse(1, "", $"Cspell config file not found at expected location: {cspellConfigPath}");
            }

            var npxOptions = new NpxOptions(
                null,
                ["cspell", "lint", "--config", cspellConfigPath, "--root", packageRepoRoot, await languageChecks.GetSpellingCheckPath(packageRepoRoot, packagePath)],
                logOutputStream: true
            );
            var processResult = await npxHelper.Run(npxOptions, ct: ct);

            // If cspell checked 0 files, treat as success
            if (processResult.Output != null && processResult.Output.Contains("Files checked: 0"))
            {
                return new CLICheckResponse(0, processResult.Output);
            }

            // If fix is requested and there are spelling issues, use Microagent to automatically apply fixes
            if (fixCheckErrors && processResult.ExitCode != 0 && !string.IsNullOrWhiteSpace(processResult.Output))
            {
                try
                {
                    var fixResult = await RunSpellingFixMicroagent(packageRepoRoot, processResult.Output, ct);
                    return new PackageCheckResponse(0, fixResult.Summary);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running spelling fix microagent");
                    return new PackageCheckResponse(processResult.ExitCode, processResult.Output, ex.Message);
                }
            }

            return new PackageCheckResponse(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckSpellingCommonAsync");
            return new PackageCheckResponse(1, "", ex.Message);
        }
    }

    /// <summary>
    /// Validates package path and discovers repository root.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="gitHelper">Git helper for repository operations</param>
    /// <returns>Repository root path if successful, or CLICheckResponse with error if validation fails</returns>
    public static (string? repoRoot, CLICheckResponse? errorResponse) ValidatePackageAndDiscoverRepo(string packagePath, IGitHelper gitHelper)
    {
        if (!Directory.Exists(packagePath))
        {
            return (null, new CLICheckResponse(1, "", $"Package path does not exist: {packagePath}"));
        }

        // Find the SDK repository root by looking for common repository indicators
        var packageRepoRoot = gitHelper.DiscoverRepoRoot(packagePath);
        if (string.IsNullOrEmpty(packageRepoRoot))
        {
            return (null, new CLICheckResponse(1, "", $"Could not find repository root from package path: {packagePath}"));
        }

        return (packageRepoRoot, null);
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
    /// <param name="microagentHostService">Microagent host service</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the spelling fix operation</returns>
    private static async Task<SpellingFixResult> RunSpellingFixMicroagent(string repoRoot, string cspellOutput, IMicroagentHostService microagentHostService, CancellationToken ct)
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

        return await microagentHostService.RunAgentToCompletion(agent, ct);
    }
}