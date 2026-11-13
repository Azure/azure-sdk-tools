// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models;
using YamlDotNet.RepresentationModel;

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

    public static TypeSpecLocation ParseTypeSpecLocation(string text)
    {
        var metadata = ParseYaml(text);
        return new(Scalar(metadata, "directory"), Scalar(metadata, "commit"), Scalar(metadata, "repo"));
    }

    internal static string ProviderNamespace(string main)
    {
        main = Regex.Replace(Regex.Replace(main, @"/\*[\s\S]*?\*/", ""), @"//[^\n]*", "");
        var explicitNamespace = Regex.Match(main, "@armProviderNamespace\\(\\s*\"([\\w.-]+)\"\\s*\\)").Groups[1].Value;
        return explicitNamespace.Length > 0 ? explicitNamespace :
            Regex.Match(main, @"@armProviderNamespace\b(?!\s*\()[\s\S]*?\bnamespace\s+([\w.]+)\s*[;{]").Groups[1].Value;
    }

    internal static bool IsCommitSha(string value) => Regex.IsMatch(value, @"\A[a-f0-9]{40}\z", RegexOptions.IgnoreCase);

    internal static YamlMappingNode ParseYaml(string text)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        return stream.Documents.Count == 1 && stream.Documents[0].RootNode is YamlMappingNode map ? map :
            throw new FormatException("Expected one YAML mapping.");
    }

    internal static string Scalar(YamlMappingNode mapping, string name)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode(name), out var value) || value is not YamlScalarNode scalar || scalar.Value is null)
        {
            return "";
        }
        // Preserve YAML types: an implicit boolean or number is not metadata text.
        if (scalar.Style is YamlDotNet.Core.ScalarStyle.Plain or YamlDotNet.Core.ScalarStyle.Any &&
            Regex.IsMatch(scalar.Value, @"\A(?:null|true|false|~|[-+]?\d+(?:\.\d+)?)\z", RegexOptions.IgnoreCase))
        {
            return "";
        }
        return scalar.Value;
    }

}
