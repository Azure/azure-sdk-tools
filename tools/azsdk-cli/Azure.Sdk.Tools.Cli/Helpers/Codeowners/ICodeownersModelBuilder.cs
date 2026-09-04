// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Builds the CODEOWNERS model from a checkout's ownership YAML: the repo-level
/// <c>.github/owners.config.yaml</c> and the per-service <c>sdk/&lt;service&gt;/owners.yaml</c>
/// fragments.
/// <para>
/// This is the single path from YAML to <see cref="RenderedEntry"/>. <c>generate</c> writes what it
/// returns and <c>check-package</c> resolves against it, so neither owns a private copy of the
/// loading, filtering, or ordering rules.
/// </para>
/// </summary>
public interface ICodeownersModelBuilder
{
    /// <summary>
    /// Loads the repository, drops the fragment entries the caches will not stand behind, and
    /// renders what remains.
    /// <para>
    /// Always returns a model. Anything unusable is dropped and reported in
    /// <see cref="CodeownersModel.Dropped"/> rather than failing the build: a repository is expected
    /// to contain some decayed ownership at any given moment, and refusing to render would leave the
    /// checked-in CODEOWNERS frozen at whatever it happened to say. <c>lint-fragments</c> is what
    /// blocks the pull request that introduces the problem.
    /// </para>
    /// </summary>
    /// <param name="repoRoot">Absolute path to the repository root.</param>
    /// <param name="omitFallbackSections">
    /// Drops sections marked <c>exclude-from-check-package</c>. Those sections exist so that no path
    /// in the repository is unowned; counting them as ownership would make every package look owned.
    /// </param>
    Task<CodeownersModel> Build(string repoRoot, bool omitFallbackSections, CancellationToken ct);
}
