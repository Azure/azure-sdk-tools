// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class PackageInfoTests
{
    private TempDirectory _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = TempDirectory.Create("azsdk_pkginfo_tests");
    }

    [TearDown]
    public void TearDown()
    {
        _tempRoot.Dispose();
    }

    private string CreateSdkPackage(string language, string relativeSubPath)
    {
        // Simulate repo root structure: <root>/sdk/<service>/<package>
    var repoRoot = Path.Combine(_tempRoot.DirectoryPath, "azure-sdk-for-" + language);
        var packagePath = Path.Combine(repoRoot, "sdk", relativeSubPath);
        Directory.CreateDirectory(packagePath);
        return packagePath;
    }

    [Test]
    public void Init_InvalidPath_Throws()
    {
        // Create a directory that doesn't include the required '/sdk/' separator
    var badPath = Path.Combine(_tempRoot.DirectoryPath, "azure-sdk-for-js", "storage", "packageOnly");
        Directory.CreateDirectory(badPath);
        var info = new PythonPackageInfo();
        Assert.Throws<ArgumentException>(() => info.Init(badPath)); // path validation still enforced
    }

    [Test]
    public void BasicProperties_Work()
    {
        var pkgPath = CreateSdkPackage("python", Path.Combine("service", "mypkg"));
        var info = new PythonPackageInfo();
        info.Init(pkgPath);

        Assert.That(info.PackagePath, Is.EqualTo(Path.GetFullPath(pkgPath)));
        Assert.That(info.RepoRoot, Does.Contain("azure-sdk-for-python"));
        Assert.That(info.RelativePath, Is.EqualTo(Path.Combine("service", "mypkg")));
        Assert.That(info.ServiceName, Is.EqualTo("service"));
        Assert.That(info.PackageName, Is.EqualTo("mypkg"));
        Assert.That(info.Language, Is.EqualTo("python"));
    }

    [Test]
    public async Task GetPackageVersion_FromPackageJson()
    {
        var pkgPath = CreateSdkPackage("js", Path.Combine("storage", "azpkg"));
        File.WriteAllText(Path.Combine(pkgPath, "package.json"), "{\n  \"name\": \"@azure/azpkg\",\n  \"version\": \"2.3.4\"\n}");
        var info = new TypeScriptPackageInfo();
        info.Init(pkgPath);
    Assert.That(await info.GetPackageVersionAsync(), Is.EqualTo("2.3.4"));
    }

    [Test]
    public async Task GetPackageVersion_FromPyProject()
    {
        var pkgPath = CreateSdkPackage("python", Path.Combine("ai", "azai"));
        File.WriteAllText(Path.Combine(pkgPath, "pyproject.toml"), "[project]\nname='azai'\nversion='1.0.1'\n");
        var info = new PythonPackageInfo();
        info.Init(pkgPath);
    Assert.That(await info.GetPackageVersionAsync(), Is.EqualTo("1.0.1"));
    }

    [Test]
    public async Task GetPackageVersion_FromCsProj()
    {
        var pkgPath = CreateSdkPackage("net", Path.Combine("data", "Azure.Data.Test"));
        File.WriteAllText(Path.Combine(pkgPath, "Azure.Data.Test.csproj"), "<Project><PropertyGroup><Version>5.6.7</Version></PropertyGroup></Project>");
        var info = new DotNetPackageInfo();
        info.Init(pkgPath);
    Assert.That(await info.GetPackageVersionAsync(), Is.EqualTo("5.6.7"));
    }

    [Test]
    public async Task GetPackageVersion_FromGo_VersionGo()
    {
    var pkgPath = CreateSdkPackage("go", Path.Combine("security", "keyvault", "azkeys"));
        File.WriteAllText(Path.Combine(pkgPath, "version.go"), "package azkeys\n\nconst (\n    moduleName = \"github.com/Azure/azure-sdk-for-go/sdk/security/keyvault/azkeys\"\n    version    = \"v1.4.1-beta.1\"\n)\n");
        var info = new GoPackageInfo();
        info.Init(pkgPath);
    Assert.That(await info.GetPackageVersionAsync(), Is.EqualTo("v1.4.1-beta.1"));
    }
}
