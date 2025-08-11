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
}

/// <summary>
/// Base implementation of language repository service.
/// Language-specific implementations should inherit from this class and override methods as needed.
/// </summary>
public class LanguageRepoService : ILanguageRepoService
{
    protected readonly IProcessHelper _processHelper;

    public LanguageRepoService(IProcessHelper processHelper)
    {
        _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
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

    public virtual async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;
        return new FailureCLICheckResponse(1, "AnalyzeDependencies not implemented for this language");
    }

    public virtual async Task<CLICheckResponse> FormatCodeAsync(string packagePath)
    {
        await Task.CompletedTask;
        return new FailureCLICheckResponse(1, "FormatCode not implemented for this language");
    }

    public virtual async Task<CLICheckResponse> LintCodeAsync(string packagePath)
    {
        await Task.CompletedTask;
        return new FailureCLICheckResponse(1, "LintCode not implemented for this language");
    }

    public virtual async Task<CLICheckResponse> RunTestsAsync(string packagePath)
    {
        await Task.CompletedTask;
        return new FailureCLICheckResponse(1, "RunTests not implemented for this language");
    }
}
