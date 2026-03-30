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
    private readonly INpxHelper _npxHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<CommonValidationHelpers> _logger;
    private readonly ICopilotAgentRunner _copilotAgentRunner;

    public CommonValidationHelpers(
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,
        ILogger<CommonValidationHelpers> logger,
        ICopilotAgentRunner copilotAgentRunner)
    {
        _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
        _npxHelper = npxHelper ?? throw new ArgumentNullException(nameof(npxHelper));
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
                    NextSteps =
                    [
                        "Ensure you are running this command from within a clone of an Azure SDK repository.",
                        "Verify that 'eng/common/scripts/Verify-ChangeLog.ps1' exists at the repository root.",
                        "If the script is missing, run 'git restore eng/common/scripts/Verify-ChangeLog.ps1' or re-sync your branch."
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
                    NextSteps =
                    [
                        "Review the output above for specific changelog validation errors.",
                        "Ensure the CHANGELOG.md file exists and follows the expected format.",
                        "Verify that the changelog contains an entry for the current package version."
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
                NextSteps =
                [
                    "An unexpected error occurred during changelog validation.",
                    "Ensure 'pwsh' (PowerShell) is installed and available on your PATH.",
                    "Check that you have read access to the repository and the package directory."
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
                    NextSteps =
                    [
                        "Ensure you are running this command from within a clone of an Azure SDK repository.",
                        "Verify that 'eng/common/scripts/Verify-Readme.ps1' exists at the repository root.",
                        "If the script is missing, run 'git restore eng/common/scripts/Verify-Readme.ps1' or re-sync your branch."
                    ]
                };
            }

            var settingsPath = Path.Combine(packageRepoRoot, "eng", ".docsettings.yml");

            if (!File.Exists(settingsPath))
            {
                return new PackageCheckResponse(1, "", $"Doc settings file not found at expected location: {settingsPath}")
                {
                    NextSteps =
                    [
                        "Verify that 'eng/.docsettings.yml' exists at the repository root.",
                        "If the file is missing, run 'git restore eng/.docsettings.yml' or re-sync your branch.",
                        "This file is required for README validation and defines doc scanning configuration."
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
                    NextSteps =
                    [
                        "Review the output above for specific README validation errors.",
                        "Ensure a README.md file exists in your package directory.",
                        "Verify the README follows the expected format and contains required sections."
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
                NextSteps =
                [
                    "An unexpected error occurred during README validation.",
                    "Ensure 'pwsh' (PowerShell) is installed and available on your PATH.",
                    "Check that you have read access to the repository and the package directory."
                ]
            };
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
            var (packageRepoRoot, errorResponse) = await ValidatePackageAndDiscoverRepoAsync(packagePath, ct);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            var cspellConfigPath = Path.Combine(packageRepoRoot, ".vscode", "cspell.json");

            if (!File.Exists(cspellConfigPath))
            {
                return new PackageCheckResponse(1, "", $"Cspell config file not found at expected location: {cspellConfigPath}")
                {
                    NextSteps =
                    [
                        "Ensure you are running this command from within a clone of an Azure SDK repository.",
                        "Verify that '.vscode/cspell.json' exists at the repository root.",
                        "If the config file is missing, run 'git restore .vscode/cspell.json' or re-sync your branch."
                    ]
                };
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
                        NextSteps =
                        [
                            "The automatic spelling fix agent encountered an error.",
                            "Review the cspell output above and fix the spelling issues manually.",
                            "For legitimate technical terms, add them to the 'words' list in '.vscode/cspell.json'."
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
                    NextSteps =
                    [
                        "Review the cspell output above for words flagged as misspelled.",
                        "Fix any genuine typos in your source files.",
                        "For legitimate technical terms, add them to the 'words' list in '.vscode/cspell.json'.",
                        "Re-run the check with '--fix-check-errors' to attempt automatic spelling fixes."
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
                NextSteps =
                [
                    "An unexpected error occurred during spelling validation.",
                    "Ensure 'npx' and 'cspell' are installed and available on your PATH.",
                    "Check that you have read access to the repository and the package directory."
                ]
            };
        }
    }

    public async Task<(string? repoRoot, PackageCheckResponse? errorResponse)> ValidatePackageAndDiscoverRepoAsync(string packagePath, CancellationToken ct = default)
    {
        if (!Directory.Exists(packagePath))
        {
            return (null, new PackageCheckResponse(1, "", $"Package path does not exist: {packagePath}")
            {
                NextSteps =
                [
                    "Verify the package path is correct and the directory exists on disk.",
                    "Ensure you have restored/cloned the repository and the package directory is present."
                ]
            });
        }

        var packageRepoRoot = await _gitHelper.DiscoverRepoRootAsync(packagePath, ct);
        if (string.IsNullOrEmpty(packageRepoRoot))
        {
            return (null, new PackageCheckResponse(1, "", $"Could not find repository root from package path: {packagePath}")
            {
                NextSteps =
                [
                    "Ensure you are running this command from within a Git repository.",
                    "Verify the package path is inside a cloned Azure SDK repository.",
                    "Check that the '.git' directory exists at the repository root."
                ]
            });
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