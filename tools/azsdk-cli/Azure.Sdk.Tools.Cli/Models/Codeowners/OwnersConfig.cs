// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// The repo-level <c>.github/owners.config.yaml</c>: the ordered list of CODEOWNERS sections plus
/// the settings that govern how fragments are discovered and rendered.
/// </summary>
public class OwnersConfig
{
    /// <summary>Schema version. Only <see cref="SupportedVersion"/> is accepted.</summary>
    public int Version { get; set; }

    public OwnersConfigSettings Configs { get; set; } = new();

    /// <summary>Ordered. Render order equals declaration order.</summary>
    public List<OwnersSection> Sections { get; set; } = [];

    public const int SupportedVersion = 1;
}

public class OwnersConfigSettings
{
    /// <summary>
    /// Repo-root-relative globs naming the complete set of locations a fragment may occupy.
    /// An <c>owners.yaml</c> found anywhere else is a validation error, so ownership cannot be
    /// hidden in an unexpected location.
    /// </summary>
    public List<string> AllowedOwnerYamlPaths { get; set; } = [];

    /// <summary>Section that receives fragment entries with no explicit section.</summary>
    public string DefaultSection { get; set; } = string.Empty;

    public string Output { get; set; } = ".github/CODEOWNERS";

    /// <summary>
    /// Minimum individual (non-team) owners on a fragment path entry. Reported by the audit and by
    /// check-package; never fails generation.
    /// </summary>
    public int MinimumPathOwners { get; set; } = 2;

    /// <summary>
    /// Minimum individual (non-team) service owners on a label-owner block, evaluated after union.
    /// Reported by the audit and by check-package; never fails generation.
    /// </summary>
    public int MinimumLabelOwners { get; set; } = 2;
}

/// <summary>
/// One CODEOWNERS section. A section may declare static entries, accept fragment entries, or both;
/// when it does both, the two sets are merged and ordered together with no positional privilege.
/// </summary>
public class OwnersSection : IYamlSourceLine
{
    /// <inheritdoc/>
    [YamlIgnore]
    public int Line { get; set; }

    /// <summary>Unique. Also the key used by <c>export-section</c> and <c>CodeownersSectionFinder</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Marks the section as a target for entries contributed by <c>owners.yaml</c> fragments.</summary>
    public bool DefinedInFiles { get; set; }

    /// <summary>
    /// When true the section's entries are ordered with <c>CodeownersEntrySorter.SortEntries</c>;
    /// when false they render in authored order. Independent of <see cref="DefinedInFiles"/>.
    /// </summary>
    public bool Sort { get; set; }

    public List<OwnersPathEntry> Paths { get; set; } = [];

    public List<OwnersLabelOwnerEntry> LabelOwners { get; set; } = [];
}
