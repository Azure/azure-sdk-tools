// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Moq;
using NUnit.Framework.Internal;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Example;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Tests.Tools;

internal class ExampleToolTests
{
    private ExampleTool tool;
    private Mock<IAzureService>? mockAzureService;
    private Mock<IDevOpsService>? mockDevOpsService;
    private MockGitHubService? mockGitHubService;
    private Mock<IProcessHelper>? mockProcessHelper;
    private Mock<IPowershellHelper>? mockPowershellHelper;
    private Mock<Azure.Sdk.Tools.Cli.Microagents.IMicroagentHostService>? mockMicroagentHostService;

    [SetUp]
    public void Setup()
    {
        // Create mock services
        mockAzureService = new Mock<IAzureService>();
        mockDevOpsService = new Mock<IDevOpsService>();
        mockGitHubService = new MockGitHubService();
        mockProcessHelper = new Mock<IProcessHelper>();
        mockPowershellHelper = new Mock<IPowershellHelper>();
        mockMicroagentHostService = new Mock<Azure.Sdk.Tools.Cli.Microagents.IMicroagentHostService>();

        // Set up Azure service mock to return a mock credential
        var mockCredential = new Mock<TokenCredential>();
        mockCredential
            .Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Azure.Core.AccessToken("mock-token", DateTimeOffset.UtcNow.AddHours(1)));

        mockAzureService
            .Setup(x => x.GetCredential(It.IsAny<string?>()))
            .Returns(mockCredential.Object);

        // Set up DevOps service mock
        mockDevOpsService
            .Setup(x => x.GetPackageWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new PackageResponse { PipelineDefinitionUrl = "https://dev.azure.com/test-pipeline" });

        // Create the tool instance
        tool = new ExampleTool(
            new TestLogger<ExampleTool>(),
            mockAzureService.Object,
            mockDevOpsService.Object,
            mockGitHubService,
            mockMicroagentHostService.Object,
            mockProcessHelper.Object,
            mockPowershellHelper.Object,
            tokenUsageHelper: new TokenUsageHelper(new OutputHelper()),
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            null
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        );
    }

    [Test]
    public async Task DemonstrateAzureService_ReturnsSuccessResponse()
    {
        // Act
        var result = await tool.DemonstrateAzureService("test-tenant");

        // Assert
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ServiceName, Is.EqualTo("Azure Authentication"));
        Assert.That(result.Operation, Is.EqualTo("GetCredential"));
        Assert.That(result.Result, Does.Contain("Successfully obtained Azure credentials"));
        Assert.That(result.Details, Is.Not.Null);
        Assert.That(result.Details.ContainsKey("credential_type"), Is.True);
        Assert.That(result.Details.ContainsKey("token_expires"), Is.True);
        Assert.That(result.Details.ContainsKey("has_token"), Is.True);
    }

    [Test]
    public async Task DemonstrateDevOpsService_ReturnsCorrectResponse()
    {
        const string packageName = "test-package";
        const string language = "csharp";

        var result = await tool.DemonstrateDevOpsService(packageName, language);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ServiceName, Is.EqualTo("Azure DevOps"));
        Assert.That(result.Operation, Is.EqualTo("GetPackagePipelineUrl"));
        Assert.That(result.Result, Does.Contain("Found package pipeline"));
        Assert.That(result.Details, Is.Not.Null);
        Assert.That(result.Details["service_type"], Is.EqualTo("Azure DevOps"));
        Assert.That(result.Details["package_pipeline_url"], Is.EqualTo("https://dev.azure.com/test-pipeline"));

        // Verify the service was called with correct parameters
        mockDevOpsService!.Verify(x => x.GetPackageWorkItemAsync(packageName, language, It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task DemonstrateGitHubService_ReturnsUserDetails()
    {
        var result = await tool.DemonstrateGitHubService();

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ServiceName, Is.EqualTo("GitHub"));
        Assert.That(result.Operation, Is.EqualTo("GetUser"));
        Assert.That(result.Details, Is.Not.Null);
        Assert.That(result.Details.ContainsKey("user_login"), Is.True);
        Assert.That(result.Details.ContainsKey("user_id"), Is.True);
        Assert.That(result.Result, Does.Contain("Retrieved user details"));
    }

    [Test]
    public async Task DemonstrateErrorHandling_NoFailure_ReturnsSuccessResponse()
    {
        var result = await tool.DemonstrateErrorHandling("normal", false);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result.ToString(), Does.Contain("successfully"));
        Assert.That(result.Result.ToString(), Does.Contain("normal"));
    }

    [Test]
    public async Task DemonstrateErrorHandling_DifferentErrorTypes_ReturnsDifferentErrors()
    {
        var result = await tool.DemonstrateErrorHandling("argument", true);
        Assert.That(result.ResponseError, Is.Not.Null);
        Assert.That(result.ResponseError, Does.Contain("ArgumentException"));

        var timeoutResult = await tool.DemonstrateErrorHandling("timeout", true);
        Assert.That(timeoutResult.ResponseError, Is.Not.Null);
        Assert.That(timeoutResult.ResponseError, Does.Contain("TimeoutException"));

        var notFoundResult = await tool.DemonstrateErrorHandling("notfound", true);
        Assert.That(notFoundResult.ResponseError, Is.Not.Null);
        Assert.That(notFoundResult.ResponseError, Does.Contain("FileNotFoundException"));

        var genericResult = await tool.DemonstrateErrorHandling("other", true);
        Assert.That(genericResult.ResponseError, Is.Not.Null);
        Assert.That(genericResult.ResponseError, Does.Contain("InvalidOperationException"));
    }

    [Test]
    public void GetCommand_ReturnsCommandWithCorrectSubCommands()
    {
        var subCommandNames = tool.GetCommandInstances().Select(c => c.Name).ToList();
        Assert.That(subCommandNames, Does.Contain("azure"));
        Assert.That(subCommandNames, Does.Contain("devops"));
        Assert.That(subCommandNames, Does.Contain("github"));
        Assert.That(subCommandNames, Does.Contain("ai"));
        Assert.That(subCommandNames, Does.Contain("error"));
        Assert.That(subCommandNames, Does.Contain("process"));
        Assert.That(subCommandNames, Does.Contain("powershell"));
        Assert.That(subCommandNames, Does.Contain("microagent"));
    }

    [Test]
    public void ExampleServiceResponse_ToString_FormatsCorrectly()
    {
        var response = new ExampleServiceResponse
        {
            ServiceName = "Test Service",
            Operation = "Test Operation",
            Result = "Test Result",
            Details = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            }
        };

        var result = response.ToString();

        Assert.That(result, Does.Contain("Service: Test Service"));
        Assert.That(result, Does.Contain("Operation: Test Operation"));
        Assert.That(result, Does.Contain("Result: Test Result"));
        Assert.That(result, Does.Contain("Details:"));
        Assert.That(result, Does.Contain("key1: value1"));
        Assert.That(result, Does.Contain("key2: value2"));
    }

    [Test]
    public async Task DemonstrateAzureService_WithNullTenant_WorksCorrectly()
    {
        var result = await tool.DemonstrateAzureService(null);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ServiceName, Is.EqualTo("Azure Authentication"));

        mockAzureService!.Verify(x => x.GetCredential(null), Times.Once);
    }

    [Test]
    public async Task DemonstrateProcessExecution_Success()
    {
        mockProcessHelper!.Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, });

        var result = await tool.DemonstrateProcessExecution("5");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ServiceName, Is.EqualTo("Process"));
        Assert.That(result.Operation, Is.EqualTo("RunSleep"));
        Assert.That(result.Result, Is.Empty);
        Assert.That(result.Details?["exit_code"], Is.EqualTo("0"));
    }

    [Test]
    public async Task DemonstrateProcessExecution_Failure()
    {
        var mockResult = new ProcessResult { ExitCode = 1 };
        mockResult.AppendStderr("Process failed");
        mockProcessHelper!.Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult);

        var result = await tool.DemonstrateProcessExecution("failcase");

        Assert.That(result.ResponseErrors, Is.Not.Empty);
        Assert.That(result.ResponseErrors[0], Does.Contain("Sleep example failed"));
        Assert.That(result.ResponseErrors[1], Does.Contain("Process failed"));
    }

    [Test]
    public async Task DemonstratePowershellExecution_Success()
    {
        mockPowershellHelper!.Setup(p => p.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, });
        var result = await tool.DemonstratePowershellExecution("foobar");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ServiceName, Is.EqualTo("PowerShell"));
        Assert.That(result.Operation, Is.EqualTo("RunTempScript"));
        Assert.That(result.Result, Is.Empty);
        Assert.That(result.Details?["exit_code"], Is.EqualTo("0"));
    }

    [Test]
    public async Task DemonstrateMicroagentFibonacci_Success()
    {
        mockMicroagentHostService!.Setup(m => m.RunAgentToCompletion(It.IsAny<Azure.Sdk.Tools.Cli.Microagents.Microagent<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(13);

        var response = await tool.DemonstrateMicroagentFibonacci(7);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Result as string, Does.Contain("Fibonacci(7) = 13"));
    }
}
