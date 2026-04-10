// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
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

    [Test]
    public void Language_ReturnsRust()
    {
        Assert.That(_service.Language, Is.EqualTo(SdkLanguage.Rust));
    }

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
        // Arrange - repo root exists but no build script
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "mypackage");
        Directory.CreateDirectory(packageDir);

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        // Act
        var (success, errorMessage, _) = await _service.BuildAsync(packageDir);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("Build script not found"));
    }

    [Test]
    public async Task BuildAsync_ScriptExists_ExecutesPowershell()
    {
        // Arrange - create the package dir and the build script
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

        // Act
        var (success, errorMessage, _) = await _service.BuildAsync(packageDir);

        // Assert
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
        // Arrange
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

        // Act
        var (success, errorMessage, _) = await _service.BuildAsync(packageDir);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("Build failed with exit code 1"));
        Assert.That(errorMessage, Does.Contain("compilation error"));
    }

    [Test]
    public async Task BuildAsync_DiscoverRepoRootFails_ReturnsFailure()
    {
        // Arrange
        var packageDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "mypackage");
        Directory.CreateDirectory(packageDir);

        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        // Act
        var (success, errorMessage, _) = await _service.BuildAsync(packageDir);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("Failed to discover local sdk repo"));
    }

    [Test]
    public async Task BuildAsync_DoesNotUseSpecGenSdkConfig()
    {
        // Arrange - verify Rust doesn't call specGenSdkConfigHelper
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

        // Act
        await _service.BuildAsync(packageDir);

        // Assert - specGenSdkConfigHelper should never be called for Rust builds
        _mockSpecGenSdkConfigHelper.Verify(
            x => x.GetConfigurationAsync(It.IsAny<string>(), It.IsAny<SpecGenSdkConfigType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
