// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan;

public class ReleasePlanAbandonedPullRequestNoteTests
{
    private Mock<IDevOpsService> _devOps = null!;
    private Mock<IGitHubService> _github = null!;
    private Mock<INotificationService> _notifications = null!;
    private ReleasePlanTool _tool = null!;
    private ReleasePlanWorkItem _plan = null!;

    [SetUp]
    public void Setup()
    {
        _devOps = new(MockBehavior.Strict);
        _github = new(MockBehavior.Strict);
        _notifications = new(MockBehavior.Strict);
        _plan = new ReleasePlanWorkItem
        {
            WorkItemId = 42, ReleasePlanId = 123, Revision = 7, Status = "In Progress",
            SDKInfo = [new SDKInfo { Language = "Python", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/1" }]
        };
        _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(() => _plan);
        _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItem { Id = 42, Fields = new Dictionary<string, object> { ["System.State"] = "Abandoned" } });
        _github.Setup(s => s.GetPullRequestAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr());
        _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<IssueComment>());
        _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueComment());
        _tool = new ReleasePlanTool(_devOps.Object, Mock.Of<IGitHelper>(), Mock.Of<ITypeSpecHelper>(),
            new TestLogger<ReleasePlanTool>(), Mock.Of<IUserHelper>(), _github.Object, Mock.Of<IEnvironmentHelper>(),
            new InputSanitizer(), new HttpClient(), Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), _notifications.Object);
    }

    private static PullRequest Pr(string? author = "azure-sdk-automation[bot]", string state = "open", bool merged = false, bool draft = false) =>
        new Octokit.Internal.SimpleJsonSerializer().Deserialize<PullRequest>(JsonSerializer.Serialize(new
        {
            state, merged, draft, title = "[AutoPR] Do not use title as identity", user = new { login = author },
            merged_at = merged ? "2026-10-08T00:00:00Z" : null
        }));

    private static IssueComment Comment(string body) =>
        new Octokit.Internal.SimpleJsonSerializer().Deserialize<IssueComment>(JsonSerializer.Serialize(new { body }));

    private void Comments(params string[] bodies) =>
        _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bodies.Select(Comment).ToArray());

    private void NoPosts() => _github.Verify(s => s.CreatePullRequestCommentAsync(It.IsAny<string>(), It.IsAny<string>(),
        It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    private void NoStateWrites() => _devOps.Verify(s => s.UpdateWorkItemAsync(It.IsAny<int>(),
        It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

    [TearDown]
    public void NoOtherSideEffects()
    {
        Assert.That(_notifications.Invocations, Is.Empty);
        Assert.That(_github.Invocations.Any(i => i.Method.Name is "UpdatePullRequestAsync" or "CreateIssueAsync"), Is.False);
    }

    [Test]
    public async Task SavedAbandonment_ThenReadsCommentsForGeneratedOpenPullRequest()
    {
        var response = await _tool.AbandonReleasePlan(42);
        Assert.That(response.Status, Is.EqualTo("Success"));
        _github.Verify(s => s.GetPullRequestIssueCommentsAsync("Azure", "azure-sdk-for-python", 1,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(_notifications.Invocations, Is.Empty);
    }

    [Test]
    public async Task PostIsAfterConfirmedStateSave_UsesTrustedPlanLinkAndLeavesSharedPrOpen()
    {
        var saved = false;
        _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()))
            .Callback(() => saved = true)
            .ReturnsAsync(new WorkItem { Id = 42, Fields = new Dictionary<string, object> { ["System.State"] = "Abandoned" } });
        _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
            .Callback(() => Assert.That(saved, Is.True)).ReturnsAsync(Array.Empty<IssueComment>());
        string? posted = null;
        _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, body, _) =>
            {
                Assert.That(saved, Is.True);
                posted = body;
            }).ReturnsAsync(new IssueComment());
        var response = await _tool.AbandonReleasePlan(42);
        Assert.Multiple(() =>
        {
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(posted, Does.StartWith("<!-- azsdk-abandoned-release-plan:42 -->\n"));
            Assert.That(posted, Does.Contain(ReleasePlanWorkItem.DashboardBaseUrl + "123"));
            Assert.That(posted, Does.Contain("shared").And.Contain("left open"));
            Assert.That(posted, Does.Not.Contain("untrusted"));
        });
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("<!-- azsdk-abandoned-release-plan:42 -->", false)]
    [TestCase("Other text\r\n<!-- azsdk-abandoned-release-plan:42 -->\r\nNote", false)]
    [TestCase("<!-- azsdk-abandoned-release-plan:142 -->", true)]
    [TestCase("<!-- azsdk-abandoned-release-plan:43 -->", true)]
    [TestCase("Quoted <!-- azsdk-abandoned-release-plan:42 -->", true)]
    [TestCase("<!-- azsdk-abandoned-release-plan:42 --> suffix", true)]
    public async Task DedupRequiresExactMarkerLineForThisPlan(string body, bool posts)
    {
        Comments(body);
        var response = await _tool.AbandonReleasePlan(42);
        Assert.That(response.ResponseError, Is.Null);
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(),
            It.IsAny<CancellationToken>()), posts ? Times.Once : Times.Never);
    }

    [Test]
    public async Task RepeatedAbandonment_RepairsOnlyMissingNoteWithoutSecondStateWrite()
    {
        var comments = new List<IssueComment>();
        _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => comments);
        _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, body, _) => comments.Add(Comment(body)))
            .ReturnsAsync(new IssueComment());
        Assert.That((await _tool.AbandonReleasePlan(42)).Status, Is.EqualTo("Success"));
        _plan.Status = "Abandoned";
        Assert.That((await _tool.AbandonReleasePlan(42)).Status, Is.EqualTo("Success"));
        Assert.That((await _tool.AbandonReleasePlan(42)).Status, Is.EqualTo("Success"));
        _devOps.Verify(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("human")]
    [TestCase("azure-sdk-automation")]
    [TestCase("other[bot]")]
    [TestCase(null)]
    public async Task AutoPrTitleDoesNotAuthorizeHumanOtherBotOrUnknownAuthor(string? author)
    {
        _github.Setup(s => s.GetPullRequestAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr(author));
        var response = await _tool.AbandonReleasePlan(42);
        Assert.That(response.Status, Is.EqualTo("Success"));
        NoPosts();
        _github.Verify(s => s.GetPullRequestIssueCommentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
        if (author == null)
        {
            Assert.That(response.Details.Any(d => d.StartsWith("Warning:", StringComparison.Ordinal)), Is.True);
        }
    }

    [TestCase("open", true)]
    [TestCase("closed", false)]
    [TestCase("closed", true)]
    public async Task AlreadyAbandonedClosedOrMergedPr_NoCommentOrStateChange(string state, bool merged)
    {
        _plan.Status = "Abandoned";
        _plan.SDKInfo[0].ReleaseStatus = "Released";
        _github.Setup(s => s.GetPullRequestAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr(state: state, merged: merged));
        var response = await _tool.AbandonReleasePlan(42);
        Assert.That(response.Status, Is.EqualTo("Success"));
        NoPosts();
        NoStateWrites();
        if (merged)
        {
            Assert.That(response.NextSteps, Is.Not.Empty);
        }
    }

    [Test]
    public async Task ActiveClosedPr_AllowsAbandonmentButNoNote()
    {
        _github.Setup(s => s.GetPullRequestAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr(state: "closed"));
        Assert.That((await _tool.AbandonReleasePlan(42)).Status, Is.EqualTo("Success"));
        NoPosts();
    }

    [TestCase("merged")]
    [TestCase("released")]
    [TestCase("finished")]
    [TestCase("malformed")]
    public async Task ActivePlanGuardBlocked_NeverReadsCommentsOrWritesNotes(string failure)
    {
        if (failure == "merged")
        {
            _github.Setup(s => s.GetPullRequestAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Pr(merged: true));
        }
        else if (failure == "released") { _plan.SDKInfo[0].ReleaseStatus = "Released"; }
        else if (failure == "finished") { _plan.Status = "Finished"; }
        else { _plan.SDKInfo[0].SdkPullRequestUrl = "https://external.example/Azure/azure-sdk-for-python/pull/1"; }
        Assert.That((await _tool.AbandonReleasePlan(42)).ResponseError, Is.Not.Null);
        NoPosts();
        NoStateWrites();
        _github.Verify(s => s.GetPullRequestIssueCommentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("throw")]
    [TestCase("null")]
    [TestCase("wrong-state")]
    [TestCase("wrong-id")]
    public async Task StateUpdateNotVerified_NeverReadsCommentsOrPosts(string failure)
    {
        var setup = _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()));
        if (failure == "throw") { setup.ThrowsAsync(new InvalidOperationException("revision conflict")); }
        else
        {
            setup.ReturnsAsync(failure == "null" ? null! : new WorkItem
            {
                Id = failure == "wrong-id" ? 43 : 42,
                Fields = new Dictionary<string, object> { ["System.State"] = failure == "wrong-state" ? "In Progress" : "Abandoned" }
            });
        }
        Assert.That((await _tool.AbandonReleasePlan(42)).ResponseError, Is.Not.Null);
        NoPosts();
        _github.Verify(s => s.GetPullRequestIssueCommentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CanonicalDuplicateLinks_PostOnceToActualPublicOrPrivateRepository(bool isPrivate)
    {
        var repo = isPrivate ? "azure-sdk-for-python-pr" : "azure-sdk-for-python";
        _plan.SDKInfo =
        [
            new SDKInfo { Language = "Python", SdkPullRequestUrl = $"https://github.com/Azure/{repo}/pull/1" },
            new SDKInfo { Language = "Python", SdkPullRequestUrl = $"https://github.com/azure/{repo.ToUpperInvariant()}/pull/0001/" }
        ];
        _github.Setup(s => s.GetPullRequestAsync("Azure", repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Pr());
        _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<IssueComment>());
        _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", repo, 1, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IssueComment());
        Assert.That((await _tool.AbandonReleasePlan(42)).Status, Is.EqualTo("Success"));
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", repo, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("pr-read", true)]
    [TestCase("comments", true)]
    [TestCase("null-comments", true)]
    [TestCase("post", true)]
    [TestCase("comments", false)]
    [TestCase("post", false)]
    public async Task NoteFailure_ContinuesOtherPrsWithSuccessWarningsAndNoPostRetry(string failure, bool alreadyAbandoned)
    {
        if (alreadyAbandoned) { _plan.Status = "Abandoned"; }
        _plan.SDKInfo.Add(new SDKInfo { Language = "Python", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/2" });
        _github.Setup(s => s.GetPullRequestAsync("Azure", "azure-sdk-for-python", 2, It.IsAny<CancellationToken>())).ReturnsAsync(Pr());
        _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", "azure-sdk-for-python", 2, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<IssueComment>());
        _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 2, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IssueComment());
        if (failure == "pr-read")
        {
            _github.Setup(s => s.GetPullRequestAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>())).ThrowsAsync(new UnauthorizedAccessException());
        }
        else if (failure == "comments" || failure == "null-comments")
        {
            var setup = _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()));
            if (failure == "comments") { setup.ThrowsAsync(new InvalidOperationException("cannot read comments")); }
            else { setup.ReturnsAsync((IReadOnlyList<IssueComment>)null!); }
        }
        else
        {
            _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("response lost after POST"));
        }
        var response = await _tool.AbandonReleasePlan(42);
        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo("Success"));
            Assert.That(response.ExitCode, Is.Zero);
            Assert.That(response.Details.Any(d => d.Contains("Warning: plan 42 remains Abandoned", StringComparison.Ordinal)), Is.True);
            Assert.That(response.NextSteps, Has.Count.EqualTo(1));
            Assert.That(response.ToString(), Does.Contain("Warning:").And.Contain("retry"));
        });
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(),
            It.IsAny<CancellationToken>()), failure == "post" ? Times.Once : Times.Never);
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 2, It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        if (alreadyAbandoned) { NoStateWrites(); }
    }

    [Test]
    public async Task LostPostResponse_LaterRetryFindsSavedMarkerWithoutPostingAgain()
    {
        var comments = new List<IssueComment>();
        _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => comments);
        _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, body, _) => comments.Add(Comment(body)))
            .ThrowsAsync(new IOException("POST accepted but response lost"));
        var first = await _tool.AbandonReleasePlan(42);
        Assert.That(first.Status, Is.EqualTo("Success"));
        Assert.That(first.NextSteps, Is.Not.Empty);
        _plan.Status = "Abandoned";
        var retry = await _tool.AbandonReleasePlan(42);
        Assert.That(retry.Status, Is.EqualTo("Success"));
        Assert.That(retry.NextSteps, Is.Null);
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _devOps.Verify(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AlreadyAbandonedGeneratedDraft_RepairsMissingNoteWithoutStateWrite()
    {
        _plan.Status = "Abandoned";
        _github.Setup(s => s.GetPullRequestAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr(draft: true));
        Assert.That((await _tool.AbandonReleasePlan(42)).Status, Is.EqualTo("Success"));
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        NoStateWrites();
    }

    [Test]
    public async Task TwoAbandonedPlansSharingPr_EachGetsItsOwnMarkerWithoutChangingPrState()
    {
        _plan.Status = "Abandoned";
        var comments = new List<IssueComment>();
        _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => comments);
        _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, body, _) => comments.Add(Comment(body)))
            .ReturnsAsync(new IssueComment());
        Assert.That((await _tool.AbandonReleasePlan(42)).Status, Is.EqualTo("Success"));
        _plan.WorkItemId = 43;
        _plan.ReleasePlanId = 124;
        _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(43, It.IsAny<CancellationToken>())).ReturnsAsync(_plan);
        Assert.That((await _tool.AbandonReleasePlan(43)).Status, Is.EqualTo("Success"));
        Assert.That(comments.Select(c => c.Body.Split('\n')[0]), Is.EqualTo(new[]
        {
            "<!-- azsdk-abandoned-release-plan:42 -->", "<!-- azsdk-abandoned-release-plan:43 -->"
        }));
        NoStateWrites();
    }

    [TestCase(false, 123)]
    [TestCase(true, 123)]
    [TestCase(false, 0)]
    public async Task DashboardLink_UsesExistingTrustedNumericMapping(bool isTest, int releasePlanId)
    {
        _plan.IsTestReleasePlan = isTest;
        _plan.ReleasePlanId = releasePlanId;
        Assert.That((await _tool.AbandonReleasePlan(42)).Status, Is.EqualTo("Success"));
        var expected = (isTest ? ReleasePlanWorkItem.DashboardBaseUrlTest : ReleasePlanWorkItem.DashboardBaseUrl)
            + (releasePlanId > 0 ? releasePlanId : 42);
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1,
            It.Is<string>(body => body.Contains(expected) && body.StartsWith("<!-- azsdk-abandoned-release-plan:42 -->\n")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("closed")]
    [TestCase("merged")]
    [TestCase("human")]
    [TestCase("unreadable")]
    public async Task ChangeAfterCommentsRead_SkipsBeforePost(string change)
    {
        var sequence = _github.SetupSequence(s => s.GetPullRequestAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr()).ReturnsAsync(Pr());
        if (change == "unreadable") { sequence.ThrowsAsync(new IOException("read failed")); }
        else { sequence.ReturnsAsync(change == "human" ? Pr("human") : Pr(state: "closed", merged: change == "merged")); }
        var response = await _tool.AbandonReleasePlan(42);
        Assert.That(response.Status, Is.EqualTo("Success"));
        Assert.That(response.NextSteps, Is.Not.Empty);
        NoPosts();
    }

    [TestCase("null-sdk")]
    [TestCase("null-entry")]
    [TestCase("bad-url")]
    [TestCase("changed-links")]
    [TestCase("changed-state")]
    [TestCase("changed-identity")]
    public async Task AlreadyAbandoned_UnverifiableCurrentLinks_WarnsWithoutAnyWrite(string failure)
    {
        _plan.Status = "Abandoned";
        if (failure == "null-sdk") { _plan.SDKInfo = null!; }
        else if (failure == "null-entry") { _plan.SDKInfo.Add(null!); }
        else if (failure == "bad-url") { _plan.SDKInfo[0].SdkPullRequestUrl += "?untrusted=1"; }
        else
        {
            var fresh = new ReleasePlanWorkItem
            {
                WorkItemId = failure == "changed-identity" ? 43 : 42, ReleasePlanId = 123, Revision = 7,
                Status = failure == "changed-state" ? "In Progress" : "Abandoned", SDKInfo = failure == "changed-links" ? [] : _plan.SDKInfo
            };
            _devOps.SetupSequence(s => s.GetReleasePlanForWorkItemAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_plan).ReturnsAsync(fresh);
        }
        var response = await _tool.AbandonReleasePlan(42);
        Assert.That(response.Status, Is.EqualTo("Success"));
        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.NextSteps, Is.Not.Empty);
        Assert.That(_github.Invocations, Is.Empty);
        Assert.That(response.Details, Has.None.Contains("remains Abandoned"));
        NoStateWrites();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NoSdkLinks_NoGithubCallsOrNotes(bool alreadyAbandoned)
    {
        _plan.SDKInfo = [];
        if (alreadyAbandoned) { _plan.Status = "Abandoned"; }
        Assert.That((await _tool.AbandonReleasePlan(42)).Status, Is.EqualTo("Success"));
        Assert.That(_github.Invocations, Is.Empty);
        if (alreadyAbandoned) { NoStateWrites(); }
    }

    [TestCase("after-save")]
    [TestCase("comments")]
    [TestCase("post")]
    public void CallerCancellationAfterSave_PropagatesAndDoesNotContinueOrRetry(string phase)
    {
        using var cts = new CancellationTokenSource();
        _plan.SDKInfo.Add(new SDKInfo { Language = "Python", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/2" });
        _github.Setup(s => s.GetPullRequestAsync("Azure", "azure-sdk-for-python", 2, It.IsAny<CancellationToken>())).ReturnsAsync(Pr());
        if (phase == "after-save")
        {
            _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, cts.Token))
                .Callback(() => cts.Cancel()).ReturnsAsync(new WorkItem
                {
                    Id = 42, Fields = new Dictionary<string, object> { ["System.State"] = "Abandoned" }
                });
        }
        else if (phase == "comments")
        {
            _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", "azure-sdk-for-python", 1, cts.Token))
                .Callback(() => cts.Cancel()).ReturnsAsync(Array.Empty<IssueComment>());
        }
        else
        {
            _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<string>(), cts.Token))
                .Callback(() => cts.Cancel()).ReturnsAsync(new IssueComment());
        }
        Assert.CatchAsync<OperationCanceledException>(async () => await _tool.AbandonReleasePlan(42, ct: cts.Token));
        _github.Verify(s => s.CreatePullRequestCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), phase == "post" ? Times.Once : Times.Never);
        _github.Verify(s => s.GetPullRequestIssueCommentsAsync("Azure", "azure-sdk-for-python", 2,
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
