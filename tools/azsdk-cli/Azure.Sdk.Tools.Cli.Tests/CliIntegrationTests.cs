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

    [Test]
    public async Task TestSimpleFailCase()
    {
        var (cmd, logger) = GetTestInstanceWithLogger<HelloWorldTool>();

        var output = "";
        outputServiceMock
            .Setup(s => s.Output(It.IsAny<string>()))
            .Callback<string>(s => output = s);

        var exitCode = await cmd.InvokeAsync(["hello-world", "HI. MY NAME IS", "--fail"]);
        Assert.That(exitCode, Is.EqualTo(1));

        var expected = "[ERROR] RESPONDING TO 'HI. MY NAME IS' with FAIL: 1";

        outputServiceMock
            .Verify(s => s.Output(It.IsAny<string>()), Times.Once);

        var input = output.Replace("\r", "");

        Assert.That(output, Is.EqualTo(expected));
    }

    [Test]
    public async Task TestWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("This test is only applicable on Windows.");
        }

        var (cmd, logger) = GetTestInstanceWithLogger<HelloWorldTool>();

        var output = "";
        outputServiceMock
            .Setup(s => s.Output(It.IsAny<string>()))
            .Callback<string>(s => output = s);

        var exitCode = await cmd.InvokeAsync(["hello-world", "HI. MY NAME IS"]);
        Assert.That(exitCode, Is.EqualTo(0));

        var expected = @"
Message: RESPONDING TO 'HI. MY NAME IS' with SUCCESS: 0
Result: null
Duration: 1ms".TrimStart();

        outputServiceMock
            .Verify(s => s.Output(It.IsAny<string>()), Times.Once);

        Assert.That(output, Is.EqualTo(expected));
    }

    [Test]
    public async Task TestHelloWorldCLIOptions()
    {
        var (cmd, logger) = GetTestInstanceWithLogger<HelloWorldTool>();

        var output = "";
        outputServiceMock
            .Setup(s => s.Output(It.IsAny<string>()))
            .Callback<string>(s => output = s);

        var exitCode = await cmd.InvokeAsync(["hello-world", "HI. MY NAME IS"]);
        Assert.That(exitCode, Is.EqualTo(0));

        var expected = @"
Message: RESPONDING TO 'HI. MY NAME IS' with SUCCESS: 0
Result: null
Duration: 1ms".TrimStart();

        outputServiceMock
            .Verify(s => s.Output(It.IsAny<string>()), Times.Once);

        var input = output.Replace("\r", "");

        Assert.That(output, Is.EqualTo(expected));
    }

    [Test]
    public async Task TestHelloWorldCLIOptionsFail()
    {
        var (cmd, logger) = GetTestInstanceWithLogger<HelloWorldTool>();

        var output = "";
        outputServiceMock
            .Setup(s => s.Output(It.IsAny<string>()))
            .Callback<string>(s => output = s);

        var exitCode = await cmd.InvokeAsync(["hello-world", "HI. MY NAME IS", "--fail"]);
        Assert.That(exitCode, Is.EqualTo(1));

        var expected = "[ERROR] RESPONDING TO 'HI. MY NAME IS' with FAIL: 1";

        outputServiceMock
            .Verify(s => s.Output(It.IsAny<string>()), Times.Once);

        var input = output.Replace("\r", "");

        Assert.That(output, Is.EqualTo(expected));
    }
}
