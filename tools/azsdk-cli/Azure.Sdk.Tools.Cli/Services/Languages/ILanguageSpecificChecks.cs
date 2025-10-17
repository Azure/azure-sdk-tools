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
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix dependency issues</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the dependency analysis</returns>
    Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CLICheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Updates code snippets in the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix snippet issues</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the snippet update operation</returns>
    Task<CLICheckResponse> UpdateSnippetsAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CLICheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Lints code in the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to automatically fix linting issues</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the code linting operation</returns>
    Task<CLICheckResponse> LintCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CLICheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Formats code in the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to automatically apply code formatting</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the code formatting operation</returns>
    Task<CLICheckResponse> FormatCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CLICheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Checks AOT compatibility for the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the AOT compatibility check</returns>
    Task<CLICheckResponse> CheckAotCompatAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CLICheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Checks generated code for the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the generated code check</returns>
    Task<CLICheckResponse> CheckGeneratedCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CLICheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Gets the SDK package name for the specified package using language-specific rules.
    /// </summary>
    /// <param name="repo">Repository root path</param>
    /// <param name="packagePath">Package path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>SDK package name</returns>
    Task<string> GetSDKPackageName(string repo, string packagePath, CancellationToken cancellationToken = default)
    {
        // Default implementation: use the directory name as the package path
        return Task.FromResult(Path.GetFileName(packagePath));
    }
}
