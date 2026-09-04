// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Checks the ownership YAML against the things it cannot verify about itself: whether its owners
/// are real code owners, whether it names enough of them, and whether its labels exist.
/// </summary>
public interface ICodeownersLintHelper
{
    /// <summary>
    /// Runs every lint rule against the ownership YAML under <paramref name="repoRoot"/> and reports
    /// what it finds. Lint never edits the repository.
    /// </summary>
    Task<CodeownersLintResponse> RunLint(string repoRoot, CancellationToken ct);
}
