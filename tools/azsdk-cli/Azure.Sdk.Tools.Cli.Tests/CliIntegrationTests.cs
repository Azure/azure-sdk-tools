using System.CommandLine;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.AzurePipelinesTool;
using Azure.Sdk.Tools.Cli.Tools.HelloWorldTool;
using Moq;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Tests
{
    internal class CliIntegrationTests
    {

        private Tuple<Command, TestLogger<T>> GetTestInstanceWithLogger<T>() where T : MCPTool
        {
            var testLogger = new TestLogger<T>();

            var tool = (T)Activator.CreateInstance(
                typeof(T),
                args: [testLogger]
            )!;
            // Nothing needs to be added here. The parameter 'services' is already optional by using 'object[] services = null'.
            // The assignment 'services ??= Array.Empty<object>();' ensures it defaults to an empty array if not provided.
            var tuple = new Tuple<Command, TestLogger<T>>(
                item1: tool.GetCommand(),
                item2: testLogger
            );
            return tuple;
        }

        private static readonly object[] HelloWorldArgs = new[]
        {
            new object[] { new[] { "hello-world", "HI. MY NAME IS" } },
        };
        [Test, TestCaseSource(nameof(HelloWorldArgs))]
        public async Task TestHelloWorldCLIOptions(string[] args)
        {
            var (cmd, logger) = GetTestInstanceWithLogger<HelloWorldTool>();

            var exitCode = await cmd.InvokeAsync(args);
            Assert.That(exitCode, Is.EqualTo(0));

            // todo: move this to some helper function so we can just get the CommandResponse from a specific log index
            var rawState = logger.Logs.Single();
            var kvps = rawState
                    as IReadOnlyCollection<KeyValuePair<string, object>>;

            Assert.That(kvps, Is.Not.Null);
            var dict = kvps.ToDictionary(k => k.Key, v => v.Value);
            var cmdRes = dict["result"] as CommandResponse;

            Assert.That(cmdRes, Is.Not.Null);

            Assert.That($"RESPONDING TO {args[1]}", Is.EqualTo(cmdRes.Result));
        }

        private static readonly object[] AzurePipelineToolArgs = new[]
        {
            new object[] { new[] { "azp", "analyze", "--project", "public", "--build-id", "4817839", "--log-id", "187" } },
            // new object[] { new[] { "azp", "analyze" } },
        };
        [Test, TestCaseSource(nameof(AzurePipelineToolArgs))]
        public async Task TestAzurePipelineCLIOptions(string[] args)
        {
            var azureService = new AzureService();
            var aiAgentServiceMock = new Mock<IAIAgentService>();
            aiAgentServiceMock.Setup(a =>
                a.QueryFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(("test response", new TokenUsage("gpt-test", 0, 0)));

            var logger = new TestLogger<AzurePipelinesTool>();
            var cmd = new AzurePipelinesTool(azureService, aiAgentServiceMock.Object, logger).GetCommand();

            var exitCode = await cmd.InvokeAsync(args);
            Assert.That(exitCode, Is.EqualTo(0));
        }
    }
}
