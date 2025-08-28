using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Update;

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
    /// Analyzes dependencies for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the dependency analysis</returns>
    Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken cancellationToken = default);

    /// Creates the corresponding update language service for this language.
    /// Each language repo service knows how to create its own update service,
    /// eliminating the need for string-based discrimination in factories.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    /// <returns>The update language service for this language</returns>
    IUpdateLanguageService CreateUpdateService(IServiceProvider serviceProvider);
}
