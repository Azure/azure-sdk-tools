using System.ComponentModel;
using System.Threading;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Prompts;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using ModelContextProtocol.Protocol;

namespace Azure.Sdk.Tools.Cli.Services;

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
    public static async Task<CLICheckResponse> ValidateChangelogCommon(
        ILanguageSpecificChecks languageChecks,
        IProcessHelper processHelper,
        IGitHelper gitHelper,
        ILogger logger,
        string packagePath, 
        bool fixCheckErrors = false, 
        CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var (packageRepoRoot, errorResponse) = ValidatePackageAndDiscoverRepo(packagePath, gitHelper);
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
            var args = new[] { "-File", scriptPath, "-PackageName", await languageChecks.GetSDKPackageName(packageRepoRoot, packagePath, ct) };

            // Use a longer timeout for changelog validation - 5 minutes should be sufficient
            var timeout = TimeSpan.FromMinutes(5);
            var processResult = await processHelper.Run(new(command, args, timeout: timeout, workingDirectory: packagePath), ct);
            stopwatch.Stop();

            return new CLICheckResponse(processResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ValidateChangelogCommon");
            return new CLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
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
    public static async Task<CLICheckResponse> ValidateReadmeCommon(
        IProcessHelper processHelper,
        IGitHelper gitHelper,
        ILogger logger,
        string packagePath, 
        bool fixCheckErrors = false, 
        CancellationToken ct = default)
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
            var processResult = await processHelper.Run(new(command, args, timeout: timeout, workingDirectory: packagePath), ct);

            return new CLICheckResponse(processResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ValidateReadmeCommon");
            return new CLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
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
    public static async Task<CLICheckResponse> CheckSpellingCommon(
        ILanguageSpecificChecks languageChecks,
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,
        ILogger logger,
        IMicroagentHostService microagentHostService,
        string packagePath, 
        bool fixCheckErrors = false, 
        CancellationToken ct = default)
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
                return new CLICheckResponse(1, "", $"Cspell config file not found at expected location: {cspellConfigPath}");
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
                    var fixResult = await RunSpellingFixMicroagent(packageRepoRoot, processResult.Output, microagentHostService, ct);
                    return new CLICheckResponse(0, fixResult.Summary);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error running spelling fix microagent");
                    return new CLICheckResponse(processResult.ExitCode, processResult.Output, ex.Message);
                }
            }

            return new CLICheckResponse(processResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in CheckSpellingCommon");
            return new CLICheckResponse(1, "", ex.Message);
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