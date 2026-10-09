// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers.EngSys;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.EngSys;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.EngSys;

[TestFixture]
public class PipelineFixEvaluatorToolTests
{
    [Test]
    public async Task EvaluatePipelineFixes_CallsHelperForEachDate()
    {
        var helper = new Mock<IPipelineFixEvaluatorHelper>();
        var until = new DateTimeOffset(2026, 8, 31, 8, 30, 0, TimeSpan.FromHours(-7));
        var windowEnd = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        helper
            .Setup(service => service.EvaluatePipelineFixesAsync(
                "Azure", "azure-sdk-for-python", It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var tool = new PipelineFixEvaluatorTool(helper.Object, new TestLogger<PipelineFixEvaluatorTool>());

        var response = await tool.EvaluatePipelineFixes("Azure", "azure-sdk-for-python", 3, until);

        Assert.Multiple(() =>
        {
            Assert.That(response.Owner, Is.EqualTo("Azure"));
            Assert.That(response.Repo, Is.EqualTo("azure-sdk-for-python"));
            Assert.That(response.Dates!.Select(date => date.Date), Is.EqualTo(new[]
            {
                new DateOnly(2026, 8, 31),
                new DateOnly(2026, 8, 30),
                new DateOnly(2026, 8, 29),
            }));
        });
        for (var offset = 0; offset < 3; offset++)
        {
            var dayUntil = windowEnd.AddDays(-offset);
            helper.Verify(service => service.EvaluatePipelineFixesAsync(
                "Azure",
                "azure-sdk-for-python",
                dayUntil.AddDays(-1),
                dayUntil,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Test]
    public async Task EvaluatePipelineFixes_InvalidDayCountDoesNotCallHelper()
    {
        var helper = new Mock<IPipelineFixEvaluatorHelper>();
        var tool = new PipelineFixEvaluatorTool(helper.Object, new TestLogger<PipelineFixEvaluatorTool>());

        var response = await tool.EvaluatePipelineFixes("Azure", "repo", 0);

        Assert.That(response.ResponseError, Does.Contain("greater than zero"));
        helper.VerifyNoOtherCalls();
    }
}
