// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Services.Languages.Samples;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Samples;

public class TypeScriptSourceInputProviderTests
{

    [Test]
    public void Prefers_DistEsm_Over_Src_When_Both_Exist()
    {
        var provider = new TypeScriptSourceInputProvider();
        using var temp = TempDirectory.Create("azsdk-ts-test");
        var root = temp.DirectoryPath;
        File.WriteAllText(Path.Combine(root, "package.json"), "{\n  \"name\": \"@azure/testpkg\",\n  \"version\": \"1.0.0\"\n}");
        var distEsm = Path.Combine(root, "dist", "esm");
        var src = Path.Combine(root, "src");
        Directory.CreateDirectory(distEsm);
        Directory.CreateDirectory(src);

        var inputs = provider.Create(root);

        Assert.Multiple(() =>
        {
            Assert.That(inputs.Any(i => i.Path == distEsm), "dist/esm directory should be included");
            Assert.That(inputs.All(i => i.Path != src), "src directory should NOT be included when dist/esm exists");
        });


        var distInput = inputs.Single(i => i.Path == distEsm);
        Assert.That(distInput.IncludeExtensions, Is.Not.Null);
        Assert.That(distInput.IncludeExtensions, Does.Contain(".d.ts"));
    }

    [Test]
    public void Includes_Src_When_DistEsm_Missing()
    {
        var provider = new TypeScriptSourceInputProvider();
        using var temp = TempDirectory.Create("azsdk-ts-test");
        var root = temp.DirectoryPath;
        File.WriteAllText(Path.Combine(root, "package.json"), "{\n  \"name\": \"@azure/testpkg\",\n  \"version\": \"1.0.0\"\n}");
        var src = Path.Combine(root, "src");
        Directory.CreateDirectory(src);

        var inputs = provider.Create(root);
        var srcInput = inputs.Single(i => i.Path == src);
        Assert.That(srcInput.IncludeExtensions, Does.Contain(".ts"));
    }

    [Test]
    public void Throws_When_No_Source_Directories()
    {
        var provider = new TypeScriptSourceInputProvider();
        using var temp = TempDirectory.Create("azsdk-ts-test");
        var root = temp.DirectoryPath;
        File.WriteAllText(Path.Combine(root, "package.json"), "{\n  \"name\": \"@azure/testpkg\",\n  \"version\": \"1.0.0\"\n}");
        var ex = Assert.Throws<InvalidOperationException>(() => provider.Create(root));
        Assert.That(ex!.Message, Does.Contain("No valid TypeScript source directories"));
    }

    [Test]
    public void Includes_sample_env_When_Present()
    {
        var provider = new TypeScriptSourceInputProvider();
        using var temp = TempDirectory.Create("azsdk-ts-test");
        var root = temp.DirectoryPath;
        File.WriteAllText(Path.Combine(root, "package.json"), "{\n  \"name\": \"@azure/testpkg\",\n  \"version\": \"1.0.0\"\n}");
        var src = Path.Combine(root, "src");
        Directory.CreateDirectory(src);
        var sampleEnv = Path.Combine(root, "sample.env");
        File.WriteAllText(sampleEnv, "KEY=VALUE");

        var inputs = provider.Create(root);
        Assert.That(inputs.Any(i => i.Path == sampleEnv), "sample.env should be included as a file input");
    }

    [Test]
    public void Includes_samples_dev_Directory()
    {
        var provider = new TypeScriptSourceInputProvider();
        using var temp = TempDirectory.Create("azsdk-ts-test");
        var root = temp.DirectoryPath;
        File.WriteAllText(Path.Combine(root, "package.json"), "{\n  \"name\": \"@azure/testpkg\",\n  \"version\": \"1.0.0\"\n}");
        var src = Path.Combine(root, "src");
        Directory.CreateDirectory(src);
        var samplesDev = Path.Combine(root, "samples-dev");
        Directory.CreateDirectory(samplesDev);

        var inputs = provider.Create(root);
        Assert.That(inputs.Any(i => i.Path == samplesDev), "samples-dev directory should be included");
        var sdInput = inputs.Single(i => i.Path == samplesDev);
        Assert.That(sdInput.IncludeExtensions, Does.Contain(".ts"));
    }

    [Test]
    public void Includes_snippets_spec_File()
    {
        var provider = new TypeScriptSourceInputProvider();
        using var temp = TempDirectory.Create("azsdk-ts-test");
        var root = temp.DirectoryPath;
        File.WriteAllText(Path.Combine(root, "package.json"), "{\n  \"name\": \"@azure/testpkg\",\n  \"version\": \"1.0.0\"\n}");
        var src = Path.Combine(root, "src");
        Directory.CreateDirectory(src);
        var testDir = Path.Combine(root, "test");
        Directory.CreateDirectory(testDir);
        var snippetsSpec = Path.Combine(testDir, "snippets.spec.ts");
        File.WriteAllText(snippetsSpec, "// test snippets");

        var inputs = provider.Create(root);
        Assert.That(inputs.Any(i => i.Path == snippetsSpec), "snippets.spec.ts should be included");
    }
}
