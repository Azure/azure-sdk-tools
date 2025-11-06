using Azure.Sdk.Tools.Cli.Models.Responses.Package;

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
    Task<PackageCheckResponse> AnalyzeDependencies(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageCheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Validates the README for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix README issues</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the README validation</returns>
    Task<PackageCheckResponse> ValidateReadme(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageCheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Checks spelling in the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix spelling issues where supported by cspell</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the spelling check</returns>
    Task<PackageCheckResponse> CheckSpelling(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageCheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Updates code snippets in the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix snippet issues</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the snippet update operation</returns>
    Task<PackageCheckResponse> UpdateSnippets(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageCheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Lints code in the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to automatically fix linting issues</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the code linting operation</returns>
    Task<PackageCheckResponse> LintCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageCheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Formats code in the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to automatically apply code formatting</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the code formatting operation</returns>
    Task<PackageCheckResponse> FormatCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageCheckResponse(1, "", "Not implemented for this language."));
    }

    /// Validate samples for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the sample validation</returns>
    Task<PackageCheckResponse> ValidateSamples(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageCheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Checks AOT compatibility for the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the AOT compatibility check</returns>
    Task<PackageCheckResponse> CheckAotCompat(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageCheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Checks generated code for the specific package using language-specific tools.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the generated code check</returns>
    Task<PackageCheckResponse> CheckGeneratedCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageCheckResponse(1, "", "Not implemented for this language."));
    }

    /// <summary>
    /// Validates the changelog for the specific package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory</param>
    /// <param name="fixCheckErrors">Whether to attempt to automatically fix changelog issues</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the changelog validation</returns>
    Task<PackageCheckResponse> ValidateChangelog(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageCheckResponse(1, "", "Not implemented for this language."));
    }
}
