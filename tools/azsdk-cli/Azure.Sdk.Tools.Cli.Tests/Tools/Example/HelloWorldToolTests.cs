// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if DEBUG
using System.CommandLine;
using Moq;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Example;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Example;

internal class HelloWorldToolTests
{
    [Test]
    public async Task TestHelloWorldCLIOptions()
    {
        OutputHelper outputHelper = new(OutputHelper.OutputModes.Hidden);
        var tool = new HelloWorldTool(new TestLogger<HelloWorldTool>());
        tool.Initialize(outputHelper, new Mock<ITelemetryService>().Object, new MockUpgradeService());
        var cmd = tool.GetCommandInstances().First();

        var parseResult = cmd.Parse(["hello-world", "HI. MY NAME IS"]);
        var exitCode = await parseResult.InvokeAsync();
        Assert.That(exitCode, Is.EqualTo(0));

        var expected = @"
RESPONDING TO 'HI. MY NAME IS' with SUCCESS: 0
Duration: 1ms
".TrimStart();

        Assert.That(outputHelper.Outputs.Count(), Is.EqualTo(1));
        Assert.That(outputHelper.Outputs.First().Stream, Is.EqualTo(OutputHelper.StreamType.Stdout));
        Assert.That(outputHelper.Outputs.Last().Output, Is.EqualTo(expected));
    }

    [Test]
    public async Task TestHelloWorldCLIOptionsFail()
    {
        OutputHelper outputHelper = new(OutputHelper.OutputModes.Hidden);
        var tool = new HelloWorldTool(new TestLogger<HelloWorldTool>());
        tool.Initialize(outputHelper, new Mock<ITelemetryService>().Object, new MockUpgradeService());
        var cmd = tool.GetCommandInstances().First();

        var parseResult = cmd.Parse(["hello-world", "HI. MY NAME IS", "--fail"]);
        var exitCode = await parseResult.InvokeAsync();
        Assert.That(exitCode, Is.EqualTo(1));

        var expected = "[ERROR] RESPONDING TO 'HI. MY NAME IS' with FAIL: 1";

        Assert.That(outputHelper.Outputs.Count(), Is.EqualTo(1));
        Assert.That(outputHelper.Outputs.First().Stream, Is.EqualTo(OutputHelper.StreamType.Stderr));
        Assert.That(outputHelper.Outputs.Last().Output, Is.EqualTo(expected));
    }
}

#endif
