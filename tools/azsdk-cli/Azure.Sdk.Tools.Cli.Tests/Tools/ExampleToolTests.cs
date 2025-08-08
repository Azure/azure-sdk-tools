// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.MockServices;
using Azure.Sdk.Tools.Cli.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Tools;

[TestFixture]
internal class ExampleToolTests
{
    private ExampleTool? tool;
    private Mock<ILogger<ExampleTool>>? mockLogger;
    private Mock<IOutputService>? mockOutput;
    private Mock<IAzureService>? mockAzureService;
    private Mock<IDevOpsService>? mockDevOpsService;
    private MockGitHubService? mockGitHubService;
    private Mock<IAzureAgentServiceFactory>? mockAgentServiceFactory;
    private Mock<AzureOpenAIClient>? mockOpenAIClient;

    [SetUp]
    public void Setup()
    {
        // Create mock services
        mockLogger = new Mock<ILogger<ExampleTool>>();
        mockOutput = new Mock<IOutputService>();
        mockAzureService = new Mock<IAzureService>();
        mockDevOpsService = new Mock<IDevOpsService>();
        mockGitHubService = new MockGitHubService();
        mockAgentServiceFactory = new Mock<IAzureAgentServiceFactory>();

        // Create a mock for AzureOpenAIClient
        mockOpenAIClient = new Mock<AzureOpenAIClient>();

        // Set up Azure service mock to return a mock credential
        var mockCredential = new Mock<TokenCredential>();
        mockCredential
            .Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Azure.Core.AccessToken("mock-token", DateTimeOffset.UtcNow.AddHours(1)));

        mockAzureService
            .Setup(x => x.GetCredential(It.IsAny<string?>()))
            .Returns(mockCredential.Object);

        // Create the tool instance
        tool = new ExampleTool(
            mockLogger.Object,
            mockOutput.Object,
            mockAzureService.Object,
            mockDevOpsService.Object,
            mockGitHubService,
            mockAgentServiceFactory.Object,
            mockOpenAIClient.Object
        );
    }

    [Test]
    public async Task DemonstrateAzureService_ReturnsSuccessResponse()
    {
        // Act
        var result = await tool!.DemonstrateAzureService("test-tenant", false);

        // Assert
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ServiceName, Is.EqualTo("Azure Authentication"));
        Assert.That(result.Operation, Is.EqualTo("GetCredential"));
        Assert.That(result.Result, Contains.Substring("Successfully obtained Azure credentials"));
        Assert.That(result.Details, Is.Not.Null);
        Assert.That(result.Details!.ContainsKey("credential_type"), Is.True);
        Assert.That(result.Details.ContainsKey("token_expires"), Is.True);
        Assert.That(result.Details.ContainsKey("has_token"), Is.True);
    }

    [Test]
    public async Task DemonstrateAzureService_WithVerbose_LogsInformation()
    {
        // Act
        await tool!.DemonstrateAzureService("test-tenant", true);

        // Assert - Verify that info logs were called (you'd need to set up the mock to verify this)
        mockLogger!.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting Azure service demonstration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task DemonstrateDevOpsService_WithWorkItemId_ReturnsCorrectResponse()
    {
        // Arrange
        const int workItemId = 12345;
        const string projectInfo = "test-project";

        // Act
        var result = await tool!.DemonstrateDevOpsService(projectInfo, workItemId, false);

        // Assert
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ServiceName, Is.EqualTo("Azure DevOps"));
        Assert.That(result.Operation, Is.EqualTo("GetReleasePlan"));
        Assert.That(result.Result, Contains.Substring(projectInfo));
        Assert.That(result.Details, Is.Not.Null);
        Assert.That(result.Details!["work_item_id"], Is.EqualTo(workItemId.ToString()));
    }

    [Test]
    public async Task DemonstrateDevOpsService_WithoutWorkItemId_ReturnsListProjectsOperation()
    {
        // Arrange
        const string projectInfo = "test-project";

        // Act
        var result = await tool!.DemonstrateDevOpsService(projectInfo, null, false);

        // Assert
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ServiceName, Is.EqualTo("Azure DevOps"));
        Assert.That(result.Operation, Is.EqualTo("ListProjects"));
        Assert.That(result.Details!["simulated_operation"], Is.EqualTo("ListProjects"));
    }

    [Test]
    public async Task DemonstrateGitHubService_UserOperation_ReturnsUserDetails()
    {
        // Act
        var result = await tool!.DemonstrateGitHubService("user", null, null, false);

        // Assert
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ServiceName, Is.EqualTo("GitHub"));
        Assert.That(result.Operation, Is.EqualTo("user"));
        Assert.That(result.Details, Is.Not.Null);
        Assert.That(result.Details!.ContainsKey("user_login"), Is.True);
        Assert.That(result.Details.ContainsKey("user_id"), Is.True);
    }

    [Test]
    public async Task DemonstrateGitHubService_PullRequestOperation_ReturnsPRDetails()
    {
        // Act
        var result = await tool!.DemonstrateGitHubService("pullrequest", "testowner/testrepo", 123, false);

        // Assert
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ServiceName, Is.EqualTo("GitHub"));
        Assert.That(result.Operation, Is.EqualTo("pullrequest"));
        Assert.That(result.Details, Is.Not.Null);
        Assert.That(result.Details!.ContainsKey("pr_title"), Is.True);
        Assert.That(result.Details.ContainsKey("pr_state"), Is.True);
    }

    [Test]
    public async Task DemonstrateGitHubService_InvalidRepository_ThrowsArgumentException()
    {
        // Act & Assert
        var result = await tool!.DemonstrateGitHubService("pullrequest", "invalid-repo", 123, false);

        Assert.That(result.ResponseError, Is.Not.Null);
        Assert.That(result.ResponseError, Contains.Substring("Repository must be in format 'owner/repo'"));
    }

    [Test]
    public async Task DemonstrateErrorHandling_ForceFailure_ReturnsErrorResponse()
    {
        // Act
        var result = await tool!.DemonstrateErrorHandling("argument", true);

        // Assert
        Assert.That(result.ResponseError, Is.Not.Null);
        Assert.That(result.ResponseError, Contains.Substring("ArgumentException"));
        Assert.That(result.ResponseError, Contains.Substring("Simulated argument validation error"));
    }

    [Test]
    public async Task DemonstrateErrorHandling_NoFailure_ReturnsSuccessResponse()
    {
        // Act
        var result = await tool!.DemonstrateErrorHandling("normal", false);

        // Assert
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result, Contains.Substring("successfully"));
    }

    [Test]
    public void GetCommand_ReturnsCommandWithCorrectSubCommands()
    {
        // Act
        var command = tool!.GetCommand();

        // Assert
        Assert.That(command.Name, Is.EqualTo("demo"));
        Assert.That(command.Description, Contains.Substring("Comprehensive demonstration"));
        Assert.That(command.Subcommands.Count, Is.EqualTo(5));

        var subCommandNames = command.Subcommands.Select(sc => sc.Name).ToList();
        Assert.That(subCommandNames, Contains.Item("azure"));
        Assert.That(subCommandNames, Contains.Item("devops"));
        Assert.That(subCommandNames, Contains.Item("github"));
        Assert.That(subCommandNames, Contains.Item("ai"));
        Assert.That(subCommandNames, Contains.Item("error"));
    }

    [Test]
    public void ExampleServiceResponse_ToString_FormatsCorrectly()
    {
        // Arrange
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

        // Act
        var result = response.ToString();

        // Assert
        Assert.That(result, Contains.Substring("Service: Test Service"));
        Assert.That(result, Contains.Substring("Operation: Test Operation"));
        Assert.That(result, Contains.Substring("Result: Test Result"));
        Assert.That(result, Contains.Substring("Details:"));
        Assert.That(result, Contains.Substring("key1: value1"));
        Assert.That(result, Contains.Substring("key2: value2"));
    }

    [Test]
    public void ExampleAIResponse_ToString_FormatsCorrectly()
    {
        // Arrange
        var response = new ExampleAIResponse
        {
            Prompt = "Test prompt",
            ResponseText = "Test response",
            Model = "gpt-4",
            TokenUsage = new Dictionary<string, int>
            {
                ["prompt_tokens"] = 10,
                ["completion_tokens"] = 20,
                ["total_tokens"] = 30
            }
        };

        // Act
        var result = response.ToString();

        // Assert
        Assert.That(result, Contains.Substring("Prompt: Test prompt"));
        Assert.That(result, Contains.Substring("Model: gpt-4"));
        Assert.That(result, Contains.Substring("AI Response: Test response"));
        Assert.That(result, Contains.Substring("Token Usage:"));
        Assert.That(result, Contains.Substring("total_tokens: 30"));
    }
}
