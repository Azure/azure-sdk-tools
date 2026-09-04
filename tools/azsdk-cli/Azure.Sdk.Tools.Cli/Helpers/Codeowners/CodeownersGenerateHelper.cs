// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Renders <c>.github/CODEOWNERS</c> from the in-repo ownership YAML and writes it.
/// </summary>
public interface ICodeownersGenerateHelper
{
    /// <param name="repoRoot">Absolute path to the repository root.</param>
    /// <param name="omitFallbackSections">Drop sections marked <c>exclude-from-check-package</c>.</param>
    /// <param name="outputPath">
    /// Where to write, overriding the config's <c>configs.output</c>. Relative paths resolve against
    /// <paramref name="repoRoot"/>.
    /// </param>
    Task<CodeownersGenerateResult> Generate(
        string repoRoot,
        bool omitFallbackSections,
        string? outputPath,
        CancellationToken ct);
}

/// <summary>
/// Outcome of a generate run. Generation always succeeds: anything unusable is dropped and reported
/// in <see cref="Dropped"/>.
/// </summary>
public class CodeownersGenerateResult
{
    /// <summary>Repo-relative path the CODEOWNERS content was written to.</summary>
    public string OutputPath { get; set; } = string.Empty;

    public string RenderedContent { get; set; } = string.Empty;

    /// <summary>Entries and owners excluded from the rendered file, with the reason for each.</summary>
    public IReadOnlyList<DroppedItem> Dropped { get; set; } = [];
}

public class CodeownersGenerateHelper(ICodeownersModelBuilder modelBuilder) : ICodeownersGenerateHelper
{
    public async Task<CodeownersGenerateResult> Generate(
        string repoRoot,
        bool omitFallbackSections,
        string? outputPath,
        CancellationToken ct)
    {
        var model = await modelBuilder.Build(repoRoot, omitFallbackSections, ct);

        var relativePath = string.IsNullOrWhiteSpace(outputPath) ? model.OutputPath : outputPath;
        var outputFile = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        await File.WriteAllTextAsync(outputFile, model.Content, ct);

        return new CodeownersGenerateResult
        {
            OutputPath = relativePath,
            RenderedContent = model.Content,
            Dropped = model.Dropped,
        };
    }
}
