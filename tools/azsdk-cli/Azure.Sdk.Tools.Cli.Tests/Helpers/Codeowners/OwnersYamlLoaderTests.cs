// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>
/// Loads the canonical samples from <c>docs/specs/assets/codeowners/</c>, so the schema the loader
/// accepts and the schema the spec documents cannot drift apart without a test failing.
/// </summary>
[TestFixture]
public class OwnersYamlLoaderTests
{
    private static string ReadAsset(string fileName) => File.ReadAllText(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "codeowners", fileName));

    [Test]
    public void LoadConfig_ReferenceConfig_ReadsSettingsAndSections()
    {
        var config = OwnersYamlLoader.LoadConfig(ReadAsset("owners.config.yaml"), ".github/owners.config.yaml");

        Assert.Multiple(() =>
        {
            Assert.That(config.Version, Is.EqualTo(1));
            Assert.That(config.Configs.AllowedOwnerYamlPaths, Is.EqualTo(new[] { "sdk/*/owners.yaml", "sdk/*/owners.yml" }));
            Assert.That(config.Configs.DefaultSection, Is.EqualTo("Client Libraries"));
            Assert.That(config.Configs.Output, Is.EqualTo(".github/CODEOWNERS"));
            Assert.That(config.Configs.MinimumPathOwners, Is.EqualTo(2));
            Assert.That(config.Configs.MinimumLabelOwners, Is.EqualTo(2));
        });

        Assert.That(config.Sections.Select(s => s.Name), Is.EqualTo(new[]
        {
            "Repository Root",
            "SDK",
            "End-to-End Samples",
            "Core Libraries",
            "Client Libraries",
            "Management Fallback",
            "Management Libraries",
            "Provisioning Libraries",
            "EngSys",
            "Code Generation",
            "Automation",
            "Repository configuration",
        }), "sections must load in declaration order; that order is the render order");
    }

    [Test]
    public void LoadConfig_SectionFlagsDefaultToFalse()
    {
        var config = OwnersYamlLoader.LoadConfig(ReadAsset("owners.config.yaml"), ".github/owners.config.yaml");

        var repositoryRoot = config.Sections.Single(s => s.Name == "Repository Root");
        var clientLibraries = config.Sections.Single(s => s.Name == "Client Libraries");

        Assert.Multiple(() =>
        {
            Assert.That(repositoryRoot.DefinedInFiles, Is.False);
            Assert.That(repositoryRoot.Sort, Is.False);
            Assert.That(clientLibraries.DefinedInFiles, Is.True);
            Assert.That(clientLibraries.Sort, Is.True);
        });
    }

    [Test]
    public void LoadConfig_SectionCarriesBothStaticPathsAndLabelOwners()
    {
        var config = OwnersYamlLoader.LoadConfig(ReadAsset("owners.config.yaml"), ".github/owners.config.yaml");

        // Management Libraries is the case that matters: fragment-populated and sorted, yet still
        // declaring hand-curated static entries that merge into the same set.
        var managementLibraries = config.Sections.Single(s => s.Name == "Management Libraries");

        Assert.Multiple(() =>
        {
            Assert.That(managementLibraries.DefinedInFiles, Is.True);
            Assert.That(managementLibraries.Paths, Is.Not.Empty);
            Assert.That(managementLibraries.LabelOwners, Is.Not.Empty);
        });

        var apiCenter = managementLibraries.Paths[0];
        Assert.Multiple(() =>
        {
            Assert.That(apiCenter.Path, Is.EqualTo("/sdk/apicenter/Azure.ResourceManager.ApiCenter/"));
            Assert.That(apiCenter.Owners, Is.EqualTo(new[] { "test-user-03", "test-user-04" }));
            Assert.That(apiCenter.PrLabels, Is.EqualTo(new[] { "API Center", "Mgmt" }));
        });
    }

    [Test]
    public void LoadConfig_StaticPathEntryMayOmitPrLabels()
    {
        var config = OwnersYamlLoader.LoadConfig(ReadAsset("owners.config.yaml"), ".github/owners.config.yaml");

        var repositoryRoot = config.Sections.Single(s => s.Name == "Repository Root");

        Assert.That(repositoryRoot.Paths[0].PrLabels, Is.Empty);
    }

    [Test]
    public void LoadFragment_ReferenceFragment_ReadsEntriesAndProvenance()
    {
        var fragment = OwnersYamlLoader.LoadFragment(ReadAsset("sdk-ai-owners.yaml"), "sdk/ai/owners.yaml");

        Assert.Multiple(() =>
        {
            Assert.That(fragment.Version, Is.EqualTo(1));
            Assert.That(fragment.Section, Is.Null, "this fragment relies on configs.default-section");
            Assert.That(fragment.FilePath, Is.EqualTo("sdk/ai/owners.yaml"));
            Assert.That(fragment.Directory, Is.EqualTo("sdk/ai"));
            Assert.That(fragment.Paths.Select(p => p.Path), Is.EqualTo(new[]
            {
                ".",
                "Azure.AI.Inference/",
                "Azure.AI.Projects/",
            }));
        });

        var inference = fragment.Paths[1];
        Assert.Multiple(() =>
        {
            Assert.That(inference.Owners, Is.EqualTo(new[] { "test-user-07", "test-user-09", "test-user-23" }));
            Assert.That(inference.PrLabels, Is.EqualTo(new[] { "AI Model Inference" }));
            Assert.That(inference.Section, Is.Null);
        });

        var aiProjects = fragment.LabelOwners.Single(l => l.Labels.Contains("AI Projects"));
        Assert.Multiple(() =>
        {
            Assert.That(aiProjects.ServiceOwners, Is.EqualTo(new[] { "test-user-07", "test-user-18", "test-user-23" }));
            Assert.That(aiProjects.AzureSdkOwners, Is.EqualTo(new[] { "test-user-07" }));
        });
    }

    [Test]
    public void LoadFragment_RecordsDeclarationLineForEachEntry()
    {
        var fragment = OwnersYamlLoader.LoadFragment(ReadAsset("sdk-ai-owners.yaml"), "sdk/ai/owners.yaml");

        var lines = fragment.Paths.Select(p => p.Line).ToList();

        Assert.That(lines, Is.All.GreaterThan(0), "validation errors must be able to name a line");
        Assert.That(lines, Is.Ordered.Ascending, "lines follow declaration order");
    }

    [Test]
    public void LoadFragment_EntrySectionOverrideIsRead()
    {
        var yaml = """
            version: 1
            section: Client Libraries
            paths:
              - path: Azure.ResourceManager.AI/
                section: Management Libraries
                owners: [test-user-07]
                pr-labels: [Mgmt]
            """;

        var fragment = OwnersYamlLoader.LoadFragment(yaml, "sdk/ai/owners.yaml");

        Assert.Multiple(() =>
        {
            Assert.That(fragment.Section, Is.EqualTo("Client Libraries"));
            Assert.That(fragment.Paths[0].Section, Is.EqualTo("Management Libraries"));
        });
    }

    [Test]
    public void LoadFragment_UnknownKeyIsRejected()
    {
        var yaml = """
            version: 1
            paths:
              - path: Azure.AI.Inference/
                owners: [test-user-07]
                pr-labels: [AI Model Inference]
                colour: blue
            """;

        var ex = Assert.Throws<OwnersYamlException>(() => OwnersYamlLoader.LoadFragment(yaml, "sdk/ai/owners.yaml"));

        Assert.That(ex!.Message, Does.Contain("sdk/ai/owners.yaml:").And.Contain("colour"));
    }

    [Test]
    public void LoadFragment_NonCanonicalKeyNamesTheCanonicalOne()
    {
        var yaml = """
            version: 1
            paths:
              - path: Azure.AI.Inference/
                owners: [test-user-07]
                pr-label: [AI Model Inference]
            """;

        var ex = Assert.Throws<OwnersYamlException>(() => OwnersYamlLoader.LoadFragment(yaml, "sdk/ai/owners.yaml"));

        Assert.That(ex!.Message, Does.Contain("Did you mean 'pr-labels'?"));
    }

    [Test]
    public void LoadFragment_UnsupportedVersionIsRejected()
    {
        var ex = Assert.Throws<OwnersYamlException>(
            () => OwnersYamlLoader.LoadFragment("version: 2\npaths: []\n", "sdk/ai/owners.yaml"));

        Assert.That(ex!.Message, Does.Contain("unsupported schema version '2'"));
    }

    [Test]
    public void LoadFragment_EmptyFileIsRejected()
    {
        var ex = Assert.Throws<OwnersYamlException>(
            () => OwnersYamlLoader.LoadFragment("# nothing but a comment\n", "sdk/ai/owners.yaml"));

        Assert.That(ex!.Message, Does.Contain("file is empty"));
    }
}
