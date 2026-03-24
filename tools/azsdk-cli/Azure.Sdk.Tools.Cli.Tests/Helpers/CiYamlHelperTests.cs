// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class CiYamlHelperTests
{
    private CiYamlHelper _helper = null!;

    [SetUp]
    public void SetUp()
    {
        _helper = new CiYamlHelper();
    }

    [Test]
    public void GenerateSafeName_RemovesDots()
    {
        Assert.That(_helper.GenerateSafeName("Azure.Storage.Blobs"), Is.EqualTo("AzureStorageBlobs"));
        Assert.That(_helper.GenerateSafeName("Azure.AI.DocumentIntelligence"), Is.EqualTo("AzureAIDocumentIntelligence"));
        Assert.That(_helper.GenerateSafeName("Azure.ResourceManager.Compute"), Is.EqualTo("AzureResourceManagerCompute"));
    }

    [Test]
    public void CreateClientCiYaml_GeneratesCorrectContent()
    {
        var yaml = _helper.CreateClientCiYaml("healthdataaiservices", "Azure.Health.Deidentification");

        Assert.That(yaml, Does.Contain("ServiceDirectory: healthdataaiservices"));
        Assert.That(yaml, Does.Contain("- name: Azure.Health.Deidentification"));
        Assert.That(yaml, Does.Contain("safeName: AzureHealthDeidentification"));
        Assert.That(yaml, Does.Contain("template: /eng/pipelines/templates/stages/archetype-sdk-client.yml"));
        Assert.That(yaml, Does.Contain("sdk/healthdataaiservices/"));
        Assert.That(yaml, Does.Contain("ArtifactName: packages"));
        Assert.That(yaml, Does.Contain("# NOTE: Please refer to https://aka.ms/azsdk/engsys/ci-yaml"));
    }

    [Test]
    public void CreateClientCiYaml_IncludesTriggerAndPrSections()
    {
        var yaml = _helper.CreateClientCiYaml("storage", "Azure.Storage.Blobs");

        // Trigger section
        Assert.That(yaml, Does.Contain("trigger:"));
        Assert.That(yaml, Does.Contain("- main"));
        Assert.That(yaml, Does.Contain("- hotfix/*"));
        Assert.That(yaml, Does.Contain("- release/*"));

        // PR section
        Assert.That(yaml, Does.Contain("pr:"));
        Assert.That(yaml, Does.Contain("- feature/*"));
    }

    [Test]
    public void HasArtifact_ReturnsTrueWhenPresent()
    {
        var yaml = """
            extends:
              parameters:
                Artifacts:
                - name: Azure.Storage.Blobs
                  safeName: AzureStorageBlobs
            """;

        Assert.That(_helper.HasArtifact(yaml, "Azure.Storage.Blobs"), Is.True);
    }

    [Test]
    public void HasArtifact_ReturnsFalseWhenAbsent()
    {
        var yaml = """
            extends:
              parameters:
                Artifacts:
                - name: Azure.Storage.Blobs
                  safeName: AzureStorageBlobs
            """;

        Assert.That(_helper.HasArtifact(yaml, "Azure.Storage.Queues"), Is.False);
    }

    [Test]
    public void HasArtifact_DoesNotPartialMatch()
    {
        var yaml = """
            extends:
              parameters:
                Artifacts:
                - name: Azure.Storage.Blobs.Batch
                  safeName: AzureStorageBlobsBatch
            """;

        Assert.That(_helper.HasArtifact(yaml, "Azure.Storage.Blobs"), Is.False);
    }

    [Test]
    public void AddArtifactToCiYaml_AppendsNewArtifact()
    {
        var existingYaml = """
            extends:
              parameters:
                ServiceDirectory: storage
                Artifacts:
                - name: Azure.Storage.Blobs
                  safeName: AzureStorageBlobs
            """;

        var result = _helper.AddArtifactToCiYaml(existingYaml, "Azure.Storage.Queues");

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("- name: Azure.Storage.Blobs"));
        Assert.That(result, Does.Contain("- name: Azure.Storage.Queues"));
        Assert.That(result, Does.Contain("safeName: AzureStorageQueues"));
    }

    [Test]
    public void AddArtifactToCiYaml_ReturnsNullWhenArtifactExists()
    {
        var existingYaml = """
            extends:
              parameters:
                Artifacts:
                - name: Azure.Storage.Blobs
                  safeName: AzureStorageBlobs
            """;

        var result = _helper.AddArtifactToCiYaml(existingYaml, "Azure.Storage.Blobs");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindCiYamlPath_ReturnsPathWhenExists()
    {
        using var tempDir = TestHelpers.TempDirectory.Create("ci_yaml_find");
        var serviceDir = Path.Combine(tempDir.DirectoryPath, "sdk", "storage");
        Directory.CreateDirectory(serviceDir);
        File.WriteAllText(Path.Combine(serviceDir, "ci.yml"), "trigger: none");

        var result = _helper.FindCiYamlPath(tempDir.DirectoryPath, "storage");

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.EndWith("ci.yml"));
    }

    [Test]
    public void FindCiYamlPath_ReturnsNullWhenNotExists()
    {
        using var tempDir = TestHelpers.TempDirectory.Create("ci_yaml_find");

        var result = _helper.FindCiYamlPath(tempDir.DirectoryPath, "nonexistent");

        Assert.That(result, Is.Null);
    }
}
