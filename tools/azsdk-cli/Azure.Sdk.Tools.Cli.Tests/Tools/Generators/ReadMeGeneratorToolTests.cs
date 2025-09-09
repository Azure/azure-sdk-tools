using System.CommandLine;
using System.CommandLine.Parsing;
using Moq;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Generators
{
    internal class ReadMeGeneratorToolTests
    {
        private ReadMeGeneratorTool tool;
        private TestOutputHelper outputHelper;
        private Mock<IMicroagentHostService>? mockMicroAgentService;

        [SetUp]
        public void Setup()
        {
            outputHelper = new TestOutputHelper();
            mockMicroAgentService = new Mock<IMicroagentHostService>();

            tool = new ReadMeGeneratorTool(
                new TestLogger<ReadMeGeneratorTool>(),
                outputHelper,
                mockMicroAgentService.Object
            );
        }

        [Test]
        public async Task TestReadmeGeneratorTool()
        {
            var readmeContents = "This is a test response for the readme generation.";
            mockMicroAgentService?.Setup(svc => svc.RunAgentToCompletion(
                It.IsAny<Microagent<ReadmeGenerator.ReadmeContents>>(), It.IsAny<CancellationToken>())
            ).Returns(() => Task.FromResult(new ReadmeGenerator.ReadmeContents(readmeContents)));

            (DirectoryInfo root, string packagePath) = await CreateFakeLanguageRepo();

            var readmeOutputPath = Path.GetTempFileName();
            var readmeTemplatePath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "README-template.go.md");

            try
            {
                var command = tool.GetCommand();

                int exitCode = command.Invoke($"--output-path \"{readmeOutputPath}\" --service-url \"https://learn.microsoft.com/azure/service-bus-messaging\" --template-path {readmeTemplatePath} --package-path {packagePath}");

                Assert.Multiple(() =>
                {
                    Assert.That(exitCode, Is.EqualTo(0), "Command should execute successfully");
                    Assert.That(outputHelper.Outputs.First().Method, Is.EqualTo("Output"));
                    Assert.That(outputHelper.Outputs.First().OutputValue, Is.EqualTo($"Readme written to {readmeOutputPath}"));
                });

                Assert.That(File.Exists(readmeOutputPath), Is.True, "Readme output file should be created");
            }
            finally
            {
                Directory.Delete(root.FullName, true);
            }
        }

        [Test]
        public void TestReadmeGeneratorToolLive()
        {
            var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

            if (endpoint == null)
            {
                Assert.Ignore("Skipping test as AZURE_OPENAI_ENDPOINT is not set");
            }

            var languageRepo = Environment.GetEnvironmentVariable("AZURE_SDK_FOR_GO_PATH");

            if (languageRepo == null)
            {
                Assert.Ignore("Skipping test as AZURE_SDK_FOR_GO_PATH is not set");
            }

            var command = tool.GetCommand();
            var readmeOutputPath = Path.GetTempFileName();
            var readmeTemplatePath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "README-template.go.md");

            int exitCode = command.Invoke($"--output-path \"{readmeOutputPath}\" --service-url \"https://learn.microsoft.com/azure/service-bus-messaging\" --template-path {readmeTemplatePath} --package-path {Path.Join(languageRepo, "sdk", "messaging", "azservicebus")}");
            Assert.That(exitCode, Is.EqualTo(0), "Command should execute successfully");

            Assert.That(File.Exists(readmeOutputPath), Is.True, "Readme output file should be created");

            Assert.Multiple(() =>
            {
                Assert.That(outputHelper.Outputs.First().Method, Is.EqualTo("Output"));
                Assert.That(outputHelper.Outputs.First().OutputValue, Is.EqualTo($"Readme written to {readmeOutputPath}"));
            });
        }

        /// <summary>
        /// These tests all correspond to "LLM needs to do more work" type of returns from the tool, like removing
        /// hardcoded locales, like 'en-us', or having it regenerate if some of the templates tokens make it through
        /// and weren't replaced.
        /// </summary>
        /// <returns></returns>
        [TestCase(
            "Bad link, still has locale in it: https://learn.microsoft.com/fr-fr/blahblah",
            "The readme contains links with locales. Keep the link, but remove these locales from links: (fr-fr).", TestName = "Links with locales in them")]
        [TestCase(
            "Still referencing aztemplate and (package path)",
            "The readme contains placeholders (aztemplate,(package path)) that should be removed and replaced with a proper package name")]
        [TestCase(
            "Still referencing placeholder (package path)",
            "The readme contains placeholders ((package path)) that should be removed and replaced with a proper package name")]
        public async Task TestBadReadmeContent(string readmeContent, string expectedFeedback)
        {
            mockMicroAgentService?.Setup(svc => svc.RunAgentToCompletion(
                It.IsAny<Microagent<ReadmeGenerator.ReadmeContents>>(), It.IsAny<CancellationToken>())
            ).Returns(() => Task.FromResult(new ReadmeGenerator.ReadmeContents(readmeContent)));

            (DirectoryInfo root, string packagePath) = await CreateFakeLanguageRepo();

            var readmeOutputPath = Path.GetTempFileName();
            var readmeTemplatePath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "README-template.go.md");

            try
            {
                var command = tool.GetCommand();

                int exitCode = command.Invoke($"--output-path \"{readmeOutputPath}\" --service-url \"https://learn.microsoft.com/azure/service-bus-messaging\" --template-path {readmeTemplatePath} --package-path {packagePath}");

                Assert.Multiple(() =>
                {
                    Assert.That(exitCode, Is.EqualTo(1), "Command should fail, as the final readme doesn't pass validation");
                    Assert.That(outputHelper.Outputs.First().Method, Is.EqualTo("OutputError"));
                    Assert.That(outputHelper.Outputs.First().OutputValue, Is.EqualTo($"ReadmeGenerator failed with validation errors: {expectedFeedback}"));
                });

                Assert.That(File.Exists(readmeOutputPath), Is.True, "Readme output file should be created");
            }
            finally
            {
                Directory.Delete(root.FullName, true);
            }
        }

        private static async Task<(DirectoryInfo root, string packagePath)> CreateFakeLanguageRepo()
        {
            var root = Directory.CreateTempSubdirectory("readme-generator-tool-test");
            var scriptsDir = Path.Combine(root.FullName, "eng", "common", "scripts");
            var packagePath = Path.Combine(root.FullName, "sdk", "messaging", "azservicebus");

            Directory.CreateDirectory(packagePath);
            Directory.CreateDirectory(scriptsDir);

            await File.WriteAllTextAsync(Path.Join(scriptsDir, "Verify-Links.ps1"), "Write-Host 'passed'");
            return (root, packagePath);
        }
    }
}
