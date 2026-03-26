// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Nodes;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

public class PackageInfoHelperTests
{
    private PackageInfoHelper packageInfoHelper;
    private TempDirectory? tempDirectory;

    [SetUp]
    public void Setup()
    {
        var logger = new TestLogger<PackageInfoHelper>();
        var gitHelper = new Mock<IGitHelper>();
        packageInfoHelper = new PackageInfoHelper(logger, gitHelper.Object);
    }

    [TearDown]
    public void TearDown() => tempDirectory?.Dispose();

    [Test]
    public void FilterByArtifacts_ReturnsFilteredMatches()
    {
        var packages = new List<PackageInfo>
        {
            new()
            {
                PackageName = "PackageA",
                ArtifactName = "artifact-a"
            },
            new()
            {
                PackageName = "PackageB",
                ArtifactName = "artifact-b"
            }
        };

        var result = packageInfoHelper.FilterPackagesByArtifact(packages, ["artifact-b"]);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].PackageName, Is.EqualTo("PackageB"));
    }

    [Test]
    public void FilterByArtifacts_UsesNoFilterWhenListIsEmpty()
    {
        var packages = new List<PackageInfo>
        {
            new()
            {
                PackageName = "PackageA",
                ArtifactName = "artifact-a"
            }
        };

        var result = packageInfoHelper.FilterPackagesByArtifact(packages, []);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public void WritePackageInfoFile_WritesJsonWithCorrectFormat()
    {
        tempDirectory = TempDirectory.Create(nameof(WritePackageInfoFile_WritesJsonWithCorrectFormat));
        var repoRoot = tempDirectory.DirectoryPath;
        var packageDir = Path.Combine(repoRoot, "sdk", "storage", "storage-blob");
        Directory.CreateDirectory(packageDir);

        var packageInfo = new PackageInfo
        {
            PackageName = "Azure.Storage.Blobs",
            ArtifactName = "Azure.Storage.Blobs",
            PackageVersion = "1.2.3",
            DirectoryPath = "sdk/storage/storage-blob",
            ReadMePath = "sdk/storage/storage-blob/README.md",
            ChangeLogPath = "sdk/storage/storage-blob/CHANGELOG.md"
        };

        var outputPath = Path.Combine(repoRoot, "out", "Azure.Storage.Blobs.json");
        packageInfoHelper.WritePackageInfoFile(packageInfo, outputPath, addDevVersion: false);

        var output = JsonNode.Parse(File.ReadAllText(outputPath)) as JsonObject;
        Assert.That(output, Is.Not.Null);
        Assert.That(output?["Name"]?.ToString(), Is.EqualTo("Azure.Storage.Blobs"));
        Assert.That(output?["Version"]?.ToString(), Is.EqualTo("1.2.3"));
        Assert.That(output?["DirectoryPath"]?.ToString(), Is.EqualTo("sdk/storage/storage-blob"));
        Assert.That(output?["ReadMePath"]?.ToString(), Is.EqualTo("sdk/storage/storage-blob/README.md"));
        Assert.That(output?["ChangeLogPath"]?.ToString(), Is.EqualTo("sdk/storage/storage-blob/CHANGELOG.md"));
    }

    [Test]
    public void WritePackageInfoFile_SetsDevVersion_WhenAddDevVersionIsTrue()
    {
        var tempDirectory = TempDirectory.Create(nameof(WritePackageInfoFile_SetsDevVersion_WhenAddDevVersionIsTrue));
        var repoRoot = tempDirectory.DirectoryPath;
        var outputPath = Path.Combine(repoRoot, "out", "Azure.Storage.Blobs.json");

        var packageInfo = new PackageInfo
        {
            PackageName = "Azure.Storage.Blobs",
            ArtifactName = "Azure.Storage.Blobs",
            PackageVersion = "2.0.0",
            DirectoryPath = "sdk/storage/storage-blob"
        };

        packageInfoHelper.WritePackageInfoFile(packageInfo, outputPath, addDevVersion: true);

        var output = JsonNode.Parse(File.ReadAllText(outputPath)) as JsonObject;
        Assert.That(output, Is.Not.Null);
        Assert.That(output?["Version"]?.ToString(), Is.EqualTo("2.0.0"));
        Assert.That(output?["DevVersion"]?.ToString(), Is.EqualTo("2.0.0"));
    }

    [Test]
    public void WritePackageInfoFile_DoesNotSetDevVersion_WhenAddDevVersionIsFalse()
    {
        var tempDirectory = TempDirectory.Create(nameof(WritePackageInfoFile_DoesNotSetDevVersion_WhenAddDevVersionIsFalse));
        var repoRoot = tempDirectory.DirectoryPath;
        var outputPath = Path.Combine(repoRoot, "out", "Azure.Storage.Blobs.json");

        var packageInfo = new PackageInfo
        {
            PackageName = "Azure.Storage.Blobs",
            PackageVersion = "1.0.0",
            DirectoryPath = "sdk/storage/storage-blob"
        };

        packageInfoHelper.WritePackageInfoFile(packageInfo, outputPath, addDevVersion: false);

        var output = JsonNode.Parse(File.ReadAllText(outputPath)) as JsonObject;
        Assert.That(output, Is.Not.Null);
        Assert.That(output?["Version"]?.ToString(), Is.EqualTo("1.0.0"));
        Assert.That(output?["DevVersion"], Is.Null);
    }

    [Test]
    public void PopulateCommonCiMetadata_PopulatesSharedCiFields()
    {
        tempDirectory = TempDirectory.Create(nameof(PopulateCommonCiMetadata_PopulatesSharedCiFields));
        var repoRoot = tempDirectory.DirectoryPath;
        var serviceDirectory = "storage/azblob";
        var ciDirectory = Path.Combine(repoRoot, "sdk", "storage", "azblob");

        Directory.CreateDirectory(Path.Combine(repoRoot, "eng", "common"));
        Directory.CreateDirectory(Path.Combine(ciDirectory, "src"));
        Directory.CreateDirectory(Path.Combine(ciDirectory, "custom"));

        File.WriteAllText(Path.Combine(ciDirectory, "ci.yml"), """
extends:
  parameters:
    TriggeringPaths:
      - /eng/common
      - src
    MatrixConfigs:
      - Name: linux
    AdditionalMatrixConfigs:
      - Name: windows
    Artifacts:
      - name: sdk/storage/azblob
        triggeringPaths:
          - /sdk/storage/azblob/custom
        additionalValidationPackages:
          - sdk/core/azcore
""");

        var info = new PackageInfo
        {
            RepoRoot = repoRoot,
            ServiceDirectory = serviceDirectory,
            ArtifactName = "sdk/storage/azblob",
            Language = SdkLanguage.Go
        };

        packageInfoHelper.PopulateCommonCiMetadata(info);

        Assert.That(info.CiParameters.MatrixConfigs, Has.Count.EqualTo(2));
        Assert.That(info.TriggeringPaths, Has.Some.EqualTo((NormalizedPath)"/eng/common"));
        Assert.That(info.TriggeringPaths, Has.Some.EqualTo((NormalizedPath)"/sdk/storage/azblob/src"));
        Assert.That(info.TriggeringPaths, Has.Some.EqualTo((NormalizedPath)"/sdk/storage/azblob/custom"));
        Assert.That(info.TriggeringPaths, Has.Some.EqualTo((NormalizedPath)"/sdk/storage/azblob/ci.yml"));
        Assert.That(info.AdditionalValidationPackages, Is.Not.Null);
        Assert.That(info.AdditionalValidationPackages!, Has.Some.EqualTo((NormalizedPath)"sdk/core/azcore"));
    }

    [Test]
    public void GetLanguageCiParameters_ReturnsTypedLanguageParameters()
    {
        tempDirectory = TempDirectory.Create(nameof(GetLanguageCiParameters_ReturnsTypedLanguageParameters));
        var repoRoot = tempDirectory.DirectoryPath;
        var serviceDirectory = "storage/blob";
        var ciDirectory = Path.Combine(repoRoot, "sdk", "storage", "blob");

        Directory.CreateDirectory(ciDirectory);

        File.WriteAllText(Path.Combine(ciDirectory, "ci.yml"), """
extends:
  parameters:
    LicenseCheck: false
    UsePipelineProxy: false
    MatrixConfigs:
      - Name: linux
    Artifacts:
      - name: sdk/storage/blob
""");

        var info = new PackageInfo
        {
            RepoRoot = repoRoot,
            ServiceDirectory = serviceDirectory,
            ArtifactName = "sdk/storage/blob",
            Language = SdkLanguage.Go
        };

        var languageParameters = packageInfoHelper.GetLanguageCiParameters<TestGoCiPipelineYamlParameters>(info);
        Assert.That(languageParameters, Is.TypeOf<TestGoCiPipelineYamlParameters>());

        var typed = (TestGoCiPipelineYamlParameters)languageParameters!;
        Assert.That(typed.LicenseCheck, Is.False);
        Assert.That(typed.UsePipelineProxy, Is.False);

        info.CiParameters.LicenseCheck = typed.LicenseCheck;
        info.CiParameters.UsePipelineProxy = typed.UsePipelineProxy;
        Assert.That(info.CiParameters.LicenseCheck, Is.False);
        Assert.That(info.CiParameters.UsePipelineProxy, Is.False);
    }

    [Test]
    public void GetLanguageCiParameters_UsesPocoDefaults_WhenYamlKeysMissing()
    {
        tempDirectory = TempDirectory.Create(nameof(GetLanguageCiParameters_UsesPocoDefaults_WhenYamlKeysMissing));
        var repoRoot = tempDirectory.DirectoryPath;
        var serviceDirectory = "storage/blob";
        var ciDirectory = Path.Combine(repoRoot, "sdk", "storage", "blob");

        Directory.CreateDirectory(ciDirectory);

        File.WriteAllText(Path.Combine(ciDirectory, "ci.yml"), """
extends:
  parameters:
    Artifacts:
      - name: sdk/storage/blob
""");

        var info = new PackageInfo
        {
            RepoRoot = repoRoot,
            ServiceDirectory = serviceDirectory,
            ArtifactName = "sdk/storage/blob",
            Language = SdkLanguage.Go
        };

        var languageParameters = packageInfoHelper.GetLanguageCiParameters<TestGoCiPipelineYamlParametersWithDefaults>(info);
        Assert.That(languageParameters, Is.TypeOf<TestGoCiPipelineYamlParametersWithDefaults>());

        var typed = (TestGoCiPipelineYamlParametersWithDefaults)languageParameters!;
        Assert.That(typed.LicenseCheck, Is.True);
        Assert.That(typed.UsePipelineProxy, Is.True);
    }

    private class TestGoCiPipelineYamlParameters : CiPipelineYamlParametersBase
    {
        public bool? LicenseCheck { get; set; }
        public bool? UsePipelineProxy { get; set; }
    }

    private class TestGoCiPipelineYamlParametersWithDefaults : CiPipelineYamlParametersBase
    {
        public bool? LicenseCheck { get; set; } = true;
        public bool? UsePipelineProxy { get; set; } = true;
    }
}
