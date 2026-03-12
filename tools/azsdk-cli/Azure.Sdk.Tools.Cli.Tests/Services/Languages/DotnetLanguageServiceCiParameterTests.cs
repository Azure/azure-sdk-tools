// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
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
    CheckAOTCompat: true
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
        Assert.That(parameters.CheckAotCompat, Is.True);
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
        Assert.That(parameters.CheckAotCompat, Is.False, "CheckAotCompat should default to false");
        Assert.That(parameters.AotTestInputs, Is.Null, "AotTestInputs should default to null");
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
    CheckAOTCompat: true
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
        info.CiParameters.CheckAotCompat = parameters.CheckAotCompat;
        info.CiParameters.AotTestInputs = parameters.AotTestInputs;

        Assert.That(info.CiParameters.BuildSnippets, Is.False);
        Assert.That(info.CiParameters.CheckAotCompat, Is.True);
        Assert.That(info.CiParameters.AotTestInputs, Is.Null);
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
        info.CiParameters.CheckAotCompat = parameters.CheckAotCompat;
        info.CiParameters.AotTestInputs = parameters.AotTestInputs;

        Assert.That(info.CiParameters.BuildSnippets, Is.True, "Should default to true when no ci.yml");
        Assert.That(info.CiParameters.CheckAotCompat, Is.False, "Should default to false when no ci.yml");
        Assert.That(info.CiParameters.AotTestInputs, Is.Null);
    }

    [Test]
    public void ApplyLanguageCiParameters_ParsesAotTestInputs()
    {
        var repoRoot = _tempDir.DirectoryPath;
        var serviceDirectory = "ai";
        var ciDirectory = Path.Combine(repoRoot, "sdk", serviceDirectory);
        Directory.CreateDirectory(ciDirectory);

        File.WriteAllText(Path.Combine(ciDirectory, "ci.yml"), """
extends:
  parameters:
    CheckAOTCompat: true
    AOTTestInputs:
      - BuildConfiguration: Release
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
        Assert.That(parameters!.CheckAotCompat, Is.True);
        Assert.That(parameters.AotTestInputs, Is.Not.Null);
        Assert.That(parameters.AotTestInputs, Has.Count.EqualTo(1));
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
    CheckAOTCompat: false
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
        Assert.That(parameters.CheckAotCompat, Is.False);
    }

    // Test helper classes (mirrors the pattern used in PackageInfoHelperTests for Go)
    private class TestDotnetCiPipelineYamlParameters : CiPipelineYamlParametersBase
    {
        [YamlMember(Alias = "BuildSnippets")]
        public bool? BuildSnippets { get; set; }

        [YamlMember(Alias = "CheckAOTCompat")]
        public bool? CheckAotCompat { get; set; }

        [YamlMember(Alias = "AOTTestInputs")]
        public List<Dictionary<string, object?>>? AotTestInputs { get; set; }
    }

    private class TestDotnetCiPipelineYamlParametersWithDefaults : CiPipelineYamlParametersBase
    {
        [YamlMember(Alias = "BuildSnippets")]
        public bool? BuildSnippets { get; set; } = true;

        [YamlMember(Alias = "CheckAOTCompat")]
        public bool? CheckAotCompat { get; set; } = false;

        [YamlMember(Alias = "AOTTestInputs")]
        public List<Dictionary<string, object?>>? AotTestInputs { get; set; }
    }
}
