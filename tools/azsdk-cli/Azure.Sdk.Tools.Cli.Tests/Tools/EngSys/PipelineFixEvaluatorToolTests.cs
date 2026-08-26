// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers.EngSys;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.EngSys;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.EngSys;

[TestFixture]
public class PipelineFixEvaluatorToolTests
{
    [Test]
    public async Task EvaluatePipelineFixes_WithUntil_UsesExactUtcWindow()
    {
        var helper = new Mock<IPipelineFixEvaluatorHelper>();
        var until = new DateTimeOffset(2026, 8, 18, 8, 30, 0, TimeSpan.FromHours(-7));
        var expectedUntil = until.ToUniversalTime();
        var expectedSince = expectedUntil.AddDays(-1);
        helper
            .Setup(x => x.EvaluatePipelineFixesAsync(
                "Azure",
                "azure-sdk-for-python",
                expectedSince,
                expectedUntil,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var tool = new PipelineFixEvaluatorTool(
            helper.Object,
            new TestLogger<PipelineFixEvaluatorTool>());

        var response = await tool.EvaluatePipelineFixes(
            "Azure",
            "azure-sdk-for-python",
            1,
            until);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Since, Is.EqualTo(expectedSince));
        Assert.That(response.Until, Is.EqualTo(expectedUntil));
        Assert.That(response.Results, Is.Empty);
        helper.VerifyAll();
    }
}