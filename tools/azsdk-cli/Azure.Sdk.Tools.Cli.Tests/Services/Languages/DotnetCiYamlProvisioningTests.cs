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

    private void SetupPackageInfo(string serviceName, string packageName, string sdkType = "client")
    {
        var packagePath = Path.Combine(_repoRoot, "sdk", serviceName, packageName);
        Directory.CreateDirectory(Path.Combine(packagePath, "src"));
        File.WriteAllText(Path.Combine(packagePath, "src", $"{packageName}.csproj"), "<Project/>");

        // Match ParsePackagePathAsync for this specific package path
        _packageInfoHelper.Setup(p => p.ParsePackagePathAsync(
            It.Is<string>(s => s.Contains(packageName)),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((_repoRoot, $"sdk/{serviceName}/{packageName}", packagePath));

        // Mock MSBuild GetPackageInfo output — the Identity field is a space-delimited
        // string with single-quoted values: 'pkgPath' 'serviceDir' 'pkgName' 'pkgVersion' 'sdkType' 'isNewSdk' 'dllFolder'
        var identity = $"'{packagePath}' '{serviceName}' '{packageName}' '1.0.0' '{sdkType}' 'true' 'bin/'";
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

    [Test]
    public async Task UpdateMetadataAsync_CreatesMgmtCiYaml_WhenNoneExists()
    {
        var serviceName = "dns";
        var packageName = "Azure.ResourceManager.Dns";
        SetupPackageInfo(serviceName, packageName, "mgmt");

        var packagePath = Path.Combine(_repoRoot, "sdk", serviceName, packageName);

        var result = await _service.UpdateMetadataAsync(packagePath, CancellationToken.None);

        var ciPath = Path.Combine(_repoRoot, "sdk", serviceName, "ci.mgmt.yml");
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(File.Exists(ciPath), Is.True, "ci.mgmt.yml should be created");

        var content = File.ReadAllText(ciPath);
        Assert.That(content, Does.Contain("ServiceDirectory: dns"));
        Assert.That(content, Does.Contain("- name: Azure.ResourceManager.Dns"));
        Assert.That(content, Does.Contain("safeName: AzureResourceManagerDns"));
        Assert.That(content, Does.Contain("trigger: none"));
        Assert.That(content, Does.Contain("LimitForPullRequest: true"));
        Assert.That(content, Does.Contain("archetype-sdk-client.yml"));
    }

    [Test]
    public async Task UpdateMetadataAsync_AppendsArtifactToMgmtCiYaml_WhenExists()
    {
        SetupPackageInfo("dns", "Azure.ResourceManager.Dns.V2", "mgmt");

        var serviceDir = Path.Combine(_repoRoot, "sdk", "dns");
        Directory.CreateDirectory(serviceDir);
        File.WriteAllText(Path.Combine(serviceDir, "ci.mgmt.yml"), """
            extends:
              template: /eng/pipelines/templates/stages/archetype-sdk-client.yml
              parameters:
                ServiceDirectory: dns
                LimitForPullRequest: true
                Artifacts:
                - name: Azure.ResourceManager.Dns
                  safeName: AzureResourceManagerDns
            """);

        var packagePath = Path.Combine(_repoRoot, "sdk", "dns", "Azure.ResourceManager.Dns.V2");
        var result = await _service.UpdateMetadataAsync(packagePath, CancellationToken.None);

        var content = File.ReadAllText(Path.Combine(serviceDir, "ci.mgmt.yml"));
        Assert.That(content, Does.Contain("- name: Azure.ResourceManager.Dns"));
        Assert.That(content, Does.Contain("- name: Azure.ResourceManager.Dns.V2"));
        Assert.That(content, Does.Contain("safeName: AzureResourceManagerDnsV2"));
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
    }

    [Test]
    public async Task UpdateMetadataAsync_ReturnsFailure_ForUnsupportedSdkType()
    {
        var serviceName = "functions";
        var packageName = "Microsoft.Azure.Functions.Worker";
        SetupPackageInfo(serviceName, packageName, "functions");

        var packagePath = Path.Combine(_repoRoot, "sdk", serviceName, packageName);

        var result = await _service.UpdateMetadataAsync(packagePath, CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors, Does.Contain("CI YAML provisioning is only supported for dataplane and management SDKs (type was 'Functions'). No changes were made."));

        // Verify no CI files were created
        var ciYmlPath = Path.Combine(_repoRoot, "sdk", serviceName, "ci.yml");
        var ciMgmtPath = Path.Combine(_repoRoot, "sdk", serviceName, "ci.mgmt.yml");
        Assert.That(File.Exists(ciYmlPath), Is.False, "ci.yml should not be created");
        Assert.That(File.Exists(ciMgmtPath), Is.False, "ci.mgmt.yml should not be created");
    }

    #region Package Discovery Tests

    [Test]
    public async Task DiscoverPackageDirectories_FindsPackagesWithSrcCsproj()
    {
        SetupPackageInfo("storage", "Azure.Storage.Blobs");
        SetupPackageInfo("storage", "Azure.Storage.Queues");

        var packages = await _service.DiscoverPackagesAsync(_repoRoot, "storage");

        Assert.That(packages, Has.Count.EqualTo(2), "Both src-based packages should be discovered");
    }

    [Test]
    public async Task DiscoverPackageDirectories_SkipsTestProjects()
    {
        var serviceDir = Path.Combine(_repoRoot, "sdk", "storage");

        // Create ONLY test project csprojs (no src/) — should NOT be discovered
        var testsDir = Path.Combine(serviceDir, "Azure.Storage.Blobs", "tests");
        Directory.CreateDirectory(testsDir);
        File.WriteAllText(Path.Combine(testsDir, "Azure.Storage.Blobs.Tests.csproj"), "<Project/>");

        var perfDir = Path.Combine(serviceDir, "Azure.Storage.Blobs", "perf");
        Directory.CreateDirectory(perfDir);
        File.WriteAllText(Path.Combine(perfDir, "Azure.Storage.Blobs.Perf.csproj"), "<Project/>");

        var samplesDir = Path.Combine(serviceDir, "Azure.Storage.Blobs", "samples");
        Directory.CreateDirectory(samplesDir);
        File.WriteAllText(Path.Combine(samplesDir, "Azure.Storage.Blobs.Samples.csproj"), "<Project/>");

        // No src/ csproj exists, so no packages should be found
        var packages = await _service.DiscoverPackagesAsync(_repoRoot, "storage");
        Assert.That(packages, Has.Count.EqualTo(0), "Test/perf/samples csprojs should not be discovered");
    }

    #endregion
}
