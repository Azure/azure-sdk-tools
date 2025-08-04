using Moq;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

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
}
