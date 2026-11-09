using System.Text.Json;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class ServiceApiReadinessToolTests
{
    [Test]
    public void ReadinessHasOneReadOnlyMcpToolWithAlternativePackageSources()
    {
        var tool = CreateTool();
        var methods = typeof(ServiceApiReadinessTool).GetMethods()
            .Where(method => method.IsDefined(typeof(McpServerToolAttribute), false)).ToArray();
        Assert.That(typeof(ServiceApiReadinessTool).IsDefined(typeof(McpServerToolTypeAttribute), false), Is.True);
        Assert.That(methods, Has.Length.EqualTo(1));
        Assert.That(methods.Single().Name, Is.EqualTo(nameof(ServiceApiReadinessTool.CheckServiceApiVersion)));

        var descriptor = McpServerTool.Create(methods.Single(), _ => tool).ProtocolTool;
        Assert.That(descriptor.Name, Is.EqualTo("azsdk_check_service_api_version"));
        Assert.That(descriptor.Annotations?.ReadOnlyHint, Is.True);
        Assert.That(descriptor.InputSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name),
            Is.EquivalentTo(new[] { "pullRequestUrl", "packagePath" }));
        Assert.That(descriptor.InputSchema.TryGetProperty("required", out var required) ? required.GetArrayLength() : 0, Is.Zero);
    }

    [TestCase(ServiceApiReadinessResponse.Passed)]
    [TestCase(ServiceApiReadinessResponse.NotDetected)]
    [TestCase(ServiceApiReadinessResponse.Inconclusive)]
    [TestCase(ServiceApiReadinessResponse.Error)]
    public async Task McpDispatchReturnsTheSameCapturedHeadAndReadinessContract(string state)
    {
        var head = new string('a', 40);
        var service = new Mock<IServiceApiReadinessService>(MockBehavior.Strict);
        service.Setup(value => value.CheckAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceApiReadinessResponse { ReadinessStatus = state, HeadSha = head });
        var tool = CreateTool(service.Object);
        var method = typeof(ServiceApiReadinessTool).GetMethod(nameof(ServiceApiReadinessTool.CheckServiceApiVersion))!;
        var server = new Mock<McpServer>();
        using var services = new ServiceCollection().BuildServiceProvider();
        server.SetupGet(value => value.Services).Returns(services);
        var context = new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = "tools/call" })
        {
            Params = new()
            {
                Name = "azsdk_check_service_api_version",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["pullRequestUrl"] = JsonSerializer.SerializeToElement("https://github.com/Azure/azure-sdk-for-java/pull/12")
                }
            }
        };

        var call = await McpServerTool.Create(method, _ => tool).InvokeAsync(context);
        Assert.That(call.IsError, Is.Not.True);
        using var json = JsonDocument.Parse(call.Content.OfType<TextContentBlock>().Single().Text);
        Assert.That(json.RootElement.GetProperty("contractVersion").GetInt32(), Is.EqualTo(1));
        Assert.That(json.RootElement.GetProperty("status").GetString(), Is.EqualTo(state));
        Assert.That(json.RootElement.GetProperty("headSha").GetString(), Is.EqualTo(head));
        service.Verify(value => value.CheckAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("https://github.com/Azure/azure-sdk-for-java/pull/12", "Azure", "azure-sdk-for-java")]
    [TestCase("https://github.com/helen229/azure-sdk-for-java/pull/12", "helen229", "azure-sdk-for-java")]
    [TestCase("https://github.com/Azure/azure-sdk-for-js/pull/12/", "Azure", "azure-sdk-for-js")]
    public async Task CliForwardsOnlyPrIdentityAndCancellation(string url, string owner, string repository)
    {
        using var cancellation = new CancellationTokenSource();
        var service = new Mock<IServiceApiReadinessService>();
        service.Setup(value => value.CheckAsync(owner, repository, 12, cancellation.Token))
            .ReturnsAsync(new ServiceApiReadinessResponse { ReadinessStatus = ServiceApiReadinessResponse.Passed });
        var tool = CreateTool(service.Object);
        var command = tool.GetCommandInstances().Single();
        var parsed = command.Parse(["--pull-request-url", url]);
        var response = await tool.HandleCommand(parsed, cancellation.Token);
        Assert.That(parsed.Errors, Is.Empty);
        Assert.That(((ServiceApiReadinessResponse)response).ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(tool.CommandHierarchy, Is.EqualTo(new[] { SharedCommandGroups.Package }));
        Assert.That(SharedOptions.ToolsList, Does.Contain(typeof(ServiceApiReadinessTool)));
        Assert.That(command.Description, Does.Contain("API version used by an SDK pull request").And.Not.Contains("writing"));
        service.VerifyAll();
    }

    [Test]
    public void CliDoesNotAcceptACallerSuppliedHeadSha()
    {
        var tool = CreateTool();
        var command = tool.GetCommandInstances().Single();
        var parsed = command.Parse(["--pull-request-url", "https://github.com/Azure/azure-sdk-for-java/pull/12", "--expected-head-sha", new string('a', 40)]);
        Assert.That(parsed.Errors, Is.Not.Empty);
    }

    [TestCase("https://example.org/Azure/azure-sdk-for-java/pull/12")]
    [TestCase("https://github.com/Azure/azure-sdk-for-java/issues/12")]
    [TestCase("https://github.com/Azure/azure-sdk-for-java/pull/0")]
    public async Task InvalidPullRequestUrlReturnsGuidanceWithoutCallingTheService(string url)
    {
        var service = new Mock<IServiceApiReadinessService>(MockBehavior.Strict);
        var tool = CreateTool(service.Object);
        var response = await tool.CheckServiceApiVersion(url);
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(response.Detail, Does.Contain("exactly one GitHub SDK pull request URL"));
        service.VerifyNoOtherCalls();
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
        var tool = CreateTool(service.Object);
        var response = await tool.CheckServiceApiVersion("https://github.com/Azure/azure-sdk-for-java/pull/12");
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(JsonSerializer.Serialize(response), Does.Not.Contain("secret response"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LocalPackagePathUsesTheExistingLanguagePackageInfo(bool useMcp)
    {
        using var directory = TempDirectory.Create("LocalReadinessTool");
        var package = new PackageInfo { PackagePath = directory.DirectoryPath, Language = SdkLanguage.JavaScript };
        var git = new Mock<IGitHelper>();
        git.Setup(helper => helper.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-js");
        var language = new Mock<LanguageService>(MockBehavior.Strict);
        language.SetupGet(service => service.Language).Returns(SdkLanguage.JavaScript);
        language.Setup(service => service.GetPackageInfo(directory.DirectoryPath, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        var readiness = new Mock<IServiceApiReadinessService>(MockBehavior.Strict);
        readiness.Setup(service => service.CheckPackageAsync(package, It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceApiReadinessResponse { InputKind = "package" });
        var tool = new ServiceApiReadinessTool(readiness.Object, NullLogger<ServiceApiReadinessTool>.Instance, git.Object, [language.Object]);
        if (useMcp)
        {
            var server = new Mock<McpServer>();
            using var services = new ServiceCollection().BuildServiceProvider();
            server.SetupGet(value => value.Services).Returns(services);
            var context = new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = "tools/call" })
            {
                Params = new()
                {
                    Name = "azsdk_check_service_api_version",
                    Arguments = new Dictionary<string, JsonElement> { ["packagePath"] = JsonSerializer.SerializeToElement(directory.DirectoryPath) }
                }
            };
            var method = typeof(ServiceApiReadinessTool).GetMethod(nameof(ServiceApiReadinessTool.CheckServiceApiVersion))!;
            var call = await McpServerTool.Create(method, _ => tool).InvokeAsync(context);
            Assert.That(call.IsError, Is.Not.True);
            using var json = JsonDocument.Parse(call.Content.OfType<TextContentBlock>().Single().Text);
            Assert.That(json.RootElement.GetProperty("inputKind").GetString(), Is.EqualTo("package"));
        }
        else
        {
            var parsed = tool.GetCommandInstances().Single().Parse(["--package-path", directory.DirectoryPath]);
            var response = (ServiceApiReadinessResponse)await tool.HandleCommand(parsed, CancellationToken.None);
            Assert.That(parsed.Errors, Is.Empty);
            Assert.That(response.InputKind, Is.EqualTo("package"));
        }
        readiness.VerifyAll();
    }

    [TestCase(null, null)]
    [TestCase("https://github.com/Azure/azure-sdk-for-java/pull/12", "some-package")]
    [TestCase(null, "")]
    public async Task AmbiguousOrAbsentPackageSourcesAreRejected(string? url, string? path)
    {
        var readiness = new Mock<IServiceApiReadinessService>(MockBehavior.Strict);
        var response = await CreateTool(readiness.Object).CheckServiceApiVersion(url, path);
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(response.Detail, Does.Contain("exactly one"));
        readiness.VerifyNoOtherCalls();
    }

    private static ServiceApiReadinessTool CreateTool(IServiceApiReadinessService? service = null) =>
        new(service ?? Mock.Of<IServiceApiReadinessService>(), NullLogger<ServiceApiReadinessTool>.Instance, Mock.Of<IGitHelper>(), []);
}
