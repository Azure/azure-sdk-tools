using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using GitHub.Copilot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;

using static Azure.Sdk.Tools.Cli.Tests.TestHelpers.TestCategories;

namespace Azure.Sdk.Tools.Cli.Tests.CopilotAgents;

[TestFixture]
internal class CopilotAgentRunnerTests
{
    private Mock<ILogger<CopilotAgentRunner>> loggerMock;
    private TokenUsageHelper tokenUsageHelper;
    private Mock<ICopilotClientWrapper> clientMock;
    private Mock<ICopilotSessionWrapper> sessionMock;
    private List<Action<SessionEvent>> eventHandlers;
    private ICollection<AIFunction>? capturedTools;

    [SetUp]
    public void Setup()
    {
        loggerMock = new Mock<ILogger<CopilotAgentRunner>>();
        tokenUsageHelper = new TokenUsageHelper(Mock.Of<IRawOutputHelper>());

        eventHandlers = [];
        capturedTools = null;

        // Setup session mock
        sessionMock = new Mock<ICopilotSessionWrapper>();
        sessionMock.Setup(s => s.On(It.IsAny<Action<SessionEvent>>()))
            .Callback<Action<SessionEvent>>(handler => eventHandlers.Add(handler))
            .Returns(() => Mock.Of<IDisposable>());
        sessionMock.Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        // Setup client mock
        clientMock = new Mock<ICopilotClientWrapper>();
        clientMock.Setup(c => c.GetAuthStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CopilotAuthStatus(IsAuthenticated: true));
        clientMock.Setup(c => c.CreateSessionAsync(
                It.IsAny<SessionConfig>(),
                It.IsAny<CancellationToken>()))
            .Callback<SessionConfig?, CancellationToken>((config, ct) =>
            {
                capturedTools = config?.Tools?.OfType<AIFunction>().ToList();
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

    [TestCase(true)]
    [TestCase(false)]
    public async Task RunAsync_TurnHooks_RetainSessionAndOwnContinuation(bool callExit)
    {
        var order = new List<string>();
        var prompts = new List<string>();
        var results = new List<string?>();
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback<MessageOptions, CancellationToken>((options, _) =>
            {
                prompts.Add(options.Prompt);
                order.Add("send");
                if (callExit)
                {
                    SimulateExitToolCall($"hypothesis-{prompts.Count}");
                }
                else
                {
                    SimulateSessionIdle();
                }
            })
            .ReturnsAsync("message");
        using var cts = new CancellationTokenSource();
        var runner = new CopilotAgentRunner(clientMock.Object, tokenUsageHelper, loggerMock.Object);
        var result = await runner.RunAsync(new CopilotAgent<string>
        {
            Instructions = "Repair",
            MaxIterations = 4,
            OnTurnStarting = ct =>
            {
                Assert.That(ct, Is.EqualTo(cts.Token));
                order.Add("start");
                return Task.CompletedTask;
            },
            OnTurnCompleted = (hypothesis, ct) =>
            {
                Assert.That(ct, Is.EqualTo(cts.Token));
                results.Add(hypothesis);
                order.Add("complete");
                return Task.FromResult(new CopilotAgentTurnResult<string>(
                    results.Count < 4, $"diagnostic-{results.Count}", string.Empty));
            },
            ValidateResult = _ => throw new AssertionException("Legacy validation must not run in turn-hook mode.")
        }, cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(order, Is.EqualTo(Enumerable.Range(0, 4).SelectMany(_ => new[] { "start", "send", "complete" })));
            Assert.That(prompts.Skip(1), Is.EqualTo(new[] { "diagnostic-1", "diagnostic-2", "diagnostic-3" }));
            Assert.That(results, Is.EqualTo(Enumerable.Range(1, 4).Select(i => callExit ? $"hypothesis-{i}" : null)));
        });
        clientMock.Verify(c => c.CreateSessionAsync(It.IsAny<SessionConfig>(), cts.Token), Times.Once);
        sessionMock.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Test]
    public async Task RunAsync_TurnHookSession_AllowListsOnlyDeclaredToolsAndExit()
    {
        SessionConfig? config = null;
        clientMock.Setup(c => c.CreateSessionAsync(It.IsAny<SessionConfig>(), It.IsAny<CancellationToken>()))
            .Callback<SessionConfig, CancellationToken>((value, _) => config = value)
            .ReturnsAsync(sessionMock.Object);
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(SimulateSessionIdle).ReturnsAsync("message");
        string[] names = ["ReadFile", "GrepSearch", "CodePatchTool", "RenameFile"];
        var runner = new CopilotAgentRunner(clientMock.Object, tokenUsageHelper, loggerMock.Object);
        await runner.RunAsync(new CopilotAgent<string>
        {
            Instructions = "Repair using host validation",
            Tools = names.Select(name => AIFunctionFactory.Create(() => "", name)),
            OnTurnCompleted = (_, _) => Task.FromResult(new CopilotAgentTurnResult<string>(false, null, string.Empty))
        });
        Assert.That(config!.AvailableTools, Is.EquivalentTo(names.Append("Exit")),
            "Built-in shell, edit, generation, build and artifact tools must not be available.");
        Assert.That(config.Tools!.Select(t => t.Name), Is.EquivalentTo(config.AvailableTools));
    }

    [Test]
    public void RunAsync_TurnHooks_RespectHardIterationLimitWithoutExit()
    {
        var completed = 0;
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(SimulateSessionIdle).ReturnsAsync("message");
        var runner = new CopilotAgentRunner(clientMock.Object, tokenUsageHelper, loggerMock.Object);
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(new CopilotAgent<string>
        {
            Instructions = "Repair",
            MaxIterations = 2,
            OnTurnCompleted = (_, _) =>
            {
                completed++;
                return Task.FromResult(new CopilotAgentTurnResult<string>(true, "Retry", null));
            }
        }));
        Assert.That(error!.Message, Does.Contain("2 iterations"));
        Assert.That(completed, Is.EqualTo(2));
        sessionMock.Verify(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestCase("start")]
    [TestCase("send")]
    [TestCase("complete")]
    public void RunAsync_TurnHooks_CancellationStopsBeforeNextStage(string cancellationStage)
    {
        using var cts = new CancellationTokenSource();
        var started = 0;
        var sent = 0;
        var completed = 0;
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                sent++;
                if (cancellationStage == "send")
                {
                    cts.Cancel();
                }
                SimulateSessionIdle();
            }).ReturnsAsync("message");
        var runner = new CopilotAgentRunner(clientMock.Object, tokenUsageHelper, loggerMock.Object);
        Assert.CatchAsync<OperationCanceledException>(() => runner.RunAsync(new CopilotAgent<string>
        {
            Instructions = "Repair",
            OnTurnStarting = _ =>
            {
                started++;
                if (cancellationStage == "start")
                {
                    cts.Cancel();
                }
                return Task.CompletedTask;
            },
            OnTurnCompleted = (_, _) =>
            {
                completed++;
                cts.Cancel();
                return Task.FromResult(new CopilotAgentTurnResult<string>(true, "Retry", null));
            }
        }, cts.Token));
        Assert.That(started, Is.EqualTo(1));
        Assert.That(sent, Is.EqualTo(cancellationStage == "start" ? 0 : 1));
        Assert.That(completed, Is.EqualTo(cancellationStage == "complete" ? 1 : 0));
        sessionMock.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Test]
    public void RunAsync_TurnHooks_SessionErrorPreventsCompletionCallback()
    {
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                DispatchEvent(new SessionErrorEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    Data = new SessionErrorData { ErrorType = "failure", Message = "broken session" }
                });
                SimulateSessionIdle();
            }).ReturnsAsync("message");
        var runner = new CopilotAgentRunner(clientMock.Object, tokenUsageHelper, loggerMock.Object);
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(new CopilotAgent<string>
        {
            Instructions = "Repair",
            OnTurnCompleted = (_, _) => throw new AssertionException("Must not validate a failed session")
        }));
        Assert.That(error!.Message, Does.Contain("broken session"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RunAsync_TurnHooks_IdleTimeoutIsDistinctFromCallerCancellation(bool cancelCaller)
    {
        using var cts = new CancellationTokenSource();
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (cancelCaller)
                {
                    cts.Cancel();
                }
            }).ReturnsAsync("message");
        var runner = new CopilotAgentRunner(clientMock.Object, tokenUsageHelper, loggerMock.Object);
        var error = Assert.CatchAsync(() => runner.RunAsync(new CopilotAgent<string>
        {
            Instructions = "Repair",
            IdleTimeout = TimeSpan.FromMilliseconds(10),
            OnTurnCompleted = (_, _) => throw new AssertionException("No idle event was received")
        }, cts.Token));
        Assert.That(error, cancelCaller ? Is.InstanceOf<OperationCanceledException>() : Is.TypeOf<TimeoutException>());
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RunAsync_TurnHookExceptions_AreSurfaced(bool failAtStart)
    {
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(SimulateSessionIdle).ReturnsAsync("message");
        var runner = new CopilotAgentRunner(clientMock.Object, tokenUsageHelper, loggerMock.Object);
        var error = Assert.ThrowsAsync<IOException>(() => runner.RunAsync(new CopilotAgent<string>
        {
            Instructions = "Repair",
            OnTurnStarting = _ => failAtStart ? throw new IOException("snapshot failure") : Task.CompletedTask,
            OnTurnCompleted = (_, _) => throw new IOException("validation failure")
        }));
        Assert.That(error!.Message, Is.EqualTo(failAtStart ? "snapshot failure" : "validation failure"));
        sessionMock.Verify(s => s.DisposeAsync(), Times.Once);
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
        var tokenUsageHelper = new TokenUsageHelper(outputHelper.Object);

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
                capturedTools = config?.Tools?.OfType<AIFunction>().ToList();
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
    public async Task RunAsync_WithoutModel_LetsCopilotSelectDefault()
    {
        SessionConfig? capturedConfig = null;
        clientMock.Setup(c => c.CreateSessionAsync(
                It.IsAny<SessionConfig>(),
                It.IsAny<CancellationToken>()))
            .Callback<SessionConfig?, CancellationToken>((config, _) =>
            {
                capturedConfig = config;
                capturedTools = config?.Tools?.OfType<AIFunction>().ToList();
            })
            .ReturnsAsync(sessionMock.Object);
        sessionMock.Setup(s => s.SendAsync(It.IsAny<MessageOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() => SimulateExitToolCall("Success"))
            .ReturnsAsync("msg-id");

        var runner = new CopilotAgentRunner(clientMock.Object, tokenUsageHelper, loggerMock.Object);

        await runner.RunAsync(new CopilotAgent<string> { Instructions = "Test" });

        Assert.That(capturedConfig?.Model, Is.Null);
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

    [Test]
    public void RunAsync_WithInvalidCliPath_ThrowsCliNotFoundError()
    {
        // Arrange - Use a fake CLI path that doesn't exist
        var copilotClient = new CopilotClient(new CopilotClientOptions
        {
            Connection = RuntimeConnection.ForStdio("/nonexistent/path/to/copilot-cli"),
            UseLoggedInUser = false,
        });
        var copilotClientWrapper = new CopilotClientWrapper(copilotClient);
        var localTokenUsageHelper = new TokenUsageHelper(Mock.Of<IRawOutputHelper>());
        var runner = new CopilotAgentRunner(
            copilotClientWrapper,
            localTokenUsageHelper,
            new TestLogger<CopilotAgentRunner>());

        var agent = new CopilotAgent<AuthTestResult>
        {
            Instructions = "Test instructions",
            Tools = []
        };

        // Act & Assert
        var ex = Assert.ThrowsAsync<CopilotCliUnavailableException>(async () =>
            await runner.RunAsync(agent));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("GitHub Copilot CLI could not be found"));
            Assert.That(ex.Message, Does.Contain("install"));
        });

    }

    [Test, Explicit]
    [Category(CopilotAgent)]
    public void RunAsync_WithInvalidGitHubToken_ThrowsNotAuthenticatedError()
    {
        // Arrange - Use an invalid GitHub token
        var copilotClient = new CopilotClient(new CopilotClientOptions
        {
            Connection = RuntimeConnection.ForStdio(),
            UseLoggedInUser = false,
            GitHubToken = "invalid_token_that_will_not_work"
        });
        var copilotClientWrapper = new CopilotClientWrapper(copilotClient);
        var localTokenUsageHelper = new TokenUsageHelper(Mock.Of<IRawOutputHelper>());
        var runner = new CopilotAgentRunner(
            copilotClientWrapper,
            localTokenUsageHelper,
            new TestLogger<CopilotAgentRunner>());

        var agent = new CopilotAgent<AuthTestResult>
        {
            Instructions = "Test instructions",
            Tools = []
        };

        // Act & Assert
        var ex = Assert.ThrowsAsync<CopilotCliUnavailableException>(async () =>
            await runner.RunAsync(agent));

        Assert.That(ex!.Message, Does.Contain("not authenticated").Or.Contain("could not be found"));
    }

    [Test]
    public void RunAsync_WhenGetAuthStatusThrows_ThrowsCliNotFoundError()
    {
        // Arrange - Mock the client wrapper to throw when GetAuthStatusAsync is called
        var mockClientWrapper = new Mock<ICopilotClientWrapper>();
        mockClientWrapper
            .Setup(c => c.GetAuthStatusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("CLI process failed to start"));

        var localTokenUsageHelper = new TokenUsageHelper(Mock.Of<IRawOutputHelper>());
        var runner = new CopilotAgentRunner(
            mockClientWrapper.Object,
            localTokenUsageHelper,
            new TestLogger<CopilotAgentRunner>());

        var agent = new CopilotAgent<AuthTestResult>
        {
            Instructions = "Test instructions",
            Tools = []
        };

        // Act & Assert
        var ex = Assert.ThrowsAsync<CopilotCliUnavailableException>(async () =>
            await runner.RunAsync(agent));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("GitHub Copilot CLI could not be found"));
            Assert.That(ex.InnerException, Is.Not.Null);
        });

        Assert.That(ex.InnerException!.Message, Does.Contain("CLI process failed"));
    }

    [Test]
    public void RunAsync_WhenNotAuthenticated_ThrowsNotAuthenticatedError()
    {
        // Arrange - Mock the client wrapper to return IsAuthenticated = false
        var mockClientWrapper = new Mock<ICopilotClientWrapper>();
        mockClientWrapper
            .Setup(c => c.GetAuthStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CopilotAuthStatus(IsAuthenticated: false));

        var localTokenUsageHelper = new TokenUsageHelper(Mock.Of<IRawOutputHelper>());
        var runner = new CopilotAgentRunner(
            mockClientWrapper.Object,
            localTokenUsageHelper,
            new TestLogger<CopilotAgentRunner>());

        var agent = new CopilotAgent<AuthTestResult>
        {
            Instructions = "Test instructions",
            Tools = []
        };

        // Act & Assert
        var ex = Assert.ThrowsAsync<CopilotCliUnavailableException>(async () =>
            await runner.RunAsync(agent));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("not authenticated"));
            Assert.That(ex.Message, Does.Contain("copilot login"));
        });

    }

    /// <summary>
    /// Simple result type for authentication tests.
    /// </summary>
    private record AuthTestResult
    {
        public bool Success { get; init; }
    }

}
