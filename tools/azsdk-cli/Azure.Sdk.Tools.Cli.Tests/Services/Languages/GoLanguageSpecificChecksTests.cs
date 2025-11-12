using System.Diagnostics;
using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    internal class GoLanguageSpecificChecksTests
    {
        private TempDirectory _tempDir = null!;
        private static string GoProgram => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "go.exe" : "go";
        private GoLanguageService LangService { get; set; } = null!;

        [SetUp]
        public async Task SetUp()
        {
            _tempDir = TempDirectory.Create("golang_checks");
            var mockGitHubService = new Mock<IGitHubService>();
            var gitHelper = new GitHelper(mockGitHubService.Object, NullLogger<GitHelper>.Instance);
            LangService = new GoLanguageService(new ProcessHelper(NullLogger<ProcessHelper>.Instance, Mock.Of<IRawOutputHelper>()), new NpxHelper(NullLogger<NpxHelper>.Instance, Mock.Of<IRawOutputHelper>()), gitHelper, NullLogger<GoLanguageService>.Instance, Mock.Of<ICommonValidationHelpers>());

            if (!await LangService.CheckDependencies(CancellationToken.None))
            {
                Assert.Ignore("golang tooling dependencies are not installed, can't run GoLanguageSpecificChecksTests");
            }

            var resp = await LangService.CreateEmptyPackage(_tempDir.DirectoryPath, "untitleddotloop", CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));
        }

        [TearDown]
        public void TearDown()
        {
            _tempDir.Dispose();
        }

        [Test]
        public async Task TestGoLanguageSpecificChecksBasic()
        {
            await File.WriteAllTextAsync(Path.Combine(_tempDir.DirectoryPath, "main.go"), """
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
                WorkingDirectory = _tempDir.DirectoryPath
            })!.WaitForExitAsync();

            var resp = await LangService.AnalyzeDependencies(_tempDir.DirectoryPath, false, CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));

            var identityLine = File.ReadAllLines(Path.Join(_tempDir.DirectoryPath, "go.mod"))
                .Where(line => line.Contains("azidentity"))
                .Select(line => line.Trim())
                .First();
            Assert.That(identityLine, Is.Not.EqualTo("github.com/Azure/azure-sdk-for-go/sdk/azidentity v1.10.0"));

            resp = await LangService.FormatCode(_tempDir.DirectoryPath, false, CancellationToken.None);
            Assert.That(File.ReadAllText(Path.Join(_tempDir.DirectoryPath, "main.go")), Does.Not.Contain("azservicebus"));

            resp = await LangService.BuildProject(_tempDir.DirectoryPath, CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));

            resp = await LangService.LintCode(_tempDir.DirectoryPath, false, CancellationToken.None);
            Assert.That(resp.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public async Task TestGoLanguageSpecificChecksCompileErrors()
        {
            await File.WriteAllTextAsync(Path.Combine(_tempDir.DirectoryPath, "main.go"), """
                package main

                import (
                )

                func main() {
                    syntax error
                }
                """);

            var resp = await LangService.BuildProject(_tempDir.DirectoryPath, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(resp.ExitCode, Is.EqualTo(1));
                Assert.That(resp.CheckStatusDetails, Does.Contain("syntax error: unexpected name error at end of statement"));
            });
        }

        [Test]
        public async Task TestGoLanguageSpecificChecksLintErrors()
        {
            await File.WriteAllTextAsync(Path.Combine(_tempDir.DirectoryPath, "main.go"), """
                package main

                import (
                )

                func unusedFunc() {}

                func main() {
                }
                """);

            var resp = await LangService.LintCode(_tempDir.DirectoryPath, false, CancellationToken.None);
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
