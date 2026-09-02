// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// A per-service <c>sdk/&lt;service&gt;/owners.yaml</c>. Path expressions are relative to the
/// directory containing the fragment and may only address that directory or something below it.
/// </summary>
public class OwnersFragment
{
    /// <summary>Must match <see cref="OwnersConfig.Version"/>.</summary>
    public int Version { get; set; }

    /// <summary>Routes every entry in this file to a section other than the config's default.</summary>
    public string? Section { get; set; }

    public List<OwnersPathEntry> Paths { get; set; } = [];

    public List<OwnersLabelOwnerEntry> LabelOwners { get; set; } = [];

    /// <summary>
    /// Repo-relative path of the file these entries were loaded from, e.g. <c>sdk/ai/owners.yaml</c>.
    /// Set by the loader, not by the YAML, and used to attribute errors, audit violations, and the
    /// provenance comments on unioned label-owner blocks.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Repo-relative directory containing the fragment, e.g. <c>sdk/ai</c>. Fragment paths resolve
    /// against this. Set by the loader.
    /// </summary>
    public string Directory { get; set; } = string.Empty;
}
