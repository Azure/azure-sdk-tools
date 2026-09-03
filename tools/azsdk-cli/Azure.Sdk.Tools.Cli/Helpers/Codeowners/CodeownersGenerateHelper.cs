// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Orchestration only: load the checkout's ownership YAML, render it, and write the result. The
/// rules live in <see cref="CodeownersRenderer"/>.
/// </summary>
public class CodeownersGenerateHelper : ICodeownersGenerateHelper
{
    public async Task<CodeownersGenerateResult> Generate(string repoRoot, CancellationToken ct)
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
            // The render is still reported so a reviewer can see what the fix would produce, but
            // nothing is written until every entry binds.
            result.Errors.AddRange(errors.Select(e => e.ToString()));
            return result;
        }

        var outputFile = Path.Combine(
            repoRoot, repository.Config.Configs.Output.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        await File.WriteAllTextAsync(outputFile, rendered.Content, ct);

        return result;
    }
}
