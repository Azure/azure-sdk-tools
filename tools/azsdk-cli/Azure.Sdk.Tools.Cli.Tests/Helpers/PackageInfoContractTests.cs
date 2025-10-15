// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using LibGit2Sharp;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

/// <summary>
/// Contract-focused tests for all <see cref="IPackageInfo"/> implementations.
/// Ensures consistent parsing of repo root, relative path, idempotent initialization,
/// and language-specific version extraction without duplicating per-language edge case tests.
/// </summary>
[TestFixture]
public class PackageInfoContractTests
{
    private TempDirectory _tempRoot = null!;

    [SetUp]
    public void SetUp() => _tempRoot = TempDirectory.Create("azsdk_pkginfo_contract_tests");
    [TearDown]
    public void TearDown() => _tempRoot.Dispose();

    private (string packagePath, GitHelper gitHelper) CreateSdkPackage(string language, string service, string package)
    {
        var repoRoot = Path.Combine(_tempRoot.DirectoryPath, $"azure-sdk-for-{language}");
        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git"))) { Repository.Init(repoRoot); }
        var sdkPath = Path.Combine(repoRoot, "sdk", service, package);
        Directory.CreateDirectory(sdkPath);
        var ghMock = new Mock<IGitHubService>();
        var gitHelper = new GitHelper(ghMock.Object, new TestLogger<GitHelper>());
        return (sdkPath, gitHelper);
    }

    private IPackageInfo CreateForLanguage(string language, GitHelper gitHelper)
        => language switch
        {
            "dotnet" => new DotNetPackageInfo(gitHelper),
            "java" => new JavaPackageInfo(gitHelper),
            "python" => new PythonPackageInfo(gitHelper),
            "typescript" => new TypeScriptPackageInfo(gitHelper),
            "go" => new GoPackageInfo(gitHelper),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported test language")
        };

    [Test]
    [TestCase("dotnet")]
    [TestCase("java")]
    [TestCase("python")]
    [TestCase("typescript")]
    [TestCase("go")]
    public void Init_Idempotent_SamePath(string language)
    {
        // Go packages require sdk/<group>/<service>/<package>; other languages use sdk/<service>/<package>
        var servicePath = language == "go" ? "group1/service" : "service";
        var (pkgPath, gitHelper) = CreateSdkPackage(language, servicePath, "pkgA");
        var info = CreateForLanguage(language, gitHelper);
        info.Init(pkgPath);
        // Second init with same path should be silent
        Assert.DoesNotThrow(() => info.Init(pkgPath));
        // Attempt re-init with different path should throw
        var (otherPath, _) = CreateSdkPackage(language, servicePath, "pkgB");
        var ex = Assert.Throws<InvalidOperationException>(() => info.Init(otherPath));
        Assert.That(ex!.Message, Does.Contain("already initialized"));
    }

    [Test]
    [TestCase("dotnet")]
    [TestCase("java")]
    [TestCase("python")]
    [TestCase("typescript")]
    [TestCase("go")]
    public void CommonProperties_AreDerivedCorrectly(string language)
    {
        string group = language == "go" ? "security" : string.Empty; // Representative group for go
        var service = language == "go" ? "keyvault" : "storage";
        var package = language switch { "dotnet" => "Azure.Storage.Blobs", "java" => "azure-storage-blob", "go" => "azkeys", _ => "storage-blob" };
        var servicePath = language == "go" ? Path.Combine(group, service) : service;
        var (pkgPath, gitHelper) = CreateSdkPackage(language, servicePath, package);
        var info = CreateForLanguage(language, gitHelper);
        info.Init(pkgPath);

        Assert.Multiple(() =>
        {
            Assert.That(info.IsInitialized, Is.True, "Should be initialized after Init");
            Assert.That(info.PackagePath, Is.EqualTo(RealPath.GetRealPath(Path.GetFullPath(pkgPath))));
            Assert.That(info.RepoRoot, Does.EndWith($"azure-sdk-for-{language}"));
            var expectedRelative = language == "go" ? Path.Combine(group, service, package) : Path.Combine(service, package);
            Assert.That(info.RelativePath, Is.EqualTo(expectedRelative));
            Assert.That(info.ServiceName, Is.EqualTo(service));
            Assert.That(info.PackageName, Is.EqualTo(package));
            Assert.That(info.Language, Is.EqualTo(language));
        });
    }

    [Test]
    public void Init_InvalidPath_Throws()
    {
        var badRoot = Path.Combine(_tempRoot.DirectoryPath, "azure-sdk-for-python", "storage", "notUnderSdkProperly");
        Directory.CreateDirectory(badRoot);
        // Need git at repo root
        Repository.Init(Path.Combine(_tempRoot.DirectoryPath, "azure-sdk-for-python"));
        var ghMock = new Mock<IGitHubService>();
        var gitHelper = new GitHelper(ghMock.Object, new TestLogger<GitHelper>());
        var info = new PythonPackageInfo(gitHelper);
        Assert.Throws<ArgumentException>(() => info.Init(badRoot));
    }

    [Test]
    [TestCase("dotnet", "Azure.Data.Test", "<Project><PropertyGroup><Version>5.6.7</Version></PropertyGroup></Project>", ".csproj", "5.6.7")]
    [TestCase("java", "azure-core", "<project><modelVersion>4.0.0</modelVersion><groupId>com.azure</groupId><artifactId>azure-core</artifactId><version>1.2.3</version></project>", "pom.xml", "1.2.3")]
    [TestCase("python", "azure-ai", "[project]\nname='azure-ai'\nversion='1.0.1'\n", "pyproject.toml", "1.0.1")]
    [TestCase("typescript", "azure-testpkg", "{\n  \"name\": \"@azure/azure-testpkg\",\n  \"version\": \"2.3.4\"\n}", "package.json", "2.3.4")]
    [TestCase("go", "azkeys", "package azkeys\n\nconst (\n    moduleName = \"github.com/Azure/azure-sdk-for-go/sdk/security/keyvault/azkeys\"\n    version    = \"v1.4.1-beta.1\"\n)\n", "version.go", "v1.4.1-beta.1")]
    public async Task VersionParsing_Works(string language, string package, string fileContent, string fileName, string expectedVersion)
    {
        var servicePath = language == "go" ? "security/keyvault" : "ai";
        var (pkgPath, gitHelper) = CreateSdkPackage(language, servicePath, package);
        File.WriteAllText(Path.Combine(pkgPath, fileName), fileContent);
        var info = CreateForLanguage(language, gitHelper);
        info.Init(pkgPath);
        var parsed = await info.GetPackageVersionAsync();
        Assert.That(parsed, Is.EqualTo(expectedVersion));
    }

    [Test]
    [TestCase("dotnet", "Azure.Data.Empty")]
    [TestCase("java", "azure-empty")]
    [TestCase("python", "azure-empty")]
    [TestCase("typescript", "azure-empty")]
    [TestCase("go", "azempty")]
    public async Task VersionParsing_MissingFile_ReturnsNull(string language, string package)
    {
        var servicePath = language == "go" ? "security/keyvault" : "missing";
        var (pkgPath, gitHelper) = CreateSdkPackage(language, servicePath, package);
        var info = CreateForLanguage(language, gitHelper);
        info.Init(pkgPath);
        var parsed = await info.GetPackageVersionAsync();
        Assert.That(parsed, Is.Null);
    }
}