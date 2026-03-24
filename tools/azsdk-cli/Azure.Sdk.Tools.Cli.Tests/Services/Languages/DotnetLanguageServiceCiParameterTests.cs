// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
public class DotnetLanguageServiceCiParameterTests
{
    private TempDirectory _tempDir = null!;
    private PackageInfoHelper _packageInfoHelper = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = TempDirectory.Create("dotnet_ci_param_tests");
        var logger = new TestLogger<PackageInfoHelper>();
        var gitHelper = new Mock<IGitHelper>();
        _packageInfoHelper = new PackageInfoHelper(logger, gitHelper.Object);
    }

    [TearDown]
    public void TearDown() => _tempDir.Dispose();

    [Test]
    public void GetLanguageCiParameters_ReturnsTypedDotNetParameters()
    {
        var repoRoot = _tempDir.DirectoryPath;
        var serviceDirectory = "ai";
        var ciDirectory = Path.Combine(repoRoot, "sdk", serviceDirectory);
        Directory.CreateDirectory(ciDirectory);

        File.WriteAllText(Path.Combine(ciDirectory, "ci.yml"), """
extends:
  parameters:
    BuildSnippets: false
    Artifacts:
      - name: Azure.AI.DocumentIntelligence
""");

        var info = new PackageInfo
        {
            RepoRoot = repoRoot,
            ServiceDirectory = serviceDirectory,
            ArtifactName = "Azure.AI.DocumentIntelligence",
            Language = SdkLanguage.DotNet
        };

        var parameters = _packageInfoHelper.GetLanguageCiParameters<TestDotnetCiPipelineYamlParameters>(info);

        Assert.That(parameters, Is.Not.Null);
        Assert.That(parameters!.BuildSnippets, Is.False);
    }

    [Test]
    public void GetLanguageCiParameters_UsesDefaults_WhenYamlKeysMissing()
    {
        var repoRoot = _tempDir.DirectoryPath;
        var serviceDirectory = "storage";
        var ciDirectory = Path.Combine(repoRoot, "sdk", serviceDirectory);
        Directory.CreateDirectory(ciDirectory);

        File.WriteAllText(Path.Combine(ciDirectory, "ci.yml"), """
extends:
  parameters:
    Artifacts:
      - name: Azure.Storage.Blobs
""");

        var info = new PackageInfo
        {
            RepoRoot = repoRoot,
            ServiceDirectory = serviceDirectory,
            ArtifactName = "Azure.Storage.Blobs",
            Language = SdkLanguage.DotNet
        };

        var parameters = _packageInfoHelper.GetLanguageCiParameters<TestDotnetCiPipelineYamlParametersWithDefaults>(info);

        Assert.That(parameters, Is.Not.Null);
        Assert.That(parameters!.BuildSnippets, Is.True, "BuildSnippets should default to true");
    }

    [Test]
    public void GetLanguageCiParameters_ReturnsNull_WhenNoCiYaml()
    {
        var repoRoot = _tempDir.DirectoryPath;
        var serviceDirectory = "missing";

        var info = new PackageInfo
        {
            RepoRoot = repoRoot,
            ServiceDirectory = serviceDirectory,
            ArtifactName = "Azure.Missing.Package",
            Language = SdkLanguage.DotNet
        };

        var parameters = _packageInfoHelper.GetLanguageCiParameters<TestDotnetCiPipelineYamlParameters>(info);

        Assert.That(parameters, Is.Null);
    }

    [Test]
    public void ApplyLanguageCiParameters_PopulatesCiParameters_FromYaml()
    {
        var repoRoot = _tempDir.DirectoryPath;
        var serviceDirectory = "ai";
        var ciDirectory = Path.Combine(repoRoot, "sdk", serviceDirectory);
        Directory.CreateDirectory(ciDirectory);

        File.WriteAllText(Path.Combine(ciDirectory, "ci.yml"), """
extends:
  parameters:
    BuildSnippets: false
    Artifacts:
      - name: Azure.AI.DocumentIntelligence
""");

        var info = new PackageInfo
        {
            RepoRoot = repoRoot,
            ServiceDirectory = serviceDirectory,
            ArtifactName = "Azure.AI.DocumentIntelligence",
            Language = SdkLanguage.DotNet
        };

        // Simulate what ApplyLanguageCiParameters does
        var parameters = _packageInfoHelper.GetLanguageCiParameters<TestDotnetCiPipelineYamlParametersWithDefaults>(info)
            ?? new TestDotnetCiPipelineYamlParametersWithDefaults();

        info.CiParameters.BuildSnippets = parameters.BuildSnippets;

        Assert.That(info.CiParameters.BuildSnippets, Is.False);
    }

    [Test]
    public void ApplyLanguageCiParameters_UsesDefaults_WhenNoCiYaml()
    {
        var repoRoot = _tempDir.DirectoryPath;

        var info = new PackageInfo
        {
            RepoRoot = repoRoot,
            ServiceDirectory = "nonexistent",
            ArtifactName = "Azure.Missing.Package",
            Language = SdkLanguage.DotNet
        };

        // Simulate what ApplyLanguageCiParameters does when no ci.yml is found
        var parameters = _packageInfoHelper.GetLanguageCiParameters<TestDotnetCiPipelineYamlParametersWithDefaults>(info)
            ?? new TestDotnetCiPipelineYamlParametersWithDefaults();

        info.CiParameters.BuildSnippets = parameters.BuildSnippets;

        Assert.That(info.CiParameters.BuildSnippets, Is.True, "Should default to true when no ci.yml");
    }

    [Test]
    public void ApplyLanguageCiParameters_WorksWithCiMgmtYml()
    {
        var repoRoot = _tempDir.DirectoryPath;
        var serviceDirectory = "compute";
        var ciDirectory = Path.Combine(repoRoot, "sdk", serviceDirectory);
        Directory.CreateDirectory(ciDirectory);

        // .NET uses ci.mgmt.yml for management-plane SDKs
        File.WriteAllText(Path.Combine(ciDirectory, "ci.mgmt.yml"), """
extends:
  parameters:
    BuildSnippets: false
    Artifacts:
      - name: Azure.ResourceManager.Compute
""");

        var info = new PackageInfo
        {
            RepoRoot = repoRoot,
            ServiceDirectory = serviceDirectory,
            ArtifactName = "Azure.ResourceManager.Compute",
            Language = SdkLanguage.DotNet
        };

        var parameters = _packageInfoHelper.GetLanguageCiParameters<TestDotnetCiPipelineYamlParameters>(info);

        Assert.That(parameters, Is.Not.Null);
        Assert.That(parameters!.BuildSnippets, Is.False);
    }

    [Test]
    public void ApplyLanguageCiParameters_MatchesCorrectArtifact_WhenMultipleExist()
    {
        var repoRoot = _tempDir.DirectoryPath;
        var serviceDirectory = "storage";
        var ciDirectory = Path.Combine(repoRoot, "sdk", serviceDirectory);
        Directory.CreateDirectory(ciDirectory);

        File.WriteAllText(Path.Combine(ciDirectory, "ci.yml"), """
extends:
  parameters:
    BuildSnippets: true
    Artifacts:
      - name: Azure.Storage.Blobs
      - name: Azure.Storage.Queues
      - name: Azure.Storage.Files.Shares
""");

        var info = new PackageInfo
        {
            RepoRoot = repoRoot,
            ServiceDirectory = serviceDirectory,
            ArtifactName = "Azure.Storage.Queues",
            Language = SdkLanguage.DotNet
        };

        var parameters = _packageInfoHelper.GetLanguageCiParameters<TestDotnetCiPipelineYamlParameters>(info);

        Assert.That(parameters, Is.Not.Null);
        Assert.That(parameters!.BuildSnippets, Is.True);
    }

    // Test helper classes (mirrors the pattern used in PackageInfoHelperTests for Go)
    private class TestDotnetCiPipelineYamlParameters : CiPipelineYamlParametersBase
    {
        [YamlMember(Alias = "BuildSnippets")]
        public bool? BuildSnippets { get; set; }
    }

    private class TestDotnetCiPipelineYamlParametersWithDefaults : CiPipelineYamlParametersBase
    {
        [YamlMember(Alias = "BuildSnippets")]
        public bool? BuildSnippets { get; set; } = true;
    }
}
