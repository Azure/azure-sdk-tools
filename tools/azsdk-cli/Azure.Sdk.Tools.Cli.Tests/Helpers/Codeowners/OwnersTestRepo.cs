// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>
/// Builds a throwaway checkout whose ownership YAML is the spec's own example assets, so a rendering
/// change either matches the documented output or fails here.
/// </summary>
internal sealed class OwnersTestRepo : IDisposable
{
    private static readonly string AssetDirectory =
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "codeowners");

    public string Root { get; }

    private OwnersTestRepo()
    {
        Root = Path.Combine(Path.GetTempPath(), "owners-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>The full spec example: the reference config plus both reference fragments.</summary>
    public static OwnersTestRepo FromSpecAssets()
    {
        var repo = new OwnersTestRepo();

        repo.WriteConfig(ReadAsset("owners.config.yaml"));
        repo.WriteFragment("sdk/ai", ReadAsset("sdk-ai-owners.yaml"));
        repo.WriteFragment("sdk/openai", ReadAsset("sdk-openai-owners.yaml"));

        // Directories the fragments claim. CFG-PATH-005 compares authored trailing slashes against
        // what is actually on disk, so the tree has to be real.
        foreach (var directory in new[] { "sdk/ai/Azure.AI.Inference", "sdk/ai/Azure.AI.Projects" })
        {
            repo.CreateDirectory(directory);
        }

        return repo;
    }

    /// <summary>A minimal repo, for tests that want to isolate one rule.</summary>
    public static OwnersTestRepo Empty(string configYaml)
    {
        var repo = new OwnersTestRepo();
        repo.WriteConfig(configYaml);
        return repo;
    }

    public static string ReadAsset(string name) => File.ReadAllText(Path.Combine(AssetDirectory, name));

    public void WriteConfig(string yaml) => Write(OwnersRepositoryLoader.ConfigPath, yaml);

    public void WriteFragment(string directory, string yaml) => Write($"{directory}/owners.yaml", yaml);

    public void CreateDirectory(string relativePath) =>
        Directory.CreateDirectory(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public void Write(string relativePath, string content)
    {
        var file = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    public string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public CodeownersRenderResult Render()
    {
        var errors = new List<OwnersValidationError>();
        var repository = OwnersRepositoryLoader.Load(Root, errors);
        var rendered = CodeownersRenderer.Render(repository);

        return rendered with { Errors = [.. errors, .. rendered.Errors] };
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
    }
}
