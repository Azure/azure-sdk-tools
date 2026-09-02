// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Validates the ownership declared in a repository's owners YAML against external truth
/// (cached org and team membership, cached repo labels, and the repo working tree).
/// </summary>
public interface ICodeownersAuditHelper
{
    /// <summary>
    /// Runs every audit rule against the ownership YAML under <paramref name="repoRoot"/>.
    /// </summary>
    /// <param name="repo">Owner/name of the repository whose cached labels the YAML is validated against.</param>
    /// <param name="fix">When true, rules that support automated repair rewrite the offending YAML.</param>
    /// <param name="force">Overrides the safety threshold that caps how many owners a single run may remove.</param>
    Task<CodeownersAuditResponse> RunAudit(string repoRoot, string repo, bool fix, bool force, CancellationToken ct);
}
