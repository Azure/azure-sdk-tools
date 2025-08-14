using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Configuration;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Interface for language-specific repository operations.
/// Each language must implement these commands, though their execution will differ
/// based on language-specific tools and conventions.
/// </summary>
public interface ILanguageRepoService
{
    /// <summary>
    /// Perform dependency analysis for the target language.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct);

    /// <summary>
    /// Format code for the target language.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<CLICheckResponse> FormatCodeAsync(string packagePath);

    /// <summary>
    /// Run linting/static analysis for the target language.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<CLICheckResponse> LintCodeAsync(string packagePath);

    /// <summary>
    /// Run tests for the target language.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<CLICheckResponse> RunTestsAsync(string packagePath);

    /// <summary>
    /// Validate changelog for the target language.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<CLICheckResponse> ValidateChangelogAsync(string packagePath);

    /// <summary>
    /// Validate README for the target language.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<CLICheckResponse> ValidateReadmeAsync(string packagePath);

    /// <summary>
    /// Check spelling in the target language package using cspell.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<CLICheckResponse> CheckSpellingAsync(string packagePath);
}

/// <summary>
/// Base implementation of language repository service.
/// Language-specific implementations should inherit from this class and override methods as needed.
/// </summary>
public class LanguageRepoService : ILanguageRepoService
{
    protected readonly IProcessHelper _processHelper;
    protected readonly IGitHelper _gitHelper;

    public LanguageRepoService(IProcessHelper processHelper, IGitHelper gitHelper)
    {
        _processHelper = processHelper;
        _gitHelper = gitHelper;
    }

    /// <summary>
    /// Creates a response from a ProcessResult.
    /// </summary>
    /// <param name="result">The process result</param>
    /// <returns>Success or failure response based on exit code</returns>
    protected static CLICheckResponse CreateResponseFromProcessResult(ProcessResult result)
    {
        return result.ExitCode == 0
            ? new CLICheckResponse(result.ExitCode, result.Output)
            : new CLICheckResponse(result.ExitCode, result.Output, "Process failed");
    }

    public virtual async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;
        return new CLICheckResponse(1, "", "AnalyzeDependencies not implemented for this language");
    }

    public virtual async Task<CLICheckResponse> FormatCodeAsync(string packagePath)
    {
        await Task.CompletedTask;
        return new CLICheckResponse(1, "", "FormatCode not implemented for this language");
    }

    public virtual async Task<CLICheckResponse> LintCodeAsync(string packagePath)
    {
        await Task.CompletedTask;
        return new CLICheckResponse(1, "", "LintCode not implemented for this language");
    }

    public virtual async Task<CLICheckResponse> RunTestsAsync(string packagePath)
    {
        await Task.CompletedTask;
        return new CLICheckResponse(1, "", "RunTests not implemented for this language");
    }

    public virtual async Task<CLICheckResponse> ValidateChangelogAsync(string packagePath)
    {
        return await ValidateChangelogCommonAsync(packagePath);
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
    protected async Task<CLICheckResponse> ValidateChangelogCommonAsync(string packagePath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            if (!Directory.Exists(packagePath))
            {
                return new CLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
            }

            // Find the SDK repository root by looking for common repository indicators
            var packageRepoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
            if (string.IsNullOrEmpty(packageRepoRoot))
            {
                return new CLICheckResponse(1, "", $"Could not find repository root from package path: {packagePath}");
            }

            // Construct the path to the PowerShell script in the SDK repository
            var scriptPath = Path.Combine(packageRepoRoot, "eng", "common", "scripts", "Verify-ChangeLog.ps1");
            
            if (!File.Exists(scriptPath))
            {
                return new CLICheckResponse(1, "", $"PowerShell script not found at expected location: {scriptPath}");
            }

            var command = "pwsh";
            var args = new[] { "-File", scriptPath, "-PackageName", Path.GetFileName(packagePath) };

            // Use a longer timeout for changelog validation - 5 minutes should be sufficient
            var timeoutMs = 300_000; // 5 minutes
            var processResult = _processHelper.RunProcess(command, args, packagePath, timeoutMs);
            stopwatch.Stop();

            if (processResult.ExitCode == 0)
            {
                return new CLICheckResponse(0, System.Text.Json.JsonSerializer.Serialize(new
                {
                    Message = "Changelog validation completed successfully",
                    Duration = (int)stopwatch.ElapsedMilliseconds,
                    Output = processResult.Output
                }));
            }
            else
            {
                return new CLICheckResponse(1, processResult.Output, $"Changelog validation failed with exit code {processResult.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
        finally
        {
            await Task.CompletedTask; // Make this async for consistency
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
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            if (!Directory.Exists(packagePath))
            {
                return new CLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
            }

            // Find the SDK repository root by looking for common repository indicators
            var packageRepoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
            if (string.IsNullOrEmpty(packageRepoRoot))
            {

                return new CLICheckResponse(1, "", $"Could not find repository root from package path: {packagePath}");
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

            // Use a longer timeout for README validation - 10 minutes should be sufficient as it may need to install doc-warden
            var timeoutMs = 600_000; // 10 minutes
            var processResult = _processHelper.RunProcess(command, args, packagePath, timeoutMs);
            stopwatch.Stop();

            if (processResult.ExitCode == 0)
            {
                return new CLICheckResponse(0, System.Text.Json.JsonSerializer.Serialize(new
                {
                    Message = "README validation completed successfully",
                    Duration = (int)stopwatch.ElapsedMilliseconds,
                    Output = processResult.Output
                }));
            }
            else
            {
                return new CLICheckResponse(1, processResult.Output, $"README validation failed with exit code {processResult.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
        finally
        {
            await Task.CompletedTask; // Make this async for consistency
        }
    }

    /// <summary>
    /// Common spelling check implementation that works for most Azure SDK languages.
    /// Uses the PowerShell script from eng/common/scripts/check-spelling-in-changed-files.ps1.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    protected async Task<CLICheckResponse> CheckSpellingCommonAsync(string packagePath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            if (!Directory.Exists(packagePath))
            {
                return new CLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
            }

            // Find the SDK repository root by looking for common repository indicators
            var packageRepoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
            if (string.IsNullOrEmpty(packageRepoRoot))
            {
                return new CLICheckResponse(1, "", $"Could not find repository root from package path: {packagePath}");
            }

            // Construct the path to the PowerShell script in the SDK repository
            var scriptPath = Path.Combine(packageRepoRoot, "eng", "common", "scripts", "check-spelling-in-changed-files.ps1");
            
            if (!File.Exists(scriptPath))
            {
                return new CLICheckResponse(1, "", $"PowerShell script not found at expected location: {scriptPath}");
            }

            // Construct the path to the cspell config file
            var cspellConfigPath = Path.Combine(packageRepoRoot, ".vscode", "cspell.json");
            
            if (!File.Exists(cspellConfigPath))
            {
                return new CLICheckResponse(1, "", $"Cspell config file not found at expected location: {cspellConfigPath}");
            }

            // Get the target branch (usually main or master) for the repository
            var targetBranch = _gitHelper.GetBranchName(packageRepoRoot);
            if (string.IsNullOrEmpty(targetBranch))
            {
                targetBranch = "main"; // fallback to main if we can't determine the default branch
            }

            var command = "pwsh";
            var args = new[] { 
                "-File", scriptPath, 
                "-CspellConfigPath", cspellConfigPath,
                "-SpellCheckRoot", packageRepoRoot,
                "-TargetCommittish", $"origin/{targetBranch}",
                "-ExitWithError"
            };

            // Use a longer timeout for spelling check - 10 minutes should be sufficient as it may need to install npm packages
            var timeoutMs = 600_000; // 10 minutes
            var processResult = _processHelper.RunProcess(command, args, packageRepoRoot, timeoutMs);
            stopwatch.Stop();

            if (processResult.ExitCode == 0)
            {
                return new CLICheckResponse(0, System.Text.Json.JsonSerializer.Serialize(new
                {
                    Message = "Spelling check completed successfully",
                    Duration = (int)stopwatch.ElapsedMilliseconds,
                    Output = processResult.Output
                }));
            }
            else
            {
                return new CLICheckResponse(1, processResult.Output, $"Spelling check failed with exit code {processResult.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
        }
        finally
        {
            await Task.CompletedTask; // Make this async for consistency
        }
    }
}
