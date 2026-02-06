// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

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
    /// The HTTPS clone URL for the repository.
    /// </summary>
    public string CloneUrl => $"https://github.com/{EffectiveOwner}/{Name}.git";
}
