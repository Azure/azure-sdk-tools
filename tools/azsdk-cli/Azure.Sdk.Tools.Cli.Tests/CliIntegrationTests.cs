using Moq;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.HelloWorldTool;

namespace Azure.Sdk.Tools.Cli.Tests;

internal class CliIntegrationTests
{
    private Mock<OutputService> outputServiceMock = new(MockBehavior.Strict);

    private Tuple<Command, TestLogger<T>> GetTestInstanceWithLogger<T>() where T : MCPTool
    {
        var testLogger = new TestLogger<T>();
        outputServiceMock = new(OutputModes.Plain) { CallBase = true };
        outputServiceMock.Setup(s => s.Output(It.IsAny<string>())).Verifiable();

        var tool = (T)Activator.CreateInstance(
            typeof(T),
            args: [testLogger, outputServiceMock.Object]
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
        new object[] { new[] { "hello-world", "HI. MY NAME IS" } },
    };
    [Test, TestCaseSource(nameof(HelloWorldArgs))]
    public async Task TestHelloWorldCLIOptions(string[] args)
    {
        var (cmd, logger) = GetTestInstanceWithLogger<HelloWorldTool>();

        var output = "";
        outputServiceMock
            .Setup(s => s.Output(It.IsAny<string>()))
            .Callback<string>(s => output = s);

        var exitCode = await cmd.InvokeAsync(args);
        Assert.That(exitCode, Is.EqualTo(0));

        var expected = @"
Message: RESPONDING TO 'HI. MY NAME IS' with SUCCESS: 0
Result: null
Duration: 1ms".TrimStart();

        outputServiceMock
            .Verify(s => s.Output(It.IsAny<string>()), Times.Once);

        Assert.That(output, Is.EqualTo(expected));
    }
}
