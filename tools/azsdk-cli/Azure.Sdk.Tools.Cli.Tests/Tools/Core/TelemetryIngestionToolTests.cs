// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if DEBUG
using System.Diagnostics;
using Moq;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Core;

internal class TelemetryIngestionToolTests
{
    private Mock<ITelemetryService> telemetryService = null!;
    private Mock<IUserPromptProcessor> promptProcessor = null!;
    private TestLogger<TelemetryIngestionTool> logger = null!;
    private TelemetryIngestionTool tool = null!;

    [SetUp]
    public void Setup()
    {
        telemetryService = new Mock<ITelemetryService>();
        promptProcessor = new Mock<IUserPromptProcessor>();
        logger = new TestLogger<TelemetryIngestionTool>();
        tool = new TelemetryIngestionTool(telemetryService.Object, promptProcessor.Object, logger);

        // Default: StartActivity returns a null activity (telemetry disabled path)
        telemetryService
            .Setup(s => s.StartActivity(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Activity?)null);
    }

    [Test]
    public async Task SkillInvocation_WithSkillName_Succeeds()
    {
        var result = await tool.IngestActivityLog(
            clientType: "vscode",
            eventType: "skill_invocation",
            sessionId: "session-123",
            skillName: "generate-sdk-locally");

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.ResponseError, Is.Null);

        var response = (TelemetryIngestionResponse)result;
        Assert.That(response.EventType, Is.EqualTo("skill_invocation"));
        Assert.That(response.SkillName, Is.EqualTo("generate-sdk-locally"));
    }

    [Test]
    public async Task SkillInvocation_WithoutSkillName_ReturnsValidationError()
    {
        var result = await tool.IngestActivityLog(
            clientType: "vscode",
            eventType: "skill_invocation",
            skillName: null);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseError, Does.Contain("skill-name is required"));
    }

    [Test]
    public async Task SkillInvocation_WithEmptySkillName_ReturnsValidationError()
    {
        var result = await tool.IngestActivityLog(
            clientType: "copilot-cli",
            eventType: "skill_invocation",
            skillName: "  ");

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseError, Does.Contain("skill-name is required"));
    }

    [Test]
    public async Task UserPrompt_WithBody_CallsProcessorAndSucceeds()
    {
        var analysisResult = new UserPromptAnalysisResult
        {
            Category = "sdk_generation",
            PromptSummary = "Generate Python SDK for storage service",
            Language = "Python",
            PackageName = "azure-storage-blob",
            TypeSpecProject = "specification/storage"
        };

        promptProcessor
            .Setup(p => p.AnalyzePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysisResult);

        var result = await tool.IngestActivityLog(
            clientType: "vscode",
            eventType: "user_prompt",
            sessionId: "session-456",
            body: "Generate the Python SDK for azure-storage-blob");

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.ResponseError, Is.Null);

        var response = (TelemetryIngestionResponse)result;
        Assert.That(response.PromptCategory, Is.EqualTo("sdk_generation"));
        Assert.That(response.PromptDetails, Is.EqualTo("Generate Python SDK for storage service"));
        Assert.That(response.Language, Is.EqualTo("Python"));
        Assert.That(response.PackageName, Is.EqualTo("azure-storage-blob"));
        Assert.That(response.TypeSpecProject, Is.EqualTo("specification/storage"));

        promptProcessor.Verify(
            p => p.AnalyzePromptAsync("Generate the Python SDK for azure-storage-blob", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task UserPrompt_WithoutBody_ReturnsValidationError()
    {
        var result = await tool.IngestActivityLog(
            clientType: "vscode",
            eventType: "user_prompt",
            body: null);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseError, Does.Contain("body is required"));
    }

    [Test]
    public async Task UserPrompt_WithEmptyBody_ReturnsValidationError()
    {
        var result = await tool.IngestActivityLog(
            clientType: "copilot-cli",
            eventType: "user_prompt",
            body: "  ");

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseError, Does.Contain("body is required"));
    }

    [Test]
    public async Task UserPrompt_DoesNotRequireSkillName()
    {
        promptProcessor
            .Setup(p => p.AnalyzePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPromptAnalysisResult
            {
                Category = "fix_build_failure",
                PromptSummary = "Fix .NET build failure"
            });

        var result = await tool.IngestActivityLog(
            clientType: "vscode",
            eventType: "user_prompt",
            skillName: null,
            body: "Fix my .NET build failure");

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
    }

    [Test]
    public async Task SkillInvocation_DoesNotRequireBody()
    {
        var result = await tool.IngestActivityLog(
            clientType: "vscode",
            eventType: "skill_invocation",
            skillName: "pipeline-troubleshooting",
            body: null);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
    }

    [Test]
    public async Task SkillInvocation_DoesNotCallPromptProcessor()
    {
        var result = await tool.IngestActivityLog(
            clientType: "vscode",
            eventType: "skill_invocation",
            skillName: "generate-sdk-locally");

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        promptProcessor.Verify(
            p => p.AnalyzePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task UserPrompt_WhenProcessorFails_ReturnsError()
    {
        promptProcessor
            .Setup(p => p.AnalyzePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Copilot SDK failed"));

        var result = await tool.IngestActivityLog(
            clientType: "vscode",
            eventType: "user_prompt",
            body: "Generate SDK");

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseError, Does.Contain("Failed to ingest telemetry event"));
    }

    [Test]
    public async Task UserPrompt_WhenAnalysisFails_SkipsPromptFields()
    {
        promptProcessor
            .Setup(p => p.AnalyzePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPromptAnalysisResult
            {
                Category = "unknown",
                PromptSummary = "Prompt analysis failed",
                IsSuccessful = false
            });

        var result = await tool.IngestActivityLog(
            clientType: "vscode",
            eventType: "user_prompt",
            sessionId: "session-789",
            body: "Generate SDK for storage");

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.ResponseError, Is.Null);

        var response = (TelemetryIngestionResponse)result;
        Assert.That(response.PromptCategory, Is.Null);
        Assert.That(response.PromptDetails, Is.Null);
        Assert.That(response.Language, Is.Null);
        Assert.That(response.PackageName, Is.Null);
        Assert.That(response.TypeSpecProject, Is.Null);
        // Base fields should still be recorded
        Assert.That(response.EventType, Is.EqualTo("user_prompt"));
        Assert.That(response.ClientType, Is.EqualTo("vscode"));
        Assert.That(response.SessionId, Is.EqualTo("session-789"));
    }

    [Test]
    public async Task UnknownEventType_Succeeds_WithNoValidationError()
    {
        // Unknown event types pass validation (no specific requirements)
        var result = await tool.IngestActivityLog(
            clientType: "vscode",
            eventType: "custom_event");

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.ResponseError, Is.Null);
    }
}

#endif
