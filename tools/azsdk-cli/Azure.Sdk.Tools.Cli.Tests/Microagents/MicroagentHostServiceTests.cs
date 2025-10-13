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
        var tokenUsageHelper = new TokenUsageHelper(Mock.Of<Azure.Sdk.Tools.Cli.Helpers.IRawOutputHelper>());
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

#pragma warning disable OPENAI001
        var chatCompletion = OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [exitToolCall]);
#pragma warning restore OPENAI001

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

#pragma warning disable OPENAI001
        chatClientMock.SetupSequence(client => client.CompleteChatAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [regularToolCall]), Mock.Of<PipelineResponse>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [exitToolCall]), Mock.Of<PipelineResponse>()));
#pragma warning restore OPENAI001

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

#pragma warning disable OPENAI001
        chatClientMock.SetupSequence(client => client.CompleteChatAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [regularToolCall]), Mock.Of<PipelineResponse>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [regularToolCall]), Mock.Of<PipelineResponse>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [exitToolCall]), Mock.Of<PipelineResponse>()));
#pragma warning restore OPENAI001

        // Act/Assert
        Assert.ThrowsAsync<Exception>(async () =>
        {
            await microagentHostService.RunAgentToCompletion(microagent);
        });
    }

    [Test]
    public async Task RunAgentToCompletion_ValidateResult_Success()
    {
        // Arrange
        var exitToolCall = ChatToolCall.CreateFunctionToolCall(
            "fakeid",
            "Exit",
            BinaryData.FromString("""{"Result":"ValidResult"}""")
        );

#pragma warning disable OPENAI001
        var chatCompletion = OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [exitToolCall]);
#pragma warning restore OPENAI001

        var validationCallbackCalled = false;
        var agentDefinition = new Microagent<string>
        {
            Instructions = "You are a helpful assistant.",
            ValidateResult = async (result) =>
            {
                validationCallbackCalled = true;
                Assert.That(result, Is.EqualTo("ValidResult"));
                return await Task.FromResult(new MicroagentValidationResult { Success = true });
            }
        };

        chatClientMock.Setup(client => client.CompleteChatAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientResult.FromValue(chatCompletion, Mock.Of<PipelineResponse>()));

        // Act
        var result = await microagentHostService.RunAgentToCompletion(agentDefinition);

        // Assert
        Assert.That(result, Is.EqualTo("ValidResult"));
        Assert.That(validationCallbackCalled, Is.True, "Validation callback should have been called");
    }

    [Test]
    public async Task RunAgentToCompletion_ValidateResult_FailureThenSuccess()
    {
        // Arrange
        var firstExitToolCall = ChatToolCall.CreateFunctionToolCall(
            "fakeid1",
            "Exit",
            BinaryData.FromString("""{"Result":"InvalidResult"}""")
        );

        var secondExitToolCall = ChatToolCall.CreateFunctionToolCall(
            "fakeid2",
            "Exit",
            BinaryData.FromString("""{"Result":"ValidResult"}""")
        );

        var validationCallCount = 0;
        var agentDefinition = new Microagent<string>
        {
            Instructions = "You are a helpful assistant.",
            ValidateResult = (result) =>
            {
                validationCallCount++;
                if (result == "InvalidResult")
                {
                    return Task.FromResult(new MicroagentValidationResult
                    {
                        Success = false,
                        Reason = "Result is invalid because it contains 'Invalid'"
                    });
                }
                return Task.FromResult(new MicroagentValidationResult { Success = true });
            }
        };

#pragma warning disable OPENAI001
        chatClientMock.SetupSequence(client => client.CompleteChatAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [firstExitToolCall]), Mock.Of<PipelineResponse>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [secondExitToolCall]), Mock.Of<PipelineResponse>()));
#pragma warning restore OPENAI001

        // Act
        var result = await microagentHostService.RunAgentToCompletion(agentDefinition);

        // Assert
        Assert.That(result, Is.EqualTo("ValidResult"));
        Assert.That(validationCallCount, Is.EqualTo(2), "Validation should have been called twice");
    }

    [Test]
    public async Task RunAgentToCompletion_ValidateResult_FailureWithReason()
    {
        // Arrange
        var firstExitToolCall = ChatToolCall.CreateFunctionToolCall(
            "fakeid1",
            "Exit",
            BinaryData.FromString("""{"Result":"InvalidResult"}""")
        );

        var secondExitToolCall = ChatToolCall.CreateFunctionToolCall(
            "fakeid2",
            "Exit",
            BinaryData.FromString("""{"Result":"CorrectedResult"}""")
        );

        var agentDefinition = new Microagent<string>
        {
            Instructions = "You are a helpful assistant.",
            ValidateResult = (result) =>
            {
                if (result == "InvalidResult")
                {
                    return Task.FromResult(new MicroagentValidationResult
                    {
                        Success = false,
                        Reason = "Custom validation error message"
                    });
                }
                return Task.FromResult(new MicroagentValidationResult { Success = true });
            }
        };

#pragma warning disable OPENAI001
        chatClientMock.SetupSequence(client => client.CompleteChatAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [firstExitToolCall]), Mock.Of<PipelineResponse>()))
            .ReturnsAsync(ClientResult.FromValue(OpenAIChatModelFactory.ChatCompletion(role: ChatMessageRole.Assistant, toolCalls: [secondExitToolCall]), Mock.Of<PipelineResponse>()));
#pragma warning restore OPENAI001

        // Act
        var result = await microagentHostService.RunAgentToCompletion(agentDefinition);

        // Assert
        Assert.That(result, Is.EqualTo("CorrectedResult"));

        // Verify that the validation error was passed to the chat completion
        chatClientMock.Verify(client => client.CompleteChatAsync(
            It.Is<IReadOnlyList<ChatMessage>>(messages => 
            messages.Any(m => m.Content.Any(c => 
                c.Text != null && c.Text.Contains("Custom validation error message")))),
            It.IsAny<ChatCompletionOptions>(), 
            It.IsAny<CancellationToken>()), 
            Times.Exactly(2));
    }
}
