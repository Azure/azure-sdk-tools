using System.CommandLine;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.HelloWorldTool;

namespace Azure.Sdk.Tools.Cli.Tests
{
    internal class CliIntegrationTests
    {

        private Tuple<Command, TestLogger<T>> GetTestInstanceWithLogger<T>() where T : MCPTool
        {
            var testLogger = new TestLogger<T>();

            var tool = (T)Activator.CreateInstance(
                typeof(T),
                args: new object[] { testLogger }
            )!;

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
            var dict = kvps.ToDictionary(k => k.Key, v => v.Value);
            var cmdRes = dict["result"] as CommandResponse;

            Assert.That($"RESPONDING TO {args[1]}", Is.EqualTo(cmdRes.Result));
        }
    }
}
