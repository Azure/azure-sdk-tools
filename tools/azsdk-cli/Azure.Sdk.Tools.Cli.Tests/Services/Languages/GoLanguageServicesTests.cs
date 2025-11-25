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
        private static string GoProgram => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "go.exe" : "go";
        private GoLanguageService LangService { get; set; } = null!;

        private readonly Version goMinimumVersion = Version.Parse("1.24");

        [SetUp]
        public async Task SetUp()
        {
            tempDir = TempDirectory.Create("golang_checks");
            var mockGitHubService = new Mock<IGitHubService>();
            var gitHelper = new GitHelper(mockGitHubService.Object, NullLogger<GitHelper>.Instance);
            LangService = new GoLanguageService(
                new ProcessHelper(NullLogger<ProcessHelper>.Instance, Mock.Of<IRawOutputHelper>()),
                gitHelper,
                NullLogger<GoLanguageService>.Instance, Mock.Of<ICommonValidationHelpers>(),
                Mock.Of<IFileHelper>());

            if (!await LangService.CheckDependencies(CancellationToken.None))
            {
                Assert.Ignore("golang tooling dependencies are not installed, can't run GoLanguageSpecificChecksTests");
            }

            var resp = await LangService.CreateEmptyPackage(tempDir.DirectoryPath, "untitleddotloop", CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));

            // check that our current version of Go is new enough for these tests.
            var version = await GoLanguageService.GetGoModVersionAsync(Path.Join(tempDir.DirectoryPath, "go.mod"));

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
            await File.WriteAllTextAsync(Path.Combine(tempDir.DirectoryPath, "main.go"), """
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
                WorkingDirectory = tempDir.DirectoryPath
            })!.WaitForExitAsync();

            var resp = await LangService.AnalyzeDependencies(tempDir.DirectoryPath, false, CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));

            var goModPath = Path.Join(tempDir.DirectoryPath, "go.mod");

            var identityLine = File.ReadAllLines(goModPath)
                .Where(line => line.Contains("azidentity"))
                .Select(line => line.Trim())
                .First();
            Assert.That(identityLine, Is.Not.EqualTo("github.com/Azure/azure-sdk-for-go/sdk/azidentity v1.10.0"), "go get updates dependencies properly");

            var currentVersion = await GoLanguageService.GetGoModVersionAsync(goModPath);
            Assert.That(currentVersion, Is.GreaterThanOrEqualTo(goMinimumVersion));

            resp = await LangService.FormatCode(tempDir.DirectoryPath, false, CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));

            resp = await LangService.BuildProject(tempDir.DirectoryPath, CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));

            resp = await LangService.LintCode(tempDir.DirectoryPath, false, CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public async Task TestGoLanguageSpecificChecksCompileErrors()
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir.DirectoryPath, "main.go"), """
                package main

                func main() {
                    syntax error
                }
                """);

            var resp = await LangService.BuildProject(tempDir.DirectoryPath, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(resp.ExitCode, Is.EqualTo(1));
                Assert.That(resp.CheckStatusDetails, Does.Contain("syntax error: unexpected name error at end of statement"));
            });
        }

        [Test]
        public async Task TestGoLanguageSpecificChecksLintErrors()
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir.DirectoryPath, "main.go"), """
                package main

                import (
                )

                func unusedFunc() {}

                func main() {
                }
                """);

            var resp = await LangService.LintCode(tempDir.DirectoryPath, false, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(resp.ExitCode, Is.EqualTo(1));
                Assert.That(resp.CheckStatusDetails, Does.Contain("is unused (unused)"));
            });
        }

        [Test]
        public async Task TestGetSDKPackageName()
        {
            Assert.That(
                await LangService.GetSDKPackageName(Path.Combine("/hello", "world", "az") + Path.DirectorySeparatorChar, Path.Combine("/hello", "world", "az", "sdk", "messaging", "azservicebus")),
                Is.EqualTo(Path.Combine("sdk", "messaging", "azservicebus"))
            );

            Assert.That(
                await LangService.GetSDKPackageName(Path.Combine("/hello", "world", "az"), Path.Combine("/hello", "world", "az", "sdk", "messaging", "azservicebus")),
                Is.EqualTo(Path.Combine("sdk", "messaging", "azservicebus")));
        }

        [Test]
        public async Task TestGetPackageInfo()
        {
            var actualSdkRepo = IgnoreTestIfRepoNotConfigured();
            var packageInfo = await LangService.GetPackageInfo(Path.Join(actualSdkRepo, "sdk/messaging/azservicebus"));

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
    }
}
