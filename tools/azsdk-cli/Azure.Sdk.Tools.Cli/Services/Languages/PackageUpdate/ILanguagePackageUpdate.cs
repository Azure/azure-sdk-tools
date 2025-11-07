using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Service interface for language-specific package operations including metadata updates, changelog content updates, and version updates.
/// </summary>
public interface ILanguagePackageUpdate
{
    /// <summary>
    /// Updates the package metadata content for a specified package.
    /// </summary>
    /// <param name="packagePath">The absolute path to the package directory.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A response indicating the result of the metadata update operation.</returns>
    Task<PackageOperationResponse> UpdateMetadataAsync(string packagePath, CancellationToken ct);

    /// <summary>
    /// Updates the changelog content for a specified package.
    /// </summary>
    /// <param name="packagePath">The absolute path to the package directory.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A response indicating the result of the changelog update operation.</returns>
    Task<PackageOperationResponse> UpdateChangelogContentAsync(string packagePath, CancellationToken ct);

    /// <summary>
    /// Updates the version for a specified package.
    /// </summary>
    /// <param name="packagePath">The absolute path to the package directory.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A response indicating the result of the version update operation.</returns>
    Task<PackageOperationResponse> UpdateVersionAsync(string packagePath, CancellationToken ct);
}
