using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// .NET implementation of package update operations.
/// </summary>
public class DotNetPackageUpdate : ILanguagePackageUpdate
{
    private readonly ILogger<DotNetPackageUpdate> _logger;

    public DotNetPackageUpdate(ILogger<DotNetPackageUpdate> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PackageOperationResponse> UpdateMetadataAsync(string packagePath, CancellationToken ct)
    {
        _logger.LogInformation("No built-in metadata update implementation for .NET packages at path: {PackagePath}", packagePath);
        return await Task.FromResult(PackageOperationResponse.CreateSuccess("No metadata updates performed for .NET package.", null));
    }

    /// <inheritdoc />
    public async Task<PackageOperationResponse> UpdateChangelogContentAsync(string packagePath, CancellationToken ct)
    {
        _logger.LogInformation("No built-in changelog content update implementation for .NET packages at path: {PackagePath}", packagePath);
        return await Task.FromResult(PackageOperationResponse.CreateSuccess("No changelog content updates performed for .NET package.", null));
    }

    /// <inheritdoc />
    public async Task<PackageOperationResponse> UpdateVersionAsync(string packagePath, CancellationToken ct)
    {
        _logger.LogInformation("No built-in version update implementation for .NET packages at path: {PackagePath}", packagePath);
        return await Task.FromResult(PackageOperationResponse.CreateSuccess("No version updates performed for .NET package.", null));
    }
}
