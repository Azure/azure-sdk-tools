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

    /// <summary>
    /// The fragment governing <paramref name="directoryPath"/>: the nearest one declared at or above
    /// it. Null when no fragment covers the directory.
    /// <para>
    /// This answers "which file do I edit", which is a different question from "who owns this path".
    /// Ownership can resolve to an entry declared in a fragment further up the tree, or to a static
    /// entry in the owners config, but a fix still belongs in the nearest governing file.
    /// </para>
    /// <para>
    /// Answered from the loaded fragments rather than by walking the working tree, so callers inherit
    /// the discovery rules — the configured file name and <c>allowed-owner-yaml-paths</c> — instead of
    /// each reimplementing them and disagreeing about which file governs a directory.
    /// </para>
    /// </summary>
    public OwnersFragment? FindGoverningFragment(string directoryPath)
    {
        var candidate = directoryPath.Replace('\\', '/').Trim('/');

        return Fragments
            .Where(fragment => IsSelfOrUnder(candidate, fragment.Directory))
            .MaxBy(fragment => fragment.Directory.Length);
    }

    private static bool IsSelfOrUnder(string candidate, string directory) =>
        directory.Length == 0
        || candidate.Equals(directory, StringComparison.OrdinalIgnoreCase)
        || candidate.StartsWith($"{directory}/", StringComparison.OrdinalIgnoreCase);
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
