using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

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
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<CLICheckResponse> AnalyzeDependenciesAsync(CancellationToken ct);

    /// <summary>
    /// Format code for the target language.
    /// </summary>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<CLICheckResponse> FormatCodeAsync();

    /// <summary>
    /// Run linting/static analysis for the target language.
    /// </summary>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<CLICheckResponse> LintCodeAsync();

    /// <summary>
    /// Run tests for the target language.
    /// </summary>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<CLICheckResponse> RunTestsAsync();
}

/// <summary>
/// Base implementation of language repository service.
/// Language-specific implementations should inherit from this class and override methods as needed.
/// </summary>
public class LanguageRepoService : ILanguageRepoService
{
    protected readonly string _packagePath;
    protected readonly IProcessHelper _processHelper;

    public LanguageRepoService(string packagePath, IProcessHelper processHelper)
    {
        if (string.IsNullOrEmpty(packagePath))
            throw new ArgumentException("Package path cannot be null or empty", nameof(packagePath));
        if (processHelper == null)
            throw new ArgumentNullException(nameof(processHelper));
            
        _packagePath = packagePath;
        _processHelper = processHelper;
    }

    /// <summary>
    /// Creates a response from a ProcessResult.
    /// </summary>
    /// <param name="result">The process result</param>
    /// <returns>Success or failure response based on exit code</returns>
    protected static CLICheckResponse CreateResponseFromProcessResult(ProcessResult result)
    {
        return result.ExitCode == 0
            ? new SuccessCLICheckResponse(result.ExitCode, result.Output)
            : new FailureCLICheckResponse(result.ExitCode, result.Output, "Process failed");
    }

    public virtual async Task<CLICheckResponse> AnalyzeDependenciesAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        return new FailureCLICheckResponse(1, "AnalyzeDependencies not implemented for this language");
    }

    public virtual async Task<CLICheckResponse> FormatCodeAsync()
    {
        await Task.CompletedTask;
        return new FailureCLICheckResponse(1, "FormatCode not implemented for this language");
    }

    public virtual async Task<CLICheckResponse> LintCodeAsync()
    {
        await Task.CompletedTask;
        return new FailureCLICheckResponse(1, "LintCode not implemented for this language");
    }

    public virtual async Task<CLICheckResponse> RunTestsAsync()
    {
        await Task.CompletedTask;
        return new FailureCLICheckResponse(1, "RunTests not implemented for this language");
    }
}
