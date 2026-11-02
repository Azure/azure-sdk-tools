using System.Text.Json;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class ServiceApiReadinessToolTests
{
    [Test]
    public void ReadinessIsCliOnly()
    {
        Assert.That(typeof(ServiceApiReadinessTool).IsDefined(typeof(McpServerToolTypeAttribute), false), Is.False);
        Assert.That(typeof(ServiceApiReadinessTool).GetMethods().Any(method => method.IsDefined(typeof(McpServerToolAttribute), false)), Is.False);
    }

    [Test]
    public async Task CliForwardsOnlyPrIdentityAndCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var service = new Mock<IServiceApiReadinessService>();
        service.Setup(value => value.CheckAsync("Azure", "azure-sdk-for-java", 12, cancellation.Token))
            .ReturnsAsync(new ServiceApiReadinessResponse { ReadinessStatus = ServiceApiReadinessResponse.Passed });
        var tool = new ServiceApiReadinessTool(service.Object, NullLogger<ServiceApiReadinessTool>.Instance);
        var command = tool.GetCommandInstances().Single();
        var parsed = command.Parse(["--owner", "Azure", "--repo", "azure-sdk-for-java", "--pr", "12"]);
        var response = await tool.HandleCommand(parsed, cancellation.Token);
        Assert.That(parsed.Errors, Is.Empty);
        Assert.That(((ServiceApiReadinessResponse)response).ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(tool.CommandHierarchy, Is.EqualTo(new[] { SharedCommandGroups.Package }));
        Assert.That(SharedOptions.ToolsList, Does.Contain(typeof(ServiceApiReadinessTool)));
        service.VerifyAll();
    }

    [Test]
    public void CliDoesNotAcceptACallerSuppliedHeadSha()
    {
        var tool = new ServiceApiReadinessTool(Mock.Of<IServiceApiReadinessService>(), NullLogger<ServiceApiReadinessTool>.Instance);
        var command = tool.GetCommandInstances().Single();
        var parsed = command.Parse(["--owner", "Azure", "--repo", "azure-sdk-for-java", "--pr", "12", "--expected-head-sha", new string('a', 40)]);
        Assert.That(parsed.Errors, Is.Not.Empty);
    }

    [TestCase(ServiceApiReadinessResponse.Passed)]
    [TestCase(ServiceApiReadinessResponse.NotDetected)]
    [TestCase(ServiceApiReadinessResponse.Inconclusive)]
    [TestCase(ServiceApiReadinessResponse.Error)]
    public void JsonContractKeepsTheFourReadinessStatesDistinct(string state)
    {
        var response = new ServiceApiReadinessResponse { ReadinessStatus = state, ResponseError = state == ServiceApiReadinessResponse.Error ? "Safe failure" : null };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
        Assert.That(json.RootElement.GetProperty("contractVersion").GetInt32(), Is.EqualTo(1));
        Assert.That(json.RootElement.GetProperty("status").GetString(), Is.EqualTo(state));
        Assert.That(response.ExitCode, Is.EqualTo(state == ServiceApiReadinessResponse.Error ? 1 : 0));
    }

    [Test]
    public async Task UnexpectedServiceFailureIsSanitized()
    {
        var service = new Mock<IServiceApiReadinessService>();
        service.Setup(value => value.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret response"));
        var tool = new ServiceApiReadinessTool(service.Object, NullLogger<ServiceApiReadinessTool>.Instance);
        var response = await tool.CheckServiceApiVersion("Azure", "azure-sdk-for-java", 12);
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(JsonSerializer.Serialize(response), Does.Not.Contain("secret response"));
    }
}
