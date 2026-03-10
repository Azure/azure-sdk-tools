// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
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
    private Mock<IPackageInfoHelper> _mockPackageInfoHelper = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = TempDirectory.Create("JavaScriptLanguageServiceVersionUpdateTests");
        _mockPackageInfoHelper = new Mock<IPackageInfoHelper>();

        _mockPackageInfoHelper
            .Setup(h => h.ParsePackagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string packagePath, CancellationToken _) =>
                (_tempDirectory.DirectoryPath, string.Empty, RealPath.GetRealPath(packagePath)));

        _jsLanguageService = new JavaScriptLanguageService(
            Mock.Of<IProcessHelper>(),
            Mock.Of<INpxHelper>(),
            Mock.Of<IGitHelper>(),
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
    public async Task UpdatePackageVersionInFilesAsync_WhenPackageJsonIsValid_ShouldUpdateVersion()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        await WritePackageJsonAsync(packagePath, "@azure/test-package", "1.0.0");
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");

        var result = await _jsLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));

        // Verify package.json version was updated
        var packageJsonContent = await File.ReadAllTextAsync(Path.Combine(packagePath, "package.json"));
        Assert.That(packageJsonContent, Does.Contain("\"1.2.3\""));
        Assert.That(packageJsonContent, Does.Not.Contain("\"1.0.0\""));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WithConstantPaths_ShouldUpdateVersionInConstantFiles()
    {
        var packagePath = _tempDirectory.DirectoryPath;

        // Create a constant file that contains the version
        var srcDir = Path.Combine(packagePath, "src");
        Directory.CreateDirectory(srcDir);
        var constantFilePath = Path.Combine(srcDir, "version.ts");
        await File.WriteAllTextAsync(constantFilePath, "export const SDK_VERSION = \"1.0.0\";\n");

        // Create package.json with //metadata.constantPaths
        await WritePackageJsonWithConstantsAsync(packagePath, "@azure/test-package", "1.0.0",
            [new ConstantPath("src/version.ts", "SDK_VERSION")]);
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");

        var result = await _jsLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Message, Does.Contain("1 constant file"));

        // Verify constant file was updated
        var constantContent = await File.ReadAllTextAsync(constantFilePath);
        Assert.That(constantContent, Does.Contain("\"1.2.3\""));
        Assert.That(constantContent, Does.Not.Contain("\"1.0.0\""));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WhenConstantFileNotFound_ShouldSkipMissingFile()
    {
        var packagePath = _tempDirectory.DirectoryPath;

        // Create package.json with //metadata.constantPaths pointing to non-existent file
        await WritePackageJsonWithConstantsAsync(packagePath, "@azure/test-package", "1.0.0",
            [new ConstantPath("src/nonexistent.ts", "SDK_VERSION")]);
        await WriteChangelogForVersionAsync(packagePath, "1.2.3");

        var result = await _jsLanguageService.UpdateVersionAsync(packagePath, "stable", "1.2.3", "2025-12-01", CancellationToken.None);

        // Should still succeed - missing constant files are skipped (logged as warning)
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));

        // Verify package.json version was updated
        var packageJsonContent = await File.ReadAllTextAsync(Path.Combine(packagePath, "package.json"));
        Assert.That(packageJsonContent, Does.Contain("\"1.2.3\""));
    }

    [Test]
    public async Task UpdatePackageVersionInFilesAsync_WithNoConstantPaths_ShouldUpdateOnlyPackageJson()
    {
        var packagePath = _tempDirectory.DirectoryPath;
        await WritePackageJsonAsync(packagePath, "@azure/test-package", "1.0.0");
        await WriteChangelogForVersionAsync(packagePath, "2.0.0-beta.1");

        var result = await _jsLanguageService.UpdateVersionAsync(packagePath, "beta", "2.0.0-beta.1", "2025-12-01", CancellationToken.None);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));

        var packageJsonContent = await File.ReadAllTextAsync(Path.Combine(packagePath, "package.json"));
        Assert.That(packageJsonContent, Does.Contain("\"2.0.0-beta.1\""));
    }

    [Test]
    public void ReplaceVersionInContent_ShouldReplaceSimpleSemver()
    {
        const string content = "export const SDK_VERSION = \"1.0.0\";\n";
        var result = JavaScriptLanguageService.ReplaceVersionInContent(content, "SDK_VERSION", "2.0.0");
        Assert.That(result, Is.EqualTo("export const SDK_VERSION = \"2.0.0\";\n"));
    }

    [Test]
    public void ReplaceVersionInContent_ShouldReplacePrereleaseSemver()
    {
        const string content = "const packageVersion = \"1.0.0-beta.2\";";
        var result = JavaScriptLanguageService.ReplaceVersionInContent(content, "packageVersion", "2.0.0-beta.1");
        Assert.That(result, Is.EqualTo("const packageVersion = \"2.0.0-beta.1\";"));
    }

    [Test]
    public void ReplaceVersionInContent_WhenNoMatch_ShouldReturnOriginalContent()
    {
        const string content = "export const OTHER_CONST = \"some value\";\n";
        var result = JavaScriptLanguageService.ReplaceVersionInContent(content, "SDK_VERSION", "2.0.0");
        Assert.That(result, Is.EqualTo(content));
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

    private static async Task WritePackageJsonWithConstantsAsync(
        string packagePath,
        string packageName,
        string version,
        IEnumerable<ConstantPath> constantPaths)
    {
        var constantPathsJson = string.Join(",\n      ", constantPaths.Select(cp =>
            $@"{{ ""path"": ""{cp.Path}"", ""prefix"": ""{cp.Prefix}"" }}"));

        var packageJsonPath = Path.Combine(packagePath, "package.json");
        var content = $@"{{
  ""name"": ""{packageName}"",
  ""version"": ""{version}"",
  ""sdk-type"": ""client"",
  ""//metadata"": {{
    ""constantPaths"": [
      {constantPathsJson}
    ]
  }}
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

    private record ConstantPath(string Path, string Prefix);
}
