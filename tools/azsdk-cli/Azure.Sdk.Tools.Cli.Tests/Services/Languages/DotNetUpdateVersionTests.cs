// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
internal class DotNetUpdateVersionTests
{
    private Mock<IProcessHelper> _processHelperMock = null!;
    private Mock<IGitHelper> _gitHelperMock = null!;
    private Mock<IPowershellHelper> _powerShellHelperMock = null!;
    private Mock<ICommonValidationHelpers> _commonValidationHelperMock = null!;
    private Mock<IChangelogHelper> _changelogHelperMock = null!;
    private DotnetLanguageService _languageService = null!;
    private string _tempDir = null!;
    private string _packagePath = null!;

    [SetUp]
    public void SetUp()
    {
        _processHelperMock = new Mock<IProcessHelper>();
        _gitHelperMock = new Mock<IGitHelper>();
        _powerShellHelperMock = new Mock<IPowershellHelper>();
        _commonValidationHelperMock = new Mock<ICommonValidationHelpers>();
        _changelogHelperMock = new Mock<IChangelogHelper>();

        _languageService = new DotnetLanguageService(
            _processHelperMock.Object,
            _powerShellHelperMock.Object,
            _gitHelperMock.Object,
            NullLogger<DotnetLanguageService>.Instance,
            _commonValidationHelperMock.Object,
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            _changelogHelperMock.Object);

        // Create a temporary directory structure
        _tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-version-test-{Guid.NewGuid()}");
        _packagePath = Path.Combine(_tempDir, "sdk", "test", "Azure.Test.Package");
        Directory.CreateDirectory(Path.Combine(_packagePath, "src"));
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private void SetupMockGetPackageInfo(string packageName, string version)
    {
        // Setup git helper
        _gitHelperMock
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDir);

        // Mock the MSBuild GetPackageInfo call - use proper format with single quotes
        var jsonOutput = $@"{{
  ""TargetResults"": {{
    ""GetPackageInfo"": {{
      ""Items"": [
        {{
          ""Identity"": ""'{_packagePath}' 'test' '{packageName}' '{version}' 'client' 'true' 'bin' 'false'""
        }}
      ]
    }}
  }}
}}";

        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout(jsonOutput);

        _processHelperMock
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(p => p.Command == "dotnet" && 
                    p.Args != null && 
                    p.Args.Any(a => a.Contains("-getTargetResult:GetPackageInfo"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);
    }

    private void SetupChangelogHelperSuccess()
    {
        _changelogHelperMock
            .Setup(x => x.GetChangelogPath(It.IsAny<string>()))
            .Returns(Path.Combine(_packagePath, "CHANGELOG.md"));
        _changelogHelperMock
            .Setup(x => x.HasEntryForVersion(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        _changelogHelperMock
            .Setup(x => x.UpdateReleaseDate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(ChangelogUpdateResult.CreateSuccess("Changelog updated"));
    }

    private void CreateCsprojFile(string packageName, string version, string? apiCompatVersion = null)
    {
        var apiCompatElement = apiCompatVersion != null 
            ? $"\n    <ApiCompatVersion>{apiCompatVersion}</ApiCompatVersion>" 
            : "";

        var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <Version>{version}</Version>{apiCompatElement}
  </PropertyGroup>
</Project>";

        var csprojPath = Path.Combine(_packagePath, "src", $"{packageName}.csproj");
        File.WriteAllText(csprojPath, csprojContent);
    }

    [Test]
    public async Task UpdateVersionAsync_NoCsprojFile_ReturnsPartialSuccess()
    {
        // Arrange
        SetupMockGetPackageInfo("Azure.Test.Package", "1.0.0");
        SetupChangelogHelperSuccess();
        // Don't create a .csproj file

        // Act
        var result = await _languageService.UpdateVersionAsync(_packagePath, "stable", "1.1.0", "2025-01-30", CancellationToken.None);

        // Assert - changelog updated but no csproj = partial success
        Assert.Multiple(() =>
        {
            Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
            Assert.That(result.Result, Is.EqualTo("partial"));
            Assert.That(result.Message, Does.Contain("No .csproj file found"));
        });
    }

    [Test]
    public async Task UpdateVersionAsync_NoVersionElement_ReturnsPartialSuccess()
    {
        // Arrange
        SetupMockGetPackageInfo("Azure.Test.Package", "1.0.0");
        SetupChangelogHelperSuccess();

        // Create a .csproj without Version element
        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
</Project>";
        var csprojPath = Path.Combine(_packagePath, "src", "Azure.Test.Package.csproj");
        File.WriteAllText(csprojPath, csprojContent);

        // Act
        var result = await _languageService.UpdateVersionAsync(_packagePath, "stable", "1.1.0", "2025-01-30", CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
            Assert.That(result.Result, Is.EqualTo("partial")); // Changelog updated but version file failed
            Assert.That(result.Message, Does.Contain("No <Version> element found"));
        });
    }

    [Test]
    public async Task UpdateVersionAsync_PrereleaseToPrerelease_UpdatesVersionOnly()
    {
        // Arrange
        const string packageName = "Azure.Test.Package";
        const string currentVersion = "1.0.0-beta.1";
        const string newVersion = "1.0.0-beta.2";

        SetupMockGetPackageInfo(packageName, currentVersion);
        SetupChangelogHelperSuccess();
        CreateCsprojFile(packageName, currentVersion);

        // Act
        var result = await _languageService.UpdateVersionAsync(_packagePath, "beta", newVersion, "2025-01-30", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));

        // Verify the .csproj was updated
        var csprojPath = Path.Combine(_packagePath, "src", $"{packageName}.csproj");
        var csprojContent = File.ReadAllText(csprojPath);
        Assert.That(csprojContent, Does.Contain($"<Version>{newVersion}</Version>"));
        Assert.That(csprojContent, Does.Not.Contain("<ApiCompatVersion>")); // No ApiCompatVersion for prerelease to prerelease
    }

    [Test]
    public async Task UpdateVersionAsync_GAToNewVersion_AddsApiCompatVersion()
    {
        // Arrange
        const string packageName = "Azure.Test.Package";
        const string currentVersion = "1.0.0";
        const string newVersion = "1.1.0";

        SetupMockGetPackageInfo(packageName, currentVersion);
        SetupChangelogHelperSuccess();
        CreateCsprojFile(packageName, currentVersion);

        // Act
        var result = await _languageService.UpdateVersionAsync(_packagePath, "stable", newVersion, "2025-01-30", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));

        // Verify the .csproj was updated with ApiCompatVersion
        var csprojPath = Path.Combine(_packagePath, "src", $"{packageName}.csproj");
        var csprojContent = File.ReadAllText(csprojPath);
        Assert.Multiple(() =>
        {
            Assert.That(csprojContent, Does.Contain($"<Version>{newVersion}</Version>"));
            Assert.That(csprojContent, Does.Contain($"<ApiCompatVersion>{currentVersion}</ApiCompatVersion>"));
            Assert.That(csprojContent, Does.Contain("ApiCompatVersion is managed automatically"));
        });
    }

    [Test]
    public async Task UpdateVersionAsync_GAToPrerelease_AddsApiCompatVersion()
    {
        // Arrange
        const string packageName = "Azure.Test.Package";
        const string currentVersion = "1.0.0";
        const string newVersion = "2.0.0-beta.1";

        SetupMockGetPackageInfo(packageName, currentVersion);
        SetupChangelogHelperSuccess();
        CreateCsprojFile(packageName, currentVersion);

        // Act
        var result = await _languageService.UpdateVersionAsync(_packagePath, "beta", newVersion, "2025-01-30", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));

        // Verify the .csproj was updated with ApiCompatVersion
        var csprojPath = Path.Combine(_packagePath, "src", $"{packageName}.csproj");
        var csprojContent = File.ReadAllText(csprojPath);
        Assert.Multiple(() =>
        {
            Assert.That(csprojContent, Does.Contain($"<Version>{newVersion}</Version>"));
            Assert.That(csprojContent, Does.Contain($"<ApiCompatVersion>{currentVersion}</ApiCompatVersion>"));
        });
    }

    [Test]
    public async Task UpdateVersionAsync_SameVersion_DoesNotAddApiCompatVersion()
    {
        // Arrange - when current and new version are the same, no ApiCompatVersion should be added
        const string packageName = "Azure.Test.Package";
        const string version = "1.0.0";

        SetupMockGetPackageInfo(packageName, version);
        SetupChangelogHelperSuccess();
        CreateCsprojFile(packageName, version);

        // Act
        var result = await _languageService.UpdateVersionAsync(_packagePath, "stable", version, "2025-01-30", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));

        // Verify no ApiCompatVersion was added (same version)
        var csprojPath = Path.Combine(_packagePath, "src", $"{packageName}.csproj");
        var csprojContent = File.ReadAllText(csprojPath);
        Assert.That(csprojContent, Does.Not.Contain("<ApiCompatVersion>"));
    }

    [Test]
    public async Task UpdateVersionAsync_ExistingApiCompatVersion_UpdatesIt()
    {
        // Arrange
        const string packageName = "Azure.Test.Package";
        const string currentVersion = "2.0.0";
        const string existingApiCompatVersion = "1.0.0";
        const string newVersion = "2.1.0";

        SetupMockGetPackageInfo(packageName, currentVersion);
        SetupChangelogHelperSuccess();
        CreateCsprojFile(packageName, currentVersion, existingApiCompatVersion);

        // Act
        var result = await _languageService.UpdateVersionAsync(_packagePath, "stable", newVersion, "2025-01-30", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));

        // Verify the ApiCompatVersion was updated to the previous GA version
        var csprojPath = Path.Combine(_packagePath, "src", $"{packageName}.csproj");
        var csprojContent = File.ReadAllText(csprojPath);
        Assert.Multiple(() =>
        {
            Assert.That(csprojContent, Does.Contain($"<Version>{newVersion}</Version>"));
            Assert.That(csprojContent, Does.Contain($"<ApiCompatVersion>{currentVersion}</ApiCompatVersion>"));
        });
    }

    [Test]
    public async Task UpdateVersionAsync_MultipleCsprojFiles_ReturnsPartialSuccess()
    {
        // Arrange
        const string packageName = "Azure.Test.Package";
        const string currentVersion = "1.0.0";
        const string newVersion = "1.1.0";

        SetupMockGetPackageInfo(packageName, currentVersion);
        SetupChangelogHelperSuccess();

        // Create multiple .csproj files
        var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <Version>{currentVersion}</Version>
  </PropertyGroup>
</Project>";

        File.WriteAllText(Path.Combine(_packagePath, "src", $"{packageName}.csproj"), csprojContent);
        File.WriteAllText(Path.Combine(_packagePath, "src", "Another.csproj"), csprojContent);

        // Act
        var result = await _languageService.UpdateVersionAsync(_packagePath, "stable", newVersion, "2025-01-30", CancellationToken.None);

        // Assert - should succeed on changelog but fail on version file
        Assert.Multiple(() =>
        {
            Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded)); // Changelog updated successfully
            Assert.That(result.Result, Is.EqualTo("partial")); // But version file update failed
            Assert.That(result.Message, Does.Contain("Multiple .csproj files found"));
            Assert.That(result.NextSteps, Does.Contain("Remove the extra .csproj file(s) - only one .csproj file should exist in the src directory"));
        });
    }

    [Test]
    public async Task UpdateVersionAsync_NoSrcDirectory_ReturnsPartialSuccess()
    {
        // Arrange
        SetupMockGetPackageInfo("Azure.Test.Package", "1.0.0");
        SetupChangelogHelperSuccess();

        // Delete the src directory
        Directory.Delete(Path.Combine(_packagePath, "src"), recursive: true);

        // Act
        var result = await _languageService.UpdateVersionAsync(_packagePath, "stable", "1.1.0", "2025-01-30", CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
            Assert.That(result.Result, Is.EqualTo("partial"));
            Assert.That(result.Message, Does.Contain("Source directory not found"));
        });
    }

    [Test]
    public async Task UpdateVersionAsync_NoChangelogEntry_ReturnsFailure()
    {
        // Arrange
        const string packageName = "Azure.Test.Package";
        SetupMockGetPackageInfo(packageName, "1.0.0");
        CreateCsprojFile(packageName, "1.0.0");

        // Setup changelog helper to indicate no entry for version
        _changelogHelperMock
            .Setup(x => x.GetChangelogPath(It.IsAny<string>()))
            .Returns(Path.Combine(_packagePath, "CHANGELOG.md"));
        _changelogHelperMock
            .Setup(x => x.HasEntryForVersion(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _languageService.UpdateVersionAsync(_packagePath, "stable", "1.1.0", "2025-01-30", CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
            Assert.That(result.ResponseErrors, Does.Contain("No changelog entry found for version 1.1.0."));
        });
    }

    [Test]
    public async Task UpdateVersionAsync_NoChangelog_ReturnsFailure()
    {
        // Arrange
        const string packageName = "Azure.Test.Package";
        SetupMockGetPackageInfo(packageName, "1.0.0");
        CreateCsprojFile(packageName, "1.0.0");

        // Setup changelog helper to indicate no changelog
        _changelogHelperMock
            .Setup(x => x.GetChangelogPath(It.IsAny<string>()))
            .Returns((string?)null);

        // Act
        var result = await _languageService.UpdateVersionAsync(_packagePath, "stable", "1.1.0", "2025-01-30", CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
            Assert.That(result.ResponseErrors, Does.Contain("No CHANGELOG.md found in package directory."));
        });
    }
}
