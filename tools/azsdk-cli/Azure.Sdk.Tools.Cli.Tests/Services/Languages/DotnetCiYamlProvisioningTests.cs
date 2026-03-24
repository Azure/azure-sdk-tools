// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
public class DotnetCiYamlProvisioningTests
{
    private TempDirectory _tempDir = null!;
    private DotnetLanguageService _service = null!;
    private Mock<IProcessHelper> _processHelper = null!;
    private Mock<IGitHelper> _gitHelper = null!;
    private Mock<IPackageInfoHelper> _packageInfoHelper = null!;
    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = TempDirectory.Create("dotnet_ci_provision");
        _repoRoot = _tempDir.DirectoryPath;

        _processHelper = new Mock<IProcessHelper>();
        _gitHelper = new Mock<IGitHelper>();
        _packageInfoHelper = new Mock<IPackageInfoHelper>();

        _gitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-net");
        _gitHelper.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_repoRoot);

        _service = new DotnetLanguageService(
            _processHelper.Object,
            Mock.Of<IPowershellHelper>(),
            Mock.Of<Azure.Sdk.Tools.Cli.CopilotAgents.ICopilotAgentRunner>(),
            _gitHelper.Object,
            new TestLogger<DotnetLanguageService>(),
            Mock.Of<ICommonValidationHelpers>(),
            _packageInfoHelper.Object,
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());
    }

    [TearDown]
    public void TearDown() => _tempDir.Dispose();

    private void SetupPackageInfo(string serviceName, string packageName)
    {
        var packagePath = Path.Combine(_repoRoot, "sdk", serviceName, packageName);
        Directory.CreateDirectory(Path.Combine(packagePath, "src"));
        File.WriteAllText(Path.Combine(packagePath, "src", $"{packageName}.csproj"), "<Project/>");

        _packageInfoHelper.Setup(p => p.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((_repoRoot, $"sdk/{serviceName}/{packageName}", packagePath));

        // Mock MSBuild GetPackageInfo output — the Identity field is a space-delimited
        // string with single-quoted values: 'pkgPath' 'serviceDir' 'pkgName' 'pkgVersion' 'sdkType' 'isNewSdk' 'dllFolder'
        var identity = $"'{packagePath}' '{serviceName}' '{packageName}' '1.0.0' 'client' 'true' 'bin/'";
        var escapedIdentity = identity.Replace("\\", "\\\\");
        var json = "{\"TargetResults\":{\"GetPackageInfo\":{\"Result\":\"Success\",\"Items\":[{\"Identity\":\"" + escapedIdentity + "\"}]}}}";

        var msbuildOutput = new ProcessResult { ExitCode = 0 };
        msbuildOutput.AppendStdout(json);

        _processHelper.Setup(p => p.Run(
            It.IsAny<ProcessOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(msbuildOutput);
    }

    [Test]
    public async Task UpdateMetadataAsync_CreatesCiYaml_WhenNoneExists()
    {
        var serviceName = "healthdataaiservices";
        var packageName = "Azure.Health.Deidentification";
        SetupPackageInfo(serviceName, packageName);

        var packagePath = Path.Combine(_repoRoot, "sdk", serviceName, packageName);

        // Verify our setup produced the right directory structure
        Assert.That(Directory.Exists(Path.Combine(packagePath, "src")), Is.True, "src/ dir should exist");
        Assert.That(File.Exists(Path.Combine(packagePath, "src", $"{packageName}.csproj")), Is.True, "csproj should exist");

        var result = await _service.UpdateMetadataAsync(packagePath, CancellationToken.None);

        var ciPath = Path.Combine(_repoRoot, "sdk", serviceName, "ci.yml");
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(File.Exists(ciPath), Is.True, "ci.yml should be created");

        var content = File.ReadAllText(ciPath);
        Assert.That(content, Does.Contain("ServiceDirectory: healthdataaiservices"));
        Assert.That(content, Does.Contain("- name: Azure.Health.Deidentification"));
        Assert.That(content, Does.Contain("safeName: AzureHealthDeidentification"));
        Assert.That(content, Does.Contain("archetype-sdk-client.yml"));
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
    }

    [Test]
    public async Task UpdateMetadataAsync_AppendsArtifact_WhenCiYamlExists()
    {
        SetupPackageInfo("storage", "Azure.Storage.Queues");

        // Create existing ci.yml with one artifact
        var serviceDir = Path.Combine(_repoRoot, "sdk", "storage");
        Directory.CreateDirectory(serviceDir);
        File.WriteAllText(Path.Combine(serviceDir, "ci.yml"), """
            extends:
              template: /eng/pipelines/templates/stages/archetype-sdk-client.yml
              parameters:
                ServiceDirectory: storage
                Artifacts:
                - name: Azure.Storage.Blobs
                  safeName: AzureStorageBlobs
            """);

        var packagePath = Path.Combine(_repoRoot, "sdk", "storage", "Azure.Storage.Queues");
        var result = await _service.UpdateMetadataAsync(packagePath, CancellationToken.None);

        var content = File.ReadAllText(Path.Combine(serviceDir, "ci.yml"));
        Assert.That(content, Does.Contain("- name: Azure.Storage.Blobs"));
        Assert.That(content, Does.Contain("- name: Azure.Storage.Queues"));
        Assert.That(content, Does.Contain("safeName: AzureStorageQueues"));
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
    }

    [Test]
    public async Task UpdateMetadataAsync_NoOp_WhenArtifactAlreadyExists()
    {
        SetupPackageInfo("storage", "Azure.Storage.Blobs");

        // Create existing ci.yml that already has this artifact
        var serviceDir = Path.Combine(_repoRoot, "sdk", "storage");
        Directory.CreateDirectory(serviceDir);
        var originalContent = """
            extends:
              parameters:
                Artifacts:
                - name: Azure.Storage.Blobs
                  safeName: AzureStorageBlobs
            """;
        File.WriteAllText(Path.Combine(serviceDir, "ci.yml"), originalContent);

        var packagePath = Path.Combine(_repoRoot, "sdk", "storage", "Azure.Storage.Blobs");
        var result = await _service.UpdateMetadataAsync(packagePath, CancellationToken.None);

        var content = File.ReadAllText(Path.Combine(serviceDir, "ci.yml"));
        Assert.That(content, Is.EqualTo(originalContent), "ci.yml should not be modified");
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Message, Does.Contain("already exists"));
    }
}
