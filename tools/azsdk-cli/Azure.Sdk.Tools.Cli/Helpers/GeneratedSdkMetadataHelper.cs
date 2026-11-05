// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

public static class GeneratedSdkMetadataHelper
{
    public static (string Directory, string Pattern) GetMetadataFileLocation(SdkLanguage language) => language switch
    {
        SdkLanguage.Java => ("src/main/resources/META-INF", "*_metadata.json"),
        SdkLanguage.JavaScript or SdkLanguage.DotNet => ("", "metadata.json"),
        SdkLanguage.Python => ("", "_metadata.json"),
        SdkLanguage.Go => ("testdata", "_metadata.json"),
        _ => ("", "")
    };

    public static async Task PopulateApiVersionsAsync(PackageInfo packageInfo, ILogger logger, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        packageInfo.ApiVersions = [];

        var packagePath = Path.GetFullPath(packageInfo.PackagePath);
        var location = GetMetadataFileLocation(packageInfo.Language);
        if (string.IsNullOrEmpty(location.Pattern))
        {
            return;
        }
        var directory = Path.Combine(packagePath, location.Directory.Replace('/', Path.DirectorySeparatorChar));

        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            var versions = new HashSet<PackageApiVersion>();
            foreach (var metadataFile in Directory.GetFiles(directory, location.Pattern, SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await using var stream = File.OpenRead(metadataFile);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    versions.UnionWith(PackageInfoHelper.ReadApiVersions(document.RootElement));
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
                {
                    throw new IOException($"Unable to read SDK API versions from '{metadataFile}': {ex.Message}", ex);
                }
            }

            packageInfo.ApiVersions = [.. versions
                .OrderBy(value => value.Namespace, StringComparer.Ordinal)
                .ThenBy(value => value.Version, StringComparer.Ordinal)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Unable to discover API versions for package at {PackagePath}", packagePath);
        }
    }

}
