// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Xml.Linq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

/// <summary>
/// Tests for JavaLanguageService.UpdatePackageVersionInFilesAsync method.
/// Validates that the Java-specific version update logic correctly updates pom.xml files.
/// </summary>
[TestFixture]
public class JavaLanguageServiceVersionUpdateTests
{
    private TempDirectory _tempDirectory = null!;
    private JavaLanguageService _javaLanguageService = null!;
    private Mock<IMavenHelper> _mockMavenHelper = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = TempDirectory.Create("JavaLanguageServiceVersionUpdateTests");
        _mockMavenHelper = new Mock<IMavenHelper>();
        
        var gitHelperMock = new Mock<IGitHelper>();
        gitHelperMock.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-java");

        _javaLanguageService = new JavaLanguageService(
            new Mock<IProcessHelper>().Object,
            gitHelperMock.Object,
            _mockMavenHelper.Object,
            new Mock<IMicroagentHostService>().Object,
            NullLogger<JavaLanguageService>.Instance,
            new Mock<ICommonValidationHelpers>().Object,
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());
    }

    [TearDown]
    public void TearDown()
    {
        _tempDirectory.Dispose();
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenPomXmlNotFound_ShouldReturnFailure()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;

        // Act
        var result = await _javaLanguageService.UpdateVersionAsync(packagePath, "beta", "1.2.3", "2025-12-01", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("No pom.xml file found"));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenVersionElementExists_ShouldUpdateVersion()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        var pomPath = Path.Combine(packagePath, "pom.xml");
        var changelogPath = Path.Combine(packagePath, "CHANGELOG.md");
        var oldVersion = "1.0.0";
        var newVersion = "1.2.3";

        // Create a simple pom.xml
        var pomContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0""
         xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
         xsi:schemaLocation=""http://maven.apache.org/POM/4.0.0 http://maven.apache.org/xsd/maven-4.0.0.xsd"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.azure</groupId>
    <artifactId>azure-test-package</artifactId>
    <version>{oldVersion}</version>
</project>";
        await File.WriteAllTextAsync(pomPath, pomContent);

        // Create a changelog with an entry for the new version
        var changelogContent = $@"# Release History

## {newVersion} (Unreleased)

### Features Added

- Test feature

## {oldVersion} (2025-01-01)

Initial release
";
        await File.WriteAllTextAsync(changelogPath, changelogContent);

        // Act
        var result = await _javaLanguageService.UpdateVersionAsync(packagePath, "stable", newVersion, "2025-12-01", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        
        // Verify the pom.xml was updated
        var updatedPom = await File.ReadAllTextAsync(pomPath);
        Assert.That(updatedPom, Does.Contain($"<version>{newVersion}</version>"));
        Assert.That(updatedPom, Does.Not.Contain($"<version>{oldVersion}</version>"));

        // Parse and verify XML structure
        var doc = XDocument.Parse(updatedPom);
        var ns = doc.Root!.Name.Namespace;
        var versionElement = doc.Root.Element(ns + "version");
        Assert.That(versionElement, Is.Not.Null);
        Assert.That(versionElement!.Value, Is.EqualTo(newVersion));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenVersionInParent_ShouldReturnPartial()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        var pomPath = Path.Combine(packagePath, "pom.xml");
        var changelogPath = Path.Combine(packagePath, "CHANGELOG.md");
        var newVersion = "1.2.3";

        // Create a pom.xml with version in parent
        var pomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0""
         xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
         xsi:schemaLocation=""http://maven.apache.org/POM/4.0.0 http://maven.apache.org/xsd/maven-4.0.0.xsd"">
    <modelVersion>4.0.0</modelVersion>
    <parent>
        <groupId>com.azure</groupId>
        <artifactId>azure-parent</artifactId>
        <version>1.0.0</version>
    </parent>
    <artifactId>azure-test-package</artifactId>
</project>";
        await File.WriteAllTextAsync(pomPath, pomContent);

        // Create a changelog with an entry for the new version
        var changelogContent = $@"# Release History

## {newVersion} (Unreleased)

### Features Added

- Test feature
";
        await File.WriteAllTextAsync(changelogPath, changelogContent);

        // Act
        var result = await _javaLanguageService.UpdateVersionAsync(packagePath, "beta", newVersion, "2025-12-01", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Result, Is.EqualTo("partial"));
        Assert.That(result.Message, Does.Contain("Version is defined in parent POM"));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WithInvalidPomXml_ShouldReturnFailure()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        var pomPath = Path.Combine(packagePath, "pom.xml");

        // Create an invalid pom.xml
        await File.WriteAllTextAsync(pomPath, "This is not valid XML");

        // Act
        var result = await _javaLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WithBetaVersion_ShouldUpdateCorrectly()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        var pomPath = Path.Combine(packagePath, "pom.xml");
        var changelogPath = Path.Combine(packagePath, "CHANGELOG.md");
        var oldVersion = "1.0.0-beta.1";
        var newVersion = "1.0.0-beta.2";

        // Create a pom.xml with beta version
        var pomContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0""
         xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
         xsi:schemaLocation=""http://maven.apache.org/POM/4.0.0 http://maven.apache.org/xsd/maven-4.0.0.xsd"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.azure</groupId>
    <artifactId>azure-test-package</artifactId>
    <version>{oldVersion}</version>
</project>";
        await File.WriteAllTextAsync(pomPath, pomContent);

        // Create a changelog with an entry for the new version
        var changelogContent = $@"# Release History

## {newVersion} (Unreleased)

### Features Added

- Test feature

## {oldVersion} (2025-01-01)

Initial release
";
        await File.WriteAllTextAsync(changelogPath, changelogContent);

        // Act
        var result = await _javaLanguageService.UpdateVersionAsync(packagePath, "beta", newVersion, "2025-12-01", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        
        // Verify the pom.xml was updated with beta version
        var updatedPom = await File.ReadAllTextAsync(pomPath);
        Assert.That(updatedPom, Does.Contain($"<version>{newVersion}</version>"));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_PreservesWhitespaceAndFormatting()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        var pomPath = Path.Combine(packagePath, "pom.xml");
        var changelogPath = Path.Combine(packagePath, "CHANGELOG.md");
        var oldVersion = "1.0.0";
        var newVersion = "2.0.0";

        // Create a pom.xml with specific formatting
        var pomContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0""
         xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
         xsi:schemaLocation=""http://maven.apache.org/POM/4.0.0 http://maven.apache.org/xsd/maven-4.0.0.xsd"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.azure</groupId>
    <artifactId>azure-test-package</artifactId>
    <version>{oldVersion}</version>
    <description>Test package</description>
</project>";
        await File.WriteAllTextAsync(pomPath, pomContent);

        // Create a changelog
        var changelogContent = $@"# Release History

## {newVersion} (Unreleased)

Initial release
";
        await File.WriteAllTextAsync(changelogPath, changelogContent);

        // Act
        await _javaLanguageService.UpdateVersionAsync(packagePath, "stable", newVersion, "2025-12-01", CancellationToken.None);

        // Assert
        var updatedPom = await File.ReadAllTextAsync(pomPath);
        
        // Verify version was updated
        Assert.That(updatedPom, Does.Contain($"<version>{newVersion}</version>"));
        
        // Verify other elements are preserved
        Assert.That(updatedPom, Does.Contain("<groupId>com.azure</groupId>"));
        Assert.That(updatedPom, Does.Contain("<artifactId>azure-test-package</artifactId>"));
        Assert.That(updatedPom, Does.Contain("<description>Test package</description>"));
    }
}
