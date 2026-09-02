// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Orchestration only: load the checkout's ownership YAML, render it, and either write the result or
/// compare it with what is on disk. The rules live in <see cref="CodeownersRenderer"/>.
/// </summary>
public class CodeownersGenerateHelper : ICodeownersGenerateHelper
{
    public async Task<CodeownersGenerateResult> Generate(string repoRoot, bool check, CancellationToken ct)
    {
        var result = new CodeownersGenerateResult();
        var errors = new List<OwnersValidationError>();

        OwnersRepository repository;
        try
        {
            repository = OwnersRepositoryLoader.Load(repoRoot, errors);
        }
        catch (OwnersYamlException ex)
        {
            // Without a config there is no output path and no section list, so there is nothing
            // further worth reporting.
            result.Errors.Add(ex.Message);
            return result;
        }

        var rendered = CodeownersRenderer.Render(repository);
        errors.AddRange(rendered.Errors);

        result.OutputPath = repository.Config.Configs.Output;
        result.RenderedContent = rendered.Content;

        if (errors.Count > 0)
        {
            result.Errors.AddRange(errors.Select(e => e.ToString()));
            return result;
        }

        var outputFile = Path.Combine(
            repoRoot, repository.Config.Configs.Output.Replace('/', Path.DirectorySeparatorChar));

        var existing = File.Exists(outputFile) ? await File.ReadAllTextAsync(outputFile, ct) : null;
        result.IsUpToDate = string.Equals(existing, rendered.Content, StringComparison.Ordinal);

        if (!check && !result.IsUpToDate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
            await File.WriteAllTextAsync(outputFile, rendered.Content, ct);
        }

        return result;
    }
}
