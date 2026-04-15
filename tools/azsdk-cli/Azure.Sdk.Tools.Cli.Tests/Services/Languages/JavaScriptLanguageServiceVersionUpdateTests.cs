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
public class JavaScriptLanguageServiceVersionUpdateTests
{
    private TempDirectory _tempDirectory = null!;
    private JavaScriptLanguageService _jsLanguageService = null!;
    private Mock<IProcessHelper> _mockProcessHelper = null!;
    private Mock<IPackageInfoHelper> _mockPackageInfoHelper = null!;
    private Mock<IGitHelper> _mockGitHelper = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = TempDirectory.Create("JavaScriptLanguageServiceVersionUpdateTests");
        _mockProcessHelper = new Mock<IProcessHelper>();
        _mockPackageInfoHelper = new Mock<IPackageInfoHelper>();
        _mockGitHelper = new Mock<IGitHelper>();

        _mockPackageInfoHelper
            .Setup(h => h.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string packagePath, CancellationToken _) =>
                (_tempDirectory.DirectoryPath, string.Empty, RealPath.GetRealPath(packagePath)));

        _mockGitHelper
            .Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-js");
        _mockGitHelper
            .Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        _jsLanguageService = new JavaScriptLanguageService(
            _mockProcessHelper.Object,
            Mock.Of<INpxHelper>(),
            Mock.Of<ICopilotAgentRunner>(),
            _mockGitHelper.Object,
            NullLogger<JavaScriptLanguageService>.Instance,
            Mock.Of<ICommonValidationHelpers>(),
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
    public async Task UpdatePackageVersionInFilesAsync_WhenPackageJsonNotFound_ShouldReturnFailure()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");

        var result = await _jsLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Result, Is.EqualTo("partial"));
        Assert.That(result.Message, Does.Contain("version file update requires additional steps"));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenSetVersionScriptMissing_ShouldReturnFailure()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        await WritePackageJsonAsync(packagePath, "@azure/test-package", "1.0.0");
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");

        var result = await _jsLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Result, Is.EqualTo("partial"));
        Assert.That(result.Message, Does.Contain("version file update requires additional steps"));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenNpmInstallFails_ShouldReturnFailure()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        await WritePackageJsonAsync(packagePath, "@azure/test-package", "1.0.0");
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");
        CreateVersioningScript(_tempDirectory.DirectoryPath);

        _mockProcessHelper
            .Setup(h => h.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult(1, "npm install failed"));

        var result = await _jsLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Result, Is.EqualTo("partial"));
        Assert.That(result.Message, Does.Contain("version file update requires additional steps"));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenSetVersionScriptFails_ShouldReturnFailure()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        await WritePackageJsonAsync(packagePath, "@azure/test-package", "1.0.0");
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");
        CreateVersioningScript(_tempDirectory.DirectoryPath);

        _mockProcessHelper
            .SetupSequence(h => h.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult(0, "npm install ok"))   // eng-package-utils install
            .ReturnsAsync(CreateProcessResult(0, "npm install ok"))   // versioning install
            .ReturnsAsync(CreateProcessResult(1, "set-version failed")); // node set-version.js

        var result = await _jsLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Result, Is.EqualTo("partial"));
        Assert.That(result.Message, Does.Contain("version file update requires additional steps"));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenScriptSucceeds_ShouldCallSetVersionScriptWithCorrectArgs()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        await WritePackageJsonAsync(packagePath, "@azure/test-package", "1.0.0");
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");
        CreateVersioningScript(_tempDirectory.DirectoryPath);

        _mockProcessHelper
            .Setup(h => h.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult(0, "ok"));

        var result = await _jsLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));

        // Verify node set-version.js was called with correct artifact name and version
        _mockProcessHelper.Verify(h => h.Run(
            It.Is<ProcessOptions>(o =>
                o.Args.Contains("./set-version.js") &&
                o.Args.Contains("--artifact-name") &&
                o.Args.Contains("azure-test-package") &&
                o.Args.Contains("--new-version") &&
                o.Args.Contains("1.2.3") &&
                o.Args.Contains("--repo-root")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_ArtifactName_ShouldStripAtAndReplaceSlash()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        // Package name with @ and / which should be replaced to compute artifact name
        await WritePackageJsonAsync(packagePath, "@azure/keyvault-secrets", "1.0.0");
        await WriteChangelogForVersionAsync(packagePath, "4.9.0");
        CreateVersioningScript(_tempDirectory.DirectoryPath);

        _mockProcessHelper
            .Setup(h => h.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult(0, "ok"));

        await _jsLanguageService.UpdateVersionAsync(packagePath, "stable", "4.9.0", "2025-12-01", CancellationToken.None);

        // Verify artifact name is "azure-keyvault-secrets" (@ stripped, / replaced with -)
        _mockProcessHelper.Verify(h => h.Run(
            It.Is<ProcessOptions>(o => o.Args.Contains("azure-keyvault-secrets")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async Task WritePackageJsonAsync(string packagePath, string packageName, string version)
    {
        var packageJsonPath = Path.Combine(packagePath, "package.json");
        var content = $@"{{
  ""name"": ""{packageName}"",
  ""version"": ""{version}"",
  ""sdk-type"": ""client""
}}
";
        await File.WriteAllTextAsync(packageJsonPath, content);
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

    private static void CreateVersioningScript(string repoRoot)
    {
        // Create eng-package-utils so that the npm install for it is also invoked,
        // matching the SetPackageVersion PowerShell logic which always runs it.
        Directory.CreateDirectory(Path.Combine(repoRoot, "eng", "tools", "eng-package-utils"));
        var versioningDir = Path.Combine(repoRoot, "eng", "tools", "versioning");
        Directory.CreateDirectory(versioningDir);
        File.WriteAllText(Path.Combine(versioningDir, "set-version.js"), "console.log('set-version')");
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
