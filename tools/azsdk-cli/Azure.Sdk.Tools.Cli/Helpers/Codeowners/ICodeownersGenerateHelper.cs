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
    /// <param name="check">
    /// When true, nothing is written. The result reports whether the rendered content matches what is
    /// already on disk, which is what CI uses to detect a stale CODEOWNERS file.
    /// </param>
    Task<CodeownersGenerateResult> Generate(string repoRoot, bool check, CancellationToken ct);
}

/// <summary>
/// Outcome of a generate run. The caller maps this onto the CLI exit codes CI depends on:
/// 0 = valid and in sync, 1 = a CFG-* validation error, 2 = valid but stale.
/// </summary>
public class CodeownersGenerateResult
{
    /// <summary>Repo-relative path the CODEOWNERS content was (or would be) written to.</summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>Rendered file content. Populated even in check mode.</summary>
    public string RenderedContent { get; set; } = string.Empty;

    /// <summary>False when the file at <see cref="OutputPath"/> differs from <see cref="RenderedContent"/>.</summary>
    public bool IsUpToDate { get; set; }

    /// <summary>CFG-* validation errors. Non-empty means nothing was written.</summary>
    public List<string> Errors { get; } = [];
}

public class CodeownersGenerateHelper : ICodeownersGenerateHelper
{
    public Task<CodeownersGenerateResult> Generate(string repoRoot, bool check, CancellationToken ct)
        => throw new NotImplementedException(
            "CODEOWNERS generation from ownership YAML is not implemented yet. " +
            "See tools/azsdk-cli/docs/specs/8-operations-codeowners-management.spec.md.");
}
