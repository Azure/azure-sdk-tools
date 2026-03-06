// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
public class JavaLanguageServiceVersionUpdateTests
{
    private TempDirectory _tempDirectory = null!;
    private JavaLanguageService _javaLanguageService = null!;
    private Mock<IMavenHelper> _mockMavenHelper = null!;
    private Mock<IPythonHelper> _mockPythonHelper = null!;
    private Mock<IPackageInfoHelper> _mockPackageInfoHelper = null!;
    private Mock<IProcessHelper> _mockProcessHelper = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = TempDirectory.Create("JavaLanguageServiceVersionUpdateTests");
        _mockMavenHelper = new Mock<IMavenHelper>();
        _mockPythonHelper = new Mock<IPythonHelper>();
        _mockPackageInfoHelper = new Mock<IPackageInfoHelper>();
        _mockProcessHelper = new Mock<IProcessHelper>();

        _mockPackageInfoHelper
            .Setup(h => h.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string packagePath, CancellationToken _) =>
                (_tempDirectory.DirectoryPath, string.Empty, RealPath.GetRealPath(packagePath)));

        var gitHelperMock = new Mock<IGitHelper>();
        gitHelperMock.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-java");
        gitHelperMock.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        _javaLanguageService = new JavaLanguageService(
            _mockProcessHelper.Object,
            gitHelperMock.Object,
            _mockMavenHelper.Object,
            _mockPythonHelper.Object,
            new Mock<ICopilotAgentRunner>().Object,
            NullLogger<JavaLanguageService>.Instance,
            new Mock<ICommonValidationHelpers>().Object,
            _mockPackageInfoHelper.Object,
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            new ChangelogHelper(NullLogger<ChangelogHelper>.Instance));
    }

    [TearDown]
    public void TearDown()
    {
        _tempDirectory.Dispose();
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenPomXmlNotFound_ShouldReturnFailure()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");

        var result = await _javaLanguageService.UpdateVersionAsync(packagePath, "beta", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Result, Is.EqualTo("partial"));
        Assert.That(result.Message, Does.Contain("version file update requires additional steps"));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenScriptsMissing_ShouldReturnFailure()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        await WritePomAsync(packagePath, "com.azure", "azure-test-package", "1.0.0");
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");

        var result = await _javaLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Result, Is.EqualTo("partial"));
        Assert.That(result.Message, Does.Contain("version file update requires additional steps"));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WithInvalidPomXml_ShouldReturnFailure()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        var pomPath = Path.Combine(packagePath, "pom.xml");
        await File.WriteAllTextAsync(pomPath, "This is not valid XML");
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");
        CreateVersioningScripts(_tempDirectory.DirectoryPath);

        var result = await _javaLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Result, Is.EqualTo("partial"));
        Assert.That(result.Message, Does.Contain("version file update requires additional steps"));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenSetVersionsFails_ShouldReturnFailure()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        await WritePomAsync(packagePath, "com.azure", "azure-test-package", "1.0.0");
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");
        CreateVersioningScripts(_tempDirectory.DirectoryPath);

        _mockPythonHelper
            .Setup(h => h.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult(1, "boom"));

        var result = await _javaLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Result, Is.EqualTo("partial"));
        Assert.That(result.Message, Does.Contain("version file update requires additional steps"));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenUpdateVersionsFails_ShouldReturnFailure()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        await WritePomAsync(packagePath, "com.azure", "azure-test-package", "1.0.0");
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");
        CreateVersioningScripts(_tempDirectory.DirectoryPath);

        _mockPythonHelper
            .SetupSequence(h => h.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult(0, "ok"))
            .ReturnsAsync(CreateProcessResult(1, "propagation failed"));

        var result = await _javaLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Result, Is.EqualTo("partial"));
        Assert.That(result.Message, Does.Contain("version file update requires additional steps"));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenScriptsSucceed_ShouldRunBothScriptsAndReturnSuccess()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        await WritePomAsync(packagePath, "com.azure", "azure-test-package", "1.0.0");
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");
        CreateVersioningScripts(_tempDirectory.DirectoryPath);

        _mockPythonHelper
            .SetupSequence(h => h.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult(0, "set ok"))
            .ReturnsAsync(CreateProcessResult(0, "update ok"));

        var result = await _javaLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));

        _mockPythonHelper.Verify(h => h.Run(
            It.Is<PythonOptions>(o => o.Args.Any(a => a.EndsWith("set_versions.py", StringComparison.OrdinalIgnoreCase))),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockPythonHelper.Verify(h => h.Run(
            It.Is<PythonOptions>(o =>
                o.Args.Any(a => a.EndsWith("update_versions.py", StringComparison.OrdinalIgnoreCase)) &&
                o.Args.Contains("--library-list") &&
                o.Args.Contains("com.azure:azure-test-package")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async Task WritePomAsync(string packagePath, string groupId, string artifactId, string version)
    {
        var pomPath = Path.Combine(packagePath, "pom.xml");
        var pomContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0""
         xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
         xsi:schemaLocation=""http://maven.apache.org/POM/4.0.0 http://maven.apache.org/xsd/maven-4.0.0.xsd"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>{groupId}</groupId>
    <artifactId>{artifactId}</artifactId>
    <version>{version}</version>
</project>";

        await File.WriteAllTextAsync(pomPath, pomContent);
    }

    private static async Task WriteChangelogForVersionAsync(string packagePath, string version)
    {
        var changelogPath = Path.Combine(packagePath, "CHANGELOG.md");
        var changelogContent = $@"# Release History

## {version} (Unreleased)

### Features Added

- Test feature
";

        await File.WriteAllTextAsync(changelogPath, changelogContent);
    }

    private static void CreateVersioningScripts(string repoRoot)
    {
        var versioningDir = Path.Combine(repoRoot, "eng", "versioning");
        Directory.CreateDirectory(versioningDir);
        File.WriteAllText(Path.Combine(versioningDir, "set_versions.py"), "print('set versions')");
        File.WriteAllText(Path.Combine(versioningDir, "update_versions.py"), "print('update versions')");
    }

    private static ProcessResult CreateProcessResult(int exitCode, string output)
    {
        return new ProcessResult
        {
            ExitCode = exitCode,
            OutputDetails = [(StdioLevel.StandardOutput, output)]
        };
    }
}
