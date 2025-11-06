// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
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

    private (string packagePath, GitHelper gitHelper, IOutputHelper, IProcessHelper, IPowershellHelper, IMicroagentHostService, INpxHelper, ICommonValidationHelpers) CreateSdkPackage(string service, string package)
    {
        var repoRoot = Path.Combine(_tempRoot.DirectoryPath, "azure-sdk-repo-root");
        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git"))) { Repository.Init(repoRoot); }
        var sdkPath = Path.Combine(repoRoot, "sdk", service, package);
        Directory.CreateDirectory(sdkPath);
        var ghMock = new Mock<IGitHubService>();
        var outputMock = new Mock<IOutputHelper>();
        var processHelperMock = new Mock<IProcessHelper>();
        var powershellMock = new Mock<IPowershellHelper>();
        var gitHelper = new GitHelper(ghMock.Object, new TestLogger<GitHelper>());
        var microAgentMock = new Mock<IMicroagentHostService>();
        var npxHelperMock = new Mock<INpxHelper>();
        var commonValidationHelper = new Mock<ICommonValidationHelpers>();

        return (sdkPath, gitHelper, outputMock.Object, processHelperMock.Object, powershellMock.Object, microAgentMock.Object, npxHelperMock.Object, commonValidationHelper.Object);
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

    private void SetupDotNetPackage(string packagePath, string version)
    {
        CreateTestFile(packagePath, "src/test.csproj", $"<Project><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>");
    }

    private void SetupJavaPackage(string packagePath, string artifactId, string version)
    {
        CreateTestFile(packagePath, "pom.xml", $"<project><modelVersion>4.0.0</modelVersion><groupId>com.azure</groupId><artifactId>{artifactId}</artifactId><version>{version}</version></project>");
    }

    private void SetupPythonPackage(string packagePath, string packageName, string version)
    {
        CreateTestFile(packagePath, "sdk_packaging.toml", $"[packaging]\npackage_name = \"{packageName}\"\n");
        var modulePath = packageName.Replace('-', Path.DirectorySeparatorChar);
        CreateTestFile(packagePath, $"{modulePath}/_version.py", $"VERSION = \"{version}\"\n");
    }

    private void SetupJavaScriptPackage(string packagePath, string packageName, string version)
    {
        CreateTestFile(packagePath, "package.json", $"{{\n  \"name\": \"@azure/{packageName}\",\n  \"version\": \"{version}\"\n}}");
    }

    private void SetupGoPackage(string packagePath, string version)
    {
        CreateTestFile(packagePath, "version.go", $"package azkeys\n\nconst (\n    moduleName = \"github.com/Azure/azure-sdk-for-go/sdk/security/keyvault/azkeys\"\n    version    = \"{version}\"\n)\n");
    }

    private void SetupPackageForLanguage(SdkLanguage language, string packagePath, string packageName, string version)
    {
        switch (language)
        {
            case SdkLanguage.DotNet:
                SetupDotNetPackage(packagePath, version);
                break;
            case SdkLanguage.Java:
                SetupJavaPackage(packagePath, packageName, version);
                break;
            case SdkLanguage.Python:
                SetupPythonPackage(packagePath, packageName, version);
                break;
            case SdkLanguage.JavaScript:
                SetupJavaScriptPackage(packagePath, packageName, version);
                break;
            case SdkLanguage.Go:
                SetupGoPackage(packagePath, version);
                break;
        }
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
        var (pkgPath, gitHelper, outputHelper, processHelper, powershellHelper, microAgentMock, npxHelper, commonValidationHelper) = CreateSdkPackage(servicePath, package);
        var helper = CreateHelperForLanguage(language, gitHelper, outputHelper, processHelper, powershellHelper, microAgentMock, npxHelper, commonValidationHelper);
        var info = await helper.GetPackageInfo(pkgPath);

        Assert.Multiple(() =>
        {
            Assert.That(info.PackagePath, Is.EqualTo(RealPath.GetRealPath(pkgPath)));
            Assert.That(info.RepoRoot, Does.EndWith("azure-sdk-repo-root"));
            var expectedRelative = language == SdkLanguage.Go ? Path.Combine(group, service, package) : Path.Combine(service, package);
            Assert.That(info.RelativePath, Is.EqualTo(expectedRelative));
            Assert.That(info.ServiceName, Is.EqualTo(service));
            Assert.That(info.PackageName, Is.Null);
            Assert.That(info.Language, Is.EqualTo(language));
        });
    }

    [Test]
    [TestCase(SdkLanguage.DotNet, "Azure.Data.Test", "5.6.7")]
    [TestCase(SdkLanguage.Java, "azure-core", "1.2.3")]
    [TestCase(SdkLanguage.Python, "azure-ai-test", "1.0.1")]
    [TestCase(SdkLanguage.JavaScript, "azure-testpkg", "2.3.4")]
    [TestCase(SdkLanguage.Go, "azkeys", "v1.4.1-beta.1")]
    public async Task VersionParsing_Works(SdkLanguage language, string package, string expectedVersion)
    {
        var servicePath = language == SdkLanguage.Go ? "security/keyvault" : "ai";
        var (pkgPath, gitHelper, outputHelper, processHelper, powershellHelper, microAgentMock, npxHelper, commonValidationHelper) = CreateSdkPackage(servicePath, package);

        SetupPackageForLanguage(language, pkgPath, package, expectedVersion);

        var helper = CreateHelperForLanguage(language, gitHelper, outputHelper, processHelper, powershellHelper, microAgentMock, npxHelper, commonValidationHelper);
        var info = await helper.GetPackageInfo(pkgPath);
        Assert.That(info.PackageVersion, Is.EqualTo(expectedVersion));
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
        var (pkgPath, gitHelper, outputHelper, processHelper, powershellHelper, microAgentMock, npxHelper, commonValidationHelper) = CreateSdkPackage(servicePath, package);
        var helper = CreateHelperForLanguage(language, gitHelper, outputHelper, processHelper, powershellHelper, microAgentMock, npxHelper, commonValidationHelper);
        var info = await helper.GetPackageInfo(pkgPath);
        Assert.That(info.PackageVersion, Is.Null);
    }

    private static LanguageService CreateHelperForLanguage(SdkLanguage language, IGitHelper gitHelper, IOutputHelper outputHelper, IProcessHelper processHelper, IPowershellHelper powershellHelper, IMicroagentHostService microAgentMock, INpxHelper npxHelper, ICommonValidationHelpers commonValidationHelper) => language switch
    {
        ///var powershellHelper = new Mock<IPowershellHelper>();

        SdkLanguage.DotNet => new DotnetLanguageService(processHelper, powershellHelper, gitHelper, new TestLogger<DotnetLanguageService>(), commonValidationHelper),
        SdkLanguage.Java => new JavaLanguageService(processHelper, gitHelper, microAgentMock, new TestLogger<JavaLanguageService>(), commonValidationHelper),
        SdkLanguage.Python => new PythonLanguageService(processHelper, npxHelper, gitHelper, new TestLogger<PythonLanguageService>(), commonValidationHelper),
        SdkLanguage.JavaScript => new JavaScriptLanguageService(processHelper, npxHelper, gitHelper, new TestLogger<JavaScriptLanguageService>(), commonValidationHelper),
        SdkLanguage.Go => new GoLanguageService(processHelper, npxHelper, gitHelper, new TestLogger<GoLanguageService>(), commonValidationHelper),
        _ => throw new ArgumentException($"Unsupported language '{language}'", nameof(language))
    };
}
