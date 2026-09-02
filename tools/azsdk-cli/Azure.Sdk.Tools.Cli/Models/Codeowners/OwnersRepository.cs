// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// The ownership YAML of one repository: the config plus every fragment it admits, already read
/// from disk. Everything downstream of loading works from this and touches no files.
/// </summary>
public sealed class OwnersRepository
{
    /// <summary>Absolute path to the checkout. Needed to resolve paths for <c>CFG-PATH-005</c>.</summary>
    public required string RepoRoot { get; init; }

    public required OwnersConfig Config { get; init; }

    /// <summary>Ordered by repo-relative path, <see cref="StringComparer.Ordinal"/> ascending. That order is provenance order.</summary>
    public required IReadOnlyList<OwnersFragment> Fragments { get; init; }
}

/// <summary>
/// One CODEOWNERS block, bound to its section and carrying enough provenance to explain itself in an
/// error, an audit violation, or a <c># Sources:</c> comment.
/// </summary>
public sealed class RenderedEntry
{
    public required CodeownersEntry Entry { get; init; }

    public required string SectionName { get; init; }

    /// <summary>
    /// Repo-relative paths of the fragments that contributed, in provenance order. Empty for entries
    /// declared statically in the owners config, which is its own provenance.
    /// </summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>Where to send an author to change this entry, as <c>file:line</c>.</summary>
    public required string DeclaredAt { get; init; }

    public bool IsFromFragment => Sources.Count > 0;
}
