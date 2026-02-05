// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.TypeSpec;

[TestFixture]
public class DelegateApiViewFeedbackToolTests
{
    private DelegateApiViewFeedbackTool _tool = null!;
    private Mock<IAPIViewFeedbackHelpers> _mockHelper = null!;
    private Mock<IGitHubService> _mockGitHubService = null!;
    private TestLogger<DelegateApiViewFeedbackTool> _logger = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<DelegateApiViewFeedbackTool>();
        _mockHelper = new Mock<IAPIViewFeedbackHelpers>();
        _mockGitHubService = new Mock<IGitHubService>();
        _tool = new DelegateApiViewFeedbackTool(_mockHelper.Object, _mockGitHubService.Object, _logger);
    }

    #region No Comments Tests

    [Test]
    public async Task DelegateApiViewFeedbackAsync_WithNoComments_ReturnsNoActionableComments()
    {
        var apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=abc";
        var metadata = CreateMetadataWithPullRequest(prNumber: 123, prRepo: "Azure/azure-rest-api-specs");
        
        _mockHelper.Setup(x => x.GetConsolidatedComments(apiViewUrl)).ReturnsAsync(new List<ConsolidatedComment>());
        _mockHelper.Setup(x => x.GetMetadata(apiViewUrl)).ReturnsAsync(metadata);

        // Not using dry run - should still not create issue when no comments
        var response = await _tool.DelegateApiViewFeedbackAsync(apiViewUrl, dryRun: false);

        Assert.That(response.Message, Does.Contain("No actionable comments"));
        _mockGitHubService.Verify(x => x.CreateIssueAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()), Times.Never);
    }

    #endregion

    #region PR-based SHA Detection Tests

    [Test]
    public async Task DelegateApiViewFeedbackAsync_WithSpecsRepoPullRequest_DetectsCommitShaAndDirectory()
    {
        var apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=abc";
        var comments = CreateSampleComments();
        var metadata = CreateMetadataWithPullRequest(prNumber: 12345, prRepo: "Azure/azure-rest-api-specs");

        _mockHelper.Setup(x => x.GetConsolidatedComments(apiViewUrl)).ReturnsAsync(comments);
        _mockHelper.Setup(x => x.GetMetadata(apiViewUrl)).ReturnsAsync(metadata);
        _mockHelper.Setup(x => x.DetectShaAndTspPath(metadata))
            .ReturnsAsync(("abc123sha", "specification/widget/Widget", "Azure/azure-rest-api-specs"));

        var response = await _tool.DelegateApiViewFeedbackAsync(apiViewUrl, dryRun: true);

        Assert.That(response.Message, Does.Contain("DRY RUN"));
        Assert.That(response.Message, Does.Contain("abc123sha"));
        Assert.That(response.Message, Does.Contain("specification/widget/Widget"));
        Assert.That(response.Message, Does.Contain("Target: Azure/azure-rest-api-specs"));
        
        _mockHelper.Verify(x => x.DetectShaAndTspPath(metadata), Times.Once);
    }

    [Test]
    public async Task DelegateApiViewFeedbackAsync_WithSdkRepoPullRequest_GetsRepoFromTspLocation()
    {
        // PR is in azure-sdk-for-python, tsp-location.yaml points to specs repo
        var apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=abc";
        var comments = CreateSampleComments();
        var metadata = CreateMetadataWithPullRequest(prNumber: 99999, prRepo: "Azure/azure-sdk-for-python");

        _mockHelper.Setup(x => x.GetConsolidatedComments(apiViewUrl)).ReturnsAsync(comments);
        _mockHelper.Setup(x => x.GetMetadata(apiViewUrl)).ReturnsAsync(metadata);
        // Helper detects SHA, directory, and repo from tsp-location.yaml in SDK PR
        _mockHelper.Setup(x => x.DetectShaAndTspPath(metadata))
            .ReturnsAsync(("sdksha456", "specification/widget/Widget", "Azure/azure-rest-api-specs"));

        var response = await _tool.DelegateApiViewFeedbackAsync(apiViewUrl, dryRun: true);

        Assert.That(response.Message, Does.Contain("DRY RUN"));
        Assert.That(response.Message, Does.Contain("sdksha456"));
        Assert.That(response.Message, Does.Contain("specification/widget/Widget"));
        // Target repo should be derived from tsp-location.yaml
        Assert.That(response.Message, Does.Contain("Target: Azure/azure-rest-api-specs"));
    }

    #endregion

    #region Branch-based SHA Detection Tests

    [Test]
    public async Task DelegateApiViewFeedbackAsync_WithBranchLabel_DetectsCommitShaAndDirectory()
    {
        // RevisionLabel contains branch name that can be used to detect SHA
        var apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=abc";
        var comments = CreateSampleComments();
        var metadata = CreateMetadataWithBranch(branchLabel: "feature/add-widget-api");

        _mockHelper.Setup(x => x.GetConsolidatedComments(apiViewUrl)).ReturnsAsync(comments);
        _mockHelper.Setup(x => x.GetMetadata(apiViewUrl)).ReturnsAsync(metadata);
        _mockHelper.Setup(x => x.DetectShaAndTspPath(metadata))
            .ReturnsAsync(("branchsha456", "specification/widget/Widget.Management", "Azure/azure-rest-api-specs"));

        var response = await _tool.DelegateApiViewFeedbackAsync(apiViewUrl, dryRun: true);

        Assert.That(response.Message, Does.Contain("branchsha456"));
        Assert.That(response.Message, Does.Contain("specification/widget/Widget.Management"));
        Assert.That(response.Message, Does.Contain("Target: Azure/azure-rest-api-specs"));
    }

    [Test]
    public async Task DelegateApiViewFeedbackAsync_WithBranchLabel_NoShaDetected_FallsBackToDefault()
    {
        // Branch doesn't exist or can't find SHA - should still work with fallback
        var apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=abc";
        var comments = CreateSampleComments();
        var metadata = CreateMetadataWithBranch(branchLabel: "nonexistent-branch");

        _mockHelper.Setup(x => x.GetConsolidatedComments(apiViewUrl)).ReturnsAsync(comments);
        _mockHelper.Setup(x => x.GetMetadata(apiViewUrl)).ReturnsAsync(metadata);
        _mockHelper.Setup(x => x.DetectShaAndTspPath(metadata))
            .ReturnsAsync((null, null, null));

        var response = await _tool.DelegateApiViewFeedbackAsync(apiViewUrl, dryRun: true);

        Assert.That(response.Message, Does.Contain("DRY RUN"));
        Assert.That(response.Message, Does.Not.Contain("**Commit SHA**"));
        // Falls back to default repo
        Assert.That(response.Message, Does.Contain("Target: Azure/azure-rest-api-specs"));
    }

    #endregion

    #region Multiple Comments Tests

    [Test]
    public async Task DelegateApiViewFeedbackAsync_WithMultipleCommentsInThread_ConsolidatesCorrectly()
    {
        var apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=abc";
        // Multiple comments in a single thread - simulates back-and-forth discussion
        var comments = new List<ConsolidatedComment>
        {
            new ConsolidatedComment
            {
                ThreadId = "thread-1",
                LineNo = 100,
                LineId = "azure.test.package.models.TestModel",
                LineText = "class TestModel:",
                Comment = "Please rename this to FooModel for consistency\n\n---\n\nI agree, FooModel is better\n\n---\n\nActually, let's use BarModel instead"
            }
        };
        var metadata = CreateMetadataWithPullRequest(prNumber: 123, prRepo: "Azure/azure-rest-api-specs");

        _mockHelper.Setup(x => x.GetConsolidatedComments(apiViewUrl)).ReturnsAsync(comments);
        _mockHelper.Setup(x => x.GetMetadata(apiViewUrl)).ReturnsAsync(metadata);
        _mockHelper.Setup(x => x.DetectShaAndTspPath(metadata))
            .ReturnsAsync(("sha123", null, "Azure/azure-rest-api-specs"));

        var response = await _tool.DelegateApiViewFeedbackAsync(apiViewUrl, dryRun: true);

        // Verify the consolidated comment appears in the output
        Assert.That(response.Message, Does.Contain("FooModel"));
        Assert.That(response.Message, Does.Contain("BarModel"));
        // Should show as 1 unresolved comment (thread count, not individual messages)
        Assert.That(response.Message, Does.Contain("1 unresolved"));
    }

    #endregion

    #region Issue Creation Tests

    [Test]
    public async Task DelegateApiViewFeedbackAsync_WithDryRun_DoesNotCreateIssue()
    {
        var apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=abc";
        var comments = CreateSampleComments();
        var metadata = CreateMetadataWithPullRequest(prNumber: 123, prRepo: "Azure/azure-rest-api-specs");

        _mockHelper.Setup(x => x.GetConsolidatedComments(apiViewUrl)).ReturnsAsync(comments);
        _mockHelper.Setup(x => x.GetMetadata(apiViewUrl)).ReturnsAsync(metadata);
        _mockHelper.Setup(x => x.DetectShaAndTspPath(metadata))
            .ReturnsAsync(("sha123", null, "Azure/azure-rest-api-specs"));

        var response = await _tool.DelegateApiViewFeedbackAsync(apiViewUrl, dryRun: true);

        _mockGitHubService.Verify(x => x.CreateIssueAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()), Times.Never);
    }

    [Test]
    public async Task DelegateApiViewFeedbackAsync_CreatesIssue_WithCorrectTitleAndBody()
    {
        var apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=abc";
        var comments = CreateSampleComments();
        var metadata = CreateMetadataWithPullRequest(prNumber: 555, prRepo: "Azure/azure-rest-api-specs");

        _mockHelper.Setup(x => x.GetConsolidatedComments(apiViewUrl)).ReturnsAsync(comments);
        _mockHelper.Setup(x => x.GetMetadata(apiViewUrl)).ReturnsAsync(metadata);
        _mockHelper.Setup(x => x.DetectShaAndTspPath(metadata))
            .ReturnsAsync(("commitsha123", "specification/widget/Widget", "Azure/azure-rest-api-specs"));

        var mockIssue = CreateMockIssue(42, "https://github.com/Azure/azure-rest-api-specs/issues/42");
        _mockGitHubService.Setup(x => x.CreateIssueAsync(
            "Azure", 
            "azure-rest-api-specs", 
            It.Is<string>(title => title.Contains("Address APIView feedback") && title.Contains("azure-test-package")),
            It.Is<string>(body => 
                body.Contains("**Package Name**: azure-test-package") &&
                body.Contains("**Language**: Python") &&
                body.Contains("**Commit SHA**: commitsha123") &&
                body.Contains("## Feedback to Address") &&
                body.Contains("## Constraints") &&
                body.Contains("## Output Requirements") &&
                body.Contains("| LineNo | Element | LineText | CommentText |")),
            It.IsAny<List<string>?>()))
            .ReturnsAsync(mockIssue);

        var response = await _tool.DelegateApiViewFeedbackAsync(apiViewUrl, dryRun: false);

        Assert.That(response.Message, Does.Contain("Issue created"));
        Assert.That(response.Message, Does.Contain("https://github.com/Azure/azure-rest-api-specs/issues/42"));
        
        _mockGitHubService.Verify(x => x.CreateIssueAsync(
            "Azure", "azure-rest-api-specs", It.IsAny<string>(), It.IsAny<string>(), 
            It.Is<List<string>>(a => a != null && a.Count == 1 && a[0] == "copilot-swe-agent[bot]")), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task DelegateApiViewFeedbackAsync_WhenHelperThrowsException_ReturnsError()
    {
        var apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=abc";
        _mockHelper.Setup(x => x.GetConsolidatedComments(apiViewUrl))
            .ThrowsAsync(new InvalidOperationException("APIView service unavailable"));

        var response = await _tool.DelegateApiViewFeedbackAsync(apiViewUrl);

        Assert.That(response.Message, Does.Contain("Error:"));
        Assert.That(response.Message, Does.Contain("APIView service unavailable"));
    }

    [Test]
    public async Task DelegateApiViewFeedbackAsync_WhenGitHubServiceThrowsException_ReturnsError()
    {
        var apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=abc";
        var comments = CreateSampleComments();
        var metadata = CreateMetadataWithPullRequest(prNumber: 123, prRepo: "Azure/azure-rest-api-specs");

        _mockHelper.Setup(x => x.GetConsolidatedComments(apiViewUrl)).ReturnsAsync(comments);
        _mockHelper.Setup(x => x.GetMetadata(apiViewUrl)).ReturnsAsync(metadata);
        _mockHelper.Setup(x => x.DetectShaAndTspPath(metadata))
            .ReturnsAsync(("sha123", null, "Azure/azure-rest-api-specs"));
        _mockGitHubService.Setup(x => x.CreateIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()))
            .ThrowsAsync(new Octokit.ApiException("GitHub API rate limit exceeded", System.Net.HttpStatusCode.Forbidden));

        var response = await _tool.DelegateApiViewFeedbackAsync(apiViewUrl, dryRun: false);

        Assert.That(response.Message, Does.Contain("Error:"));
    }

    #endregion

    #region Helper Methods

    private List<ConsolidatedComment> CreateSampleComments()
    {
        return new List<ConsolidatedComment>
        {
            new ConsolidatedComment
            {
                ThreadId = "thread-1",
                LineNo = 100,
                LineId = "azure.test.package.models.TestModel",
                LineText = "class TestModel:",
                Comment = "Please rename this to FooModel for consistency"
            },
            new ConsolidatedComment
            {
                ThreadId = "thread-2",
                LineNo = 200,
                LineId = "azure.test.package.operations.TestOperations.get_item",
                LineText = "def get_item(self, item_id: str) -> Item:",
                Comment = "Consider adding pagination support"
            }
        };
    }

    private ReviewMetadata CreateMetadataWithPullRequest(int prNumber, string prRepo)
    {
        return new ReviewMetadata
        {
            ReviewId = "review-123",
            PackageName = "azure-test-package",
            Language = "Python",
            Revision = new RevisionMetadata
            {
                RevisionId = "abc",
                PullRequestNo = prNumber,
                PullRequestRepository = prRepo,
                RevisionLabel = null
            }
        };
    }

    private ReviewMetadata CreateMetadataWithBranch(string branchLabel)
    {
        return new ReviewMetadata
        {
            ReviewId = "review-123",
            PackageName = "azure-test-package",
            Language = "Python",
            Revision = new RevisionMetadata
            {
                RevisionId = "abc",
                PullRequestNo = null,
                PullRequestRepository = null,
                RevisionLabel = branchLabel
            }
        };
    }

    private Issue CreateMockIssue(int number, string htmlUrl)
    {
        return new Issue(
            url: $"https://api.github.com/repos/Azure/azure-rest-api-specs/issues/{number}",
            htmlUrl: htmlUrl,
            commentsUrl: "",
            eventsUrl: "",
            number: number,
            state: ItemState.Open,
            title: "Test Issue",
            body: "Test body",
            closedBy: null,
            user: null,
            labels: new List<Label>(),
            assignee: null,
            assignees: new List<User>(),
            milestone: null,
            comments: 0,
            pullRequest: null,
            closedAt: null,
            createdAt: DateTimeOffset.Now,
            updatedAt: null,
            id: number,
            nodeId: $"node-{number}",
            locked: false,
            repository: null,
            reactions: null,
            activeLockReason: null,
            stateReason: null
        );
    }

    #endregion
}
