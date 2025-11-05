using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// .NET-specific implementation for package update operations.
/// </summary>
public class DotNetPackageUpdate : ILanguagePackageUpdate
{
    private readonly ILogger<DotNetPackageUpdate> _logger;
    private readonly IResponseFactory _responseFactory;

    public DotNetPackageUpdate(
        ILogger<DotNetPackageUpdate> logger,
        IResponseFactory responseFactory)
    {
        _logger = logger;
        _responseFactory = responseFactory;
    }

    /// <inheritdoc />
    public async Task<PackageOperationResponse> UpdateMetadataAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;
        throw new NotImplementedException(".NET metadata update is not yet implemented.");
    }

    /// <inheritdoc />
    public async Task<PackageOperationResponse> UpdateChangelogContentAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;
        throw new NotImplementedException(".NET changelog update is not yet implemented.");
    }

    /// <inheritdoc />
    public async Task<PackageOperationResponse> UpdateVersionAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;
        throw new NotImplementedException(".NET version update is not yet implemented.");
    }
}
