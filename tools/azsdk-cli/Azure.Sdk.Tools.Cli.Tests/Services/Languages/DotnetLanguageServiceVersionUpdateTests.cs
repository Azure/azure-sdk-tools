// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
internal class DotnetLanguageServiceVersionUpdateTests
{
    private Mock<IProcessHelper> _processHelperMock = null!;
    private Mock<IPowershellHelper> _powershellHelperMock = null!;
    private Mock<IGitHelper> _gitHelperMock = null!;
    private Mock<IPackageInfoHelper> _packageInfoHelperMock = null!;
    private DotnetLanguageService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _processHelperMock = new Mock<IProcessHelper>();
        _powershellHelperMock = new Mock<IPowershellHelper>();
        _gitHelperMock = new Mock<IGitHelper>();
        _packageInfoHelperMock = new Mock<IPackageInfoHelper>();

        _service = new DotnetLanguageService(
            _processHelperMock.Object,
            _powershellHelperMock.Object,
            Mock.Of<ICopilotAgentRunner>(),
            _gitHelperMock.Object,
            new TestLogger<DotnetLanguageService>(),
            Mock.Of<ICommonValidationHelpers>(),
            _packageInfoHelperMock.Object,
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());
    }

    #region UpdatePackageVersionInFilesAsync Tests

    [Test]
    public async Task UpdateVersionInFiles_ReturnsFailure_WhenNoCsprojFound()
    {
        // Arrange - src/ directory exists but contains no .csproj files
        using var tempDir = TempDirectory.Create("dotnet-version-no-csproj");
        Directory.CreateDirectory(Path.Combine(tempDir.DirectoryPath, "src"));

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain(".csproj"));
    }

    [Test]
    public async Task UpdateVersionInFiles_ReturnsFailure_WhenSrcDirMissing()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("dotnet-version-no-src");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
    }

    [Test]
    public async Task UpdateVersionInFiles_FallsBackToDirectUpdate_WhenRepoRootNotFound()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("dotnet-version-no-repo");
        var srcDir = Path.Combine(tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(srcDir);

        var csprojPath = Path.Combine(srcDir, "TestPackage.csproj");
        await File.WriteAllTextAsync(csprojPath, CreateSampleCsproj("1.0.0"));

        _packageInfoHelperMock
            .Setup(p => p.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Not a git repo"));

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(csprojPath);
        Assert.That(updatedContent, Does.Contain("<Version>2.0.0</Version>"));
    }

    [Test]
    public async Task UpdateVersionInFiles_DirectUpdate_NoVersionElement_ReturnsFailure()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("dotnet-version-no-element");
        var srcDir = Path.Combine(tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(srcDir);

        var csprojContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(
            Path.Combine(srcDir, "TestPackage.csproj"), csprojContent);

        _packageInfoHelperMock
            .Setup(p => p.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string.Empty, string.Empty, tempDir.DirectoryPath));

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("<Version>"));
    }

    [Test]
    public async Task UpdateVersionInFiles_DirectUpdate_VersionAlreadySet_ReportsSuccess()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("dotnet-version-already-set");
        var srcDir = Path.Combine(tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(srcDir);

        var csprojPath = Path.Combine(srcDir, "TestPackage.csproj");
        await File.WriteAllTextAsync(csprojPath, CreateSampleCsproj("2.0.0"));

        _packageInfoHelperMock
            .Setup(p => p.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string.Empty, string.Empty, tempDir.DirectoryPath));

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Message, Does.Contain("already set"));
    }

    [Test]
    public async Task UpdateVersionInFiles_DirectUpdate_UpdatesBetaVersion()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("dotnet-version-beta");
        var srcDir = Path.Combine(tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(srcDir);

        var csprojPath = Path.Combine(srcDir, "TestPackage.csproj");
        await File.WriteAllTextAsync(csprojPath, CreateSampleCsproj("1.0.0"));

        _packageInfoHelperMock
            .Setup(p => p.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string.Empty, string.Empty, tempDir.DirectoryPath));

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "1.1.0-beta.1", "beta");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(csprojPath);
        Assert.That(updatedContent, Does.Contain("<Version>1.1.0-beta.1</Version>"));
    }

    #endregion

    #region Release Type Validation Tests

    [Test]
    public async Task UpdateVersion_ReturnsFailure_WhenStableReleaseTypeWithPrereleaseVersion()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("dotnet-version-mismatch-stable");
        var packagePath = Path.Combine(tempDir.DirectoryPath, "sdk", "test", "Azure.Test");
        var srcDir = Path.Combine(packagePath, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "Azure.Test.csproj"), CreateSampleCsproj("1.0.0"));

        _packageInfoHelperMock
            .Setup(p => p.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tempDir.DirectoryPath, "test/Azure.Test", packagePath));

        // Act — stable release type with beta version
        var result = await _service.UpdateVersionAsync(
            packagePath, "stable", "1.0.0-beta.1", null, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("stable"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("pre-release"));
    }

    [Test]
    public async Task UpdateVersion_ReturnsFailure_WhenGAVersionWithoutExplicitStableReleaseType()
    {
        // Arrange — user passes a GA version but doesn't confirm with --release-type stable
        using var tempDir = TempDirectory.Create("dotnet-version-ga-no-confirm");
        var packagePath = Path.Combine(tempDir.DirectoryPath, "sdk", "test", "Azure.Test");
        var srcDir = Path.Combine(packagePath, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "Azure.Test.csproj"), CreateSampleCsproj("1.0.0"));

        _packageInfoHelperMock
            .Setup(p => p.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tempDir.DirectoryPath, "test/Azure.Test", packagePath));

        // Act — GA version with default release type "beta"
        var result = await _service.UpdateVersionAsync(
            packagePath, "beta", "1.0.0", null, CancellationToken.None);

        // Assert — should require explicit --release-type stable
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("stable (GA)"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("explicit confirmation"));
    }

    [Test]
    public async Task UpdateVersion_ReturnsFailure_WhenInferredGAVersionWithDefaultBetaReleaseType()
    {
        // Arrange — stable package, version=null (inferred), releaseType="beta" (tool default)
        using var tempDir = TempDirectory.Create("dotnet-version-inferred-ga");
        var packagePath = tempDir.DirectoryPath;
        var srcDir = Path.Combine(packagePath, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(
            Path.Combine(srcDir, "TestPackage.csproj"),
            CreateSampleCsproj("1.0.0"));

        // Mock GetPackageInfo to return stable version
        _processHelperMock
            .Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult(0, """{"TargetResults":{"GetPackageInfo":{"Items":[{"Identity":"'path' 'test' 'TestPackage' '1.0.0' 'client'"}]}}}"""));

        _packageInfoHelperMock
            .Setup(p => p.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string.Empty, string.Empty, packagePath));

        // Act — version=null (inferred as "1.0.0"), releaseType="beta" (tool default)
        var result = await _service.UpdateVersionAsync(
            packagePath, "beta", null, "2025-06-15", CancellationToken.None);

        // Assert — should require explicit --release-type stable
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("stable (GA)"));
    }

    #endregion

    #region Version Promotion Tests

    [Test]
    public async Task UpdateVersion_PromotesBetaToStable_RenamesChangelogEntry()
    {
        // Arrange — changelog has a beta entry, user wants to promote to stable
        using var tempDir = TempDirectory.Create("dotnet-version-promote");
        var packagePath = tempDir.DirectoryPath;
        var srcDir = Path.Combine(packagePath, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(
            Path.Combine(srcDir, "TestPackage.csproj"),
            CreateSampleCsproj("12.28.0-beta.2"));

        var changelogContent = """
            # Release History

            ## 12.28.0-beta.2 (Unreleased)

            ### Features Added
            - some random feature
            - another random feature

            ### Breaking Changes

            ### Bugs Fixed

            ### Other Changes

            """;
        var changelogPath = Path.Combine(packagePath, "CHANGELOG.md");
        await File.WriteAllTextAsync(changelogPath, changelogContent);

        _packageInfoHelperMock
            .Setup(p => p.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string.Empty, string.Empty, packagePath));

        // This test exercises the full changelog flow, so use a real ChangelogHelper
        var realChangelogHelper = new ChangelogHelper(new TestLogger<ChangelogHelper>());
        var serviceWithRealChangelog = new DotnetLanguageService(
            _processHelperMock.Object,
            _powershellHelperMock.Object,
            Mock.Of<ICopilotAgentRunner>(),
            _gitHelperMock.Object,
            new TestLogger<DotnetLanguageService>(),
            Mock.Of<ICommonValidationHelpers>(),
            _packageInfoHelperMock.Object,
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            realChangelogHelper);

        // Act — promote from 12.28.0-beta.2 to stable 12.28.0
        var result = await serviceWithRealChangelog.UpdateVersionAsync(
            packagePath, "stable", "12.28.0", "2025-06-15", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));

        // Verify changelog was updated: entry title should now be "## 12.28.0 (2025-06-15)"
        var updatedChangelog = await File.ReadAllTextAsync(changelogPath);
        Assert.That(updatedChangelog, Does.Contain("## 12.28.0 (2025-06-15)"));
        Assert.That(updatedChangelog, Does.Not.Contain("12.28.0-beta.2"));

        // Verify features content was preserved
        Assert.That(updatedChangelog, Does.Contain("some random feature"));
        Assert.That(updatedChangelog, Does.Contain("another random feature"));

        // Verify csproj was updated
        var updatedCsproj = await File.ReadAllTextAsync(Path.Combine(srcDir, "TestPackage.csproj"));
        Assert.That(updatedCsproj, Does.Contain("<Version>12.28.0</Version>"));
    }

    #endregion

    #region Helpers

    private static string CreateSampleCsproj(string version) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>netstandard2.0</TargetFramework>
            <Version>{version}</Version>
            <Description>Test package</Description>
          </PropertyGroup>
        </Project>
        """;

    private static ProcessResult CreateProcessResult(int exitCode, string output)
    {
        var result = new ProcessResult { ExitCode = exitCode };
        result.AppendStdout(output);
        return result;
    }

    /// <summary>
    /// Uses reflection to call the protected UpdatePackageVersionInFilesAsync method for testing.
    /// </summary>
    private async Task<PackageOperationResponse> InvokeUpdatePackageVersionInFilesAsync(
        string packagePath, string version, string? releaseType)
    {
        var method = typeof(DotnetLanguageService)
            .GetMethod("UpdatePackageVersionInFilesAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.That(method, Is.Not.Null, "UpdatePackageVersionInFilesAsync method not found");

        var task = (Task<PackageOperationResponse>)method!.Invoke(
            _service,
            [packagePath, version, releaseType, CancellationToken.None])!;

        return await task;
    }

    #endregion
}
