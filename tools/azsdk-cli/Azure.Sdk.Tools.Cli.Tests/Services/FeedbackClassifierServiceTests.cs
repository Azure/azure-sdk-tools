// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

/// <summary>
/// Tests for FeedbackClassifierService batch classification and parsing logic.
/// Uses mocked ICopilotAgentRunner to control LLM responses and verify parsing behavior.
/// </summary>
[TestFixture]
public class FeedbackClassifierServiceTests
{
    private Mock<ICopilotAgentRunner> _mockAgentRunner = null!;
    private Mock<ITypeSpecHelper> _mockTypeSpecHelper = null!;
    private ILoggerFactory _loggerFactory = null!;
    private string _testTspPath = null!;
    private string _specRepoRoot = null!;

    [SetUp]
    public void Setup()
    {
        _mockAgentRunner = new Mock<ICopilotAgentRunner>();
        _mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        
        // Set up a fake tsp project path
        _specRepoRoot = Path.Combine(Path.GetTempPath(), "test-spec-repo-" + Guid.NewGuid().ToString("N")[..8]);
        _testTspPath = Path.Combine(_specRepoRoot, "specification", "widget", "Widget.Management");
        
        // Mock the spec repo root detection
        _mockTypeSpecHelper.Setup(x => x.GetSpecRepoRootPath(_testTspPath)).Returns(_specRepoRoot);
        
        // Create the customization guide file that the service expects
        var guidePath = Path.Combine(_specRepoRoot, "eng", "common", "knowledge", "customizing-client-tsp.md");
        Directory.CreateDirectory(Path.GetDirectoryName(guidePath)!);
        File.WriteAllText(guidePath, "# TypeSpec Client Customizations\nTest reference content.");
    }

    [TearDown]
    public void TearDown()
    {
        _loggerFactory?.Dispose();
        
        // Clean up temp files
        if (Directory.Exists(_specRepoRoot))
        {
            try { Directory.Delete(_specRepoRoot, recursive: true); } catch { }
        }
    }

    private FeedbackClassifierService CreateService(string? packagePath = null)
    {
        return new FeedbackClassifierService(
            _mockAgentRunner.Object,
            _loggerFactory,
            _mockTypeSpecHelper.Object,
            _testTspPath,
            packagePath);
    }

    private static FeedbackItem CreateTestItem(string text, string? id = null)
    {
        var item = new FeedbackItem { Text = text };
        if (id != null) item.Id = id;
        item.FormattedPrompt = $"Text: {text}\nContext: ";
        return item;
    }

    #region ClassifyAsync Flow Tests

    [Test]
    public async Task ClassifyAsync_EmptyList_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        var items = new List<FeedbackItem>();

        // Act
        var result = await service.ClassifyAsync(items, "global context");

        // Assert
        Assert.That(result, Is.True);
        _mockAgentRunner.Verify(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ClassifyAsync_AllItemsResolved_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        var item1 = CreateTestItem("Rename FooClient", "item-1");
        var item2 = CreateTestItem("Keep as is", "item-2");
        var items = new List<FeedbackItem> { item1, item2 };

        // Mock the batch classification response
        var batchResponse = """
            [item-1]
            Classification: SUCCESS
            Reason: Rename completed successfully

            [item-2]
            Classification: FAILURE
            Reason: Requires code changes
            """;
        _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        // Act
        var result = await service.ClassifyAsync(items, "global context");

        // Assert
        Assert.That(result, Is.True, "All items resolved (SUCCESS or FAILURE) should return true");
        Assert.That(item1.Status, Is.EqualTo(FeedbackStatus.SUCCESS));
        Assert.That(item2.Status, Is.EqualTo(FeedbackStatus.FAILURE));
    }

    [Test]
    public async Task ClassifyAsync_ItemsStillTspApplicable_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var item1 = CreateTestItem("Rename FooClient", "item-1");
        var items = new List<FeedbackItem> { item1 };

        // Mock response with TSP_APPLICABLE classification
        var batchResponse = """
            [item-1]
            Classification: TSP_APPLICABLE
            Reason: Can use @@clientName decorator
            """;
        _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        // Act
        var result = await service.ClassifyAsync(items, "global context");

        // Assert
        Assert.That(result, Is.False, "Items still TSP_APPLICABLE should return false");
        Assert.That(item1.Status, Is.EqualTo(FeedbackStatus.TSP_APPLICABLE));
    }

    [Test]
    public async Task ClassifyAsync_FailureItems_GeneratesNextAction()
    {
        // Arrange
        var service = CreateService();
        var item1 = CreateTestItem("Need custom serialization", "item-1");
        var items = new List<FeedbackItem> { item1 };

        // First call returns batch classification
        // Second call returns guidance for FAILURE item
        var callCount = 0;
        _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return """
                        [item-1]
                        Classification: FAILURE
                        Reason: Custom serialization requires code changes
                        """;
                }
                return "Add custom serializer in _serialization.py following the pattern in the documentation.";
            });

        // Act
        var result = await service.ClassifyAsync(items, "global context", language: "python");

        // Assert
        Assert.That(item1.Status, Is.EqualTo(FeedbackStatus.FAILURE));
        Assert.That(item1.NextAction, Is.Not.Null.And.Not.Empty);
        Assert.That(item1.NextAction, Does.Contain("serializer").Or.Contains("serialization"));
    }

    #endregion

    #region Parsing and Classification Tests

    [Test]
    [TestCase("SUCCESS", FeedbackStatus.SUCCESS)]
    [TestCase("FAILURE", FeedbackStatus.FAILURE)]
    [TestCase("TSP_APPLICABLE", FeedbackStatus.TSP_APPLICABLE)]
    public async Task ClassifyAsync_ClassificationMapping_MapsCorrectly(string classification, FeedbackStatus expectedStatus)
    {
        // Arrange
        var service = CreateService();
        var item = CreateTestItem("Test feedback", "test-id");
        var items = new List<FeedbackItem> { item };

        var batchResponse = $"""
            [test-id]
            Classification: {classification}
            Reason: Test reason
            """;
        _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        // Act
        await service.ClassifyAsync(items, "global context");

        // Assert
        Assert.That(item.Status, Is.EqualTo(expectedStatus));
    }

    [Test]
    public async Task ClassifyAsync_ValidFormat_UpdatesReasonAndContext()
    {
        // Arrange
        var service = CreateService();
        var item = CreateTestItem("Rename method", "item-abc");
        var items = new List<FeedbackItem> { item };

        var batchResponse = """
            [item-abc]
            Classification: SUCCESS
            Reason: Method was renamed using @@clientName decorator
            """;
        _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        // Act
        await service.ClassifyAsync(items, "global context");

        // Assert
        Assert.That(item.Reason, Is.EqualTo("Method was renamed using @@clientName decorator"));
        Assert.That(item.Context, Does.Contain("Classification: SUCCESS"));
        Assert.That(item.Context, Does.Contain("Reason: Method was renamed using @@clientName decorator"));
    }

    [Test]
    public async Task ClassifyAsync_MissingItemInResponse_DefaultsToFailure()
    {
        // Arrange
        var service = CreateService();
        var item1 = CreateTestItem("First item", "item-1");
        var item2 = CreateTestItem("Second item", "item-2");
        var items = new List<FeedbackItem> { item1, item2 };

        // Response only contains item-1, missing item-2
        var batchResponse = """
            [item-1]
            Classification: SUCCESS
            Reason: Completed
            """;
        _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        // Act
        await service.ClassifyAsync(items, "global context");

        // Assert
        Assert.That(item1.Status, Is.EqualTo(FeedbackStatus.SUCCESS));
        Assert.That(item2.Status, Is.EqualTo(FeedbackStatus.FAILURE), "Missing items should default to FAILURE");
        Assert.That(item2.Context, Does.Contain("missing from batch LLM response"));
    }

    [Test]
    public async Task ClassifyAsync_MultipleItems_AllParsedCorrectly()
    {
        // Arrange
        var service = CreateService();
        var items = new List<FeedbackItem>
        {
            CreateTestItem("Rename client", "id-1"),
            CreateTestItem("Keep as is", "id-2"),
            CreateTestItem("Needs code change", "id-3")
        };

        var batchResponse = """
            [id-1]
            Classification: TSP_APPLICABLE
            Reason: Use @@clientName

            [id-2]
            Classification: SUCCESS
            Reason: No action needed

            [id-3]
            Classification: FAILURE
            Reason: Requires custom implementation
            """;
        _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        // Act
        await service.ClassifyAsync(items, "global context");

        // Assert
        Assert.That(items[0].Status, Is.EqualTo(FeedbackStatus.TSP_APPLICABLE));
        Assert.That(items[0].Reason, Is.EqualTo("Use @@clientName"));
        
        Assert.That(items[1].Status, Is.EqualTo(FeedbackStatus.SUCCESS));
        Assert.That(items[1].Reason, Is.EqualTo("No action needed"));
        
        Assert.That(items[2].Status, Is.EqualTo(FeedbackStatus.FAILURE));
        Assert.That(items[2].Reason, Is.EqualTo("Requires custom implementation"));
    }

    #endregion
}
