using System.Diagnostics;
using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    internal class GoLanguageSpecificChecksTests
    {
        private string GoPackageDir { get; set; }
        private static string GoProgram => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "go.exe" : "go";

        private GoLanguageSpecificChecks LangService { get; set; }

        [SetUp]
        public async Task SetUp()
        {
            GoPackageDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(GoPackageDir);

            var mockGitHubService = new Mock<IGitHubService>();
            var gitHelper = new GitHelper(mockGitHubService.Object, NullLogger<GitHelper>.Instance);
            LangService = new GoLanguageSpecificChecks(new ProcessHelper(NullLogger<ProcessHelper>.Instance, Mock.Of<IRawOutputHelper>()), new NpxHelper(NullLogger<NpxHelper>.Instance, Mock.Of<IRawOutputHelper>()), gitHelper, NullLogger<GoLanguageSpecificChecks>.Instance);

            if (!await LangService.CheckDependencies(CancellationToken.None))
            {
                Assert.Ignore("golang tooling dependencies are not installed, can't run GoLanguageSpecificChecksTests");
            }

            var resp = await LangService.CreateEmptyPackage(GoPackageDir, "untitleddotloop", CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(GoPackageDir) && Directory.Exists(GoPackageDir))
            {
                try
                {
                    Directory.Delete(GoPackageDir, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to cleanup temp directory {0}: {1}", GoPackageDir, ex);
                }
            }
        }

        [Test]
        public async Task TestGoLanguageSpecificChecksBasic()
        {
            await File.WriteAllTextAsync(Path.Combine(GoPackageDir, "main.go"), """
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

            await Process.Start(new ProcessStartInfo() { FileName = GoProgram, ArgumentList = { "get", "github.com/Azure/azure-sdk-for-go/sdk/azidentity@v1.10.0" }, WorkingDirectory = GoPackageDir })!.WaitForExitAsync();

            var resp = await LangService.AnalyzeDependenciesAsync(GoPackageDir, false, CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));

            var identityLine = File.ReadAllLines(Path.Join(GoPackageDir, "go.mod")).Where(line => line.Contains("azidentity")).Select(line => line.Trim()).First();
            Assert.That(identityLine, Is.Not.EqualTo("github.com/Azure/azure-sdk-for-go/sdk/azidentity v1.10.0"));

            resp = await LangService.FormatCodeAsync(GoPackageDir, false, CancellationToken.None);
            Assert.That(File.ReadAllText(Path.Join(GoPackageDir, "main.go")), Does.Not.Contain("azservicebus"));

            resp = await LangService.BuildProjectAsync(GoPackageDir, CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));

            resp = await LangService.LintCodeAsync(GoPackageDir, false, CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public async Task TestGoLanguageSpecificChecksCompileErrors()
        {
            await File.WriteAllTextAsync(Path.Combine(GoPackageDir, "main.go"), """
                package main

                import (
                )

                func main() {
                    syntax error
                }
                """);

            var resp = await LangService.BuildProjectAsync(GoPackageDir, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(resp.ExitCode, Is.EqualTo(1));
                Assert.That(resp.CheckStatusDetails, Does.Contain("syntax error: unexpected name error at end of statement"));
            });
        }

        [Test]
        public async Task TestGoLanguageSpecificChecksLintErrors()
        {
            await File.WriteAllTextAsync(Path.Combine(GoPackageDir, "main.go"), """
                package main

                import (
                )

                func unusedFunc() {}

                func main() {
                }
                """);

            var resp = await LangService.LintCodeAsync(GoPackageDir, false, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(resp.ExitCode, Is.EqualTo(1));
                Assert.That(resp.CheckStatusDetails, Does.Contain("func `unusedFunc` is unused (unused)"));
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
    }
}
