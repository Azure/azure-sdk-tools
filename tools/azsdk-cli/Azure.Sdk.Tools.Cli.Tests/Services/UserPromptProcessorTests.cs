// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Moq;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

internal class UserPromptProcessorTests
{
    private Mock<ICopilotAgentRunner> agentRunner = null!;
    private TestLogger<UserPromptProcessor> logger = null!;
    private UserPromptProcessor processor = null!;

    [SetUp]
    public void Setup()
    {
        agentRunner = new Mock<ICopilotAgentRunner>();
        logger = new TestLogger<UserPromptProcessor>();
        processor = new UserPromptProcessor(agentRunner.Object, logger);
    }

    [Test]
    public async Task AnalyzePrompt_ValidJsonResult_ParsesCorrectly()
    {
        var jsonResult = """
        {
          "category": "sdk_generation",
          "prompt_summary": "Generate Python SDK for storage service",
          "language": "Python",
          "package_name": "azure-storage-blob",
          "typespec_project": "specification/storage"
        }
        """;

        agentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonResult);

        var result = await processor.AnalyzePromptAsync("Generate the Python SDK for azure-storage-blob");

        Assert.That(result.Category, Is.EqualTo("sdk_generation"));
        Assert.That(result.PromptSummary, Is.EqualTo("Generate Python SDK for storage service"));
        Assert.That(result.Language, Is.EqualTo("Python"));
        Assert.That(result.PackageName, Is.EqualTo("azure-storage-blob"));
        Assert.That(result.TypeSpecProject, Is.EqualTo("specification/storage"));
        Assert.That(result.IsSuccessful, Is.True);
    }

    [Test]
    public async Task AnalyzePrompt_JsonWithCodeFences_ParsesCorrectly()
    {
        var jsonResult = """
        ```json
        {
          "category": "fix_build_failure",
          "prompt_summary": "Fix .NET SDK build failure",
          "language": ".NET",
          "package_name": null,
          "typespec_project": null
        }
        ```
        """;

        agentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonResult);

        var result = await processor.AnalyzePromptAsync("Fix the build failure in my .NET SDK");

        Assert.That(result.Category, Is.EqualTo("fix_build_failure"));
        Assert.That(result.PromptSummary, Is.EqualTo("Fix .NET SDK build failure"));
        Assert.That(result.Language, Is.EqualTo(".NET"));
    }

    [Test]
    public async Task AnalyzePrompt_InvalidCategory_DefaultsToUnknown()
    {
        var jsonResult = """
        {
          "category": "invalid_category",
          "prompt_summary": "Some prompt",
          "language": null,
          "package_name": null,
          "typespec_project": null
        }
        """;

        agentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonResult);

        var result = await processor.AnalyzePromptAsync("Something");

        Assert.That(result.Category, Is.EqualTo("unknown"));
    }

    [Test]
    public async Task AnalyzePrompt_EmptyPrompt_ReturnsUnknown()
    {
        var result = await processor.AnalyzePromptAsync("");

        Assert.That(result.Category, Is.EqualTo("unknown"));
        Assert.That(result.PromptSummary, Is.EqualTo("Empty prompt"));
        Assert.That(result.IsSuccessful, Is.False);

        agentRunner.Verify(
            r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task AnalyzePrompt_WhitespacePrompt_ReturnsUnknown()
    {
        var result = await processor.AnalyzePromptAsync("   ");

        Assert.That(result.Category, Is.EqualTo("unknown"));
        Assert.That(result.IsSuccessful, Is.False);
        agentRunner.Verify(
            r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task AnalyzePrompt_AgentRunnerThrows_ReturnsGracefulFallback()
    {
        agentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Copilot CLI not available"));

        var result = await processor.AnalyzePromptAsync("Generate SDK");

        Assert.That(result.Category, Is.EqualTo("unknown"));
        Assert.That(result.PromptSummary, Is.EqualTo("Prompt analysis failed"));
        Assert.That(result.IsSuccessful, Is.False);
    }

    [Test]
    public async Task AnalyzePrompt_MalformedJson_ReturnsGracefulFallback()
    {
        agentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("this is not json at all");

        var result = await processor.AnalyzePromptAsync("Generate SDK");

        Assert.That(result.Category, Is.EqualTo("unknown"));
        Assert.That(result.PromptSummary, Is.EqualTo("Failed to parse analysis result"));
        Assert.That(result.IsSuccessful, Is.False);
    }

    [Test]
    public async Task AnalyzePrompt_NullFields_HandledGracefully()
    {
        var jsonResult = """
        {
          "category": "sdk_build_and_test",
          "prompt_summary": "Build and test Java SDK",
          "language": null,
          "package_name": null,
          "typespec_project": null
        }
        """;

        agentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonResult);

        var result = await processor.AnalyzePromptAsync("Build and test my Java SDK");

        Assert.That(result.Category, Is.EqualTo("sdk_build_and_test"));
        Assert.That(result.Language, Is.Null);
        Assert.That(result.PackageName, Is.Null);
        Assert.That(result.TypeSpecProject, Is.Null);
    }

    [Test]
    public async Task AnalyzePrompt_LongSummary_IsTruncated()
    {
        var longSummary = new string('A', 250);
        var jsonResult = $$"""
        {
          "category": "sdk_generation",
          "prompt_summary": "{{longSummary}}",
          "language": null,
          "package_name": null,
          "typespec_project": null
        }
        """;

        agentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonResult);

        var result = await processor.AnalyzePromptAsync("A very long prompt");

        Assert.That(result.PromptSummary!.Length, Is.LessThanOrEqualTo(200));
        Assert.That(result.PromptSummary, Does.EndWith("..."));
    }

    [Test]
    public async Task AnalyzePrompt_AllValidCategories_Accepted()
    {
        var validCategories = new[]
        {
            "typespec_authoring_or_update",
            "typespec_customization",
            "sdk_generation",
            "sdk_build_and_test",
            "release_planning",
            "sdk_release",
            "changelog_and_metadata_update",
            "fix_build_failure",
            "analyze_pipeline_error",
            "sdk_validations"
        };

        foreach (var category in validCategories)
        {
            var jsonResult = $$"""
            {
              "category": "{{category}}",
              "prompt_summary": "Test prompt",
              "language": null,
              "package_name": null,
              "typespec_project": null
            }
            """;

            agentRunner
                .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(jsonResult);

            var result = await processor.AnalyzePromptAsync("Test");
            Assert.That(result.Category, Is.EqualTo(category), $"Category '{category}' should be accepted");
        }
    }
}
