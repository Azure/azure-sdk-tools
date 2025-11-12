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
        // Setting this lets you run tests that require a live environment.
        private readonly string? actualSdkRepo = Environment.GetEnvironmentVariable("AZSDK_CLI_TEST_AZSDKGO");

        private TempDirectory tempDir = null!;
        private static string GoProgram => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "go.exe" : "go";
        private GoLanguageService LangService { get; set; } = null!;

        [SetUp]
        public async Task SetUp()
        {
            tempDir = TempDirectory.Create("golang_checks");
            var mockGitHubService = new Mock<IGitHubService>();
            var gitHelper = new GitHelper(mockGitHubService.Object, NullLogger<GitHelper>.Instance);
            LangService = new GoLanguageService(
                new ProcessHelper(NullLogger<ProcessHelper>.Instance, Mock.Of<IRawOutputHelper>()),
                gitHelper,
                NullLogger<GoLanguageService>.Instance, Mock.Of<ICommonValidationHelpers>());

            if (!await LangService.CheckDependencies(CancellationToken.None))
            {
                Assert.Ignore("golang tooling dependencies are not installed, can't run GoLanguageSpecificChecksTests");
            }

            var resp = await LangService.CreateEmptyPackage(tempDir.DirectoryPath, "untitleddotloop", CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));
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
                    "github.com/Azure/azure-sdk-for-go/sdk/messaging/azservicebus"      // an unused dep we're going to remove
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

            var identityLine = File.ReadAllLines(Path.Join(tempDir.DirectoryPath, "go.mod"))
                .Where(line => line.Contains("azidentity"))
                .Select(line => line.Trim())
                .First();
            Assert.That(identityLine, Is.Not.EqualTo("github.com/Azure/azure-sdk-for-go/sdk/azidentity v1.10.0"));

            resp = await LangService.FormatCode(tempDir.DirectoryPath, false, CancellationToken.None);
            Assert.That(File.ReadAllText(Path.Join(tempDir.DirectoryPath, "main.go")), Does.Not.Contain("azservicebus"));

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

                import (
                )

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
            if (actualSdkRepo == null)
            {
                Assert.Ignore("Skipping test that uses a real Go repo");
            }

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
    }
}
