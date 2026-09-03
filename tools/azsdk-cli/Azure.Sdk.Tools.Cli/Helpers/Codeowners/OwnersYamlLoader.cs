// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
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

        foreach (var section in config.Sections)
        {
            Normalize(section.Paths, section.LabelOwners);
        }

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

        Normalize(fragment.Paths, fragment.LabelOwners);

        return fragment;
    }

    /// <summary>
    /// Canonicalizes owner and label lists so every consumer downstream compares like with like.
    /// This is the only place either is normalized: rendering, the union key, duplicate detection,
    /// the audit, and check-package all read the result rather than re-deriving it, which is what
    /// keeps them from disagreeing about whether <c>@alice</c> and <c>alice</c>, or <c>%AI</c> and
    /// <c>ai</c>, are the same thing.
    /// </summary>
    private static void Normalize(List<OwnersPathEntry> paths, List<OwnersLabelOwnerEntry> labelOwners)
    {
        foreach (var path in paths)
        {
            path.Owners = CanonicalizeOwners(path.Owners);
            path.PrLabels = CanonicalizeLabels(path.PrLabels);
        }

        foreach (var labelOwner in labelOwners)
        {
            labelOwner.Labels = CanonicalizeLabels(labelOwner.Labels);
            labelOwner.ServiceOwners = CanonicalizeOwners(labelOwner.ServiceOwners);
            labelOwner.AzureSdkOwners = CanonicalizeOwners(labelOwner.AzureSdkOwners);
        }
    }

    /// <summary>
    /// Strips the optional leading '@' so aliases match the cache keys and each other. GitHub
    /// usernames and team slugs are case-insensitive, so casing only decides which spelling renders.
    /// </summary>
    private static List<string> CanonicalizeOwners(List<string> owners) =>
        DistinctPreservingOrder(owners.Select(owner => owner.Trim().TrimStart('@').Trim()));

    /// <summary>
    /// Strips the optional leading '%' moniker prefix. GitHub label names are unique
    /// case-insensitively, so <c>[AI, ai]</c> names one label and must collapse to one.
    /// </summary>
    private static List<string> CanonicalizeLabels(List<string> labels) =>
        DistinctPreservingOrder(labels.Select(CodeownersEntrySorter.NormalizeLabel));

    /// <summary>Drops blanks and case-insensitive repeats, keeping the first spelling of each.</summary>
    private static List<string> DistinctPreservingOrder(IEnumerable<string> values) =>
        [.. values.Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];

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
