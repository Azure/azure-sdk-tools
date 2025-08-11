using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Sdk.Tools.Cli.Helpers;
using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Services;
using System.Threading.Tasks; // added for async SetUp
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    internal class GoLanguageRepoServiceTests
    {
        private string GoPackageDir { get; set; }
        private string GoProgram => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "go.exe" : "go";

        private GoLanguageRepoService LangService { get; set; }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
            bool found = paths.Any(p => File.Exists(Path.Combine(p, GoProgram)));
            if (!found)
            {
                Assert.Ignore("No go tooling in path, can't run Go language specific language tests");
            }
        }

        [SetUp]
        public async Task SetUp()
        {
            GoPackageDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(GoPackageDir);

            var mockGitHubService = new Mock<IGitHubService>();
            var gitHelper = new GitHelper(mockGitHubService.Object, NullLogger<GitHelper>.Instance);
            LangService = new GoLanguageRepoService(new ProcessHelper(NullLogger<ProcessHelper>.Instance), gitHelper, NullLogger<GoLanguageRepoService>.Instance);

            var resp = await LangService.CreateEmptyPackage(GoPackageDir, "untitleddotloop");
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
        public async Task TestGoLanguageRepoServiceBasic()
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

            var resp = await LangService.AnalyzeDependenciesAsync(GoPackageDir);
            Assert.That(resp.ExitCode, Is.EqualTo(0));

            var identityLine = File.ReadAllLines(Path.Join(GoPackageDir, "go.mod")).Where(line => line.Contains("azidentity")).Select(line => line.Trim()).First();
            Assert.That(identityLine, Is.Not.EqualTo("github.com/Azure/azure-sdk-for-go/sdk/azidentity v1.10.0"));

            resp = await LangService.FormatCodeAsync(GoPackageDir);
            Assert.That(File.ReadAllText(Path.Join(GoPackageDir, "main.go")), Does.Not.Contain("azservicebus"));

            resp = await LangService.BuildProjectAsync(GoPackageDir);
            Assert.That(resp.ExitCode, Is.EqualTo(0));

            resp = await LangService.LintCodeAsync(GoPackageDir);
            Assert.That(resp.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public async Task TestGoLanguageRepoServiceCompileErrors()
        {
            await File.WriteAllTextAsync(Path.Combine(GoPackageDir, "main.go"), """
                package main

                import (
                )                

                func main() {
                    syntax error
                }
                """);

            var resp = await LangService.BuildProjectAsync(GoPackageDir);
            Assert.Multiple(() =>
            {
                Assert.That(resp.ExitCode, Is.EqualTo(1));
                Console.WriteLine($"Output = {resp.Output}");
                Assert.That(resp.Output, Does.Contain("syntax error: unexpected name error at end of statement"));
            });
        }

        [Test]
        public async Task TestGoLanguageRepoServiceLintErrors()
        {
            await File.WriteAllTextAsync(Path.Combine(GoPackageDir, "main.go"), """
                package main

                import (
                )                

                func unusedFunc() {}

                func main() {                    
                }
                """);

            var resp = await LangService.LintCodeAsync(GoPackageDir);
            Assert.Multiple(() =>
            {
                Assert.That(resp.ExitCode, Is.EqualTo(1));
                Assert.That(resp.Output, Does.Contain("func `unusedFunc` is unused (unused)"));
            });
        }
    }
}
