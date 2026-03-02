// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Moq;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

/// <summary>
/// Contract-focused tests for all language-specific <see cref="IPackageInfoHelper"/> implementations.
/// Ensures consistent parsing of repo root, relative path, and language-specific version extraction
/// without duplicating per-language edge case tests.
/// </summary>
[TestFixture]
public class LanguageServicePackageInfoTests
{
    private List<LanguageService> languageServices = null!;
    private TempDirectory tempRoot = null!;

    [SetUp]
    public void Setup()
    {
        var gitCommandHelper = new GitCommandHelper(Mock.Of<ILogger<GitCommandHelper>>(), Mock.Of<IRawOutputHelper>());
        var gitHelper = new Mock<GitHelper>(Mock.Of<IGitHubService>(), gitCommandHelper, Mock.Of<ILogger<GitHelper>>());
        var processHelper = new ProcessHelper(new TestLogger<ProcessHelper>(), Mock.Of<IRawOutputHelper>());
        var pythonHelper = new PythonHelper(new TestLogger<PythonHelper>(), Mock.Of<IRawOutputHelper>());
        var packageInfoHelper = new PackageInfoHelper(new TestLogger<PackageInfoHelper>(), gitHelper.Object);
        languageServices = [
            new DotnetLanguageService(processHelper, Mock.Of<IPowershellHelper>(), gitHelper.Object, new TestLogger<DotnetLanguageService>(), Mock.Of<ICommonValidationHelpers>(), packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new JavaLanguageService(new Mock<IProcessHelper>().Object, gitHelper.Object, new Mock<IMavenHelper>().Object, new Mock<ICopilotAgentRunner>().Object, new TestLogger<JavaLanguageService>(), Mock.Of<ICommonValidationHelpers>(), packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new PythonLanguageService(new Mock<IProcessHelper>().Object, pythonHelper, new Mock<INpxHelper>().Object, gitHelper.Object, new TestLogger<PythonLanguageService>(), Mock.Of<ICommonValidationHelpers>(), packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new JavaScriptLanguageService(new Mock<IProcessHelper>().Object, new Mock<INpxHelper>().Object, gitHelper.Object, new TestLogger<JavaScriptLanguageService>(), Mock.Of<ICommonValidationHelpers>(), packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new GoLanguageService(processHelper, Mock.Of<IPowershellHelper>(), gitHelper.Object, new TestLogger<GoLanguageService>(), Mock.Of<ICommonValidationHelpers>(), packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>())
        ];

        tempRoot = TempDirectory.Create("azsdk_pkginfo_contract_tests");
    }

    [TearDown]
    public void TearDown() => tempRoot.Dispose();

    private static void CreateTestFile(string packagePath, string relativePath, string content)
    {
        var fullPath = Path.Combine(packagePath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
    }

    private static void SetupDotNetPackage(string packagePath, string packageName, string version, SdkType sdkType)
    {
        var sdkTypeValue = sdkType switch
        {
            SdkType.Dataplane => "client",
            SdkType.Management => "mgmt",
            SdkType.Functions => "functions",
            _ => "client"
        };

        var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <Target Name=""GetPackageInfo"" Returns=""@(PackageInfoItem)"">
    <ItemGroup>
      <PackageInfoItem Include=""'$(MSBuildProjectDirectory)' 'testservice' '{packageName}' '{version}' '{sdkTypeValue}' 'true' 'bin/Release/net8.0' 'false'"" />
    </ItemGroup>
  </Target>
</Project>";
        CreateTestFile(packagePath, $"src/{packageName}.csproj", csprojContent);
    }

    private static void SetupJavaPackage(string packagePath, string artifactId, string version)
    {
        CreateTestFile(packagePath, "pom.xml", $"<project><modelVersion>4.0.0</modelVersion><groupId>com.azure</groupId><artifactId>{artifactId}</artifactId><version>{version}</version></project>");
    }

    private static void SetupPythonPackage(string packagePath, string packageName, string version)
    {
        // packagePath is like: /tmp/xxx/azure-sdk-for-python/sdk/service/package
        // repoRoot is: /tmp/xxx/azure-sdk-for-python (3 levels up from package)
        var repoRoot = Path.GetFullPath(Path.Combine(packagePath, "..", "..", ".."));

        // Create a minimal Python script that outputs the package info
        var scriptContent = $@"#!/usr/bin/env python
import sys

# Simple mock that returns the expected format
# Format: <name> <version> <is_new_sdk> <directory> <dependent_packages>
package_name = '{packageName}'
version = '{version}'

print(f'{{package_name}} {{version}} True {{sys.argv[-1]}} ')
";
        CreateTestFile(repoRoot, Path.Combine("eng", "scripts", "get_package_properties.py"), scriptContent);
    }

    private static void SetupJavaScriptPackage(string packagePath, string packageName, string version, SdkType sdkType)
    {
        CreateTestFile(packagePath, "package.json", $$"""
{
  "name": "@azure/{{packageName}}",
  "version": "{{version}}",
  "sdk-type": "{{sdkType switch
        {
            SdkType.Dataplane => "client",
            SdkType.Management => "mgmt",
            _ => ""
        }}}"
}
""");
    }

    private static Task SetupGoPackageAsync(string packagePath, string version)
    {
        CreateTestFile(packagePath, "go.mod", "module github.com/Azure/azure-sdk-for-go/sdk/test/testpkg\ngo 1.24.0\n");
        CreateTestFile(packagePath, "internal/version.go", $"package internal\n\nconst Version = \"v{version}\"\n");
        return Task.CompletedTask;
    }


    [Test]
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net", "storage", "Azure.Storage.Blobs")]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java", "storage", "azure-storage-blob")]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python", "storage", "storage-blob")]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js", "storage", "storage-blob")]
    [TestCase(SdkLanguage.Go, "azure-sdk-for-go", "security/keyvault", "azkeys")]
    public async Task CommonProperties_AreDerivedCorrectly(SdkLanguage language, string repoName, string serviceDirectory, string package)
    {
        var service = serviceDirectory.Contains('/') ? serviceDirectory.Split('/')[^1] : serviceDirectory;
        var repoRoot = Path.Combine(tempRoot.DirectoryPath, repoName);
        var sdkPath = Path.Combine(repoRoot, "sdk", service, package);

        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git")))
        {
            await GitTestHelper.GitInitAsync(repoRoot);
        }
        Directory.CreateDirectory(sdkPath);

        var sdkLanguage = SdkLanguageHelpers.GetLanguageForRepo(repoName);
        var languageService = languageServices.First(s => s.Language == sdkLanguage);
        var info = await languageService.GetPackageInfo(sdkPath);

        Assert.Multiple(() =>
        {
            Assert.That(info.PackagePath, Is.EqualTo(RealPath.GetRealPath(sdkPath)));
            Assert.That((string)info.RepoRoot, Does.EndWith(repoName));
            var expectedRelative = Path.Combine(service, package);
            Assert.That(info.RelativePath, Is.EqualTo(expectedRelative));
            Assert.That(info.ServiceName, Is.EqualTo(service));
            Assert.That(info.PackageName, Is.Null);
            Assert.That(info.Language, Is.EqualTo(language));
        });
    }

    [Test]
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net", "data", "Azure.Data.Test", "5.6.7")]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java", "core", "azure-core", "1.2.3")]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python", "ai", "azure-ai-test", "1.0.1")]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js", "test", "azure-testpkg", "2.3.4")]
    [TestCase(SdkLanguage.Go, "azure-sdk-for-go", "security/keyvault", "azkeys", "1.4.1-beta.1")]
    public async Task VersionParsing_Works(SdkLanguage language, string repoName, string serviceDirectory, string package, string expectedVersion)
    {
        var service = serviceDirectory.Contains('/') ? serviceDirectory.Split('/')[^1] : serviceDirectory;
        var repoRoot = Path.Combine(tempRoot.DirectoryPath, repoName);
        var sdkPath = Path.Combine(repoRoot, "sdk", service, package);

        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git")))
        {
            await GitTestHelper.GitInitAsync(repoRoot);
        }
        Directory.CreateDirectory(sdkPath);

        switch (language)
        {
            case SdkLanguage.DotNet:
                SetupDotNetPackage(sdkPath, package, expectedVersion, SdkType.Unknown);
                break;
            case SdkLanguage.Java:
                SetupJavaPackage(sdkPath, package, expectedVersion);
                break;
            case SdkLanguage.Python:
                SetupPythonPackage(sdkPath, package, expectedVersion);
                break;
            case SdkLanguage.JavaScript:
                SetupJavaScriptPackage(sdkPath, package, expectedVersion, SdkType.Unknown);
                break;
            case SdkLanguage.Go:
                await SetupGoPackageAsync(sdkPath, expectedVersion);
                break;
        }

        var languageService = languageServices.First(s => s.Language == language);
        var info = await languageService.GetPackageInfo(sdkPath);
        Assert.That(info.PackageVersion, Is.EqualTo(expectedVersion));
    }

    [Test]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js", "storage", "azure-storage-blob", SdkType.Dataplane)]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js", "storage", "azure-storage-blob", SdkType.Management)]
    public async Task SdkType_IsDerivedCorrectly(SdkLanguage language, string repoName, string serviceDirectory, string package, SdkType sdkType)
    {
        var service = serviceDirectory.Contains('/') ? serviceDirectory.Split('/')[^1] : serviceDirectory;
        var repoRoot = Path.Combine(tempRoot.DirectoryPath, repoName);
        var sdkPath = Path.Combine(repoRoot, "sdk", service, package);

        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git")))
        {
            await GitTestHelper.GitInitAsync(repoRoot);
        }
        Directory.CreateDirectory(sdkPath);

        SetupJavaScriptPackage(sdkPath, package, "1.2.3", sdkType);

        var languageService = languageServices.First(s => s.Language == language);
        var info = await languageService.GetPackageInfo(sdkPath);
        Assert.That(info.SdkType, Is.EqualTo(sdkType));
    }

    [Test]
    [TestCase("sdk/resourcemanager/workloads/armworkloads", SdkType.Management)]
    [TestCase("sdk/security/keyvault/azadmin", SdkType.Dataplane)]
    public async Task GoSdkType_IsDerivedCorrectly(string packagePathUnderRepo, SdkType sdkType)
    {
        var repoRoot = Path.Combine(tempRoot.DirectoryPath, "azure-sdk-for-go");
        var sdkPath = Path.Combine(repoRoot, packagePathUnderRepo.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git")))
        {
            await GitTestHelper.GitInitAsync(repoRoot);
        }

        Directory.CreateDirectory(sdkPath);
        await SetupGoPackageAsync(sdkPath, "1.2.3");

        var languageService = languageServices.First(s => s.Language == SdkLanguage.Go);
        var info = await languageService.GetPackageInfo(sdkPath);

        Assert.Multiple(() =>
        {
            Assert.That(info.PackageName, Is.EqualTo(packagePathUnderRepo));
            Assert.That(info.ServiceDirectory, Is.EqualTo(packagePathUnderRepo["sdk/".Length..]));
            Assert.That(info.SdkType, Is.EqualTo(sdkType));
        });
    }

    [Test]
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net", "missing", "Azure.Data.Empty")]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java", "missing", "azure-empty")]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python", "missing", "azure-empty")]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js", "missing", "azure-empty")]
    [TestCase(SdkLanguage.Go, "azure-sdk-for-go", "security/keyvault", "azempty")]
    public async Task VersionParsing_MissingFile_ReturnsNull(SdkLanguage language, string repoName, string serviceDirectory, string package)
    {
        var service = serviceDirectory.Contains('/') ? serviceDirectory.Split('/')[^1] : serviceDirectory;
        var repoRoot = Path.Combine(tempRoot.DirectoryPath, repoName);
        var sdkPath = Path.Combine(repoRoot, "sdk", service, package);

        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git")))
        {
            await GitTestHelper.GitInitAsync(repoRoot);
        }
        Directory.CreateDirectory(sdkPath);

        var languageService = languageServices.First(s => s.Language == language);
        var info = await languageService.GetPackageInfo(sdkPath);
        Assert.That(info.PackageVersion, Is.Null);
    }
}
