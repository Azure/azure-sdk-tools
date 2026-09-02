// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Raised when an ownership YAML file cannot be turned into a model: unknown or non-canonical keys,
/// malformed YAML, or an unsupported schema version. Structural rules that need more than one file
/// (containment, duplicates, section binding) are validated later and reported as CFG-* errors.
/// </summary>
public class OwnersYamlException(string message) : Exception(message);

/// <summary>
/// Deserializes <c>.github/owners.config.yaml</c> and <c>owners.yaml</c> fragments.
/// Takes file content rather than a path so callers own the I/O and the loader stays testable.
/// </summary>
public static class OwnersYamlLoader
{
    /// <summary>
    /// Non-canonical spellings we expect authors to reach for, mapped to the key they meant.
    /// The unified vocabulary is plural throughout: <c>pr-labels</c>, never <c>pr-label</c>.
    /// </summary>
    private static readonly Dictionary<string, string> KeySuggestions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pr-label"] = "pr-labels",
        ["owner"] = "owners",
        ["label"] = "labels",
        ["service-owner"] = "service-owners",
        ["azure-sdk-owner"] = "azure-sdk-owners",
        ["label-owner"] = "label-owners",
    };

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .WithNodeDeserializer(
            inner => new LineTrackingNodeDeserializer(inner),
            s => s.InsteadOf<ObjectNodeDeserializer>())
        .Build();

    public static OwnersConfig LoadConfig(string yaml, string filePath)
    {
        var config = Deserialize<OwnersConfig>(yaml, filePath);
        RequireSupportedVersion(config.Version, filePath);
        return config;
    }

    /// <param name="filePath">
    /// Repo-relative path of the fragment, e.g. <c>sdk/ai/owners.yaml</c>. Its directory becomes the
    /// base that the fragment's relative path expressions resolve against.
    /// </param>
    public static OwnersFragment LoadFragment(string yaml, string filePath)
    {
        var fragment = Deserialize<OwnersFragment>(yaml, filePath);
        RequireSupportedVersion(fragment.Version, filePath);

        var normalizedPath = filePath.Replace('\\', '/');
        var lastSlash = normalizedPath.LastIndexOf('/');

        fragment.FilePath = normalizedPath;
        fragment.Directory = lastSlash > 0 ? normalizedPath[..lastSlash] : string.Empty;

        return fragment;
    }

    private static T Deserialize<T>(string yaml, string filePath) where T : new()
    {
        try
        {
            // An empty or comment-only file deserializes to null rather than throwing.
            return Deserializer.Deserialize<T>(yaml)
                ?? throw new OwnersYamlException($"{filePath}: file is empty.");
        }
        catch (YamlException ex)
        {
            throw new OwnersYamlException(
                $"{filePath}:{ex.Start.Line}: {ex.Message}{DescribeSuggestion(ex.Message)}");
        }
    }

    private static void RequireSupportedVersion(int version, string filePath)
    {
        if (version != OwnersConfig.SupportedVersion)
        {
            throw new OwnersYamlException(
                $"{filePath}: unsupported schema version '{version}'. Expected {OwnersConfig.SupportedVersion}.");
        }
    }

    /// <summary>
    /// Appends "Did you mean 'x'?" when the deserializer's message names a key we recognize as a
    /// near-miss. Best effort: the suggestion depends on YamlDotNet's message wording, so losing it
    /// degrades the error rather than breaking it.
    /// </summary>
    private static string DescribeSuggestion(string deserializerMessage)
    {
        foreach (var (nonCanonical, canonical) in KeySuggestions)
        {
            if (deserializerMessage.Contains($"'{nonCanonical}'", StringComparison.OrdinalIgnoreCase))
            {
                return $" Did you mean '{canonical}'?";
            }
        }

        return string.Empty;
    }
}
