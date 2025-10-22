// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Models;
using LibGit2Sharp;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

/// <summary>
/// Contract-focused tests for all language-specific <see cref="IPackageInfoHelper"/> implementations.
/// Ensures consistent parsing of repo root, relative path, and language-specific version extraction
/// without duplicating per-language edge case tests.
/// </summary>
[TestFixture]
public class PackageInfoContractTests
{
    private TempDirectory _tempRoot = null!;

    [SetUp]
    public void SetUp() => _tempRoot = TempDirectory.Create("azsdk_pkginfo_contract_tests");
    [TearDown]
    public void TearDown() => _tempRoot.Dispose();

    private (string packagePath, GitHelper gitHelper) CreateSdkPackage(string service, string package)
    {
        var repoRoot = Path.Combine(_tempRoot.DirectoryPath, "azure-sdk-repo-root");
        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git"))) { Repository.Init(repoRoot); }
        var sdkPath = Path.Combine(repoRoot, "sdk", service, package);
        Directory.CreateDirectory(sdkPath);
        var ghMock = new Mock<IGitHubService>();
        var gitHelper = new GitHelper(ghMock.Object, new TestLogger<GitHelper>());
        return (sdkPath, gitHelper);
    }


    [Test]
    [TestCase(SdkLanguage.DotNet)]
    [TestCase(SdkLanguage.Java)]
    [TestCase(SdkLanguage.Python)]
    [TestCase(SdkLanguage.JavaScript)]
    [TestCase(SdkLanguage.Go)]
    public async Task CommonProperties_AreDerivedCorrectly(SdkLanguage language)
    {
        string group = language == SdkLanguage.Go ? "security" : string.Empty; // Representative group for go
        var service = language == SdkLanguage.Go ? "keyvault" : "storage";
        var package = language switch { SdkLanguage.DotNet => "Azure.Storage.Blobs", SdkLanguage.Java => "azure-storage-blob", SdkLanguage.Go => "azkeys", _ => "storage-blob" };
        var servicePath = language == SdkLanguage.Go ? Path.Combine(group, service) : service;
        var (pkgPath, gitHelper) = CreateSdkPackage(servicePath, package);
        var helper = CreateHelperForLanguage(language, gitHelper);
        var info = await helper.ResolvePackageInfo(pkgPath);

        Assert.Multiple(() =>
        {
            Assert.That(info.PackagePath, Is.EqualTo(RealPath.GetRealPath(Path.GetFullPath(pkgPath))));
            Assert.That(info.RepoRoot, Does.EndWith("azure-sdk-repo-root"));
            var expectedRelative = language == SdkLanguage.Go ? Path.Combine(group, service, package) : Path.Combine(service, package);
            Assert.That(info.RelativePath, Is.EqualTo(expectedRelative));
            Assert.That(info.ServiceName, Is.EqualTo(service));
            Assert.That(info.PackageName, Is.EqualTo(package));
            Assert.That(info.Language, Is.EqualTo(language));
        });
    }

    [Test]
    [TestCase(SdkLanguage.DotNet)]
    [TestCase(SdkLanguage.Java)]
    [TestCase(SdkLanguage.Python)]
    [TestCase(SdkLanguage.JavaScript)]
    [TestCase(SdkLanguage.Go)]
    public void Resolve_InvalidPath_Throws(SdkLanguage language)
    {
        var repoRoot = Path.Combine(_tempRoot.DirectoryPath, "azure-sdk-repo-root-invalid");
        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git"))) { Repository.Init(repoRoot); }
        // Invalid because missing sdk segment entirely or insufficient depth under sdk.
        var badRoot = Path.Combine(repoRoot, "random", "folder");
        Directory.CreateDirectory(badRoot);
        var ghMock = new Mock<IGitHubService>();
        var gitHelper = new GitHelper(ghMock.Object, new TestLogger<GitHelper>());
        var helper = CreateHelperForLanguage(language, gitHelper);
        Assert.ThrowsAsync<ArgumentException>(() => helper.ResolvePackageInfo(badRoot));
    }

    [Test]
    [TestCase(SdkLanguage.DotNet, "Azure.Data.Test", "<Project><PropertyGroup><Version>5.6.7</Version></PropertyGroup></Project>", ".csproj", "5.6.7")]
    [TestCase(SdkLanguage.Java, "azure-core", "<project><modelVersion>4.0.0</modelVersion><groupId>com.azure</groupId><artifactId>azure-core</artifactId><version>1.2.3</version></project>", "pom.xml", "1.2.3")]
    [TestCase(SdkLanguage.Python, "azure-ai", "[project]\nname='azure-ai'\nversion='1.0.1'\n", "pyproject.toml", "1.0.1")]
    [TestCase(SdkLanguage.JavaScript, "azure-testpkg", "{\n  \"name\": \"@azure/azure-testpkg\",\n  \"version\": \"2.3.4\"\n}", "package.json", "2.3.4")]
    [TestCase(SdkLanguage.Go, "azkeys", "package azkeys\n\nconst (\n    moduleName = \"github.com/Azure/azure-sdk-for-go/sdk/security/keyvault/azkeys\"\n    version    = \"v1.4.1-beta.1\"\n)\n", "version.go", "v1.4.1-beta.1")]
    public async Task VersionParsing_Works(SdkLanguage language, string package, string fileContent, string fileName, string expectedVersion)
    {
        var servicePath = language == SdkLanguage.Go ? "security/keyvault" : "ai";
        var (pkgPath, gitHelper) = CreateSdkPackage(servicePath, package);
        File.WriteAllText(Path.Combine(pkgPath, fileName), fileContent);
        var helper = CreateHelperForLanguage(language, gitHelper);
        var info = await helper.ResolvePackageInfo(pkgPath);
        var parsed = await info.GetPackageVersionAsync();
        Assert.That(parsed, Is.EqualTo(expectedVersion));
    }

    [Test]
    [TestCase(SdkLanguage.DotNet, "Azure.Data.Empty")]
    [TestCase(SdkLanguage.Java, "azure-empty")]
    [TestCase(SdkLanguage.Python, "azure-empty")]
    [TestCase(SdkLanguage.JavaScript, "azure-empty")]
    [TestCase(SdkLanguage.Go, "azempty")]
    public async Task VersionParsing_MissingFile_ReturnsNull(SdkLanguage language, string package)
    {
        var servicePath = language == SdkLanguage.Go ? "security/keyvault" : "missing";
        var (pkgPath, gitHelper) = CreateSdkPackage(servicePath, package);
        var helper = CreateHelperForLanguage(language, gitHelper);
        var info = await helper.ResolvePackageInfo(pkgPath);
        var parsed = await info.GetPackageVersionAsync();
        Assert.That(parsed, Is.Null);
    }

    private static IPackageInfoHelper CreateHelperForLanguage(SdkLanguage language, IGitHelper gitHelper) => language switch
    {
        SdkLanguage.DotNet => new DotNetPackageInfoHelper(gitHelper),
        SdkLanguage.Java => new JavaPackageInfoHelper(gitHelper),
        SdkLanguage.Python => new PythonPackageInfoHelper(gitHelper),
        SdkLanguage.JavaScript => new JavaScriptPackageInfoHelper(gitHelper),
        SdkLanguage.Go => new GoPackageInfoHelper(gitHelper),
        _ => throw new ArgumentException($"Unsupported language '{language}'", nameof(language))
    };
}