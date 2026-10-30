// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

public static class GeneratedSdkMetadataHelper
{
    public static async Task PopulateApiVersionsAsync(PackageInfo packageInfo, ILogger logger, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        packageInfo.ApiVersions = [];

        var packagePath = Path.GetFullPath(packageInfo.PackagePath);
        var (directory, pattern) = packageInfo.Language switch
        {
            SdkLanguage.Java => (Path.Combine(packagePath, "src", "main", "resources", "META-INF"), "*_metadata.json"),
            SdkLanguage.JavaScript or SdkLanguage.DotNet => (packagePath, "metadata.json"),
            SdkLanguage.Python => (packagePath, "_metadata.json"),
            SdkLanguage.Go => (Path.Combine(packagePath, "testdata"), "_metadata.json"),
            _ => (string.Empty, string.Empty)
        };
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            var versions = new HashSet<PackageApiVersion>();
            foreach (var metadataFile in Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await using var stream = File.OpenRead(metadataFile);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    versions.UnionWith(ReadApiVersions(document.RootElement));
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

    public static HashSet<PackageApiVersion> ReadApiVersions(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Metadata must be a JSON object.");
        }

        var versions = new HashSet<PackageApiVersion>();
        if (root.TryGetProperty("apiVersions", out var apiVersions))
        {
            if (apiVersions.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("'apiVersions' must be a namespace-to-version object.");
            }

            foreach (var property in apiVersions.EnumerateObject())
            {
                AddVersion(property.Value, $"apiVersions.{property.Name}", property.Name);
            }
        }

        // All supported languages can use the singular field; prefer the namespace map when it has entries.
        if (versions.Count == 0 && root.TryGetProperty("apiVersion", out var apiVersion))
        {
            AddVersion(apiVersion, "apiVersion");
        }

        return versions;

        void AddVersion(JsonElement value, string field, string? namespaceName = null)
        {
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            {
                throw new JsonException($"'{field}' must be a non-empty API version string.");
            }

            versions.Add(new PackageApiVersion(value.GetString()!.Trim(), namespaceName));
        }
    }
}
