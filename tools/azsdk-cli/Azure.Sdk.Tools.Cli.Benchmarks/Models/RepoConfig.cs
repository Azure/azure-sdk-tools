// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Configuration for a GitHub repository used in benchmarks.
/// </summary>
public class RepoConfig
{
    /// <summary>
    /// The owner of the repository (e.g., "Azure").
    /// </summary>
    public required string Owner { get; init; }

    /// <summary>
    /// The name of the repository (e.g., "azure-rest-api-specs").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional override for the owner, used when working with a fork (e.g., "chrisradek").
    /// </summary>
    public string? ForkOwner { get; init; }

    /// <summary>
    /// The branch, tag, or commit SHA to use. Defaults to "main".
    /// </summary>
    public string Ref { get; init; } = "main";

    /// <summary>
    /// Returns <see cref="ForkOwner"/> if set, otherwise <see cref="Owner"/>.
    /// </summary>
    public string EffectiveOwner => ForkOwner ?? Owner;

    /// <summary>
    /// Optional list of directory paths to sparse checkout.
    /// When set, only these directories (plus root-level files) will be materialized in the worktree.
    /// When null or empty, the full repository is checked out.
    /// Uses git sparse-checkout in cone mode.
    /// </summary>
    public string[]? SparseCheckoutPaths { get; init; }

    /// <summary>
    /// The HTTPS clone URL for the repository.
    /// </summary>
    public string CloneUrl => $"https://github.com/{EffectiveOwner}/{Name}.git";

    /// <summary>
    /// Returns a new <see cref="RepoConfig"/> with the specified ref, preserving all other properties.
    /// </summary>
    public RepoConfig WithRef(string newRef) => new()
    {
        Owner = Owner,
        Name = Name,
        ForkOwner = ForkOwner,
        Ref = newRef,
        SparseCheckoutPaths = SparseCheckoutPaths
    };

    /// <summary>
    /// Parses a repo string in the format "Owner/Name" or "Owner/Name:Ref".
    /// </summary>
    /// <returns>True if parsing succeeded; false otherwise.</returns>
    public static bool TryParse(string input, out string owner, out string name, out string? gitRef)
    {
        owner = name = string.Empty;
        gitRef = null;

        var colonIndex = input.IndexOf(':');
        var repoKey = colonIndex >= 0 ? input[..colonIndex] : input;
        gitRef = colonIndex >= 0 ? input[(colonIndex + 1)..] : null;

        var parts = repoKey.Split('/');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        owner = parts[0];
        name = parts[1];
        return true;
    }
}
