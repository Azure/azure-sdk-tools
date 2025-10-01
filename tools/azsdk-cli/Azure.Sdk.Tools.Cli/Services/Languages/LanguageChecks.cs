using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Prompts;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;

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
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix spelling issues where supported by cspell</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the spelling check</returns>
    Task<CLICheckResponse> CheckSpellingAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default);

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

    public virtual async Task<CLICheckResponse> ValidateReadmeAsync(string packagePath, CancellationToken ct = default)
    {
        return await ValidateReadmeCommonAsync(packagePath, ct);
    }

    public virtual async Task<CLICheckResponse> CheckSpellingAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        return await CheckSpellingCommonAsync(packagePath, fixCheckErrors, ct);
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
                    return new CLICheckResponse(processResult.ExitCode, processResult.Output, $"Spelling fix microagent failed: {ex.Message}");
                }
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

    /// <summary>
    /// Result of the spelling fix microagent operation.
    /// </summary>
    public record SpellingFixResult(
        [property: Description("Summary of the operations performed")] string Summary,
        [property: Description("Detailed information about the fixes applied")] string Details
    );

    /// <summary>
    /// Input for reading file content tool.
    /// </summary>
    public record ReadFileToolInput(
        [property: Description("Path to the file to read")] string FilePath
    );

    /// <summary>
    /// Output for reading file content tool.
    /// </summary>
    public record ReadFileToolOutput(
        [property: Description("Content of the file")] string Content
    );

    /// <summary>
    /// Input for writing file content tool.
    /// </summary>
    public record WriteFileToolInput(
        [property: Description("Path to the file to write")] string FilePath,
        [property: Description("Content to write to the file")] string Content
    );

    /// <summary>
    /// Output for writing file content tool.
    /// </summary>
    public record WriteFileToolOutput(
        [property: Description("Success message")] string Message
    );

    /// <summary>
    /// Input for updating cspell.json words list.
    /// </summary>
    public record UpdateCspellWordsInput(
        [property: Description("List of words to add to the cspell.json words list")] List<string> Words
    );

    /// <summary>
    /// Output for updating cspell.json words list.
    /// </summary>
    public record UpdateCspellWordsOutput(
        [property: Description("Success message with number of words added")] string Message
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
        var prompt = $"""
            You are an automated spelling assistant for an Azure SDK repository. You will analyze cspell lint output and automatically fix spelling issues.

            Your tasks:
            1. Read the cspell output provided and analyze each reported spelling issue
            2. For each issue, decide whether to:
               - Fix the typo by correcting the spelling in the source file
               - Add the word to cspell.json if it's a legitimate technical term, product name, or proper noun
            3. Apply the fixes by reading files, making corrections, and writing them back
            4. Update the cspell.json file to add legitimate words to the 'words' array. DO NOT remove any words from the cspell.json file, only add words on as needed.

            Guidelines for decision making:
            - Fix obvious typos in comments, documentation, and non-code text
            - Add technical terms, API names, product names, acronyms, and proper nouns to cspell.json
            - Preserve exact casing and formatting when making corrections

            cspell lint output to analyze:
            {cspellOutput}

            Complete all fixes and return a summary of the operations performed.
            """;

        var agent = new Microagent<SpellingFixResult>
        {
            Instructions = prompt,
            MaxToolCalls = 10,
            Model = "gpt-4",
            Tools = new IAgentTool[]
            {
                AgentTool<ReadFileToolInput, ReadFileToolOutput>.FromFunc(
                    "read_file", 
                    "Read the contents of a file", 
                    async (input, ct) =>
                    {
                        var fullPath = Path.Combine(repoRoot, input.FilePath);
                        if (!File.Exists(fullPath))
                        {
                            throw new FileNotFoundException($"File not found: {input.FilePath}");
                        }
                        var content = await File.ReadAllTextAsync(fullPath, ct);
                        return new ReadFileToolOutput(content);
                    }),

                AgentTool<WriteFileToolInput, WriteFileToolOutput>.FromFunc(
                    "write_file", 
                    "Write content to a file", 
                    async (input, ct) =>
                    {
                        var fullPath = Path.Combine(repoRoot, input.FilePath);
                        var directory = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        await File.WriteAllTextAsync(fullPath, input.Content, ct);
                        return new WriteFileToolOutput($"Successfully wrote to {input.FilePath}");
                    }),

                AgentTool<UpdateCspellWordsInput, UpdateCspellWordsOutput>.FromFunc(
                    "update_cspell_words", 
                    "Add words to the cspell.json words list", 
                    async (input, ct) =>
                    {
                        var cspellPath = Path.Combine(repoRoot, ".vscode", "cspell.json");
                        if (!File.Exists(cspellPath))
                        {
                            throw new FileNotFoundException($"cspell.json not found at {cspellPath}");
                        }

                        var cspellContent = await File.ReadAllTextAsync(cspellPath, ct);
                        var cspellConfig = JsonSerializer.Deserialize<JsonDocument>(cspellContent);
                        
                        // Parse as mutable dictionary
                        var configDict = JsonSerializer.Deserialize<Dictionary<string, object>>(cspellContent);
                        
                        // Get existing words or create new array
                        var existingWords = new List<string>();
                        if (configDict != null && configDict.ContainsKey("words"))
                        {
                            var wordsElement = (JsonElement)configDict["words"];
                            if (wordsElement.ValueKind == JsonValueKind.Array)
                            {
                                existingWords = wordsElement.EnumerateArray()
                                    .Where(e => e.ValueKind == JsonValueKind.String)
                                    .Select(e => e.GetString()!)
                                    .ToList();
                            }
                        }

                        // Add new words that don't already exist
                        var wordsToAdd = input.Words.Where(w => !existingWords.Contains(w, StringComparer.OrdinalIgnoreCase)).ToList();
                        existingWords.AddRange(wordsToAdd);
                        existingWords.Sort(StringComparer.OrdinalIgnoreCase);

                        // Update the config
                        if (configDict == null) 
                        {
                            configDict = new Dictionary<string, object>();
                        }
                        configDict["words"] = existingWords;

                        // Write back to file with formatting
                        var options = new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        };
                        var updatedContent = JsonSerializer.Serialize(configDict, options);
                        await File.WriteAllTextAsync(cspellPath, updatedContent, ct);

                        return new UpdateCspellWordsOutput($"Successfully added {wordsToAdd.Count} words to cspell.json");
                    })
            }
        };

        return await _microagentHostService.RunAgentToCompletion(agent, ct);
    }
}
