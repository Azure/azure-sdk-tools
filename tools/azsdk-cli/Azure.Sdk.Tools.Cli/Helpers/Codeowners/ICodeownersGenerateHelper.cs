// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Renders <c>.github/CODEOWNERS</c> from the in-repo ownership YAML: the repo-level
/// <c>.github/owners.config.yaml</c> and the per-service <c>sdk/&lt;service&gt;/owners.yaml</c> fragments.
/// </summary>
public interface ICodeownersGenerateHelper
{
    /// <summary>
    /// Loads and validates the ownership YAML under <paramref name="repoRoot"/> and renders the
    /// CODEOWNERS file to the path named by the config's <c>configs.output</c>.
    /// </summary>
    /// <param name="repoRoot">Absolute path to the repository root.</param>
    Task<CodeownersGenerateResult> Generate(string repoRoot, CancellationToken ct);
}

/// <summary>
/// Outcome of a generate run. Errors are reported rather than thrown so a single bad entry does not
/// hide the rest of the render; the caller fails the command when any are present.
/// </summary>
public class CodeownersGenerateResult
{
    /// <summary>Repo-relative path the CODEOWNERS content was (or would be) written to.</summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>Rendered file content. Populated even when validation errors prevented a write.</summary>
    public string RenderedContent { get; set; } = string.Empty;

    /// <summary>CFG-* validation errors. Non-empty means nothing was written.</summary>
    public List<string> Errors { get; } = [];
}
