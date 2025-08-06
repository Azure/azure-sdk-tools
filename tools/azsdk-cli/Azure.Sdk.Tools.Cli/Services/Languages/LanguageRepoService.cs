using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;

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
    Task<ICLICheckResponse> AnalyzeDependenciesAsync();

    /// <summary>
    /// Format code for the target language.
    /// </summary>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<ICLICheckResponse> FormatCodeAsync();

    /// <summary>
    /// Run linting/static analysis for the target language.
    /// </summary>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<ICLICheckResponse> LintCodeAsync();

    /// <summary>
    /// Run tests for the target language.
    /// </summary>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<ICLICheckResponse> RunTestsAsync();

    /// <summary>
    /// Build/compile the project for the target language.
    /// </summary>
    /// <returns>CLI check response containing success/failure status and response message</returns>
    Task<ICLICheckResponse> BuildProjectAsync();
}

/// <summary>
/// Base implementation of language repository service.
/// Language-specific implementations should inherit from this class and override methods as needed.
/// </summary>
public class LanguageRepoService : ILanguageRepoService
{
    protected readonly string _repositoryPath;

    public LanguageRepoService(string repositoryPath)
    {
        _repositoryPath = repositoryPath ?? throw new ArgumentNullException(nameof(repositoryPath));
    }

    /// <summary>
    /// Creates a success response.
    /// </summary>
    /// <param name="message">Success message</param>
    /// <param name="exitCode">Exit code (default: 0 for success)</param>
    /// <returns>SuccessCLICheckResponse with success status and response</returns>
    protected static SuccessCLICheckResponse CreateSuccessResponse(string message, int exitCode = 0)
    {
        return new SuccessCLICheckResponse(exitCode, message);
    }

    /// <summary>
    /// Creates a failure response.
    /// </summary>
    /// <param name="message">Failure message</param>
    /// <param name="exitCode">Exit code (default: 1 for failure)</param>
    /// <param name="error">Additional error details</param>
    /// <returns>FailureCLICheckResponse with failure status and response</returns>
    protected static FailureCLICheckResponse CreateFailureResponse(string message, int exitCode = 1, string error = "")
    {
        return new FailureCLICheckResponse(exitCode, message, error);
    }

    /// <summary>
    /// Creates a cookbook response.
    /// </summary>
    /// <param name="cookbookReference">Reference to cookbook or documentation</param>
    /// <param name="message">Additional response message</param>
    /// <param name="exitCode">Exit code (default: 0 for success)</param>
    /// <returns>CookbookCLICheckResponse with cookbook reference and response</returns>
    protected static CookbookCLICheckResponse CreateCookbookResponse(string cookbookReference, string message, int exitCode = 0)
    {
        return new CookbookCLICheckResponse(exitCode, message, cookbookReference);
    }

    public virtual async Task<ICLICheckResponse> AnalyzeDependenciesAsync()
    {
        await Task.CompletedTask;
        return CreateFailureResponse("AnalyzeDependencies not implemented for this language");
    }

    public virtual async Task<ICLICheckResponse> FormatCodeAsync()
    {
        await Task.CompletedTask;
        return CreateFailureResponse("FormatCode not implemented for this language");
    }

    public virtual async Task<ICLICheckResponse> LintCodeAsync()
    {
        await Task.CompletedTask;
        return CreateFailureResponse("LintCode not implemented for this language");
    }

    public virtual async Task<ICLICheckResponse> RunTestsAsync()
    {
        await Task.CompletedTask;
        return CreateFailureResponse("RunTests not implemented for this language");
    }

    public virtual async Task<ICLICheckResponse> BuildProjectAsync()
    {
        await Task.CompletedTask;
        return CreateFailureResponse("BuildProject not implemented for this language");
    }
}