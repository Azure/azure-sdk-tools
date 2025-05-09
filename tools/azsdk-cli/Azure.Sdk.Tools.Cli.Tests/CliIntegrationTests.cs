using System.CommandLine;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.HelloWorldTool;

namespace Azure.Sdk.Tools.Cli.Tests;

internal class CliIntegrationTests
{

    private Tuple<Command, TestLogger<T>> GetTestInstanceWithLogger<T>() where T : MCPTool
    {
        var testLogger = new TestLogger<T>();
        var responseService = new ResponseService(new PlainTextFormatter());

        var tool = (T)Activator.CreateInstance(
            typeof(T),
            args: [testLogger, responseService]
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

        var rawState = logger.Logs.Single();
        var expected = @"
Exit Code: 0
Message: RESPONDING TO HI. MY NAME IS
Result: null
Duration: 0ms
";
        Assert.That(rawState.ToString(), Is.EqualTo(expected.Trim()));
    }
}
