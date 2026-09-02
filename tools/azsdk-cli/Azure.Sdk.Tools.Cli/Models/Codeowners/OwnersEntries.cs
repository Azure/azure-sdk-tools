// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// Implemented by models the YAML loader stamps with the line they were declared on, so validation
/// errors can name a file and line. Not a YAML key; it is populated during deserialization.
/// </summary>
public interface IYamlSourceLine
{
    int Line { get; set; }
}

/// <summary>
/// A <c>paths[]</c> entry. The same shape appears in <c>.github/owners.config.yaml</c> and in
/// <c>owners.yaml</c> fragments; the only difference is how <see cref="Path"/> is interpreted
/// (repo-absolute in the config, relative to the fragment directory in a fragment).
/// </summary>
public class OwnersPathEntry : IYamlSourceLine
{
    /// <inheritdoc/>
    [YamlIgnore]
    public int Line { get; set; }

    public string Path { get; set; } = string.Empty;

    public List<string> Owners { get; set; } = [];

    /// <summary>Required in fragments, optional in the owners config. Renders as <c># PRLabel:</c>.</summary>
    public List<string> PrLabels { get; set; } = [];

    /// <summary>Routes this entry to a section other than the one it was declared under.</summary>
    public string? Section { get; set; }

    /// <param name="pathExpression">The repo-absolute expression this entry renders as.</param>
    public CodeownersEntry ToCodeownersEntry(string pathExpression) => new()
    {
        PathExpression = pathExpression,
        SourceOwners = [.. Owners],
        PRLabels = [.. PrLabels],
    };
}

/// <summary>
/// A <c>label-owners[]</c> entry: a pathless block that names who owns a set of labels.
/// <see cref="Labels"/> is the key that label-owner blocks are unioned on.
/// </summary>
public class OwnersLabelOwnerEntry : IYamlSourceLine
{
    /// <inheritdoc/>
    [YamlIgnore]
    public int Line { get; set; }

    public List<string> Labels { get; set; } = [];

    /// <summary>Renders as <c># ServiceOwners:</c>. At least one of this and <see cref="AzureSdkOwners"/> is required.</summary>
    public List<string> ServiceOwners { get; set; } = [];

    /// <summary>Renders as <c># AzureSdkOwners:</c>.</summary>
    public List<string> AzureSdkOwners { get; set; } = [];

    /// <summary>Routes this entry to a section other than the one it was declared under.</summary>
    public string? Section { get; set; }

    public CodeownersEntry ToCodeownersEntry() => new()
    {
        ServiceLabels = [.. Labels],
        ServiceOwners = [.. ServiceOwners],
        AzureSdkOwners = [.. AzureSdkOwners],
    };
}
