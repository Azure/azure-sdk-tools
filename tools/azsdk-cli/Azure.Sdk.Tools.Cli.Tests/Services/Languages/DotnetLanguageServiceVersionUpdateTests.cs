// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    private DotnetLanguageService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _processHelperMock = new Mock<IProcessHelper>();
        _powershellHelperMock = new Mock<IPowershellHelper>();
        _gitHelperMock = new Mock<IGitHelper>();

        _service = new DotnetLanguageService(
            _processHelperMock.Object,
            _powershellHelperMock.Object,
            _gitHelperMock.Object,
            new TestLogger<DotnetLanguageService>(),
            Mock.Of<ICommonValidationHelpers>(),
            Mock.Of<IPackageInfoHelper>(),
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());
    }

    #region UpdatePackageVersionInFilesAsync Tests

    [Test]
    public async Task UpdateVersionInFiles_ReturnsFailure_WhenNoCsprojFound()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("dotnet-version-no-csproj");

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
    public async Task UpdateVersionInFiles_UsesScriptWhenAvailable()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("dotnet-version-script");
        var repoRoot = tempDir.DirectoryPath;

        // Create package structure: sdk/<service>/<package>/src/<package>.csproj
        var packagePath = Path.Combine(repoRoot, "sdk", "testservice", "Azure.Test.Package");
        var srcDir = Path.Combine(packagePath, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(
            Path.Combine(srcDir, "Azure.Test.Package.csproj"),
            CreateSampleCsproj("1.0.0-beta.1"));

        // Create the versioning script
        var scriptDir = Path.Combine(repoRoot, "eng", "scripts");
        Directory.CreateDirectory(scriptDir);
        await File.WriteAllTextAsync(
            Path.Combine(scriptDir, "Update-PkgVersion.ps1"),
            "# mock script");

        _gitHelperMock
            .Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoRoot);

        _powershellHelperMock
            .Setup(p => p.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult(0, "Version updated"));

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            packagePath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Message, Does.Contain("Update-PkgVersion.ps1"));

        // Verify the script was called with correct arguments
        _powershellHelperMock.Verify(
            p => p.Run(
                It.Is<PowershellOptions>(opts =>
                    opts.ScriptPath!.Contains("Update-PkgVersion.ps1")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task UpdateVersionInFiles_ScriptFailure_ReturnsFailure()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("dotnet-version-script-fail");
        var repoRoot = tempDir.DirectoryPath;

        var packagePath = Path.Combine(repoRoot, "sdk", "testservice", "Azure.Test.Package");
        var srcDir = Path.Combine(packagePath, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(
            Path.Combine(srcDir, "Azure.Test.Package.csproj"),
            CreateSampleCsproj("1.0.0"));

        var scriptDir = Path.Combine(repoRoot, "eng", "scripts");
        Directory.CreateDirectory(scriptDir);
        await File.WriteAllTextAsync(
            Path.Combine(scriptDir, "Update-PkgVersion.ps1"),
            "# mock script");

        _gitHelperMock
            .Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoRoot);

        _powershellHelperMock
            .Setup(p => p.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult(1, "Script error"));

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            packagePath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("Update-PkgVersion.ps1"));
    }

    [Test]
    public async Task UpdateVersionInFiles_FallsBackToDirectUpdate_WhenScriptNotFound()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("dotnet-version-direct");
        var repoRoot = tempDir.DirectoryPath;

        var packagePath = Path.Combine(repoRoot, "sdk", "testservice", "Azure.Test.Package");
        var srcDir = Path.Combine(packagePath, "src");
        Directory.CreateDirectory(srcDir);

        var csprojPath = Path.Combine(srcDir, "Azure.Test.Package.csproj");
        await File.WriteAllTextAsync(csprojPath, CreateSampleCsproj("1.0.0-beta.1"));

        // No script exists, repo root exists but no eng/scripts
        _gitHelperMock
            .Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoRoot);

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            packagePath, "1.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(csprojPath);
        Assert.That(updatedContent, Does.Contain("<Version>1.0.0</Version>"));
        Assert.That(updatedContent, Does.Not.Contain("1.0.0-beta.1"));
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

        _gitHelperMock
            .Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

        _gitHelperMock
            .Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

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

        _gitHelperMock
            .Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

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

        _gitHelperMock
            .Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "1.1.0-beta.1", "beta");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(csprojPath);
        Assert.That(updatedContent, Does.Contain("<Version>1.1.0-beta.1</Version>"));
    }

    [Test]
    public async Task UpdateVersionInFiles_ScriptCalledWithCorrectServiceAndPackage()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("dotnet-version-args");
        var repoRoot = tempDir.DirectoryPath;

        var packagePath = Path.Combine(repoRoot, "sdk", "storage", "Azure.Storage.Blobs");
        var srcDir = Path.Combine(packagePath, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(
            Path.Combine(srcDir, "Azure.Storage.Blobs.csproj"),
            CreateSampleCsproj("12.0.0"));

        var scriptDir = Path.Combine(repoRoot, "eng", "scripts");
        Directory.CreateDirectory(scriptDir);
        await File.WriteAllTextAsync(
            Path.Combine(scriptDir, "Update-PkgVersion.ps1"), "# mock");

        _gitHelperMock
            .Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoRoot);

        PowershellOptions? capturedOptions = null;
        _powershellHelperMock
            .Setup(p => p.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()))
            .Callback<PowershellOptions, CancellationToken>((opts, _) => capturedOptions = opts)
            .ReturnsAsync(CreateProcessResult(0, "Done"));

        // Act
        await InvokeUpdatePackageVersionInFilesAsync(
            packagePath, "13.0.0", "stable");

        // Assert - verify correct arguments were passed
        Assert.That(capturedOptions, Is.Not.Null);
        Assert.That(capturedOptions!.ScriptPath, Does.Contain("Update-PkgVersion.ps1"));
    }

    [Test]
    public async Task UpdateVersionInFiles_NonStandardLayout_FallsBackToDirectUpdate()
    {
        // Arrange - package not under sdk/<service>/<package> layout
        using var tempDir = TempDirectory.Create("dotnet-version-nonstandard");
        var repoRoot = tempDir.DirectoryPath;

        var packagePath = Path.Combine(repoRoot, "custom", "MyPackage");
        var srcDir = Path.Combine(packagePath, "src");
        Directory.CreateDirectory(srcDir);

        var csprojPath = Path.Combine(srcDir, "MyPackage.csproj");
        await File.WriteAllTextAsync(csprojPath, CreateSampleCsproj("1.0.0"));

        // Script exists but layout is non-standard
        var scriptDir = Path.Combine(repoRoot, "eng", "scripts");
        Directory.CreateDirectory(scriptDir);
        await File.WriteAllTextAsync(
            Path.Combine(scriptDir, "Update-PkgVersion.ps1"), "# mock");

        _gitHelperMock
            .Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoRoot);

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            packagePath, "2.0.0", "stable");

        // Assert - should fall back to direct .csproj update
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(csprojPath);
        Assert.That(updatedContent, Does.Contain("<Version>2.0.0</Version>"));

        // Script should not have been called
        _powershellHelperMock.Verify(
            p => p.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
