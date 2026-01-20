using System.ComponentModel;
using System.Threading;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Interface for common helper methods for validation checks
/// </summary>
public interface ICommonValidationHelpers
{
    /// <summary>
    /// Common changelog validation implementation
    /// </summary>
    /// <param name="packageName">SDK package name (provided by language-specific implementation)</param>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix changelog issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<PackageCheckResponse> ValidateChangelog(
        string packageName,
        string packagePath,
        bool fixCheckErrors = false,
        CancellationToken ct = default);

    /// <summary>
    /// Common README validation implementation
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix README issues</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<PackageCheckResponse> ValidateReadme(
        string packagePath,
        bool fixCheckErrors = false,
        CancellationToken ct = default);

    /// <summary>
    /// Common spelling check implementation
    /// </summary>
    /// <param name="spellingCheckPath">Path to check for spelling errors (provided by language-specific implementation)</param>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix spelling issues where supported by cspell</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<PackageCheckResponse> CheckSpelling(
        string spellingCheckPath,
        string packagePath,
        bool fixCheckErrors = false,
        CancellationToken ct = default);

    /// <summary>
    /// Validates package path and discovers repository root.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>Repository root path if successful, or PackageCheckResponse with error if validation fails</returns>
    (string? repoRoot, PackageCheckResponse? errorResponse) ValidatePackageAndDiscoverRepo(string packagePath);
}

/// <summary>
/// Provides common helper methods for validation checks
/// </summary>
public class CommonValidationHelpers : ICommonValidationHelpers
{
    private readonly IProcessHelper _processHelper;
    private readonly INpxHelper _npxHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<CommonValidationHelpers> _logger;
    private readonly IMicroagentHostService _microagentHostService;

    public CommonValidationHelpers(
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,
        ILogger<CommonValidationHelpers> logger,
        IMicroagentHostService microagentHostService)
    {
        _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
        _npxHelper = npxHelper ?? throw new ArgumentNullException(nameof(npxHelper));
        _gitHelper = gitHelper ?? throw new ArgumentNullException(nameof(gitHelper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _microagentHostService = microagentHostService ?? throw new ArgumentNullException(nameof(microagentHostService));
    }

    public async Task<PackageCheckResponse> ValidateChangelog(
        string packageName,
        string packagePath, 
        bool fixCheckErrors = false, 
        CancellationToken ct = default)
    {
        try
        {
            var (packageRepoRoot, errorResponse) = ValidatePackageAndDiscoverRepo(packagePath);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            var scriptPath = Path.Combine(packageRepoRoot, "eng", "common", "scripts", "Verify-ChangeLog.ps1");

            if (!File.Exists(scriptPath))
            {
                return new PackageCheckResponse(1, "", $"PowerShell script not found at expected location: {scriptPath}");
            }

            var command = "pwsh";
            var args = new[] { "-File", scriptPath, "-PackageName", packageName };

            var timeout = TimeSpan.FromMinutes(5);
            var processResult = await _processHelper.Run(new(command, args, timeout: timeout, workingDirectory: packagePath), ct);

            if (processResult.ExitCode != 0)
            {
                _logger.LogWarning("Changelog validation failed. Exit Code: {ExitCode}, Output: {Output}",
                    processResult.ExitCode, processResult.Output);
                return new PackageCheckResponse(processResult.ExitCode, processResult.Output, "Changelog validation failed.");
            }

            return new PackageCheckResponse(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateChangelog");
            return new PackageCheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
    }

    public async Task<PackageCheckResponse> ValidateReadme(
        string packagePath, 
        bool fixCheckErrors = false, 
        CancellationToken ct = default)
    {
        try
        {
            var (packageRepoRoot, errorResponse) = ValidatePackageAndDiscoverRepo(packagePath);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            var scriptPath = Path.Combine(packageRepoRoot, Constants.ENG_COMMON_SCRIPTS_PATH, "Verify-Readme.ps1");

            if (!File.Exists(scriptPath))
            {
                return new PackageCheckResponse(1, "", $"PowerShell script not found at expected location: {scriptPath}");
            }

            var settingsPath = Path.Combine(packageRepoRoot, "eng", ".docsettings.yml");

            if (!File.Exists(settingsPath))
            {
                return new PackageCheckResponse(1, "", $"Doc settings file not found at expected location: {settingsPath}");
            }

            // TODO: investigate doc-warden code, this normalizes package path for Scan Paths
            var normalizedPackagePath = Path.GetFullPath(packagePath);
            // Ensure drive letter is uppercase on Windows for consistency
            if (OperatingSystem.IsWindows() && normalizedPackagePath.Length >= 2)
            {
                normalizedPackagePath = char.ToUpperInvariant(normalizedPackagePath[0]) + normalizedPackagePath.Substring(1);
            }
            
            var command = "pwsh";
            var args = new[] {
                "-File", scriptPath,
                "-SettingsPath", settingsPath,
                "-ScanPaths", normalizedPackagePath,
            };

            var timeout = TimeSpan.FromMinutes(10);
            var processResult = await _processHelper.Run(new(command, args, timeout: timeout, workingDirectory: packagePath), ct);

            if (processResult.ExitCode != 0)
            {
                _logger.LogWarning("Readme validation failed. Exit Code: {ExitCode}, Output: {Output}",
                    processResult.ExitCode, processResult.Output);
                return new PackageCheckResponse(processResult.ExitCode, processResult.Output, "Readme validation failed.");
            }

            return new PackageCheckResponse(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateReadme");
            return new PackageCheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
    }

    public async Task<PackageCheckResponse> CheckSpelling(
        string spellingCheckPath,
        string packagePath, 
        bool fixCheckErrors = false, 
        CancellationToken ct = default)
    {
        try
        {
            var (packageRepoRoot, errorResponse) = ValidatePackageAndDiscoverRepo(packagePath);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            var cspellConfigPath = Path.Combine(packageRepoRoot, ".vscode", "cspell.json");

            if (!File.Exists(cspellConfigPath))
            {
                return new PackageCheckResponse(1, "", $"Cspell config file not found at expected location: {cspellConfigPath}");
            }

            var npxOptions = new NpxOptions(
                null,
                ["cspell", "lint", "--config", cspellConfigPath, "--root", packageRepoRoot, spellingCheckPath],
                logOutputStream: true
            );
            var processResult = await _npxHelper.Run(npxOptions, ct: ct);

            // If cspell checked 0 files, treat as success
            if (processResult.Output != null && processResult.Output.Contains("Files checked: 0"))
            {
                return new PackageCheckResponse(0, processResult.Output);
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

            if (processResult.ExitCode != 0)
            {
                _logger.LogWarning("Spelling check failed. Exit Code: {ExitCode}, Output: {Output}",
                    processResult.ExitCode, processResult.Output);
                return new PackageCheckResponse(processResult.ExitCode, processResult.Output, "Spelling check failed.");
            }

            return new PackageCheckResponse(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckSpelling");
            return new PackageCheckResponse(1, "", ex.Message);
        }
    }

    public (string? repoRoot, PackageCheckResponse? errorResponse) ValidatePackageAndDiscoverRepo(string packagePath)
    {
        if (!Directory.Exists(packagePath))
        {
            return (null, new PackageCheckResponse(1, "", $"Package path does not exist: {packagePath}"));
        }

        var packageRepoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
        if (string.IsNullOrEmpty(packageRepoRoot))
        {
            return (null, new PackageCheckResponse(1, "", $"Could not find repository root from package path: {packagePath}"));
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