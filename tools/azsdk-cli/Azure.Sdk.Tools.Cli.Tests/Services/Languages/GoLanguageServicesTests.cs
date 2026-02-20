using System.Diagnostics;
using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    internal class GoLanguageServiceTests
    {
        private TempDirectory tempDir = null!;
        private string packagePath = "";

        private static string GoProgram => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "go.exe" : "go";

        private GoLanguageService LangService { get; set; } = null!;

        private readonly Version goMinimumVersion = Version.Parse("1.24");

        [SetUp]
        public async Task SetUp()
        {
            // we'll end up with <tmp>/golang_checks<random>/sdk/template/aztemplate.
            tempDir = TempDirectory.Create("golang_checks");

            var processHelper = new ProcessHelper(NullLogger<ProcessHelper>.Instance, Mock.Of<IRawOutputHelper>());
            var pr = await processHelper.Run(new ProcessOptions("git", "git.exe", ["init", "."], workingDirectory: tempDir.DirectoryPath), CancellationToken.None);
            Assert.That(pr.ExitCode, Is.EqualTo(0));

            packagePath = Path.Combine(tempDir.DirectoryPath, "sdk", "template", "aztemplate");
            Directory.CreateDirectory(packagePath);

            LangService = new GoLanguageService(
                processHelper,
                new PowershellHelper(NullLogger<PowershellHelper>.Instance, Mock.Of<IRawOutputHelper>()),
                new GitHelper(Mock.Of<IGitHubService>(), new GitCommandHelper(NullLogger<GitCommandHelper>.Instance, Mock.Of<IRawOutputHelper>()), NullLogger<GitHelper>.Instance),
                NullLogger<GoLanguageService>.Instance, Mock.Of<ICommonValidationHelpers>(),
                Mock.Of<IFileHelper>(),
                Mock.Of<ISpecGenSdkConfigHelper>(),
                Mock.Of<IChangelogHelper>());

            if (!await LangService.CheckDependencies(CancellationToken.None))
            {
                Assert.Ignore("golang tooling dependencies are not installed, can't run GoLanguageSpecificChecksTests");
            }

            await LangService.CreateEmptyPackage(packagePath, "github.com/Azure/azure-sdk-for-go/sdk/template/aztemplate", CancellationToken.None);

            // check that our current version of Go is new enough for these tests.
            var version = await GoLanguageService.GetGoModVersionAsync(Path.Join(packagePath, "go.mod"));

            if (version.CompareTo(goMinimumVersion) < 0)
            {
                Assert.Ignore($"You'll need Go {goMinimumVersion}+, in the path, for these tests");
            }
        }

        [TearDown]
        public void TearDown()
        {
            tempDir.Dispose();
        }

        [Test]
        public async Task TestGoLanguageSpecificChecksBasic()
        {
            await File.WriteAllTextAsync(Path.Combine(packagePath, "main.go"), """
                package main

                import (
                    "github.com/Azure/azure-sdk-for-go/sdk/azidentity"
                )

                func main() {
                    cred, err := azidentity.NewDefaultAzureCredential(nil)

                    if cred == nil || err == nil {
                        panic("No!")
                    }
                }
                """);

            await Process.Start(new ProcessStartInfo()
            {
                FileName = GoProgram,
                ArgumentList = { "get", "github.com/Azure/azure-sdk-for-go/sdk/azidentity@v1.10.0" },
                WorkingDirectory = packagePath
            })!.WaitForExitAsync();

            var resp = await LangService.AnalyzeDependencies(packagePath, false, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(resp.ExitCode, Is.EqualTo(0));
                Assert.That(resp.PackageName, Is.EqualTo("sdk/template/aztemplate"));
                Assert.That(resp.Language, Is.EqualTo(SdkLanguage.Go));
            });

            var goModPath = Path.Join(packagePath, "go.mod");

            var identityLine = File.ReadAllLines(goModPath)
                .Where(line => line.Contains("azidentity"))
                .Select(line => line.Trim())
                .First();
            Assert.That(identityLine, Is.Not.EqualTo("github.com/Azure/azure-sdk-for-go/sdk/azidentity v1.10.0"), "go get updates dependencies properly");

            var currentVersion = await GoLanguageService.GetGoModVersionAsync(goModPath);
            Assert.That(currentVersion, Is.GreaterThanOrEqualTo(goMinimumVersion));

            resp = await LangService.FormatCode(packagePath, false, CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));

            resp = await LangService.BuildProject(packagePath, CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public async Task TestGoLanguageSpecificChecksCompileErrors()
        {
            await File.WriteAllTextAsync(Path.Combine(packagePath, "main.go"), """
                package main

                func main() {
                    syntax error
                }
                """);

            var resp = await LangService.BuildProject(packagePath, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(resp.ExitCode, Is.EqualTo(1));
                Assert.That(resp.CheckStatusDetails, Does.Contain("syntax error: unexpected name error at end of statement"));
            });
        }

        [Test]
        public async Task TestGoLanguageLinting()
        {
            var goRepoRoot = IgnoreTestIfRepoNotConfigured();

            var resp = await LangService.LintCode(Path.Join(goRepoRoot, "sdk", "template", "aztemplate"));

            Assert.Multiple(() =>
            {
                Assert.That(resp.ExitCode, Is.EqualTo(0));
                Assert.That(resp.PackageName, Is.EqualTo("sdk/template/aztemplate"));
                Assert.That(resp.Language, Is.EqualTo(SdkLanguage.Go));
            });
        }

        [Test]
        public async Task TestGetPackageInfo()
        {
            var actualSdkRepo = IgnoreTestIfRepoNotConfigured();
            var fullPackagePath = Path.Join(actualSdkRepo, "sdk/messaging/azservicebus");
            var packageInfo = await LangService.GetPackageInfo(fullPackagePath);

            Assert.Multiple(() =>
            {
                Assert.That(packageInfo.SdkType, Is.EqualTo(SdkType.Dataplane));
                Assert.That(packageInfo.PackageName, Is.EqualTo("sdk/messaging/azservicebus"));
                Assert.That(packageInfo.ServiceName, Is.EqualTo("messaging"));

                Assert.That(packageInfo.PackageName, Is.Not.Null.And.Not.Empty);
                Assert.That(packageInfo.PackageVersion, Is.Not.Null.And.Not.Empty);
            });

            packageInfo = await LangService.GetPackageInfo(Path.Join(actualSdkRepo, "sdk/security/keyvault/azadmin"));

            Assert.Multiple(() =>
            {
                Assert.That(packageInfo.SdkType, Is.EqualTo(SdkType.Dataplane));
                Assert.That(packageInfo.PackageName, Is.EqualTo("sdk/security/keyvault/azadmin"));
                Assert.That(packageInfo.ServiceName, Is.EqualTo("keyvault"));

                Assert.That(packageInfo.PackageName, Is.Not.Null.And.Not.Empty);
                Assert.That(packageInfo.PackageVersion, Is.Not.Null.And.Not.Empty);
            });

            packageInfo = await LangService.GetPackageInfo(Path.Join(actualSdkRepo, "sdk/storage/azblob"));

            Assert.Multiple(() =>
            {
                Assert.That(packageInfo.SdkType, Is.EqualTo(SdkType.Dataplane));
                Assert.That(packageInfo.PackageName, Is.EqualTo("sdk/storage/azblob"));
                Assert.That(packageInfo.ServiceName, Is.EqualTo("storage"));

                Assert.That(packageInfo.PackageName, Is.Not.Null.And.Not.Empty);
                Assert.That(packageInfo.PackageVersion, Is.Not.Null.And.Not.Empty);
            });

            packageInfo = await LangService.GetPackageInfo(Path.Join(actualSdkRepo, "sdk/resourcemanager/workloads/armworkloads"));

            Assert.Multiple(() =>
            {
                Assert.That(packageInfo.SdkType, Is.EqualTo(SdkType.Management));

                Assert.That(packageInfo.PackageName, Is.EqualTo("sdk/resourcemanager/workloads/armworkloads"));
                Assert.That(packageInfo.ServiceName, Is.EqualTo("workloads"));

                Assert.That(packageInfo.PackageName, Is.Not.Null.And.Not.Empty);
                Assert.That(packageInfo.PackageVersion, Is.Not.Null.And.Not.Empty);
            });
        }

        [Test]
        public async Task TestLegacyGoMod()
        {
            using var tempFolder = TempDirectory.Create("legacy_go_mod");
            var goModPath = Path.Join(tempFolder.DirectoryPath, "go.mod");

            File.WriteAllText(goModPath, $"""
                module myfakemodule
                go 1.23.0

                require (
                    golang.org/x/net v0.42.0
                )
                """);

            File.WriteAllText(Path.Join(tempFolder.DirectoryPath, "main.go"), """
                package main

                import (
                    ""html""
                    ""golang.org/x/net/html""
                )

                func main() {
                    _ = html.EscapeString(""hello"")
                }
                """);

            await LangService.AnalyzeDependencies(tempFolder.DirectoryPath, true);

            // check that we didn't upgrade the Go version.
            var version = await GoLanguageService.GetGoModVersionAsync(goModPath);
            Assert.That(version, Is.EqualTo(Version.Parse("1.23.0")));
        }

        [Test]
        public void TestGetGoModVersionAsync()
        {
            using var tempDir = TempDirectory.Create("go_mod_test");
            var goModPath = Path.Join(tempDir.DirectoryPath, "go.mod");
            File.WriteAllText(goModPath, "there's no version in here!");

            Assert.ThrowsAsync(typeof(Exception), async () => await GoLanguageService.GetGoModVersionAsync(goModPath));
        }

        [Test]
        public async Task TestGetSubPath()
        {
            var subPath = await LangService.GetSubPath(packagePath);
            Assert.That(subPath, Is.EqualTo("sdk/template/aztemplate"));
        }

        /// <summary>
        /// Ignores the test if you don't have a path to a real Go repo configured.
        /// </summary>
        /// <returns></returns>
        private static string IgnoreTestIfRepoNotConfigured()
        {
            // Setting this lets you run tests that require a live environment.
            const string GoLiveTestVarName = "AZSDK_CLI_TEST_AZSDKGO";
            var actualSdkRepo = Environment.GetEnvironmentVariable(GoLiveTestVarName);

            if (actualSdkRepo != null)
            {
                return actualSdkRepo;
            }

            Assert.Ignore("Live testing disabled for GoLanguageServiceTests: AZSDK_CLI_TEST_AZSDKGO is not set to a Go repo path");
            return "";
        }

        #region HasCustomizations Tests

        [Test]
        public void HasCustomizations_ReturnsPath_WhenInternalGenerateDirectoryExists()
        {
            var customizationDir = Path.Combine(packagePath, "internal", "generate");
            Directory.CreateDirectory(customizationDir);

            var result = LangService.HasCustomizations(packagePath, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo(customizationDir));
        }

        [Test]
        public void HasCustomizations_ReturnsPath_WhenTestdataGenerateDirectoryExists()
        {
            var customizationDir = Path.Combine(packagePath, "testdata", "generate");
            Directory.CreateDirectory(customizationDir);

            var result = LangService.HasCustomizations(packagePath, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo(customizationDir));
        }

        [Test]
        public void HasCustomizations_ReturnsNull_WhenNoCustomizationDirectoryExists()
        {
            // packagePath is already created without customization directories
            var result = LangService.HasCustomizations(packagePath, CancellationToken.None);

            Assert.That(result, Is.Null);
        }

        #endregion
    }
}
