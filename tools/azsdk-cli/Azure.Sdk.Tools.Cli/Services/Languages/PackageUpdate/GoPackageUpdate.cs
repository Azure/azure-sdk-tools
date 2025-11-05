using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Go-specific implementation for package update operations.
/// </summary>
public class GoPackageUpdate : ILanguagePackageUpdate
{
    private readonly ILogger<GoPackageUpdate> _logger;
    private readonly IResponseFactory _responseFactory;

    public GoPackageUpdate(
        ILogger<GoPackageUpdate> logger,
        IResponseFactory responseFactory)
    {
        _logger = logger;
        _responseFactory = responseFactory;
    }

    /// <inheritdoc />
    public async Task<PackageOperationResponse> UpdateMetadataAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;
        return _responseFactory.CreateSuccessResponse("noop for language-specific update metadata step", null);
    }

    /// <inheritdoc />
    public async Task<PackageOperationResponse> UpdateChangelogContentAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;
        return _responseFactory.CreateSuccessResponse("noop for language-specific update changelog content step", null);
    }

    /// <inheritdoc />
    public async Task<PackageOperationResponse> UpdateVersionAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;
        return _responseFactory.CreateSuccessResponse("noop for language-specific update version step", null);
    }
}
