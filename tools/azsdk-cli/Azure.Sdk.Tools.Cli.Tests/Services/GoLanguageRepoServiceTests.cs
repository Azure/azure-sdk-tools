using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    internal class GoLanguageRepoServiceTests
    {
        [Test]
        public async Task TestGoLanguageRepoService()
        {
            // Check if "go" is in the system PATH
            var goProgram = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "go.exe" : "go";
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
            bool found = paths.Any(p => File.Exists(Path.Combine(p, goProgram)));

            if (!found)
            {
                Assert.Ignore($"No go tooling in path, can't run Go language specific language tests");
            }

            // create a minimal go project in a temp directory
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var cwd = Directory.GetCurrentDirectory();

            try
            {
                // TODO: is this what we're expecting as a precondition?
                Directory.SetCurrentDirectory(tempDir);

                var langService = new GoLanguageRepoService(".");

                // Run 'go mod init' in the temp directory
                var result = await langService.CreateEmptyPackage("untitleddotloop");
                Assert.That(result.ExitCode, Is.EqualTo(0));

                result = await langService.AnalyzeDependenciesAsync();
                Assert.That(result.ExitCode, Is.EqualTo(0));

                // TODO: actually check that things are happening :)
            }
            finally
            {
                Directory.SetCurrentDirectory(cwd);
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to cleanup temp directory {0}: {1}", tempDir, ex);
                }
            }
        }
    }
}
