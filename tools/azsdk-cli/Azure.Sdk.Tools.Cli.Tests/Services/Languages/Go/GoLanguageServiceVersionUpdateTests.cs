// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
internal class GoLanguageServiceVersionUpdateTests
{
    private GoLanguageService _languageService = null!;

    [SetUp]
    public void SetUp()
    {
        _languageService = new GoLanguageService(
            Mock.Of<IProcessHelper>(),
            Mock.Of<IPowershellHelper>(),
            Mock.Of<IGitHelper>(),
            NullLogger<GoLanguageService>.Instance,
            Mock.Of<ICommonValidationHelpers>(),
            Mock.Of<IPackageInfoHelper>(),
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());
    }

    #region UpdatePackageVersionInFilesAsync Tests

    [Test]
    public async Task UpdateVersionInFiles_UpdatesVersionGo_WhenFileExists()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("go-version-update-test");
        var versionGoPath = Path.Combine(tempDir.DirectoryPath, "version.go");
        await File.WriteAllTextAsync(versionGoPath,
            "package azblob\n\nconst Version = \"v1.0.0\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(versionGoPath);
        Assert.That(updatedContent, Does.Contain("\"v2.0.0\""));
        Assert.That(updatedContent, Does.Not.Contain("\"v1.0.0\""));
    }

    [Test]
    public async Task UpdateVersionInFiles_UpdatesConstantsGo_WhenFileExists()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("go-constants-update-test");
        var constantsGoPath = Path.Combine(tempDir.DirectoryPath, "constants.go");
        await File.WriteAllTextAsync(constantsGoPath,
            "package azblob\n\nconst ModuleVersion = \"v1.5.0\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "1.6.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(constantsGoPath);
        Assert.That(updatedContent, Does.Contain("\"v1.6.0\""));
        Assert.That(updatedContent, Does.Not.Contain("\"v1.5.0\""));
    }

    [Test]
    public async Task UpdateVersionInFiles_UpdatesVersionInSubdirectory()
    {
        // Arrange - version file in internal/ subdirectory (common Go pattern)
        using var tempDir = TempDirectory.Create("go-subdir-version-test");
        var internalDir = Path.Combine(tempDir.DirectoryPath, "internal");
        Directory.CreateDirectory(internalDir);
        var versionGoPath = Path.Combine(internalDir, "version.go");
        await File.WriteAllTextAsync(versionGoPath,
            "package internal\n\nconst Version = \"v1.2.3\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "1.3.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(versionGoPath);
        Assert.That(updatedContent, Does.Contain("\"v1.3.0\""));
    }

    [Test]
    public async Task UpdateVersionInFiles_PreservesVPrefix()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("go-v-prefix-test");
        var versionGoPath = Path.Combine(tempDir.DirectoryPath, "version.go");
        await File.WriteAllTextAsync(versionGoPath,
            "package azcore\n\nconst Version = \"v1.0.0-beta.1\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "1.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(versionGoPath);
        Assert.That(updatedContent, Does.Contain("\"v1.0.0\""));
        // Ensure the 'v' prefix is preserved
        Assert.That(updatedContent, Does.Not.Contain("\"1.0.0\""));
    }

    [Test]
    public async Task UpdateVersionInFiles_Succeeds_WhenVersionAlreadySet()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("go-version-already-set-test");
        var versionGoPath = Path.Combine(tempDir.DirectoryPath, "version.go");
        await File.WriteAllTextAsync(versionGoPath,
            "package azblob\n\nconst Version = \"v2.0.0\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var content = await File.ReadAllTextAsync(versionGoPath);
        Assert.That(content, Does.Contain("\"v2.0.0\""));
    }

    [Test]
    public async Task UpdateVersionInFiles_ReturnsFailure_WhenNoVersionFileFound()
    {
        // Arrange - directory with no version or constants files
        using var tempDir = TempDirectory.Create("go-no-version-file-test");
        await File.WriteAllTextAsync(Path.Combine(tempDir.DirectoryPath, "main.go"),
            "package main\n\nfunc main() {}\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
    }

    [Test]
    public async Task UpdateVersionInFiles_ReturnsFailure_WhenDirectoryIsEmpty()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("go-empty-dir-test");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
    }

    [Test]
    public async Task UpdateVersionInFiles_PreservesOtherFileContent()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("go-preserve-content-test");
        var versionGoPath = Path.Combine(tempDir.DirectoryPath, "constants.go");
        var originalContent = "package azblob\n\n// Some comment\nconst ServiceName = \"blob\"\nconst ModuleVersion = \"v1.0.0\"\nconst DefaultEndpoint = \"https://blob.core.windows.net\"\n";
        await File.WriteAllTextAsync(versionGoPath, originalContent);

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "1.1.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(versionGoPath);
        Assert.That(updatedContent, Does.Contain("\"v1.1.0\""));
        Assert.That(updatedContent, Does.Contain("ServiceName = \"blob\""));
        Assert.That(updatedContent, Does.Contain("DefaultEndpoint = \"https://blob.core.windows.net\""));
        Assert.That(updatedContent, Does.Contain("// Some comment"));
    }

    [Test]
    public async Task UpdateVersionInFiles_HandlesBetaVersion()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("go-beta-version-test");
        var versionGoPath = Path.Combine(tempDir.DirectoryPath, "version.go");
        await File.WriteAllTextAsync(versionGoPath,
            "package azblob\n\nconst Version = \"v1.0.0\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "1.1.0-beta.1", "beta");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(versionGoPath);
        Assert.That(updatedContent, Does.Contain("\"v1.1.0-beta.1\""));
    }

    [Test]
    public async Task UpdateVersionInFiles_NormalizesVPrefixInInput()
    {
        // Arrange - caller passes "v2.0.0" with leading v
        using var tempDir = TempDirectory.Create("go-v-prefix-input-test");
        var versionGoPath = Path.Combine(tempDir.DirectoryPath, "version.go");
        await File.WriteAllTextAsync(versionGoPath,
            "package azblob\n\nconst Version = \"v1.0.0\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "v2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(versionGoPath);
        Assert.That(updatedContent, Does.Contain("\"v2.0.0\""));
        Assert.That(updatedContent, Does.Not.Contain("\"vv"));
    }

    [Test]
    public async Task UpdateVersionInFiles_DoesNotReplaceVersionInComments()
    {
        // Arrange - version string also appears in a comment
        using var tempDir = TempDirectory.Create("go-comment-version-test");
        var versionGoPath = Path.Combine(tempDir.DirectoryPath, "constants.go");
        var content = "package azblob\n\n// Current version is 1.0.0, see changelog for details\nconst ModuleVersion = \"v1.0.0\"\n";
        await File.WriteAllTextAsync(versionGoPath, content);

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(versionGoPath);
        Assert.That(updatedContent, Does.Contain("\"v2.0.0\""));
        // Comment should be unchanged
        Assert.That(updatedContent, Does.Contain("// Current version is 1.0.0, see changelog for details"));
    }

    #endregion

    /// <summary>
    /// Uses reflection to call the protected UpdatePackageVersionInFilesAsync method for testing.
    /// </summary>
    private async Task<PackageOperationResponse> InvokeUpdatePackageVersionInFilesAsync(
        string packagePath, string version, string? releaseType)
    {
        var method = typeof(GoLanguageService)
            .GetMethod("UpdatePackageVersionInFilesAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.That(method, Is.Not.Null, "UpdatePackageVersionInFilesAsync method not found");

        var task = (Task<PackageOperationResponse>)method!.Invoke(
            _languageService,
            [packagePath, version, releaseType, CancellationToken.None])!;

        return await task;
    }
}
