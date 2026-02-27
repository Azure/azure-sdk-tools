// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.Languages.Samples;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class DotNetPackageInfoHelperTests
{
    private TempDirectory _tempDir = null!;

    [SetUp]
    public void SetUp() => _tempDir = TempDirectory.Create("dotnet_package_info_tests");

    [TearDown]
    public void TearDown() => _tempDir.Dispose();

    private async Task<(string packagePath, GitHelper gitHelper, IProcessHelper, IPowershellHelper, ICommonValidationHelpers)> CreateTestPackageAsync()
    {
        var repoRoot = Path.Combine(_tempDir.DirectoryPath, "test-repo");
        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git"))) { await GitTestHelper.GitInitAsync(repoRoot); }
        var packagePath = Path.Combine(repoRoot, "sdk", "storage", "storage-blob");
        Directory.CreateDirectory(packagePath);
        var ghMock = new Mock<IGitHubService>();
        var gitCommandHelper = new GitCommandHelper(NullLogger<GitCommandHelper>.Instance, Mock.Of<IRawOutputHelper>());
        var gitHelper = new GitHelper(ghMock.Object, gitCommandHelper, new TestLogger<GitHelper>());
        var processMock = new Mock<IProcessHelper>();
        var powershellMock = new Mock<IPowershellHelper>();
        var commonValidationMock = new Mock<ICommonValidationHelpers>();
        return (packagePath, gitHelper, processMock.Object, powershellMock.Object, commonValidationMock.Object);
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
        var (packagePath, gitHelper, processHelper, powershellHelper, commonValidationHelpers) = await CreateTestPackageAsync();

        CreateTestFile(packagePath, "tests/samples/Sample01_Basic.cs", "#region Snippet:BasicSample\nnamespace Test; public class Sample01_Basic { }\n#endregion");
        CreateTestFile(packagePath, "tests/samples/BasicSample.cs", "#region Snippet:AnotherSample\nnamespace Test; public class BasicSample { }\n#endregion");

        // Create non-sample files
        CreateTestFile(packagePath, "tests/unit/other.cs", "namespace Test; public class NotASample { }");

        var packageInfoHelper = new PackageInfoHelper(NullLogger<PackageInfoHelper>.Instance, gitHelper);
        var helper = new DotnetLanguageService(processHelper, powershellHelper, gitHelper, new TestLogger<DotnetLanguageService>(), commonValidationHelpers, packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>());

        // Act
        var packageInfo = await helper.GetPackageInfo(packagePath);

        // Assert
        Assert.That(packageInfo.SamplesDirectory, Is.EqualTo(Path.Combine(packagePath, "tests", "samples")));
    }

    [Test]
    public async Task FindSamplesDirectory_WithSnippetFiles_ReturnsSamplesDirectory()
    {
        // Arrange
        var (packagePath, gitHelper, processHelper, powershellHelper, commonValidationHelpers) = await CreateTestPackageAsync();

        // Create snippet files that should be detected
        CreateTestFile(packagePath, "tests/snippets/Snippet01_Basic.cs", "#region Snippet:BasicSnippet\nnamespace Test; public class Snippet01_Basic { }\n#endregion");
        CreateTestFile(packagePath, "tests/snippets/BasicSnippet.cs", "#region Snippet:AnotherSnippet\nnamespace Test; public class BasicSnippet { }\n#endregion");

        // Create non-sample files
        CreateTestFile(packagePath, "tests/unit/UnitTest.cs", "namespace Test; public class UnitTest { }");

        var packageInfoHelper = new PackageInfoHelper(NullLogger<PackageInfoHelper>.Instance, gitHelper);
        var helper = new DotnetLanguageService(processHelper, powershellHelper, gitHelper, new TestLogger<DotnetLanguageService>(), commonValidationHelpers, packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>());

        // Act
        var packageInfo = await helper.GetPackageInfo(packagePath);

        // Assert
        Assert.That(packageInfo.SamplesDirectory, Is.EqualTo(Path.Combine(packagePath, "tests", "snippets")));
    }

    [Test]
    public async Task FindSamplesDirectory_WithNoSampleFiles_ReturnsDefaultPath()
    {
        // Arrange
        var (packagePath, gitHelper, processHelper, powershellHelper, commonValidationHelpers) = await CreateTestPackageAsync();

        // Create non-sample files only
        CreateTestFile(packagePath, "tests/unit/other.cs", "namespace Test; public class NotASample { }");

        var packageInfoHelper = new PackageInfoHelper(NullLogger<PackageInfoHelper>.Instance, gitHelper);
        var helper = new DotnetLanguageService(processHelper, powershellHelper, gitHelper, new TestLogger<DotnetLanguageService>(), commonValidationHelpers, packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>());

        // Act
        var packageInfo = await helper.GetPackageInfo(packagePath);

        // Assert
        Assert.That(packageInfo.SamplesDirectory, Is.EqualTo(Path.Combine(packagePath, "tests", "samples")));
    }

    [Test]
    public async Task FindSamplesDirectory_WithNoTestsDirectory_ReturnsDefaultPath()
    {
        // Arrange
        var (packagePath, gitHelper, processHelper, powershellHelper, commonValidationHelpers) = await CreateTestPackageAsync();

        var packageInfoHelper = new PackageInfoHelper(NullLogger<PackageInfoHelper>.Instance, gitHelper);
        var helper = new DotnetLanguageService(processHelper, powershellHelper, gitHelper, new TestLogger<DotnetLanguageService>(), commonValidationHelpers, packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>());

        // Act
        var packageInfo = await helper.GetPackageInfo(packagePath);

        // Assert
        Assert.That(packageInfo.SamplesDirectory, Is.EqualTo(Path.Combine(packagePath, "tests", "samples")));
    }

    [Test]
    public async Task FindSamplesDirectory_WithSnippetRegions_ReturnsCorrectDirectory()
    {
        // Arrange
        var (packagePath, gitHelper, processHelper, powershellHelper, commonValidationHelpers) = await CreateTestPackageAsync();

        // Create files with snippet regions
        CreateTestFile(packagePath, "tests/examples/SAMPLE_Basic.cs", "#region Snippet:ExampleSnippet\nnamespace Test; public class SAMPLE_Basic { }\n#endregion");
        CreateTestFile(packagePath, "tests/examples/ExampleSNIPPET.cs", "#region Snippet:AnotherExample\nnamespace Test; public class ExampleSNIPPET { }\n#endregion");

        var packageInfoHelper = new PackageInfoHelper(NullLogger<PackageInfoHelper>.Instance, gitHelper);
        var helper = new DotnetLanguageService(processHelper, powershellHelper, gitHelper, new TestLogger<DotnetLanguageService>(), commonValidationHelpers, packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>());

        // Act
        var packageInfo = await helper.GetPackageInfo(packagePath);

        // Assert
        Assert.That(packageInfo.SamplesDirectory, Is.EqualTo(Path.Combine(packagePath, "tests", "examples")));
    }

    [Test]
    public async Task FindSamplesDirectory_WithMultipleDirectories_ReturnsFirstFoundDirectory()
    {
        // Arrange
        var (packagePath, gitHelper, processHelper, powershellHelper, commonValidationHelpers) = await CreateTestPackageAsync();

        // Create files with snippet regions in multiple directories
        CreateTestFile(packagePath, "tests/examples/BasicSample.cs", "#region Snippet:ExampleSnippet\nnamespace Test; public class BasicSample { }\n#endregion");
        CreateTestFile(packagePath, "tests/snippets/BasicSnippet.cs", "#region Snippet:AnotherSnippet\nnamespace Test; public class BasicSnippet { }\n#endregion");

        var packageInfoHelper = new PackageInfoHelper(NullLogger<PackageInfoHelper>.Instance, gitHelper);
        var helper = new DotnetLanguageService(processHelper, powershellHelper, gitHelper, new TestLogger<DotnetLanguageService>(), commonValidationHelpers, packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>());

        // Act
        var packageInfo = await helper.GetPackageInfo(packagePath);

        // Assert - should return the first directory found (alphabetically, "examples" comes before "snippets")
        Assert.That(packageInfo.SamplesDirectory, Is.EqualTo(Path.Combine(packagePath, "tests", "examples")));
    }

    [Test]
    [TestCase("client", SdkType.Dataplane)]
    [TestCase("mgmt", SdkType.Management)]
    [TestCase("functions", SdkType.Functions)]
    public async Task GetPackageInfo_ParsesSdkTypeFromMSBuild(string sdkTypeValue, SdkType expectedSdkType)
    {
        // Arrange
        var (packagePath, gitHelper, processHelper, powershellHelper, commonValidationHelpers) = await CreateTestPackageAsync();

        // Create a .csproj with the GetPackageInfo target
        var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <Target Name=""GetPackageInfo"" Returns=""@(PackageInfoItem)"">
    <ItemGroup>
      <PackageInfoItem Include=""'$(MSBuildProjectDirectory)' 'testservice' 'Azure.Test.Package' '1.0.0' '{sdkTypeValue}' 'true' 'bin/Release/net8.0' 'false'"" />
    </ItemGroup>
  </Target>
</Project>";
        CreateTestFile(packagePath, "src/Azure.Test.Package.csproj", csprojContent);

        var csprojPath = Path.Combine(packagePath, "src", "Azure.Test.Package.csproj");
        Assert.That(File.Exists(csprojPath), Is.True, $"csproj file should exist at {csprojPath}");

        var realProcessHelper = new ProcessHelper(new TestLogger<ProcessHelper>(), Mock.Of<IRawOutputHelper>());
        var packageInfoHelper = new PackageInfoHelper(NullLogger<PackageInfoHelper>.Instance, gitHelper);
        var helper = new DotnetLanguageService(realProcessHelper, powershellHelper, gitHelper, new TestLogger<DotnetLanguageService>(), commonValidationHelpers, packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>());

        // Act
        var packageInfo = await helper.GetPackageInfo(packagePath);

        // Assert
        Assert.That(packageInfo.SdkType, Is.EqualTo(expectedSdkType), $"Expected SdkType to be {expectedSdkType} for sdkTypeValue '{sdkTypeValue}'");
        Assert.That(packageInfo.PackageName, Is.EqualTo("Azure.Test.Package"));
        Assert.That(packageInfo.PackageVersion, Is.EqualTo("1.0.0"));
    }
}
