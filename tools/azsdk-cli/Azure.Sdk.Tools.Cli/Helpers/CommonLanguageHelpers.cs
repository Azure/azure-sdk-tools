using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Microsoft.Extensions.AI;
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
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix spelling issues where supported by cspell</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<PackageCheckResponse> CheckSpelling(
        string packagePath,
        bool fixCheckErrors = false,
        CancellationToken ct = default);

    /// <summary>
    /// Validates package path and discovers repository root.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Repository root path if successful, or PackageCheckResponse with error if validation fails</returns>
    Task<(string? repoRoot, PackageCheckResponse? errorResponse)> ValidatePackageAndDiscoverRepoAsync(string packagePath, CancellationToken ct = default);
}

/// <summary>
/// Provides common helper methods for validation checks
/// </summary>
public class CommonValidationHelpers : ICommonValidationHelpers
{
    private readonly IProcessHelper _processHelper;
    private readonly IPowershellHelper _powershellHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<CommonValidationHelpers> _logger;
    private readonly ICopilotAgentRunner _copilotAgentRunner;

    public CommonValidationHelpers(
        IProcessHelper processHelper,
        IPowershellHelper powershellHelper,
        IGitHelper gitHelper,
        ILogger<CommonValidationHelpers> logger,
        ICopilotAgentRunner copilotAgentRunner)
    {
        _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
        _powershellHelper = powershellHelper ?? throw new ArgumentNullException(nameof(powershellHelper));
        _gitHelper = gitHelper ?? throw new ArgumentNullException(nameof(gitHelper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _copilotAgentRunner = copilotAgentRunner ?? throw new ArgumentNullException(nameof(copilotAgentRunner));
    }

    public async Task<PackageCheckResponse> ValidateChangelog(
        string packageName,
        string packagePath, 
        bool fixCheckErrors = false, 
        CancellationToken ct = default)
    {
        try
        {
            var (packageRepoRoot, errorResponse) = await ValidatePackageAndDiscoverRepoAsync(packagePath, ct);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            var scriptPath = Path.Combine(packageRepoRoot, "eng", "common", "scripts", "Verify-ChangeLog.ps1");

            if (!File.Exists(scriptPath))
            {
                return new PackageCheckResponse(1, "", $"PowerShell script not found at expected location: {scriptPath}")
                {
                    NextSteps = [
                        "Run 'azsdk verify setup' to check required tools and repository prerequisites",
                        "If eng/common scripts are missing, use the eng/common sync process: https://github.com/Azure/azure-sdk-tools/blob/main/doc/common/common_engsys.md#engcommon-sync"
                    ]
                };
            }

            var command = "pwsh";
            var args = new[] { "-File", scriptPath, "-PackageName", packageName };

            var timeout = TimeSpan.FromMinutes(5);
            var processResult = await _processHelper.Run(new(command, args, timeout: timeout, workingDirectory: packagePath), ct);

            if (processResult.ExitCode != 0)
            {
                _logger.LogWarning("Changelog validation failed. Exit Code: {ExitCode}, Output: {Output}",
                    processResult.ExitCode, processResult.Output);
                return new PackageCheckResponse(processResult.ExitCode, processResult.Output, "Changelog validation failed.")
                {
                    NextSteps = [
                        "Review and update the CHANGELOG.md file to ensure it follows the proper format",
                        "Refer to the Azure SDK changelog guidelines: https://aka.ms/azsdk/changelog"
                    ]
                };
            }

            return new PackageCheckResponse(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateChangelog");
            return new PackageCheckResponse(1, "", $"Unhandled exception: {ex.Message}")
            {
                NextSteps = [
                    "Run 'azsdk verify setup' to check all required tool dependencies are installed",
                    "Verify that the CHANGELOG.md file exists in the package directory"
                ]
            };
        }
    }

    public async Task<PackageCheckResponse> ValidateReadme(
        string packagePath, 
        bool fixCheckErrors = false, 
        CancellationToken ct = default)
    {
        try
        {
            var (packageRepoRoot, errorResponse) = await ValidatePackageAndDiscoverRepoAsync(packagePath, ct);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            var scriptPath = Path.Combine(packageRepoRoot, Constants.ENG_COMMON_SCRIPTS_PATH, "Verify-Readme.ps1");

            if (!File.Exists(scriptPath))
            {
                return new PackageCheckResponse(1, "", $"PowerShell script not found at expected location: {scriptPath}")
                {
                    NextSteps = [
                        "Run 'azsdk verify setup' to check required tools and repository prerequisites",
                        "eng/common is synced via the eng/common sync pipeline - see https://github.com/Azure/azure-sdk-tools/blob/main/doc/common/common_engsys.md#engcommon-sync"
                    ]
                };
            }

            var settingsPath = Path.Combine(packageRepoRoot, "eng", ".docsettings.yml");

            if (!File.Exists(settingsPath))
            {
                return new PackageCheckResponse(1, "", $"Doc settings file not found at expected location: {settingsPath}")
                {
                    NextSteps = [
                        "Run 'azsdk verify setup' to check required tools and repository prerequisites",
                        "Ensure the eng/.docsettings.yml file exists in the repository root",
                        "This file is required for README validation - check repository documentation setup"
                    ]
                };
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
                return new PackageCheckResponse(processResult.ExitCode, processResult.Output, "Readme validation failed.")
                {
                    NextSteps = [
                        "Create or update the README.md file to include required sections",
                        "Ensure the README follows Azure SDK documentation standards",
                        "Include installation instructions, usage examples, and API documentation links",
                        "Verify that all code samples in the README are working and up-to-date"
                    ]
                };
            }

            return new PackageCheckResponse(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateReadme");
            return new PackageCheckResponse(1, "", $"Unhandled exception: {ex.Message}")
            {
                NextSteps = [
                    "Run 'azsdk verify setup' to check all required tool dependencies are installed",
                    "Verify that a README.md file exists in the package directory"
                ]
            };
        }
    }

    public async Task<PackageCheckResponse> CheckSpelling(
        string packagePath, 
        bool fixCheckErrors = false, 
        CancellationToken ct = default)
    {
        try
        {
            var (packageRepoRoot, errorResponse) = await ValidatePackageAndDiscoverRepoAsync(packagePath, ct);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            var scriptPath = Path.Combine(packageRepoRoot, "eng", "common", "spelling", "Invoke-Cspell.ps1");

            if (!File.Exists(scriptPath))
            {
                return new PackageCheckResponse(1, "", $"Invoke-Cspell.ps1 script not found at expected location: {scriptPath}")
                {
                    NextSteps = [
                        "Run 'azsdk verify setup' to check required tools and repository prerequisites",
                        "If eng/common scripts are missing, use the eng/common sync process: https://github.com/Azure/azure-sdk-tools/blob/main/doc/common/common_engsys.md#engcommon-sync"
                    ]
                };
            }

            var cspellConfigPath = Path.Combine(packageRepoRoot, ".vscode", "cspell.json");

            if (!File.Exists(cspellConfigPath))
            {
                return new PackageCheckResponse(1, "", $"Cspell config file not found at expected location: {cspellConfigPath}")
                {
                    NextSteps = [
                        "Ensure the .vscode/cspell.json configuration file exists in the repository root",
                        "This file is required for spelling validation - check repository setup"
                    ]
                };
            }

            // Escape single quotes in paths for use in PowerShell script blocks
            var escapedScriptPath = scriptPath.Replace("'", "''");
            var escapedConfigPath = cspellConfigPath.Replace("'", "''");
            var escapedRepoRoot = packageRepoRoot.Replace("'", "''");

            // Get only the files with changes that have changed between the current branch and the default (main) branch.
            // This avoids scanning thousands of files in directories like .tox, node_modules, etc.
            var mergeBaseSha = await _gitHelper.GetMergeBaseCommitShaAsync(packageRepoRoot, "main", ct);

            // Normalize package path to be relative to the repo root and use forward slashes for git pathspecs
            var relativePackagePath = Path.GetRelativePath(packageRepoRoot, packagePath);
            var normalizedDiffPath = relativePackagePath
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');

            var changedFiles = await _gitHelper.GetChangedFilesAsync(
                packageRepoRoot,
                mergeBaseSha,
                null, // compare against working tree to include uncommitted changes
                normalizedDiffPath,
                "d", // exclude deleted files
                ct);

            if (changedFiles.Count == 0)
            {
                _logger.LogInformation("No changed files detected in {PackagePath}. Skipping spelling check.", packagePath);
                return new PackageCheckResponse(0, "No changed files detected. Spelling check skipped.");
            }

            _logger.LogInformation("Git detected {Count} changed file(s) in {PackagePath}", changedFiles.Count, packagePath);

            // Resolve changed file paths to absolute paths
            var absoluteChangedFiles = changedFiles
                .Select(f => Path.GetFullPath(Path.Combine(packageRepoRoot, f)))
                .Where(File.Exists)
                .ToList();

            if (absoluteChangedFiles.Count == 0)
            {
                _logger.LogInformation("No resolvable changed files in {PackagePath}. Skipping spelling check.", packagePath);
                return new PackageCheckResponse(0, "No resolvable changed files detected. Spelling check skipped.");
            }

            // Build file list as a PowerShell array of quoted paths
            var fileListLiteral = string.Join(", ", absoluteChangedFiles.Select(f => $"'{f.Replace("'", "''")}'"));
            var command = $"$files = @({fileListLiteral}); & '{escapedScriptPath}' -CSpellConfigPath '{escapedConfigPath}' -SpellCheckRoot '{escapedRepoRoot}' -FileList $files";

            var timeout = TimeSpan.FromMinutes(10);
            var processResult = await _powershellHelper.Run(new PowershellOptions([command], timeout: timeout, workingDirectory: packageRepoRoot), ct);

            // If fix is requested and there are spelling issues, use CopilotAgent to automatically apply fixes
            if (fixCheckErrors && processResult.ExitCode != 0 && !string.IsNullOrWhiteSpace(processResult.Output))
            {
                try
                {
                    var fixResult = await RunSpellingFixAgent(packageRepoRoot, processResult.Output, ct);
                    return new PackageCheckResponse(0, fixResult.Summary);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running spelling fix agent");
                    return new PackageCheckResponse(processResult.ExitCode, processResult.Output, ex.Message)
                    {
                        NextSteps = [
                            "Auto-fix agent failed - manually fix spelling errors listed in the output above",
                            "Add valid technical terms to the repo-root cspell configuration (e.g., .vscode/cspell.json)",
                            "Run with --fix flag again after resolving any agent configuration issues"
                        ]
                    };
                }
            }

            if (processResult.ExitCode != 0)
            {
                _logger.LogWarning("Spelling check failed. Exit Code: {ExitCode}, Output: {Output}",
                    processResult.ExitCode, processResult.Output);
                return new PackageCheckResponse(processResult.ExitCode, processResult.Output, "Spelling check failed.")
                {
                    NextSteps = [
                        "Run with --fix flag to automatically fix spelling errors using AI-assisted corrections",
                        "Add valid technical terms to the repo-root cspell configuration (e.g., .vscode/cspell.json)",
                        "Review the spelling errors listed above and fix them manually in source files"
                    ]
                };
            }

            return new PackageCheckResponse(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckSpelling");
            return new PackageCheckResponse(1, "", ex.Message)
            {
                NextSteps = [
                    "Run 'azsdk verify setup' to check all required tool dependencies are installed"
                ]
            };
        }
    }

    public async Task<(string? repoRoot, PackageCheckResponse? errorResponse)> ValidatePackageAndDiscoverRepoAsync(string packagePath, CancellationToken ct = default)
    {
        if (!Directory.Exists(packagePath))
        {
            return (null, new PackageCheckResponse(1, "", $"Package path does not exist: {packagePath}"));
        }

        var packageRepoRoot = await _gitHelper.DiscoverRepoRootAsync(packagePath, ct);
        if (string.IsNullOrEmpty(packageRepoRoot))
        {
            return (null, new PackageCheckResponse(1, "", $"Could not find repository root from package path: {packagePath}"));
        }

        return (packageRepoRoot, null);
    }

    /// <summary>
    /// Result of the spelling fix agent operation.
    /// </summary>
    public record SpellingFixResult(
        [property: Description("Summary of the operations performed")] string Summary
    );

    /// <summary>
    /// Runs a copilot agent to automatically fix spelling issues by either correcting typos or adding legitimate terms to cspell.json.
    /// </summary>
    /// <param name="repoRoot">Repository root path</param>
    /// <param name="cspellOutput">Output from cspell lint command</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the spelling fix operation</returns>
    private async Task<SpellingFixResult> RunSpellingFixAgent(string repoRoot, string cspellOutput, CancellationToken ct)
    {
        var spellingTemplate = new SpellingValidationTemplate(cspellOutput, repoRoot);
        var agent = new CopilotAgent<SpellingFixResult>
        {
            Instructions = spellingTemplate.BuildPrompt(),
            MaxIterations = 10,
            Tools =
            [
                FileTools.CreateReadFileTool(repoRoot),
                FileTools.CreateWriteFileTool(repoRoot),
                CspellTools.CreateUpdateCspellWordsTool(repoRoot)
            ]
        };

        return await _copilotAgentRunner.RunAsync(agent, ct);
    }
}
