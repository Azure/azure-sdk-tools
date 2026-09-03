// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Removes an owner alias from an ownership YAML file by editing the parsed model and writing it
/// back out.
/// <para>
/// The rewrite is whole-file: YAML comments and the author's formatting choices are not preserved,
/// and owners and labels come back in the canonical form the loader normalizes them to. Everything
/// the schema carries survives, because the model is the schema.
/// </para>
/// </summary>
public static class OwnersYamlEditor
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults | DefaultValuesHandling.OmitEmptyCollections)
        .WithQuotingNecessaryStrings()
        .Build();

    /// <summary>
    /// Returns the file content with <paramref name="alias"/> removed from every owner list, or null
    /// if the file does not parse or the alias is not in it.
    /// </summary>
    public static string? RemoveOwner(string yaml, string filePath, string alias)
    {
        OwnersFragment fragment;
        try
        {
            fragment = OwnersYamlLoader.LoadFragment(yaml, filePath);
        }
        catch (OwnersYamlException)
        {
            return null;
        }

        return RemoveOwnerFromModel(fragment, alias) ? Serialize(fragment) : null;
    }

    /// <summary>Returns true when the alias was present in at least one owner list.</summary>
    private static bool RemoveOwnerFromModel(OwnersFragment fragment, string alias)
    {
        // The loader has already stripped any leading '@', so a plain comparison is enough.
        bool Matches(string owner) => owner.Equals(alias, StringComparison.OrdinalIgnoreCase);

        var removed = 0;

        foreach (var path in fragment.Paths)
        {
            removed += path.Owners.RemoveAll(Matches);
        }

        foreach (var labelOwner in fragment.LabelOwners)
        {
            removed += labelOwner.ServiceOwners.RemoveAll(Matches);
            removed += labelOwner.AzureSdkOwners.RemoveAll(Matches);
        }

        return removed > 0;
    }

    /// <summary>
    /// YamlDotNet emits the platform's line ending; ownership files are '\n' everywhere so that a
    /// rendered CODEOWNERS does not depend on which machine last edited its inputs.
    /// </summary>
    private static string Serialize(OwnersFragment fragment) =>
        Serializer.Serialize(fragment).ReplaceLineEndings("\n");
}
