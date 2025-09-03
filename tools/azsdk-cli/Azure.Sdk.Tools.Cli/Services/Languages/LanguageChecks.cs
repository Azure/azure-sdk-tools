using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;
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
    /// Fixes changelog issues in the specific package using microagent tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the changelog fix operation</returns>
    Task<CLICheckResponse> FixChangelogAsync(string packagePath, CancellationToken ct);

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
    private readonly IMicroagentHostService _microagentHostService;

    public LanguageChecks(IProcessHelper processHelper, INpxHelper npxHelper, IGitHelper gitHelper, ILogger<LanguageChecks> logger, ILanguageSpecificCheckResolver languageSpecificCheckResolver, IMicroagentHostService microagentHostService)
    {
        _processHelper = processHelper;
        _npxHelper = npxHelper;
        _gitHelper = gitHelper;
        _logger = logger;
        _languageSpecificCheckResolver = languageSpecificCheckResolver;
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

    public virtual async Task<CLICheckResponse> FixChangelogAsync(string packagePath, CancellationToken ct)
    {
        return await FixChangelogCommonAsync(packagePath, ct);
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

            return new CLICheckResponse(processResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateChangelogCommonAsync");
            return new CLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Common changelog fix implementation that works for most Azure SDK languages.
    /// Uses AI microagent tools to read and fix changelog issues.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<CLICheckResponse> FixChangelogCommonAsync(string packagePath, CancellationToken ct)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var (packageRepoRoot, errorResponse) = ValidatePackageAndDiscoverRepo(packagePath);
            if (errorResponse != null)
            {
                return errorResponse;
            }

            // Check if changelog file exists
            var changelogPath = Path.Combine(packagePath, "CHANGELOG.md");
            if (!File.Exists(changelogPath))
            {
                return new CLICheckResponse(1, "", $"CHANGELOG.md not found at {changelogPath}");
            }

            _logger.LogInformation("Starting changelog fix using microagent tools for package at {PackagePath}", packagePath);

            // First validate the changelog to get warnings/errors
            var validationResult = await ValidateChangelogCommonAsync(packagePath, ct);
            
            if (validationResult.ExitCode == 0)
            {
                return new CLICheckResponse(0, "Changelog is already valid, no fixes needed.");
            }

            // Read current changelog content
            var originalContent = await File.ReadAllTextAsync(changelogPath, ct);

            // Use microagent to fix changelog issues
            var prompt = $"""
                You are an expert at fixing Azure SDK changelog files. Your task is to analyze the current changelog content and fix any validation issues while preserving the existing content structure and entries.

                Validation Issues Found:
                {validationResult.CheckStatusDetails}

                Current Changelog Content:
                {originalContent}

                Rules for Azure SDK Changelogs:
                - Follow the format specified in https://github.com/Azure/azure-sdk/blob/main/docs/policies/README-TEMPLATE.md
                - Keep all existing version entries and their content
                - Ensure proper markdown formatting
                - Maintain chronological order (newest versions first), the most recent release should be set to today's date (use GetCurrentDate tool to get the current date)
                - Include proper date formats
                - Fix any formatting issues identified in the validation
                - Add changelog content based on the latest git changes using the ReadChangedFilesTool, do not add content to the changelog regarding the changes made to the changelog itself.

                Please provide the corrected changelog content. Use the validate_changelog_tool to check your work.
                """;

            var fixedChangelog = await _microagentHostService.RunAgentToCompletion(new Microagent<ChangelogContents>()
            {
                Instructions = prompt,
                MaxToolCalls = 10,
                Model = "gpt-4", // Default model, could be configurable
                Tools =
                [
                    AgentTool<ChangelogContents, ChangelogValidationResult>.FromFunc("validate_changelog_tool", 
                        "Validates changelog content by writing it to the file and running full validation", 
                        async (contents, ct) => await ValidateChangelogContentWithFile(contents, changelogPath, packagePath, ct)),
                    AgentTool<ChangelogContents, ReadFileResult>.FromFunc("read_changelog_tool", 
                        "Reads the current changelog file content", 
                        async (contents, ct) => {
                            var currentContent = await File.ReadAllTextAsync(changelogPath, ct);
                            return new ReadFileResult(currentContent);
                        }),
                    new ReadChangedFilesTool(packagePath, _processHelper, _gitHelper),
                    new GetDateTool()
                ]
            }, ct);

            // Validate the fixed changelog one more time
            await File.WriteAllTextAsync(changelogPath, fixedChangelog.Contents, ct);
            var finalValidationResult = await ValidateChangelogCommonAsync(packagePath, ct);

            if (finalValidationResult.ExitCode == 0)
            {
                _logger.LogInformation("Changelog successfully fixed and validated");
                return new CLICheckResponse(0, "Changelog successfully fixed and validated.");
            }
            else
            {
                // Restore original content if validation still fails
                await File.WriteAllTextAsync(changelogPath, originalContent, ct);
                return new CLICheckResponse(1, 
                    $"Failed to fix changelog. Restored original content. Remaining issues: {finalValidationResult.CheckStatusDetails}",
                    "Changelog fix unsuccessful");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error in FixChangelogCommonAsync");
            return new CLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
    }

    // Data types for microagent operations
    public record ChangelogContents(string Contents);
    public record ChangelogValidationResult(IEnumerable<string> Issues, bool IsValid);
    public record ReadFileResult(string Content);
    public record GitChangedFilesResult(IEnumerable<string> ChangedFiles, IEnumerable<string> StagedFiles, IEnumerable<string> UntrackedFiles);

    private async Task<ChangelogValidationResult> ValidateChangelogContentWithFile(ChangelogContents contents, string changelogPath, string packagePath, CancellationToken ct)
    {
        // Save current content as backup
        var originalContent = await File.ReadAllTextAsync(changelogPath, ct);
        
        try
        {
            // Write the new content to the actual file
            await File.WriteAllTextAsync(changelogPath, contents.Contents, ct);
            
            // Run full validation on the actual file
            var validationResult = await ValidateChangelogCommonAsync(packagePath, ct);
            
            var issues = new List<string>();
            if (validationResult.ExitCode != 0)
            {
                issues.Add(validationResult.CheckStatusDetails);
            }

            return new ChangelogValidationResult(issues, validationResult.ExitCode == 0);
        }
        catch (Exception ex)
        {
            return new ChangelogValidationResult(new[] { $"Validation error: {ex.Message}" }, false);
        }
        finally
        {
            // Always restore the original content after validation
            await File.WriteAllTextAsync(changelogPath, originalContent, ct);
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

            return new CLICheckResponse(processResult);
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
