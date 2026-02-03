using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.CopilotAgents;

[TestFixture]
internal class CopilotAgentRunnerTests
{
    private Mock<ILogger<CopilotAgentRunner>> loggerMock;
    private CopilotTokenUsageHelper tokenUsageHelper;
    private Mock<ICopilotClientWrapper> clientMock;
    private Mock<ICopilotSessionWrapper> sessionMock;
    private List<SessionEventHandler> eventHandlers;
    private ICollection<AIFunction>? capturedTools;

    [SetUp]
    public void Setup()
    {
        loggerMock = new Mock<ILogger<CopilotAgentRunner>>();
        tokenUsageHelper = new CopilotTokenUsageHelper(Mock.Of<IRawOutputHelper>());

        eventHandlers = [];
        capturedTools = null;
        
        // Setup session mock
        sessionMock = new Mock<ICopilotSessionWrapper>();
        sessionMock.Setup(s => s.On(It.IsAny<SessionEventHandler>()))
            .Callback<SessionEventHandler>(handler => eventHandlers.Add(handler))
            .Returns(() => Mock.Of<IDisposable>());
        sessionMock.Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        // Setup client mock
        clientMock = new Mock<ICopilotClientWrapper>();
        clientMock.Setup(c => c.CreateSessionAsync(
                It.IsAny<SessionConfig>(),
                It.IsAny<CancellationToken>()))
            .Callback<SessionConfig?, CancellationToken>((config, ct) =>
            {
                capturedTools = config?.Tools;
            })
            .ReturnsAsync(sessionMock.Object);
    }

    private void DispatchEvent(SessionEvent evt)
    {
        foreach (var handler in eventHandlers.ToArray())
        {
            handler(evt);
        }
    }

    private void SimulateExitToolCall(string result)
    {
        // Find and invoke the Exit tool
        var exitTool = capturedTools?.FirstOrDefault(t => t.Name == "Exit");
        if (exitTool != null)
        {
            var args = new AIFunctionArguments { ["result"] = result };
            _ = exitTool.InvokeAsync(args);
        }
        // After tool execution, dispatch SessionIdleEvent to signal completion
        SimulateSessionIdle();
    }

    private void SimulateSessionIdle()
    {
        var idleEvent = new SessionIdleEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Data = new SessionIdleData()
        };
        DispatchEvent(idleEvent);
    }

    private void SimulateUsageEvent(int inputTokens, int outputTokens, string model)
    {
        var usageEvent = new AssistantUsageEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Data = new AssistantUsageData
            {
                Model = model,
                InputTokens = inputTokens,
                OutputTokens = outputTokens
            }
        };
        DispatchEvent(usageEvent);
    }

    [Test]
    public async Task RunAsync_WithExitTool_ReturnsResult()
    {
        const string expectedResult = "Success";
        
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // Simulate tool execution and session becoming idle
                SimulateExitToolCall(expectedResult);
            })
            .ReturnsAsync("msg-id");

        var runner = new CopilotAgentRunner(
            clientMock.Object,
            tokenUsageHelper,
            loggerMock.Object);

        var agent = new CopilotAgent<string>
        {
            Instructions = "Test agent"
        };

        var result = await runner.RunAsync(agent);

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task RunAsync_WithValidationSuccess_ReturnsResult()
    {
        const string expectedResult = "ValidResult";
        var validationCalled = false;

        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                SimulateExitToolCall(expectedResult);
            })
            .ReturnsAsync("msg-id");

        var runner = new CopilotAgentRunner(
            clientMock.Object,
            tokenUsageHelper,
            loggerMock.Object);

        var agent = new CopilotAgent<string>
        {
            Instructions = "Test agent",
            ValidateResult = (result) =>
            {
                validationCalled = true;
                Assert.That(result, Is.EqualTo(expectedResult));
                return Task.FromResult(new CopilotAgentValidationResult { Success = true });
            }
        };

        var result = await runner.RunAsync(agent);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(expectedResult));
            Assert.That(validationCalled, Is.True, "Validation callback should have been called");
        });

    }

    [Test]
    public async Task RunAsync_WithValidationFailureThenSuccess_Retries()
    {
        var validationCallCount = 0;
        var sendCallCount = 0;

        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                sendCallCount++;
                // First call returns "Invalid", second call returns "Valid"
                SimulateExitToolCall(sendCallCount == 1 ? "InvalidResult" : "ValidResult");
            })
            .ReturnsAsync("msg-id");

        var runner = new CopilotAgentRunner(
            clientMock.Object,
            tokenUsageHelper,
            loggerMock.Object);

        var agent = new CopilotAgent<string>
        {
            Instructions = "Test agent",
            ValidateResult = (result) =>
            {
                validationCallCount++;
                if (result == "InvalidResult")
                {
                    return Task.FromResult(new CopilotAgentValidationResult
                    {
                        Success = false,
                        Reason = "Result is invalid"
                    });
                }
                return Task.FromResult(new CopilotAgentValidationResult { Success = true });
            }
        };

        var result = await runner.RunAsync(agent);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("ValidResult"));
            Assert.That(validationCallCount, Is.EqualTo(2), "Validation should have been called twice");
        });

    }

    [Test]
    public void RunAsync_MaxIterationsExceeded_ThrowsException()
    {
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                SimulateExitToolCall("AlwaysInvalid");
            })
            .ReturnsAsync("msg-id");

        var runner = new CopilotAgentRunner(
            clientMock.Object,
            tokenUsageHelper,
            loggerMock.Object);

        var agent = new CopilotAgent<string>
        {
            Instructions = "Test agent",
            MaxIterations = 3,
            ValidateResult = (_) => Task.FromResult(new CopilotAgentValidationResult
            {
                Success = false,
                Reason = "Always fails"
            })
        };

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await runner.RunAsync(agent);
        });

        Assert.That(ex.Message, Does.Contain("3 iterations"));
    }

    [Test]
    public void RunAsync_SessionError_ThrowsException()
    {
        // Session errors are dispatched via events, not thrown from SendAsync
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                var errorEvent = new SessionErrorEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    Data = new SessionErrorData
                    {
                        ErrorType = "TestErrorType",
                        Message = "Test error message"
                    }
                };
                DispatchEvent(errorEvent);
                SimulateSessionIdle();
            })
            .ReturnsAsync("msg-id");

        var runner = new CopilotAgentRunner(
            clientMock.Object,
            tokenUsageHelper,
            loggerMock.Object);

        var agent = new CopilotAgent<string>
        {
            Instructions = "Test agent"
        };

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await runner.RunAsync(agent);
        });

        Assert.That(ex.Message, Does.Contain("Test error message"));
    }

    [Test]
    public void RunAsync_NoExitToolCalled_ThrowsException()
    {
        // SendAsync completes but Exit tool was not called
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // Signal session idle without calling Exit
                SimulateSessionIdle();
            })
            .ReturnsAsync("msg-id");

        var runner = new CopilotAgentRunner(
            clientMock.Object,
            tokenUsageHelper,
            loggerMock.Object);

        var agent = new CopilotAgent<string>
        {
            Instructions = "Test agent"
        };

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await runner.RunAsync(agent);
        });

        Assert.That(ex.Message, Does.Contain("failed to call Exit"));
    }

    [Test]
    public async Task RunAsync_TokenUsageTracking_AddsTokens()
    {
        var outputHelper = new Mock<IRawOutputHelper>();
        var tokenUsageHelper = new CopilotTokenUsageHelper(outputHelper.Object);

        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // Events are dispatched during SendAsync
                SimulateUsageEvent(100, 50, "gpt-5");
                SimulateExitToolCall("Success");
            })
            .ReturnsAsync("msg-id");

        var runner = new CopilotAgentRunner(
            clientMock.Object,
            tokenUsageHelper,
            loggerMock.Object);

        var agent = new CopilotAgent<string>
        {
            Instructions = "Test agent"
        };


        await runner.RunAsync(agent);

        Assert.That(tokenUsageHelper.TotalTokens, Is.EqualTo(150));
    }

    [Test]
    public void RunAsync_Cancellation_ThrowsTaskCanceledException()
    {
        // SendAsync respects cancellation token
        using var cts = new CancellationTokenSource();
        
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Returns<MessageOptions, CancellationToken>((options, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult("msg-id");
            });

        var runner = new CopilotAgentRunner(
            clientMock.Object,
            tokenUsageHelper,
            loggerMock.Object);

        var agent = new CopilotAgent<string>
        {
            Instructions = "Test agent"
        };

        // Cancel before running
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await runner.RunAsync(agent, cts.Token);
        });
    }

    [Test]
    public async Task RunAsync_WithCustomTools_ToolsAreRegistered()
    {
        var customTool = AIFunctionFactory.Create(() => "Tool result", "CustomTool", "A custom test tool");

        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                SimulateExitToolCall("Success");
            })
            .ReturnsAsync("msg-id");

        var runner = new CopilotAgentRunner(
            clientMock.Object,
            tokenUsageHelper,
            loggerMock.Object);

        var agent = new CopilotAgent<string>
        {
            Instructions = "Test agent",
            Tools = [customTool]
        };

        var result = await runner.RunAsync(agent);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("Success"));
            // Verify that tools were passed to session config (CustomTool + Exit)
            Assert.That(capturedTools, Has.Count.EqualTo(2));
        });

        Assert.Multiple(() =>
        {
            Assert.That(capturedTools!.Any(t => t.Name == "CustomTool"), Is.True);
            Assert.That(capturedTools!.Any(t => t.Name == "Exit"), Is.True);
        });

    }

    [Test]
    public async Task RunAsync_ValidationFailureWithObjectReason_SerializesReason()
    {
        var validationCallCount = 0;
        var sendCallCount = 0;
        string? capturedPrompt = null;

        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback<MessageOptions, CancellationToken>((options, ct) =>
            {
                sendCallCount++;
                if (sendCallCount > 1)
                {
                    capturedPrompt = options.Prompt;
                }
                SimulateExitToolCall(sendCallCount == 1 ? "FirstResult" : "SecondResult");
            })
            .ReturnsAsync("msg-id");

        var runner = new CopilotAgentRunner(
            clientMock.Object,
            tokenUsageHelper,
            loggerMock.Object);

        var agent = new CopilotAgent<string>
        {
            Instructions = "Test agent",
            ValidateResult = (result) =>
            {
                validationCallCount++;
                if (validationCallCount == 1)
                {
                    return Task.FromResult(new CopilotAgentValidationResult
                    {
                        Success = false,
                        Reason = new { Error = "Invalid", Code = 123 }
                    });
                }
                return Task.FromResult(new CopilotAgentValidationResult { Success = true });
            }
        };

        var result = await runner.RunAsync(agent);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("SecondResult"));
            Assert.That(validationCallCount, Is.EqualTo(2));
            // Verify the prompt contains the serialized validation error
            Assert.That(capturedPrompt, Does.Contain("Error"));
            Assert.That(capturedPrompt, Does.Contain("123"));
        });
    }

    [Test]
    public async Task RunAsync_SessionConfigured_WithCorrectModelAndInstructions()
    {
        SessionConfig? capturedConfig = null;
        
        clientMock.Setup(c => c.CreateSessionAsync(
                It.IsAny<SessionConfig>(),
                It.IsAny<CancellationToken>()))
            .Callback<SessionConfig?, CancellationToken>((config, ct) =>
            {
                capturedConfig = config;
                capturedTools = config?.Tools;
            })
            .ReturnsAsync(sessionMock.Object);

        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                SimulateExitToolCall("Success");
            })
            .ReturnsAsync("msg-id");

        var runner = new CopilotAgentRunner(
            clientMock.Object,
            tokenUsageHelper,
            loggerMock.Object);

        var agent = new CopilotAgent<string>
        {
            Instructions = "Custom instructions for testing",
            Model = "custom-model"
        };

        await runner.RunAsync(agent);

        Assert.That(capturedConfig, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(capturedConfig!.Model, Is.EqualTo("custom-model"));
            Assert.That(capturedConfig.SystemMessage?.Content, Is.EqualTo("Custom instructions for testing"));
            Assert.That(capturedConfig.SystemMessage?.Mode, Is.EqualTo(SystemMessageMode.Append));
        });

    }

    [Test]
    public async Task RunAsync_ValidationWithStringReason_PassesReasonToPrompt()
    {
        var sendCallCount = 0;
        string? capturedPrompt = null;

        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback<MessageOptions, CancellationToken>((options, ct) =>
            {
                sendCallCount++;
                if (sendCallCount > 1)
                {
                    capturedPrompt = options.Prompt;
                }
                SimulateExitToolCall(sendCallCount == 1 ? "First" : "Second");
            })
            .ReturnsAsync("msg-id");

        var runner = new CopilotAgentRunner(
            clientMock.Object,
            tokenUsageHelper,
            loggerMock.Object);

        var agent = new CopilotAgent<string>
        {
            Instructions = "Test",
            ValidateResult = (result) =>
            {
                if (result == "First")
                {
                    return Task.FromResult(new CopilotAgentValidationResult
                    {
                        Success = false,
                        Reason = "String validation error"
                    });
                }
                return Task.FromResult(new CopilotAgentValidationResult { Success = true });
            }
        };

        await runner.RunAsync(agent);

        Assert.That(capturedPrompt, Does.Contain("String validation error"));
    }
}
