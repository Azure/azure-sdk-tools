using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Interface for language-specific check implementations.
/// </summary>
public interface ILanguageSpecificChecks
{
    /// <summary>
    /// Analyzes dependencies for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix dependency issues</param>
    /// <returns>Result of the dependency analysis</returns>
    Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken cancellationToken, bool fixCheckErrors = false)
    {
        return Task.FromResult(new CLICheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Updates code snippets in the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix snippet issues</param>
    /// <returns>Result of the snippet update operation</returns>
    Task<CLICheckResponse> UpdateSnippetsAsync(string packagePath, CancellationToken cancellationToken, bool fixCheckErrors = false)
    {
        return Task.FromResult(new CLICheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Lints code in the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="fixCheckErrors">Whether to automatically fix linting issues</param>
    /// <returns>Result of the code linting operation</returns>
    Task<CLICheckResponse> LintCodeAsync(string packagePath, CancellationToken cancellationToken, bool fixCheckErrors = false)
    {
        return Task.FromResult(new CLICheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Formats code in the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="fixCheckErrors">Whether to automatically apply code formatting</param>
    /// <returns>Result of the code formatting operation</returns>
    Task<CLICheckResponse> FormatCodeAsync(string packagePath, CancellationToken cancellationToken, bool fixCheckErrors = false)
    {
        return Task.FromResult(new CLICheckResponse(1, "", "Not implemented for this language."));
    }
}