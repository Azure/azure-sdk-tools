// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Extensions;

/// <summary>
/// Extension methods for the PackageInfo class.
/// </summary>
public static class PackageInfoExtensions
{
    /// <summary>
    /// Creates a PackageInfo instance from a package path by resolving package information using language-specific helpers.
    /// </summary>
    /// <param name="packagePath">The absolute path to the package directory.</param>
    /// <param name="packageInfoResolver">The resolver for language-specific package info helpers.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A PackageInfo instance if successful, null otherwise.</returns>
    public static async Task<PackageInfo?> CreateFromPath(
        string packagePath,
        ILanguageSpecificResolver<IPackageInfoHelper> packageInfoResolver,
        ILogger logger,
        CancellationToken ct)
    {
        PackageInfo? packageInfo = null;
        try
        {
            var packageInfoHelper = await packageInfoResolver.Resolve(packagePath, ct);
            if (packageInfoHelper != null)
            {
                packageInfo = await packageInfoHelper.ResolvePackageInfo(packagePath, ct);
            }
            else
            {
                logger.LogError("No package info helper found for package path: {packagePath}", packagePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while parsing package path: {packagePath}", packagePath);
        }
        return packageInfo;
    }
}
