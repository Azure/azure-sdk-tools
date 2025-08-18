using System.ClientModel;
using System.ClientModel.Primitives;
using System.CommandLine;
using System.CommandLine.Parsing;
using Azure.AI.OpenAI;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.MockServices;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Generators
{
    internal class ReadMeGeneratorToolTests
    {
        [Test]
        public async Task TestReadmeGeneratorTool()
        {
            var testClients = SetupOpenAIMocks();
            (DirectoryInfo root, string packagePath) = await CreateFakeLanguageRepo();

            var readmeOutputPath = Path.GetTempFileName();
            var readmeTemplatePath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "README-template.go.md");

            try
            {
                var tool = ActivatorUtilities.CreateInstance<ReadMeGeneratorTool>(testClients.ServiceProvider);
                var command = tool.GetCommand();

                int exitCode = command.Invoke($"--output-path \"{readmeOutputPath}\" --service-url \"https://learn.microsoft.com/azure/service-bus-messaging\" --template-path {readmeTemplatePath} --package-path {packagePath}");

                Assert.Multiple(() =>
                {
                    Assert.That(exitCode, Is.EqualTo(0), "Command should execute successfully");
                    Assert.That(testClients.OutputService.Outputs.First().Method, Is.EqualTo("Output"));
                    Assert.That(testClients.OutputService.Outputs.First().OutputValue, Is.EqualTo($"Readme written to {readmeOutputPath}"));
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

            var (sp, outputServiceMock) = CreateServiceProvider();

            var tool = ActivatorUtilities.CreateInstance<ReadMeGeneratorTool>(sp);
            var command = tool.GetCommand();
            var readmeOutputPath = Path.GetTempFileName();
            var readmeTemplatePath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "README-template.go.md");

            int exitCode = command.Invoke($"--output-path \"{readmeOutputPath}\" --service-url \"https://learn.microsoft.com/azure/service-bus-messaging\" --template-path {readmeTemplatePath} --package-path {Path.Join(languageRepo, "sdk", "messaging", "azservicebus")}");
            Assert.That(exitCode, Is.EqualTo(0), "Command should execute successfully");

            Assert.That(File.Exists(readmeOutputPath), Is.True, "Readme output file should be created");

            Assert.Multiple(() =>
            {
                Assert.That(outputServiceMock.Outputs.First().Method, Is.EqualTo("Output"));
                Assert.That(outputServiceMock.Outputs.First().OutputValue, Is.EqualTo($"Readme written to {readmeOutputPath}"));
            });
        }

        /// <summary>
        /// Creates a service provider in the same way as the normal program. Can be used to instantiate real clients.
        /// </summary>
        /// <param name="customizeServices">Optional Action you can use to register your own client instances</param>
        /// <returns></returns>
        private static (ServiceProvider, MockOutputService) CreateServiceProvider(Action<ServiceCollection>? customizeServices = default)
        {
            var serviceCollection = new ServiceCollection();
            var outputService = new MockOutputService();

            
            serviceCollection.AddLogging();     // so our ILogger<T>'s will be available.

            
            ServiceRegistrations.RegisterCommonServices(serviceCollection);
            serviceCollection.AddSingleton<IOutputService>(outputService);

            customizeServices?.Invoke(serviceCollection);

            var sp = serviceCollection.BuildServiceProvider();
            return (sp, outputService);
        }
        
        private static TestClients SetupOpenAIMocks()
        {
            var (openAIClientMock, chatClientMock) = OpenAIMockHelper.Create("gpt-4.1");

            // basically - create a model using the appropriate OpenAI*ModelFactory
            // then return it, wrapped in a ClientResult. This is really similar to the online samples
            // except it's ClientResult (instead of Response).
            var chatCompletion = OpenAIChatModelFactory.ChatCompletion(
                content: new ChatMessageContent("This is a test response for the readme generation.")
            );

            chatClientMock
                .Setup(ccm => ccm.CompleteChatAsync(It.IsAny<ChatMessage[]>()))     // NOTE: I'm not checking the chat message input - I already know what I'm sending.
                .Returns(() =>
                {
                    return Task.FromResult(
                        ClientResult.FromValue(chatCompletion, Mock.Of<PipelineResponse>())
                    );
                });

            var (serviceProvider, outputService) = CreateServiceProvider((sc) =>
            {
                sc.AddLogging((lb) => lb.AddConsole());
                sc.AddSingleton(openAIClientMock.Object);

                // register the mocks too, if you want to grab them later.
                sc.AddSingleton(chatClientMock);
                sc.AddSingleton(openAIClientMock);
            });

            return new TestClients(openAIClientMock, chatClientMock, serviceProvider, outputService);
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

        private record TestClients(
            Mock<AzureOpenAIClient> OpenAIClient,
            Mock<ChatClient> ChatClient,
            ServiceProvider ServiceProvider,
            MockOutputService OutputService
        );
    }
}
