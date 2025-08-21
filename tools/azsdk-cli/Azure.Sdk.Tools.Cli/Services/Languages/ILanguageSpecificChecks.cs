using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Interface for language-specific check implementations.
/// </summary>
public interface ILanguageSpecificChecks
{
    /// <summary>
    /// Gets the language this implementation supports.
    /// </summary>
    string SupportedLanguage { get; }

    /// <summary>
    /// Determines if this implementation can handle the given package path.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <returns>True if this implementation can handle the package, false otherwise</returns>
    bool CanHandle(string packagePath);

    /// <summary>
    /// Analyzes dependencies for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the dependency analysis</returns>
    Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken cancellationToken = default);
}