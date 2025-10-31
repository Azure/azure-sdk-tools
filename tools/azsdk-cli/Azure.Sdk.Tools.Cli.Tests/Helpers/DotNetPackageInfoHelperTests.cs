// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Services;
using LibGit2Sharp;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class DotNetPackageInfoHelperTests
{
    private TempDirectory _tempDir = null!;

    [SetUp]
    public void SetUp() => _tempDir = TempDirectory.Create("dotnet_package_info_tests");

    [TearDown]
    public void TearDown() => _tempDir.Dispose();

    private (string packagePath, GitHelper gitHelper) CreateTestPackage()
    {
        var repoRoot = Path.Combine(_tempDir.DirectoryPath, "test-repo");
        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git"))) { Repository.Init(repoRoot); }
        var packagePath = Path.Combine(repoRoot, "sdk", "storage", "storage-blob");
        Directory.CreateDirectory(packagePath);
        var ghMock = new Mock<IGitHubService>();
        var gitHelper = new GitHelper(ghMock.Object, new TestLogger<GitHelper>());
        return (packagePath, gitHelper);
    }

    private void CreateTestFile(string packagePath, string relativePath, string content)
    {
        var fullPath = Path.Combine(packagePath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
    }

    [Test]
    public async Task FindSamplesDirectory_WithSampleFiles_ReturnsSamplesDirectory()
    {
        // Arrange
        var (packagePath, gitHelper) = CreateTestPackage();
        
        CreateTestFile(packagePath, "tests/samples/Sample01_Basic.cs", "namespace Test; public class Sample01_Basic { }");
        CreateTestFile(packagePath, "tests/samples/BasicSample.cs", "namespace Test; public class BasicSample { }");
        
        // Create non-sample files
        CreateTestFile(packagePath, "tests/unit/other.cs", "namespace Test; public class NotASample { }");
        
        var helper = new DotNetPackageInfoHelper(gitHelper, new TestLogger<DotNetPackageInfoHelper>());
        
        // Act
        var packageInfo = await helper.ResolvePackageInfo(packagePath);
        
        // Assert
        Assert.That(packageInfo.SamplesDirectory, Is.EqualTo(Path.Combine(packagePath, "tests", "samples")));
    }

    [Test]
    public async Task FindSamplesDirectory_WithSnippetFiles_ReturnsSamplesDirectory()
    {
        // Arrange
        var (packagePath, gitHelper) = CreateTestPackage();
        
        // Create snippet files that should be detected
        CreateTestFile(packagePath, "tests/snippets/Snippet01_Basic.cs", "namespace Test; public class Snippet01_Basic { }");
        CreateTestFile(packagePath, "tests/snippets/BasicSnippet.cs", "namespace Test; public class BasicSnippet { }");
        
        // Create non-sample files
        CreateTestFile(packagePath, "tests/unit/UnitTest.cs", "namespace Test; public class UnitTest { }");
        
        var helper = new DotNetPackageInfoHelper(gitHelper, new TestLogger<DotNetPackageInfoHelper>());
        
        // Act
        var packageInfo = await helper.ResolvePackageInfo(packagePath);
        
        // Assert
        Assert.That(packageInfo.SamplesDirectory, Is.EqualTo(Path.Combine(packagePath, "tests", "snippets")));
    }
    
    [Test]
    public async Task FindSamplesDirectory_WithNoSampleFiles_ReturnsDefaultPath()
    {
        // Arrange
        var (packagePath, gitHelper) = CreateTestPackage();
        
        // Create non-sample files only
        CreateTestFile(packagePath, "tests/unit/other.cs", "namespace Test; public class NotASample { }");
        
        var helper = new DotNetPackageInfoHelper(gitHelper, new TestLogger<DotNetPackageInfoHelper>());
        
        // Act
        var packageInfo = await helper.ResolvePackageInfo(packagePath);
        
        // Assert
        Assert.That(packageInfo.SamplesDirectory, Is.EqualTo(Path.Combine(packagePath, "tests", "samples")));
    }
    
    [Test]
    public async Task FindSamplesDirectory_WithNoTestsDirectory_ReturnsDefaultPath()
    {
        // Arrange
        var (packagePath, gitHelper) = CreateTestPackage();
        
        var helper = new DotNetPackageInfoHelper(gitHelper, new TestLogger<DotNetPackageInfoHelper>());
        
        // Act
        var packageInfo = await helper.ResolvePackageInfo(packagePath);
        
        // Assert
        Assert.That(packageInfo.SamplesDirectory, Is.EqualTo(Path.Combine(packagePath, "tests", "samples")));
    }

    [Test]
    public async Task FindSamplesDirectory_WithCaseInsensitiveSampleFiles_ReturnsCorrectDirectory()
    {
        // Arrange
        var (packagePath, gitHelper) = CreateTestPackage();
        
        // Create sample files with different casing
        CreateTestFile(packagePath, "tests/examples/SAMPLE_Basic.cs", "namespace Test; public class SAMPLE_Basic { }");
        CreateTestFile(packagePath, "tests/examples/ExampleSNIPPET.cs", "namespace Test; public class ExampleSNIPPET { }");
        
        var helper = new DotNetPackageInfoHelper(gitHelper, new TestLogger<DotNetPackageInfoHelper>());
        
        // Act
        var packageInfo = await helper.ResolvePackageInfo(packagePath);
        
        // Assert
        Assert.That(packageInfo.SamplesDirectory, Is.EqualTo(Path.Combine(packagePath, "tests", "examples")));
    }

    [Test]
    public async Task FindSamplesDirectory_WithMixedSampleAndSnippetFiles_ReturnsFirstFoundDirectory()
    {
        // Arrange
        var (packagePath, gitHelper) = CreateTestPackage();
        
        // Create both sample and snippet files (first directory alphabetically should be returned)
        CreateTestFile(packagePath, "tests/examples/BasicSample.cs", "namespace Test; public class BasicSample { }");
        CreateTestFile(packagePath, "tests/snippets/BasicSnippet.cs", "namespace Test; public class BasicSnippet { }");
        
        var helper = new DotNetPackageInfoHelper(gitHelper, new TestLogger<DotNetPackageInfoHelper>());
        
        // Act
        var packageInfo = await helper.ResolvePackageInfo(packagePath);
        
        // Assert - should return the first directory found (alphabetically, "examples" comes before "snippets")
        Assert.That(packageInfo.SamplesDirectory, Is.EqualTo(Path.Combine(packagePath, "tests", "examples")));
    }
}