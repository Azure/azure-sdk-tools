// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
public class RustLanguageServiceTests
{
    private RustLanguageService _service;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<IProcessHelper> _mockProcessHelper;
    private Mock<IPowershellHelper> _mockPowershellHelper;
    private Mock<ISpecGenSdkConfigHelper> _mockSpecGenSdkConfigHelper;
    private TempDirectory _tempDirectory;

    [SetUp]
    public void Setup()
    {
        _mockGitHelper = new Mock<IGitHelper>();
        _mockProcessHelper = new Mock<IProcessHelper>();
        _mockPowershellHelper = new Mock<IPowershellHelper>();
        _mockSpecGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();

        var languageLogger = new TestLogger<LanguageService>();
        var packageInfoHelper = new PackageInfoHelper(new TestLogger<PackageInfoHelper>(), _mockGitHelper.Object);

        _tempDirectory = TempDirectory.Create("RustLanguageServiceTests");

        _service = new RustLanguageService(
            _mockProcessHelper.Object,
            _mockPowershellHelper.Object,
            _mockGitHelper.Object,
            languageLogger,
            Mock.Of<ICommonValidationHelpers>(),
            packageInfoHelper,
            Mock.Of<IFileHelper>(),
            _mockSpecGenSdkConfigHelper.Object,
            Mock.Of<IChangelogHelper>());
    }

    [TearDown]
    public void TearDown()
    {
        _tempDirectory.Dispose();
    }

    /// <summary>
    /// Sets up the mock process helper to return a successful cargo metadata response.
    /// </summary>
    private void SetupCargoMetadata(string name, string version)
    {
        var json = JsonSerializer.Serialize(new { packages = new[] { new { name, version } } });
        _mockProcessHelper
            .Setup(x => x.Run(It.Is<ProcessOptions>(o => o.Args.Contains("metadata")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, json)] });
    }

    #region Language Identity

    [Test]
    public void Language_ReturnsRust()
    {
        Assert.That(_service.Language, Is.EqualTo(SdkLanguage.Rust));
    }

    #endregion

    #region BuildAsync Tests

    [Test]
    public async Task BuildAsync_EmptyPath_ReturnsFailure()
    {
        var (success, errorMessage, _) = await _service.BuildAsync(string.Empty);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("required and cannot be empty"));
    }

    [Test]
    public async Task BuildAsync_NonexistentPath_ReturnsFailure()
    {
        var (success, errorMessage, _) = await _service.BuildAsync("/nonexistent/path");

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("does not exist"));
    }

    [Test]
    public async Task BuildAsync_MissingBuildScript_ReturnsFailure()
    {
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "mypackage");
        Directory.CreateDirectory(packageDir);

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        var (success, errorMessage, _) = await _service.BuildAsync(packageDir);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("Build script not found"));
    }

    [Test]
    public async Task BuildAsync_ScriptExists_ExecutesPowershell()
    {
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "mypackage");
        Directory.CreateDirectory(packageDir);
        var scriptDir = Path.Combine(_tempDirectory.DirectoryPath, "eng", "scripts");
        Directory.CreateDirectory(scriptDir);
        File.WriteAllText(Path.Combine(scriptDir, "build-sdk.ps1"), "# build script");

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        _mockPowershellHelper
            .Setup(x => x.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Build succeeded")] });

        var (success, errorMessage, _) = await _service.BuildAsync(packageDir);

        Assert.That(success, Is.True);
        Assert.That(errorMessage, Is.Null);
        _mockPowershellHelper.Verify(x => x.Run(
            It.Is<PowershellOptions>(o =>
                o.Args.Contains("-PackagePath") &&
                o.Args.Contains(packageDir)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task BuildAsync_ScriptFails_ReturnsFailure()
    {
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "mypackage");
        Directory.CreateDirectory(packageDir);
        var scriptDir = Path.Combine(_tempDirectory.DirectoryPath, "eng", "scripts");
        Directory.CreateDirectory(scriptDir);
        File.WriteAllText(Path.Combine(scriptDir, "build-sdk.ps1"), "# build script");

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        _mockPowershellHelper
            .Setup(x => x.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardOutput, "compilation error")] });

        var (success, errorMessage, _) = await _service.BuildAsync(packageDir);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("Build failed with exit code 1"));
        Assert.That(errorMessage, Does.Contain("compilation error"));
    }

    [Test]
    public async Task BuildAsync_DiscoverRepoRootFails_ReturnsFailure()
    {
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "mypackage");
        Directory.CreateDirectory(packageDir);

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var (success, errorMessage, _) = await _service.BuildAsync(packageDir);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("Failed to discover local sdk repo"));
    }

    [Test]
    public async Task BuildAsync_DoesNotUseSpecGenSdkConfig()
    {
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "mypackage");
        Directory.CreateDirectory(packageDir);
        var scriptDir = Path.Combine(_tempDirectory.DirectoryPath, "eng", "scripts");
        Directory.CreateDirectory(scriptDir);
        File.WriteAllText(Path.Combine(scriptDir, "build-sdk.ps1"), "# build script");

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        _mockPowershellHelper
            .Setup(x => x.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "ok")] });

        await _service.BuildAsync(packageDir);

        _mockSpecGenSdkConfigHelper.Verify(
            x => x.GetConfigurationAsync(It.IsAny<string>(), It.IsAny<SpecGenSdkConfigType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GetPackageInfo Tests

    [Test]
    public async Task GetPackageInfo_ValidCargoToml_ReturnsCorrectInfo()
    {
        // Arrange - create sdk/core/azure_core/Cargo.toml
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "core", "azure_core");
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, "Cargo.toml"), "[package]\nname = \"azure_core\"\n");

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        SetupCargoMetadata("azure_core", "0.34.0");

        // Act
        var info = await _service.GetPackageInfo(packageDir);

        // Assert
        Assert.That(info.PackageName, Is.EqualTo("azure_core"));
        Assert.That(info.PackageVersion, Is.EqualTo("0.34.0"));
        Assert.That(info.Language, Is.EqualTo(SdkLanguage.Rust));
        Assert.That(info.SdkTypeString, Is.EqualTo("client"));
        Assert.That(info.IsNewSdk, Is.True);
        Assert.That(info.ArtifactName, Is.EqualTo("azure_core"));
        Assert.That(info.ServiceName, Is.EqualTo("core"));
        Assert.That((string)info.ServiceDirectory!, Does.Contain("core"));
    }

    [Test]
    public async Task GetPackageInfo_MgmtPackage_ReturnsMgmtSdkType()
    {
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "compute", "azure_resourcemanager_compute");
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, "Cargo.toml"), "[package]\nname = \"azure_resourcemanager_compute\"\n");

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        SetupCargoMetadata("azure_resourcemanager_compute", "0.1.0");

        var info = await _service.GetPackageInfo(packageDir);

        Assert.That(info.PackageName, Is.EqualTo("azure_resourcemanager_compute"));
        Assert.That(info.SdkTypeString, Is.EqualTo("mgmt"));
    }

    [Test]
    public async Task GetPackageInfo_NoCargoToml_ReturnsEmptyPackageInfo()
    {
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "core", "missing_crate");
        Directory.CreateDirectory(packageDir);

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        var info = await _service.GetPackageInfo(packageDir);

        Assert.That(info.PackageName, Is.Null);
        Assert.That(info.PackageVersion, Is.Null);
        Assert.That(info.Language, Is.EqualTo(SdkLanguage.Rust));
        // cargo metadata should NOT be called when Cargo.toml is missing
        _mockProcessHelper.Verify(
            x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task GetPackageInfo_WithReadmeAndChangelog_SetsPathsCorrectly()
    {
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "storage", "azure_storage_blob");
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, "Cargo.toml"), "[package]\nname = \"azure_storage_blob\"\n");
        File.WriteAllText(Path.Combine(packageDir, "README.md"), "# Azure Storage Blob");
        File.WriteAllText(Path.Combine(packageDir, "CHANGELOG.md"), "## 0.2.0\n- Initial release");

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        SetupCargoMetadata("azure_storage_blob", "0.2.0");

        var info = await _service.GetPackageInfo(packageDir);

        Assert.That((string)info.ReadMePath, Does.Contain("README.md"));
        Assert.That((string)info.ChangeLogPath, Does.Contain("CHANGELOG.md"));
    }

    [Test]
    public async Task GetPackageInfo_CargoMetadataFails_ReturnsEmptyPackageInfo()
    {
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "core", "azure_core_macros");
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, "Cargo.toml"), "[package]\nname = \"azure_core_macros\"\nversion.workspace = true\n");

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        // Simulate cargo failing (e.g., workspace root not found)
        _mockProcessHelper
            .Setup(x => x.Run(It.Is<ProcessOptions>(o => o.Args.Contains("metadata")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 101, OutputDetails = [(StdioLevel.StandardError, "error: failed to parse manifest")] });

        var info = await _service.GetPackageInfo(packageDir);

        Assert.That(info.PackageName, Is.Null);
        Assert.That(info.PackageVersion, Is.Null);
    }

    #endregion

    #region GetPackageInfo - cargo metadata edge cases

    [Test]
    public async Task GetPackageInfo_PreReleaseVersion_ExtractsCorrectly()
    {
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "core", "azure_core");
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, "Cargo.toml"), "[package]\nname = \"azure_core\"\n");

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        SetupCargoMetadata("azure_core", "1.2.3-beta.1");

        var info = await _service.GetPackageInfo(packageDir);

        Assert.That(info.PackageVersion, Is.EqualTo("1.2.3-beta.1"));
    }

    [Test]
    public async Task GetPackageInfo_CargoOutputWithExtraFields_ParsesCorrectly()
    {
        // cargo metadata returns many fields; ensure we extract just name and version
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "test", "my_crate");
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, "Cargo.toml"), "[package]\nname = \"my_crate\"\n");

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        var fullJson = JsonSerializer.Serialize(new
        {
            packages = new[] { new
            {
                name = "my_crate",
                version = "0.1.0",
                id = "my_crate 0.1.0 (path+file:///tmp/sdk/test/my_crate)",
                license = "MIT",
                description = "A test crate",
                edition = "2021",
                dependencies = new[] { new { name = "serde", version = "1.0" } }
            }}
        });

        _mockProcessHelper
            .Setup(x => x.Run(It.Is<ProcessOptions>(o => o.Args.Contains("metadata")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, fullJson)] });

        var info = await _service.GetPackageInfo(packageDir);

        Assert.That(info.PackageName, Is.EqualTo("my_crate"));
        Assert.That(info.PackageVersion, Is.EqualTo("0.1.0"));
    }

    #endregion
}
