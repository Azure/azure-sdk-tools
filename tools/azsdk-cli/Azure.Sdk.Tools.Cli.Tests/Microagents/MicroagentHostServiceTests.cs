using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Azure.Sdk.Tools.Cli.Helpers;
using NUnit.Framework;
using Moq;
using OpenAI.Chat;

namespace Azure.Sdk.Tools.Cli.Tests.Microagents;

internal class MicroagentHostServiceTests
{
    private Mock<AzureOpenAIClient> openAIClientMock;
    private Mock<Microsoft.Extensions.Logging.ILogger<MicroagentHostService>> loggerMock;
    private Mock<ChatClient> chatClientMock;
    private MicroagentHostService microagentHostService;

    [SetUp]
    public void Setup()
    {
        openAIClientMock = new Mock<AzureOpenAIClient>();
        loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<MicroagentHostService>>();
        chatClientMock = new Mock<ChatClient>();
        openAIClientMock.Setup(client => client.GetChatClient(It.IsAny<string>()))
            .Returns(chatClientMock.Object);
        var tokenUsageHelper = new TokenUsageHelper(Mock.Of<Azure.Sdk.Tools.Cli.Helpers.IOutputHelper>());
        microagentHostService = new MicroagentHostService(openAIClientMock.Object, loggerMock.Object, tokenUsageHelper);
    }

    [Test]
    public void RunAgentToCompletion_ThrowsIfToolCalledExit()
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            var agentDefinition = new Microagent<string>
            {
                Instructions = "You are a helpful assistant.",
                Tools = new List<IAgentTool>
                {
                    new ListFilesTool(".")
                    {
                        Name = "Exit",
                        Description = "Exit the agent",
                    }
                },
            };

            await microagentHostService.RunAgentToCompletion(agentDefinition);
        });
    }

    [Test]
    public async Task RunAgentToCompletion_Exits()
    {
        // Arrange
        var exitToolCall = ChatToolCall.CreateFunctionToolCall(
            "fakeid",
            "Exit",
            BinaryData.FromString("""{"Result":"Success"}""")
        );

        var chatCompletion = OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [exitToolCall]);

        var agentDefinition = new Microagent<string>
        {
            Instructions = "You are a helpful assistant."
        };
        chatClientMock.Setup(client => client.CompleteChatAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientResult.FromValue(chatCompletion, Mock.Of<PipelineResponse>()));

        // Act
        var result = await microagentHostService.RunAgentToCompletion(agentDefinition);

        // Assert
        Assert.That(result, Is.EqualTo("Success"));
    }

    [Test]
    public async Task RunAgentToCompletion_CallsTool()
    {
        // Arrange
        var tool = Mock.Of<IAgentTool>(t => t.Name == "TestTool"
            && t.Description == "Test"
            && t.InputSchema == "totally a json schema"
            && t.Invoke(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult("Tool Result")
        );

        var regularToolCall = ChatToolCall.CreateFunctionToolCall(
            "fakeid",
            "TestTool",
            BinaryData.FromString("Tool Input")
        );

        var exitToolCall = ChatToolCall.CreateFunctionToolCall(
            "fakeid2",
            "Exit",
            BinaryData.FromString("""{"Result":"Success"}""")
        );

        var microagent = new Microagent<string>
        {
            Instructions = "You are a helpful assistant.",
            Tools = [tool],
        };

        chatClientMock.SetupSequence(client => client.CompleteChatAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [regularToolCall]), Mock.Of<PipelineResponse>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [exitToolCall]), Mock.Of<PipelineResponse>()));

        // Act
        var result = await microagentHostService.RunAgentToCompletion(microagent);

        // Assert
        Assert.That(result, Is.EqualTo("Success"));
        Mock.Get(tool).Verify(x => x.Invoke("Tool Input", It.IsAny<CancellationToken>()));
    }

    [Test]
    public void RunAgentToCompletion_ThrowsIfMaxToolCallsExceeded()
    {
        // Arrange
        var tool = Mock.Of<IAgentTool>(t => t.Name == "TestTool"
            && t.Description == "Test"
            && t.InputSchema == "totally a json schema"
            && t.Invoke(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult("Tool Result")
        );

        var regularToolCall = ChatToolCall.CreateFunctionToolCall(
            "fakeid",
            "TestTool",
            BinaryData.FromString("Tool Input")
        );

        var exitToolCall = ChatToolCall.CreateFunctionToolCall(
            "fakeid2",
            "Exit",
            BinaryData.FromString("""{"Result":"Success"}""")
        );

        var microagent = new Microagent<string>
        {
            Instructions = "You are a helpful assistant.",
            Tools = [tool],
            MaxToolCalls = 2,
        };

        chatClientMock.SetupSequence(client => client.CompleteChatAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [regularToolCall]), Mock.Of<PipelineResponse>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [regularToolCall]), Mock.Of<PipelineResponse>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [exitToolCall]), Mock.Of<PipelineResponse>()));

        // Act/Assert
        Assert.ThrowsAsync<Exception>(async () =>
        {
            await microagentHostService.RunAgentToCompletion(microagent);
        });
    }
}
