// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Reflection;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

/// <summary>
/// Tests for FeedbackClassifierService batch classification and parsing logic.
/// Includes both mocked unit tests and live integration tests.
/// </summary>
[TestFixture]
public class FeedbackClassifierServiceTests
{
    private Mock<ICopilotAgentRunner> _mockAgentRunner = null!;
    private Mock<ITypeSpecHelper> _mockTypeSpecHelper = null!;
    private ILoggerFactory _loggerFactory = null!;
    private string _testTspPath = null!;
    private string _specRepoRoot = null!;
    private string _typeSpecProjectPath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Path to the test TypeSpec project for live tests
        _typeSpecProjectPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "TypeSpecTestData",
            "specification",
            "testcontoso",
            "Contoso.Management");

        // Create the customization guide file that live tests require
        // The TypeSpecHelper looks for this at <specRepoRoot>/eng/common/knowledge/customizing-client-tsp.md
        var testDataRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "TypeSpecTestData");
        var liveTestGuidePath = Path.Combine(testDataRoot, "eng", "common", "knowledge", "customizing-client-tsp.md");
        Directory.CreateDirectory(Path.GetDirectoryName(liveTestGuidePath)!);
        if (!File.Exists(liveTestGuidePath))
        {
            File.WriteAllText(liveTestGuidePath, "# TypeSpec Client Customizations\nTest reference content for live tests.");
        }
    }

    [SetUp]
    public void Setup()
    {
        _mockAgentRunner = new Mock<ICopilotAgentRunner>();
        _mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        
        // Set up a fake tsp project path for mocked tests
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

    #region Mocked Test Helpers

    private FeedbackClassifierService CreateMockedService()
    {
        return new FeedbackClassifierService(
            _mockAgentRunner.Object,
            _loggerFactory,
            _mockTypeSpecHelper.Object);
    }

    private static FeedbackItem CreateTestItem(string text, string? id = null)
    {
        var item = new FeedbackItem { Text = text };
        if (id != null) item.Id = id;
        return item;
    }

    #endregion

    #region Live Test Helpers

    private static GitHelper CreateRealGitHelper()
    {
        var rawOutputHelper = Mock.Of<IRawOutputHelper>();
        var gitCommandHelper = new GitCommandHelper(
            new TestLogger<GitCommandHelper>(),
            rawOutputHelper);
        return new GitHelper(
            Mock.Of<IGitHubService>(),
            gitCommandHelper,
            new TestLogger<GitHelper>());
    }

    private FeedbackClassifierService CreateRealService()
    {
        var rawOutputHelper = Mock.Of<IRawOutputHelper>();
        var gitHelper = CreateRealGitHelper();
        var typeSpecHelper = new TypeSpecHelper(gitHelper);

        var copilotClient = new CopilotClient(new CopilotClientOptions
        {
            UseStdio = true,
            AutoStart = true
        });
        var copilotClientWrapper = new CopilotClientWrapper(copilotClient);
        var tokenUsageHelper = new TokenUsageHelper(rawOutputHelper);
        var copilotAgentRunner = new CopilotAgentRunner(
            copilotClientWrapper,
            tokenUsageHelper,
            new TestLogger<CopilotAgentRunner>());

        return new FeedbackClassifierService(
            copilotAgentRunner,
            LoggerFactory.Create(builder => builder.AddConsole()),
            typeSpecHelper);
    }

    private static FeedbackItem CreateLiveTestItem(string text, string context = "")
    {
        var id = Guid.NewGuid().ToString();
        return new FeedbackItem
        {
            Id = id,
            Text = text,
            Context = context
        };
    }

    private static void LogClassificationResults(List<FeedbackItem> items)
    {
        TestContext.WriteLine("Classification results:");
        foreach (var item in items)
        {
            TestContext.WriteLine($"  [{item.Id}] Status: {item.Status}, Reason: {item.ClassificationReason}");
        }
    }

    #endregion

    #region ClassifyAsync Flow Tests

    [Test]
    public void ClassifyAsync_EmptyList_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateMockedService();
        var items = new List<FeedbackItem>();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(
            () => service.ClassifyItemsAsync(items, "global context", _testTspPath, language: "python", serviceName: "TestService"));

        _mockAgentRunner.Verify(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ClassifyAsync_AllItemsResolved_ReturnsTrue()
    {
        // Arrange
        var service = CreateMockedService();
        // Non-actionable API review comment -> SUCCESS
        var item1 = CreateTestItem("LGTM, the API surface looks appropriate for this service", "item-1");
        // Requires code-level implementation (no TypeSpec decorator exists for this) -> REQUIRES_MANUAL_INTERVENTION
        var item2 = CreateTestItem("Please add a convenience method that combines getDocument and analyzeDocument into a single call", "item-2");
        var items = new List<FeedbackItem> { item1, item2 };

        // Mock the batch classification response
        // Per FeedbackClassificationTemplate:
        // - SUCCESS: non-actionable (LGTM, keep as is, build succeeding)
        // - REQUIRES_MANUAL_INTERVENTION: actionable but requires code changes (no TypeSpec decorator applies)
        var batchResponse = """
            [item-1]
            Classification: SUCCESS
            Reason: Non-actionable feedback - approval comment with no requested changes

            [item-2]
            Classification: REQUIRES_MANUAL_INTERVENTION
            Reason: Convenience methods combining multiple operations require code-level implementation; no TypeSpec decorator can create new composite operations
            """;
        _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        // Act
        await service.ClassifyItemsAsync(items, "global context", _testTspPath, language: "python", serviceName: "TestService");

        // Assert
        Assert.That(item1.Status, Is.EqualTo(FeedbackStatus.SUCCESS));
        Assert.That(item2.Status, Is.EqualTo(FeedbackStatus.REQUIRES_MANUAL_INTERVENTION));
    }

    [Test]
    public async Task ClassifyAsync_ItemsStillTspApplicable_ReturnsFalse()
    {
        // Arrange
        var service = CreateMockedService();
        // API review feedback requesting a rename - addressable via @clientName decorator -> TSP_APPLICABLE
        var item1 = CreateTestItem("Rename 'DocumentIntelligenceClient' to 'DocumentAnalysisClient' for consistency with other Azure AI services", "item-1");
        var items = new List<FeedbackItem> { item1 };

        // Mock response with TSP_APPLICABLE classification
        // Per FeedbackClassificationTemplate:
        // - TSP_APPLICABLE: actionable AND TypeSpec decorators can address it
        var batchResponse = """
            [item-1]
            Classification: TSP_APPLICABLE
            Reason: Can use @@clientName(DocumentIntelligenceClient, "DocumentAnalysisClient") to rename the client
            """;
        _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        // Act
        await service.ClassifyItemsAsync(items, "global context", _testTspPath, language: "python", serviceName: "TestService");

        // Assert
        Assert.That(item1.Status, Is.EqualTo(FeedbackStatus.TSP_APPLICABLE));
    }

    /// <summary>
    /// Tests that REQUIRES_MANUAL_INTERVENTION classification is applied correctly.
    /// Note: NextAction guidance generation was removed - REQUIRES_MANUAL_INTERVENTION items are just classified.
    /// </summary>
    [Test]
    public async Task ClassifyAsync_RequiresManualInterventionItems_ClassifiedCorrectly()
    {
        // Arrange
        var service = CreateMockedService();
        // API review feedback requesting streaming support - requires code-level changes -> REQUIRES_MANUAL_INTERVENTION
        var item1 = CreateTestItem("Add support for streaming responses with progress callbacks when uploading large documents", "item-1");
        var items = new List<FeedbackItem> { item1 };

        _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                [item-1]
                Classification: REQUIRES_MANUAL_INTERVENTION
                Reason: Streaming support with progress callbacks requires code-level implementation; no TypeSpec decorator can add streaming behavior
                """);

        // Act
        await service.ClassifyItemsAsync(items, "global context", _testTspPath, language: "python", serviceName: "TestService");

        // Assert
        Assert.That(item1.Status, Is.EqualTo(FeedbackStatus.REQUIRES_MANUAL_INTERVENTION));
        Assert.That(item1.ClassificationReason, Does.Contain("streaming").IgnoreCase);
    }

    #endregion

    #region Parsing and Edge Case Tests

    [Test]
    public async Task ClassifyAsync_ValidFormat_UpdatesReasonAndContext()
    {
        // Arrange
        var service = CreateMockedService();
        // Non-actionable API review comment -> SUCCESS
        var item = CreateTestItem("The error handling looks good, approved", "item-abc");
        var items = new List<FeedbackItem> { item };

        var batchResponse = """
            [item-abc]
            Classification: SUCCESS
            Reason: Non-actionable feedback - approval comment with no requested changes
            """;
        _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        // Act
        await service.ClassifyItemsAsync(items, "global context", _testTspPath, language: "python", serviceName: "TestService");

        // Assert
        Assert.That(item.ClassificationReason, Is.EqualTo("Non-actionable feedback - approval comment with no requested changes"));
        Assert.That(item.Context, Does.Contain("Classification: SUCCESS"));
        Assert.That(item.Context, Does.Contain("Reason: Non-actionable feedback - approval comment with no requested changes"));
    }

    [Test]
    public async Task ClassifyAsync_MissingItemInResponse_DefaultsToRequiresManualIntervention()
    {
        // Arrange
        var service = CreateMockedService();
        // Non-actionable -> SUCCESS
        var item1 = CreateTestItem("Looks good to me", "item-1");
        // Actionable -> would be TSP_APPLICABLE, but response is missing
        var item2 = CreateTestItem("Make the 'InternalMetrics' operation internal so it's not exposed publicly", "item-2");
        var items = new List<FeedbackItem> { item1, item2 };

        // Response only contains item-1, missing item-2
        var batchResponse = """
            [item-1]
            Classification: SUCCESS
            Reason: Non-actionable feedback - approval
            """;
        _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        // Act
        await service.ClassifyItemsAsync(items, "global context", _testTspPath, language: "python", serviceName: "TestService");

        // Assert
        Assert.That(item1.Status, Is.EqualTo(FeedbackStatus.SUCCESS));
        Assert.That(item2.Status, Is.EqualTo(FeedbackStatus.REQUIRES_MANUAL_INTERVENTION), "Missing items should default to REQUIRES_MANUAL_INTERVENTION");
        Assert.That(item2.Context, Does.Contain("missing from batch LLM response"));
    }

    #endregion

    #region Caching Tests

    [Test]
    public async Task LoadTspCustomizationGuideAsync_CachesContentAfterFirstRead()
    {
        // Arrange
        var service = CreateMockedService();
        var guidePath = Path.Combine(_specRepoRoot, "eng", "common", "knowledge", "customizing-client-tsp.md");
        var loadMethod = typeof(FeedbackClassifierService).GetMethod(
            "LoadTspCustomizationGuideAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(loadMethod, Is.Not.Null, "Expected private LoadTspCustomizationGuideAsync method to exist.");

        // Act - first load should read from disk
        var firstLoadTask = (Task<string>)loadMethod!.Invoke(service, [_specRepoRoot, CancellationToken.None])!;
        var firstContent = await firstLoadTask;

        // Delete file to verify second load comes from in-memory cache, not disk
        File.Delete(guidePath);

        var secondLoadTask = (Task<string>)loadMethod.Invoke(service, [_specRepoRoot, CancellationToken.None])!;
        var secondContent = await secondLoadTask;

        // Assert
        Assert.That(secondContent, Is.EqualTo(firstContent));
        Assert.That(secondContent, Does.Contain("TypeSpec Client Customizations"));
    }

    #endregion

    #region Live Integration Tests

    /// <summary>
    /// Live integration test that classifies a comprehensive mix of API review feedback.
    /// Tests all three classification categories:
    /// - TSP_APPLICABLE: @clientName (renames), @access (visibility)
    /// - SUCCESS: Non-actionable (LGTM, keep as is, approvals)
    /// - REQUIRES_MANUAL_INTERVENTION: Code-level changes (custom retry, serialization)
    /// </summary>
    [Test]
    public async Task Live_ClassifyAsync_AllFeedbackCategories_ClassifiesCorrectly()
    {
        if (!await CopilotTestHelper.IsCopilotAvailableAsync())
        {
            Assert.Ignore("Skipping test as GitHub Copilot CLI is either not installed or not authenticated.");
        }

        var service = CreateRealService();

        var items = new List<FeedbackItem>
        {
            // TSP_APPLICABLE: @clientName for renames
            CreateLiveTestItem("Rename 'EmployeeClient' to 'StaffClient' for consistency with the service branding"),
            // TSP_APPLICABLE: @access for visibility
            CreateLiveTestItem("Make the 'PurgeDocuments' operation internal - it should not be exposed in the public SDK"),
            // SUCCESS: Non-actionable approval
            CreateLiveTestItem("LGTM - the pagination follows Azure SDK guidelines"),
            // SUCCESS: Keep as is
            CreateLiveTestItem("No changes needed - the model hierarchy follows Azure best practices"),
            // REQUIRES_MANUAL_INTERVENTION: Requires code-level implementation
            CreateLiveTestItem("Add support for custom retry policies with circuit breaker pattern for transient failures")
        };

        await service.ClassifyItemsAsync(
            items,
            globalContext: "Testing comprehensive feedback classification",
            tspProjectPath: _typeSpecProjectPath,
            language: "python",
            serviceName: "TestService");

        LogClassificationResults(items);

        foreach (var item in items)
        {
            Assert.That(item.ClassificationReason, Is.Not.Null.And.Not.Empty,
                $"Item '{item.Text}' should have a classification reason");
        }

        // TSP_APPLICABLE assertions - check status and that reason mentions the appropriate decorator
        Assert.That(items[0].Status, Is.EqualTo(FeedbackStatus.TSP_APPLICABLE),
            "Client rename should be TSP_APPLICABLE");
        Assert.That(items[0].ClassificationReason, Does.Contain("clientName").IgnoreCase,
            "Rename reason should mention @clientName decorator");

        Assert.That(items[1].Status, Is.EqualTo(FeedbackStatus.TSP_APPLICABLE),
            "Making operation internal should be TSP_APPLICABLE");
        Assert.That(items[1].ClassificationReason, Does.Contain("access").IgnoreCase,
            "Visibility change reason should mention @access decorator");

        // SUCCESS assertions - just check status (reason existence already verified above)
        Assert.That(items[2].Status, Is.EqualTo(FeedbackStatus.SUCCESS),
            "LGTM approval should be SUCCESS - non-actionable");
        Assert.That(items[3].Status, Is.EqualTo(FeedbackStatus.SUCCESS),
            "'No changes needed' should be SUCCESS - non-actionable");

        // REQUIRES_MANUAL_INTERVENTION assertion
        Assert.That(items[4].Status, Is.EqualTo(FeedbackStatus.REQUIRES_MANUAL_INTERVENTION),
            "Custom retry with circuit breaker requires code implementation -> REQUIRES_MANUAL_INTERVENTION");
    }

    /// <summary>
    /// Live test: Verifies items with prior compilation error context are classified as REQUIRES_MANUAL_INTERVENTION.
    /// This represents a retry scenario where previous TypeSpec customization attempt failed.
    /// </summary>
    [Test]
    public async Task Live_ClassifyAsync_WithErrorContext_ClassifiedAsRequiresManualIntervention()
    {
        if (!await CopilotTestHelper.IsCopilotAvailableAsync())
        {
            Assert.Ignore("Skipping test as GitHub Copilot CLI is either not installed or not authenticated.");
        }

        var service = CreateRealService();

        // Represents a retry scenario: originally TSP_APPLICABLE but failed compilation
        var id = Guid.NewGuid().ToString();
        var items = new List<FeedbackItem>
        {
            new FeedbackItem
            {
                Id = id,
                Text = "Rename the 'Items' property to 'Documents' for consistency",
                Context = "COMPILATION ERROR: @@clientName target not found - property 'Items' does not exist in model 'ListResponse'"
            }
        };

        await service.ClassifyItemsAsync(
            items,
            globalContext: $"--- TYPESPEC CUSTOMIZATIONS ---\nId: {id}\nText: Rename the 'Items' property to 'Documents'\nContext: COMPILATION ERROR: @@clientName target not found - property 'Items' does not exist in model 'ListResponse'",
            tspProjectPath: _typeSpecProjectPath,
            language: "python",
            serviceName: "TestService");

        LogClassificationResults(items);

        Assert.That(items[0].Status, Is.EqualTo(FeedbackStatus.REQUIRES_MANUAL_INTERVENTION),
            "Items with prior compilation errors should be classified as REQUIRES_MANUAL_INTERVENTION - indicates TypeSpec cannot address this");
    }

    /// <summary>
    /// Live test: Verifies REQUIRES_MANUAL_INTERVENTION items are classified correctly.
    /// Note: NextAction guidance generation was removed.
    /// </summary>
    [Test]
    public async Task Live_ClassifyAsync_RequiresManualInterventionItems_ClassifiedCorrectly()
    {
        if (!await CopilotTestHelper.IsCopilotAvailableAsync())
        {
            Assert.Ignore("Skipping test as GitHub Copilot CLI is either not installed or not authenticated.");
        }

        var service = CreateRealService();

        // Request that requires code-level SDK implementation - no TypeSpec decorator can address
        var items = new List<FeedbackItem>
        {
            CreateLiveTestItem("Add support for streaming responses with progress callbacks when downloading large blobs")
        };

        await service.ClassifyItemsAsync(
            items,
            globalContext: "Testing classification for code-level changes",
            tspProjectPath: _typeSpecProjectPath,
            language: "python",
            serviceName: "StorageBlob");

        LogClassificationResults(items);

        Assert.That(items[0].Status, Is.EqualTo(FeedbackStatus.REQUIRES_MANUAL_INTERVENTION),
            "Streaming with progress callbacks requires code implementation -> REQUIRES_MANUAL_INTERVENTION");
    }

    #endregion
}
